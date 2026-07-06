using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;
using OfficeOpenXml;

/// <summary>
/// Excel转SO批量生成编辑器窗口
/// 基于配置的Excel文件和工作表映射，自动生成对应的ScriptableObject资产
/// 支持单列单值和单列多值两种映射模式，提供进度显示和错误处理
/// </summary>
public class ExcelToSOGenerator : OdinEditorWindow
{
    #region 配置引用
    /// <summary>
    /// Excel转SO的总配置文件引用
    /// 拖拽ExcelToSOConfig资产到此字段，或使用右侧按钮选择
    /// </summary>
    [BoxGroup("配置引用"), LabelText("Excel转SO配置"), Required, 
     Tooltip("拖拽ExcelToSOConfig配置文件到此字段")]
    [SerializeField] private ExcelToSOConfig _excelToSoConfig;

    /// <summary>
    /// 快速选择配置文件按钮
    /// 自动查找项目中的所有ExcelToSOConfig文件，提供下拉选择
    /// </summary>
    [BoxGroup("配置引用"), LabelText("快速选择配置"), 
     ShowIf("@_excelToSoConfig == null")]
    [ValueDropdown("GetAllExcelToSOConfigs")]
    [SerializeField] private ExcelToSOConfig _quickSelectConfig;

    /// <summary>
    /// 获取项目中所有ExcelToSOConfig配置文件（用于下拉选择）
    /// </summary>
    /// <returns>配置文件的ValueDropdown列表</returns>
    private IEnumerable<ExcelToSOConfig> GetAllExcelToSOConfigs()
    {
        // 在项目中搜索所有ExcelToSOConfig类型的资产
        var guids = AssetDatabase.FindAssets("t:ExcelToSOConfig");
        var configs = new List<ExcelToSOConfig>();
        
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var config = AssetDatabase.LoadAssetAtPath<ExcelToSOConfig>(path);
            if (config != null)
            {
                configs.Add(config);
            }
        }
        
        return configs;
    }
    #endregion

    #region 生成选项
    /// <summary>
    /// 生成前清空目标文件夹
    /// 启用后会在生成SO前删除保存路径中的所有现有SO文件（避免旧数据干扰）
    /// </summary>
    [BoxGroup("生成选项"), LabelText("生成前清空目标文件夹"), 
     Tooltip("启用后会在生成前删除目标文件夹中的所有SO文件（谨慎使用）")]
    public bool clearFolderBeforeGenerate = false;

    /// <summary>
    /// 覆盖已存在的SO文件
    /// 启用后会覆盖同名的SO文件，禁用时会跳过已存在的文件（避免误覆盖）
    /// </summary>
    [BoxGroup("生成选项"), LabelText("覆盖已存在的SO文件"), 
     Tooltip("启用后会覆盖同名的SO文件，禁用时会跳过已存在的文件")]
    public bool overwriteExistingFiles = true;

    /// <summary>
    /// 生成完成后选择所有生成的SO
    /// 启用后会在生成完成后在Project窗口中选择所有新生成的SO文件
    /// </summary>
    [BoxGroup("生成选项"), LabelText("生成完成后选择所有SO"), 
     Tooltip("启用后会在生成完成后自动选择所有新生成的SO文件")]
    public bool selectGeneratedAssets = true;
    #endregion

    #region 状态信息
    /// <summary>
    /// 生成状态信息（只读）
    /// 显示当前生成进度、处理文件数量、成功/失败统计等详细信息
    /// </summary>
    [BoxGroup("生成状态"), LabelText("生成状态信息"), MultiLineProperty(5), ReadOnly]
    [ShowInInspector] private string _generationStatus = "等待生成...";

    /// <summary>
    /// 生成进度条（0-1范围）
    /// 可视化显示当前生成进度，便于监控大量文件的处理状态
    /// </summary>
    [BoxGroup("生成状态"), LabelText("生成进度"), ProgressBar(0, 1), ReadOnly]
    [ShowInInspector] private float _generationProgress = 0f;

    /// <summary>
    /// 生成统计信息（只读）
    /// 记录成功生成、跳过、失败的文件数量，便于问题排查
    /// </summary>
    [BoxGroup("生成状态"), LabelText("生成统计"), ReadOnly]
    [ShowInInspector] private string _generationStats = "成功: 0 | 跳过: 0 | 失败: 0";
    #endregion

    #region 主生成方法
    /// <summary>
    /// 批量生成SO文件的主入口方法
    /// 根据配置文件的设置，遍历所有Excel文件和工作表，生成对应的SO资产
    /// </summary>
    [BoxGroup("操作"), Button("开始生成SO", ButtonSizes.Large), GUIColor(0.4f, 0.8f, 0.4f)]
    [EnableIf("@_excelToSoConfig != null || _quickSelectConfig != null")]
    public void GenerateAllSOFromExcel()
    {
        // 确定使用的配置文件（优先使用拖拽配置，其次使用快速选择配置）
        var config = _excelToSoConfig ?? _quickSelectConfig;
        if (config == null)
        {
            UpdateStatus("错误：未选择有效的ExcelToSOConfig配置文件", Color.red);
            return;
        }

        if (config.excelFileConfigs == null || config.excelFileConfigs.Count == 0)
        {
            UpdateStatus("错误：配置文件中未添加任何Excel文件配置", Color.red);
            return;
        }

        try
        {
            // 开始生成流程
            UpdateStatus("开始生成SO文件...", Color.cyan);
            _generationProgress = 0f;

            int totalSuccess = 0;
            int totalSkipped = 0;
            int totalFailed = 0;

            // 遍历所有Excel文件配置
            for (int fileIndex = 0; fileIndex < config.excelFileConfigs.Count; fileIndex++)
            {
                var excelFileConfig = config.excelFileConfigs[fileIndex];
                UpdateStatus($"正在处理Excel文件：{Path.GetFileName(excelFileConfig.excelFilePath)}", Color.white);

                // 处理单个Excel文件
                var result = ProcessExcelFile(excelFileConfig);
                totalSuccess += result.successCount;
                totalSkipped += result.skippedCount;
                totalFailed += result.failedCount;

                // 更新进度
                _generationProgress = (float)(fileIndex + 1) / config.excelFileConfigs.Count;
                Repaint(); // 强制刷新界面显示进度
            }

            // 生成完成统计
            _generationProgress = 1f;
            UpdateStatus($"生成完成！成功：{totalSuccess}，跳过：{totalSkipped}，失败：{totalFailed}", Color.green);
            _generationStats = $"成功: {totalSuccess} | 跳过: {totalSkipped} | 失败: {totalFailed}";

            // 刷新资产数据库并可能选择生成的资产
            AssetDatabase.Refresh();
            if (selectGeneratedAssets)
            {
                // 这里可以添加选择生成资产的功能
                UpdateStatus("生成完成，资产数据库已刷新", Color.green);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"生成SO时发生异常：{ex.Message}\n{ex.StackTrace}");
            UpdateStatus($"生成失败：{ex.Message}", Color.red);
        }
    }

    /// <summary>
    /// 处理单个Excel文件，生成所有工作表的SO
    /// </summary>
    /// <param name="excelFileConfig">Excel文件配置</param>
    /// <returns>生成结果统计</returns>
    private (int successCount, int skippedCount, int failedCount) ProcessExcelFile(ExcelFileConfig excelFileConfig)
    {
        if (string.IsNullOrEmpty(excelFileConfig.excelFilePath) || !File.Exists(excelFileConfig.excelFilePath))
        {
            Debug.LogError($"Excel文件不存在或路径无效：{excelFileConfig.excelFilePath}");
            return (0, 0, 1);
        }

        if (excelFileConfig.worksheetConfigs == null || excelFileConfig.worksheetConfigs.Count == 0)
        {
            Debug.LogError($"Excel文件未配置任何工作表：{excelFileConfig.excelFilePath}");
            return (0, 0, 1);
        }

        int fileSuccess = 0;
        int fileSkipped = 0;
        int fileFailed = 0;

        try
        {
            // 使用EPPlus打开Excel文件
            using (var package = new ExcelPackage(new FileInfo(excelFileConfig.excelFilePath)))
            {
                foreach (var worksheetConfig in excelFileConfig.worksheetConfigs)
                {
                    var worksheet = package.Workbook.Worksheets[worksheetConfig.workSheetName];
                    if (worksheet == null)
                    {
                        Debug.LogError($"在工作表中找不到指定名称的工作表：{worksheetConfig.workSheetName}");
                        fileFailed++;
                        continue;
                    }

                    // 处理单个工作表
                    var result = ProcessWorksheet(worksheet, worksheetConfig, excelFileConfig.dataStartRow);
                    fileSuccess += result.successCount;
                    fileSkipped += result.skippedCount;
                    fileFailed += result.failedCount;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"处理Excel文件时发生异常：{excelFileConfig.excelFilePath}\n{ex.Message}");
            fileFailed++;
        }

        return (fileSuccess, fileSkipped, fileFailed);
    }

    /// <summary>
    /// 处理单个工作表，生成所有数据行的SO（修复类型转换问题）
    /// </summary>
    /// <param name="worksheet">Excel工作表</param>
    /// <param name="worksheetConfig">工作表配置</param>
    /// <param name="dataStartRow">数据起始行</param>
    /// <returns>生成结果统计</returns>
    private (int successCount, int skippedCount, int failedCount) ProcessWorksheet(
        ExcelWorksheet worksheet, 
        WorksheetConfig worksheetConfig, 
        int dataStartRow)
    {
        if (worksheetConfig.soTemplate == null)
        {
            Debug.LogError($"工作表未配置SO模板：{worksheetConfig.workSheetName}");
            return (0, 0, 1);
        }

        if (worksheetConfig.columnMappings == null || worksheetConfig.columnMappings.Count == 0)
        {
            Debug.LogError($"工作表未配置任何列映射：{worksheetConfig.workSheetName}");
            return (0, 0, 1);
        }

        // 确保保存路径存在
        if (!Directory.Exists(worksheetConfig.savePath))
        {
            Directory.CreateDirectory(worksheetConfig.savePath);
        }

        int worksheetSuccess = 0;
        int worksheetSkipped = 0;
        int worksheetFailed = 0;

        // 清空目标文件夹（如果启用）
        if (clearFolderBeforeGenerate && Directory.Exists(worksheetConfig.savePath))
        {
            ClearTargetFolder(worksheetConfig.savePath);
        }

        // 获取SO类型并验证
        Type soType = worksheetConfig.GetSoType();
        if (soType == null)
        {
            return (0, 0, 1);
        }

        // 遍历数据行（从dataStartRow开始）
        for (int row = dataStartRow; row <= worksheet.Dimension.End.Row; row++)
        {
            try
            {
                ScriptableObject soInstance = ScriptableObject.CreateInstance(soType);
                if (soInstance == null)
                {
                    Debug.LogError($"创建SO实例失败，类型：{soType.Name}");
                    worksheetFailed++;
                    continue;
                }

                bool rowProcessedSuccessfully = true;

                // 处理每一列的映射
                foreach (var columnMapping in worksheetConfig.columnMappings)
                {
                    if (!ProcessColumnMapping(worksheet, row, columnMapping, soInstance))
                    {
                        rowProcessedSuccessfully = false;
                        break;
                    }
                }

                if (!rowProcessedSuccessfully)
                {
                    UnityEngine.Object.DestroyImmediate(soInstance); // 清理失败实例
                    worksheetFailed++;
                    continue;
                }

                // 保存SO文件
                string fileName = GenerateFileName(worksheet, row, worksheetConfig);
                string savePath = Path.Combine(worksheetConfig.savePath, $"{fileName}.asset");

                // 检查文件是否存在并决定是否覆盖
                if (File.Exists(savePath) && !overwriteExistingFiles)
                {
                    UnityEngine.Object.DestroyImmediate(soInstance); // 清理跳过实例
                    worksheetSkipped++;
                    continue;
                }

                // 确保目录存在
                string directory = Path.GetDirectoryName(savePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                AssetDatabase.CreateAsset(soInstance, savePath);
                
                //在生成每个SO的时候 加入到SO管理器的列表中方便批量操作
                worksheetSuccess++;
            }
            catch (Exception ex)
            {
                Debug.LogError($"处理第{row}行数据时发生异常：{ex.Message}");
                worksheetFailed++;
            }
        }

        return (worksheetSuccess, worksheetSkipped, worksheetFailed);
    }
    #endregion

    #region 列映射处理
    /// <summary>
    /// 处理单个列映射，将Excel数据赋值到SO字段（使用ScriptableObject基类）
    /// </summary>
    /// <param name="worksheet">Excel工作表</param>
    /// <param name="row">当前行号</param>
    /// <param name="columnMapping">列映射配置</param>
    /// <param name="soInstance">SO实例</param>
    /// <returns>处理成功返回true，失败返回false</returns>
    private bool ProcessColumnMapping(ExcelWorksheet worksheet, int row, ColumnMapping columnMapping, ScriptableObject soInstance)
    {
        try
        {
            // 获取单元格值
            string cellValue = worksheet.Cells[row, columnMapping.excelColumnIndex]?.Text?.Trim();
            if (string.IsNullOrEmpty(cellValue))
            {
                return true; // 空值不算错误，跳过该列
            }

            switch (columnMapping.columnMappingType)
            {
                case ColumnMappingType.SingleField:
                    return ProcessSingleFieldMapping(cellValue, columnMapping, soInstance);
                
                case ColumnMappingType.MultiField:
                    return ProcessMultiFieldMapping(cellValue, columnMapping, soInstance);
                
                default:
                    Debug.LogError($"不支持的列映射类型：{columnMapping.columnMappingType}");
                    return false;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"处理列映射时发生异常（行{row}，列{columnMapping.excelColumnIndex}）：{ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 处理单列单值映射（使用ScriptableObject）
    /// </summary>
    private bool ProcessSingleFieldMapping(string cellValue, ColumnMapping columnMapping, ScriptableObject soInstance)
    {
        if (string.IsNullOrEmpty(columnMapping.singleSoFieldName))
        {
            Debug.LogError($"单列单值映射未配置目标字段名");
            return false;
        }

        return SetFieldValue(soInstance, columnMapping.singleSoFieldName, cellValue);
    }

    /// <summary>
    /// 处理单列多值映射（使用ScriptableObject）
    /// </summary>
    private bool ProcessMultiFieldMapping(string cellValue, ColumnMapping columnMapping, ScriptableObject soInstance)
    {
        if (string.IsNullOrEmpty(columnMapping.separator))
        {
            Debug.LogError($"单列多值映射未配置分隔符");
            return false;
        }

        if (columnMapping.multiFieldItems == null || columnMapping.multiFieldItems.Count == 0)
        {
            Debug.LogError($"单列多值映射未配置子字段映射");
            return false;
        }

        // 分割单元格值
        string[] splitValues = cellValue.Split(new[] { columnMapping.separator }, StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var multiFieldItem in columnMapping.multiFieldItems)
        {
            if (multiFieldItem.splitIndex < 0 || multiFieldItem.splitIndex >= splitValues.Length)
            {
                Debug.LogError($"分割索引超出范围：{multiFieldItem.splitIndex}，分割后值数量：{splitValues.Length}");
                return false;
            }

            if (string.IsNullOrEmpty(multiFieldItem.targetSoFieldName))
            {
                Debug.LogError($"多值映射子项未配置目标字段名");
                return false;
            }

            string fieldValue = splitValues[multiFieldItem.splitIndex].Trim();
            if (!SetFieldValue(soInstance, multiFieldItem.targetSoFieldName, fieldValue))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 设置SO字段值（支持类型转换，使用ScriptableObject）
    /// </summary>
    private bool SetFieldValue(ScriptableObject soInstance, string fieldName, string stringValue)
    {
        try
        {
            Type soType = soInstance.GetType(); // 这里获取的是具体子类的类型！
            FieldInfo field = soType.GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
            
            if (field == null)
            {
                Debug.LogError($"在SO类型{soType.Name}中找不到字段：{fieldName}");
                return false;
            }

            // 类型转换和赋值
            object convertedValue = ConvertStringToType(stringValue, field.FieldType);
            field.SetValue(soInstance, convertedValue);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"设置字段值失败（字段：{fieldName}，值：{stringValue}）：{ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 字符串到具体类型的转换
    /// </summary>
    private object ConvertStringToType(string value, Type targetType)
    {
        if (string.IsNullOrEmpty(value))
            return GetDefaultValue(targetType);

        try
        {
            // 处理空字符串和null
            if (value.Trim().ToLower() == "null")
                return null;

            // 基本类型转换
            if (targetType == typeof(string))
                return value;

            if (targetType == typeof(int))
                return int.Parse(value);

            if (targetType == typeof(float))
                return float.Parse(value);

            if (targetType == typeof(bool))
                return bool.Parse(value.ToLower());

            if (targetType == typeof(double))
                return double.Parse(value);

            if (targetType == typeof(long))
                return long.Parse(value);

            if (targetType == typeof(Vector2))
            {
                string[] parts = value.Split(',');
                if (parts.Length == 2)
                    return new Vector2(float.Parse(parts[0]), float.Parse(parts[1]));
            }

            if (targetType == typeof(Vector3))
            {
                string[] parts = value.Split(',');
                if (parts.Length == 3)
                    return new Vector3(float.Parse(parts[0]), float.Parse(parts[1]), float.Parse(parts[2]));
            }

            // 处理Unity对象引用（如Sprite）
            if (typeof(UnityEngine.Object).IsAssignableFrom(targetType))
            {
                return AssetDatabase.LoadAssetAtPath(value, targetType);
            }

            // 枚举类型
            if (targetType.IsEnum)
                return Enum.Parse(targetType, value);

            // 默认使用ChangeType
            return Convert.ChangeType(value, targetType);
        }
        catch (Exception ex)
        {
            Debug.LogError($"类型转换失败（值：{value}，目标类型：{targetType.Name}）：{ex.Message}");
            return GetDefaultValue(targetType);
        }
    }

    /// <summary>
    /// 获取类型的默认值
    /// </summary>
    private object GetDefaultValue(Type type)
    {
        return type.IsValueType ? Activator.CreateInstance(type) : null;
    }
    #endregion

    #region 辅助方法
    /// <summary>
    /// 生成SO文件名（基于SO类名、id和name字段）
    /// 新命名规则：SO类名_id_name（如WeaponSO_10001_突击步枪1）
    /// </summary>
    private string GenerateFileName(ExcelWorksheet worksheet, int row, WorksheetConfig worksheetConfig)
    {
        // 获取SO类名
        string soClassName = worksheetConfig.GetSoType()?.Name ?? worksheetConfig.workSheetName;
        
        string idValue = null;
        string nameValue = null;

        // 查找id和name字段的值
        foreach (var columnMapping in worksheetConfig.columnMappings)
        {
            if (columnMapping.columnMappingType == ColumnMappingType.SingleField)
            {
                string fieldName = columnMapping.singleSoFieldName?.ToLower();
                if (fieldName == "id" && string.IsNullOrEmpty(idValue))
                {
                    idValue = worksheet.Cells[row, columnMapping.excelColumnIndex]?.Text?.Trim();
                }
                else if (fieldName == "name" && string.IsNullOrEmpty(nameValue))
                {
                    nameValue = worksheet.Cells[row, columnMapping.excelColumnIndex]?.Text?.Trim();
                }
            }
            else if (columnMapping.columnMappingType == ColumnMappingType.MultiField)
            {
                // 处理多值映射中的id和name字段
                foreach (var multiFieldItem in columnMapping.multiFieldItems)
                {
                    string fieldName = multiFieldItem.targetSoFieldName?.ToLower();
                    if (fieldName == "id" && string.IsNullOrEmpty(idValue))
                    {
                        string cellValue = worksheet.Cells[row, columnMapping.excelColumnIndex]?.Text?.Trim();
                        if (!string.IsNullOrEmpty(cellValue))
                        {
                            string[] splitValues = cellValue.Split(new[] { columnMapping.separator }, StringSplitOptions.RemoveEmptyEntries);
                            if (multiFieldItem.splitIndex >= 0 && multiFieldItem.splitIndex < splitValues.Length)
                            {
                                idValue = splitValues[multiFieldItem.splitIndex].Trim();
                            }
                        }
                    }
                    else if (fieldName == "name" && string.IsNullOrEmpty(nameValue))
                    {
                        string cellValue = worksheet.Cells[row, columnMapping.excelColumnIndex]?.Text?.Trim();
                        if (!string.IsNullOrEmpty(cellValue))
                        {
                            string[] splitValues = cellValue.Split(new[] { columnMapping.separator }, StringSplitOptions.RemoveEmptyEntries);
                            if (multiFieldItem.splitIndex >= 0 && multiFieldItem.splitIndex < splitValues.Length)
                            {
                                nameValue = splitValues[multiFieldItem.splitIndex].Trim();
                            }
                        }
                    }
                }
            }
        }

        // 构建文件名
        var fileNameParts = new List<string> { soClassName };

        // 添加id部分
        if (!string.IsNullOrEmpty(idValue))
        {
            fileNameParts.Add(idValue);
        }
        else
        {
            // 如果没有id，使用行号作为备用
            fileNameParts.Add($"Row{row}");
        }

        // 添加name部分
        if (!string.IsNullOrEmpty(nameValue))
        {
            // 清理name中的非法文件名字符
            string cleanName = CleanFileName(nameValue);
            fileNameParts.Add(cleanName);
        }

        // 组合最终文件名
        string fileName = string.Join("_", fileNameParts);
        
        // 确保文件名长度不超过系统限制，并移除多余的下划线
        return CleanFileName(fileName);
    }

    /// <summary>
    /// 清理文件名中的非法字符
    /// 移除Windows/Unix系统中不允许在文件名中使用的字符
    /// </summary>
    private string CleanFileName(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
            return fileName;

        // 定义非法字符（Windows和Unix系统通用的非法文件名字符）
        char[] invalidChars = Path.GetInvalidFileNameChars();
        
        // 替换非法字符为下划线
        foreach (char invalidChar in invalidChars)
        {
            fileName = fileName.Replace(invalidChar, '_');
        }

        // 移除连续的下划线
        while (fileName.Contains("__"))
        {
            fileName = fileName.Replace("__", "_");
        }

        // 移除开头和结尾的下划线
        fileName = fileName.Trim('_');

        // 限制文件名长度（避免过长的路径）
        const int maxFileNameLength = 100;
        if (fileName.Length > maxFileNameLength)
        {
            fileName = fileName.Substring(0, maxFileNameLength).Trim('_');
        }

        // 确保文件名不为空
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = "Unnamed";
        }

        return fileName;
    }

    /// <summary>
    /// 清空目标文件夹中的所有SO文件
    /// </summary>
    private void ClearTargetFolder(string folderPath)
    {
        try
        {
            var files = Directory.GetFiles(folderPath, "*.asset", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                AssetDatabase.DeleteAsset(file);
            }
            UpdateStatus($"已清空目标文件夹：{folderPath}", Color.yellow);
        }
        catch (Exception ex)
        {
            Debug.LogError($"清空目标文件夹失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 更新状态信息
    /// </summary>
    private void UpdateStatus(string message, Color color)
    {
        _generationStatus = $"【{DateTime.Now:HH:mm:ss}】 {message}";
        Debug.Log($"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{message}</color>");
    }
    #endregion

    #region 编辑器窗口管理
    /// <summary>
    /// 打开Excel转SO生成器窗口的菜单方法
    /// </summary>
    [MenuItem("Tools/Excel转SO/批量生成SO")]
    private static void OpenWindow()
    {
        var window = GetWindow<ExcelToSOGenerator>();
        window.titleContent = new GUIContent("Excel转SO生成器");
        window.minSize = new Vector2(600, 700);
        window.Show();
    }

    /// <summary>
    /// 窗口初始化
    /// </summary>
    protected override void Initialize()
    {
        base.Initialize();
        UpdateStatus("Excel转SO生成器已就绪", Color.gray);
    }

    /// <summary>
    /// 快速选择配置变更时的回调
    /// </summary>
    private void OnQuickSelectConfigChanged()
    {
        if (_quickSelectConfig != null)
        {
            _excelToSoConfig = _quickSelectConfig;
            UpdateStatus($"已选择配置文件：{_excelToSoConfig.name}", Color.cyan);
        }
    }
    #endregion
}