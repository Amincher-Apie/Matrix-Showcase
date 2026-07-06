using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Excel转SO配置的专用编辑器窗口
/// 使用树形侧边栏组织Excel文件和工作表，提供清晰的层级导航
/// </summary>
public class ExcelToSOConfigEditor : OdinEditorWindow
{
    #region 配置引用
    [SerializeField] 
    private ExcelToSOConfig _excelToSoConfig;

    [ShowIf("@_excelToSoConfig == null")]
    [ValueDropdown("GetAllExcelToSOConfigs")]
    [SerializeField] 
    private ExcelToSOConfig _quickSelectConfig;
    #endregion

    #region 树形视图数据
    private OdinMenuTree _configTree;
    private object _currentConfigObject;
    private string _currentConfigType;
    private Vector2 _treeScrollPosition;
    private Vector2 _editorScrollPosition;
    private float _leftPanelWidth = 300f;
    // 缓存Odin的PropertyTree，避免重复创建导致布局冲突
    private PropertyTree _currentPropertyTree;
    #endregion

    #region 主界面构建
    private void BuildConfigTree()
    {
        // 清理现有的事件监听
        if (_configTree?.Selection != null)
        {
            _configTree.Selection.SelectionChanged -= OnTreeSelectionChanged;
        }

        _configTree = new OdinMenuTree(supportsMultiSelect: false)
        {
            Config = { DrawSearchToolbar = true }
        };
        
        if (_excelToSoConfig == null)
        {
            _configTree.Add("请先选择配置文件", null, EditorIcons.AlertCircle);
            return;
        }

        // 【已删除】原“根配置”节点创建代码，避免点击报错且移除无用节点
        // _configTree.Add("根配置", _excelToSoConfig, EditorIcons.House);

        // 添加Excel文件节点（直接显示在树形根目录，无“根配置”父节点）
        if (_excelToSoConfig.excelFileConfigs != null && _excelToSoConfig.excelFileConfigs.Count > 0)
        {
            for (int i = 0; i < _excelToSoConfig.excelFileConfigs.Count; i++)
            {
                var fileConfig = _excelToSoConfig.excelFileConfigs[i];
                string fileName = !string.IsNullOrEmpty(fileConfig.excelFilePath) 
                    ? System.IO.Path.GetFileName(fileConfig.excelFilePath) 
                    : $"Excel文件{i + 1}";
                
                // 添加Excel文件节点（路径保持“Excel文件/文件名”，维持层级清晰度）
                string filePath = $"Excel文件/{fileName}";
                _configTree.Add(filePath, fileConfig, GetFileIcon());

                // 添加该文件下的工作表节点
                if (fileConfig.worksheetConfigs != null && fileConfig.worksheetConfigs.Count > 0)
                {
                    for (int j = 0; j < fileConfig.worksheetConfigs.Count; j++)
                    {
                        var worksheetConfig = fileConfig.worksheetConfigs[j];
                        string worksheetName = !string.IsNullOrEmpty(worksheetConfig.workSheetName)
                            ? worksheetConfig.workSheetName
                            : $"工作表{j + 1}";
                            
                        string worksheetPath = $"{filePath}/{worksheetName}";
                        _configTree.Add(worksheetPath, worksheetConfig, EditorIcons.List);
                    }
                }

                // 添加该文件下的快速操作节点
                _configTree.Add($"{filePath}/[+] 添加新工作表", new ActionWrapper(() => AddNewWorksheet(fileConfig)), EditorIcons.Plus);
            }
        }

        // 添加根级别的快速操作节点（用于新增Excel文件）
        _configTree.Add("Excel文件/[+] 添加新Excel文件", new ActionWrapper(AddNewExcelFile), EditorIcons.Plus);

        // 注册选择变更事件
        _configTree.Selection.SelectionChanged += OnTreeSelectionChanged;
    }

    private Texture GetFileIcon()
    {
        return EditorGUIUtility.IconContent("TextAsset Icon").image;
    }

    private void OnTreeSelectionChanged(SelectionChangedType changeType)
    {
        if (_configTree?.Selection == null || _configTree.Selection.Count == 0)
            return;

        var selected = _configTree.Selection.FirstOrDefault();
        if (selected?.Value == null)
            return;

        // 处理动作按钮（添加新项）
        if (selected.Value is ActionWrapper actionWrapper)
        {
            actionWrapper.Action?.Invoke();
            return;
        }

        // 清理旧的PropertyTree避免布局冲突
        if (_currentPropertyTree != null)
        {
            _currentPropertyTree.Dispose();
            _currentPropertyTree = null;
        }

        // 更新当前选中对象并初始化新的PropertyTree
        _currentConfigObject = selected.Value;
        _currentConfigType = GetConfigTypeDisplayName(selected.Value);
        
        // 仅为非UnityObject类型创建PropertyTree（避免与Editor重复）
        if (!(_currentConfigObject is UnityEngine.Object))
        {
            _currentPropertyTree = PropertyTree.Create(_currentConfigObject);
        }
    }

    private string GetConfigTypeDisplayName(object configObject)
    {
        if (configObject == null) return "无选择";

        return configObject switch
        {
            ExcelToSOConfig => "根配置", // 保留类型判断（虽节点已删，避免其他逻辑异常）
            ExcelFileConfig => "Excel文件配置",
            WorksheetConfig => "工作表配置",
            _ => configObject.GetType().Name
        };
    }
    #endregion

    #region 数据操作
    private void AddNewExcelFile()
    {
        if (_excelToSoConfig == null) return;

        if (_excelToSoConfig.excelFileConfigs == null)
            _excelToSoConfig.excelFileConfigs = new List<ExcelFileConfig>();

        var newFileConfig = new ExcelFileConfig
        {
            excelFilePath = "",
            dataStartRow = 2,
            worksheetConfigs = new List<WorksheetConfig>()
        };

        _excelToSoConfig.excelFileConfigs.Add(newFileConfig);
        EditorUtility.SetDirty(_excelToSoConfig);
        RefreshView();
        ShowNotification(new GUIContent("已添加新的Excel文件配置"));
    }

    private void AddNewWorksheet(ExcelFileConfig parentFileConfig)
    {
        if (parentFileConfig == null) return;

        if (parentFileConfig.worksheetConfigs == null)
            parentFileConfig.worksheetConfigs = new List<WorksheetConfig>();

        var newWorksheetConfig = new WorksheetConfig
        {
            workSheetName = $"Sheet{parentFileConfig.worksheetConfigs.Count + 1}",
            savePath = "Assets/Data/SO",
            columnMappings = new List<ColumnMapping>()
        };

        parentFileConfig.worksheetConfigs.Add(newWorksheetConfig);
        EditorUtility.SetDirty(_excelToSoConfig);
        RefreshView();
        ShowNotification(new GUIContent("已添加新的工作表配置"));
    }

    private void DeleteCurrentItem()
    {
        if (_currentConfigObject == null || _currentConfigObject is ExcelToSOConfig)
            return;

        if (!EditorUtility.DisplayDialog("确认删除", "确定要删除这个配置项吗？", "删除", "取消"))
            return;

        bool removed = false;

        if (_currentConfigObject is ExcelFileConfig fileConfig)
        {
            removed = _excelToSoConfig?.excelFileConfigs?.Remove(fileConfig) ?? false;
        }
        else if (_currentConfigObject is WorksheetConfig worksheetConfig)
        {
            foreach (var fileConfigItem in _excelToSoConfig?.excelFileConfigs ?? new List<ExcelFileConfig>())
            {
                if (fileConfigItem.worksheetConfigs?.Remove(worksheetConfig) == true)
                {
                    removed = true;
                    break;
                }
            }
        }

        if (removed)
        {
            EditorUtility.SetDirty(_excelToSoConfig);
            RefreshView();
            _currentConfigObject = null;
            _currentConfigType = "无选择";
            ShowNotification(new GUIContent("配置项已删除"));
        }
    }

    private void SaveConfig()
    {
        if (_excelToSoConfig != null)
        {
            EditorUtility.SetDirty(_excelToSoConfig);
            AssetDatabase.SaveAssets();
            ShowNotification(new GUIContent("配置已保存！"));
        }
    }

    private void RefreshView()
    {
        BuildConfigTree();
        Repaint();
    }
    #endregion

    #region 辅助类
    private class ActionWrapper
    {
        public System.Action Action { get; }

        public ActionWrapper(System.Action action)
        {
            Action = action;
        }
    }
    #endregion

    #region 编辑器窗口管理
    private IEnumerable<ExcelToSOConfig> GetAllExcelToSOConfigs()
    {
        var guids = AssetDatabase.FindAssets("t:ExcelToSOConfig");
        var configs = new List<ExcelToSOConfig>();
        
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var config = AssetDatabase.LoadAssetAtPath<ExcelToSOConfig>(path);
            if (config != null) configs.Add(config);
        }
        
        return configs;
    }

    private void OnQuickSelectConfigChanged()
    {
        if (_quickSelectConfig != null)
        {
            _excelToSoConfig = _quickSelectConfig;
            BuildConfigTree();
        }
    }

    [MenuItem("Tools/Excel转SO/配置编辑器")]
    private static void OpenWindow()
    {
        var window = GetWindow<ExcelToSOConfigEditor>();
        window.titleContent = new GUIContent("Excel转SO配置编辑器");
        window.minSize = new Vector2(900, 600);
        window.Show();
    }

    protected override void Initialize()
    {
        base.Initialize();
        BuildConfigTree();
    }

    /// <summary>
    /// 使用完全自定义的GUI绘制，避免与Odin的布局冲突
    /// </summary>
    protected override void OnImGUI()
    {
        // 检测配置引用变化
        if (_excelToSoConfig == null && _quickSelectConfig != null)
        {
            _excelToSoConfig = _quickSelectConfig;
            BuildConfigTree();
        }

        // 绘制顶部配置选择区域
        DrawConfigSelectionArea();

        // 计算内容区域
        Rect contentRect = new Rect(0, 80, position.width, position.height - 80);
        
        // 绘制分割线
        Rect dividerRect = new Rect(contentRect.x + _leftPanelWidth, contentRect.y, 2, contentRect.height);
        GUI.Box(dividerRect, "");

        // 绘制左侧树形面板
        Rect leftPanelRect = new Rect(contentRect.x, contentRect.y, _leftPanelWidth, contentRect.height);
        DrawTreeView(leftPanelRect);

        // 绘制右侧配置面板
        Rect rightPanelRect = new Rect(contentRect.x + _leftPanelWidth + 2, contentRect.y, 
                                     contentRect.width - _leftPanelWidth - 2, contentRect.height);
        DrawConfigEditor(rightPanelRect);
    }

    private void DrawConfigSelectionArea()
    {
        Rect headerRect = new Rect(0, 0, position.width, 80);
        GUI.Box(headerRect, "");
        
        // 使用GUILayout在固定区域内绘制
        GUILayout.BeginArea(headerRect);
        GUILayout.BeginVertical();
        
        GUILayout.Label("配置文件选择", EditorStyles.boldLabel);
        
        GUILayout.BeginHorizontal();
        GUILayout.Label("Excel转SO配置:", GUILayout.Width(120));
        var newConfig = (ExcelToSOConfig)EditorGUILayout.ObjectField(_excelToSoConfig, typeof(ExcelToSOConfig), false);
        if (newConfig != _excelToSoConfig)
        {
            _excelToSoConfig = newConfig;
            BuildConfigTree();
        }
        GUILayout.EndHorizontal();

        if (_excelToSoConfig == null)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("快速选择:", GUILayout.Width(120));
            var newQuickConfig = (ExcelToSOConfig)EditorGUILayout.ObjectField(_quickSelectConfig, typeof(ExcelToSOConfig), false);
            if (newQuickConfig != _quickSelectConfig)
            {
                _quickSelectConfig = newQuickConfig;
                OnQuickSelectConfigChanged();
            }
            GUILayout.EndHorizontal();

            EditorGUILayout.HelpBox("请拖拽一个ExcelToSOConfig配置文件到上方字段", MessageType.Info);
        }
        else
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("保存配置")) SaveConfig();
            if (GUILayout.Button("刷新视图")) RefreshView();
            if (_currentConfigObject != null && !(_currentConfigObject is ExcelToSOConfig) && GUILayout.Button("删除当前项"))
            {
                DeleteCurrentItem();
            }
            GUILayout.EndHorizontal();
        }
        
        GUILayout.EndVertical();
        GUILayout.EndArea();
    }

    private void DrawTreeView(Rect position)
    {
        if (_configTree == null) BuildConfigTree();

        // 绘制树形视图标题
        Rect titleRect = new Rect(position.x, position.y, position.width, 20);
        GUI.Label(titleRect, "配置导航", EditorStyles.boldLabel);

        // 绘制树形视图内容
        Rect treeRect = new Rect(position.x, position.y + 25, position.width, position.height - 25);
        GUILayout.BeginArea(treeRect);
        if (_configTree != null)
        {
            _treeScrollPosition = GUILayout.BeginScrollView(_treeScrollPosition);
            _configTree.DrawMenuTree();
            GUILayout.EndScrollView();
        }
        GUILayout.EndArea();
    }

    private void DrawConfigEditor(Rect position)
    {
        GUILayout.BeginArea(position);
        
        if (_currentConfigObject == null)
        {
            GUILayout.FlexibleSpace();
            var style = new GUIStyle(EditorStyles.centeredGreyMiniLabel);
            style.fontSize = 14;
            GUILayout.Label("请在左侧选择要编辑的配置项", style);
            GUILayout.FlexibleSpace();
        }
        else
        {
            // 绘制编辑器标题
            GUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label($"当前编辑: {_currentConfigType}", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            
            EditorGUILayout.Space();

            try
            {
                if (_currentConfigObject is UnityEngine.Object unityObj)
                {
                    // Unity对象使用默认Editor绘制
                    var editor = UnityEditor.Editor.CreateEditor(unityObj);
                    _editorScrollPosition = GUILayout.BeginScrollView(_editorScrollPosition);
                    editor.OnInspectorGUI();
                    GUILayout.EndScrollView();
                }
                else
                {
                    // 非Unity对象使用缓存的PropertyTree绘制，添加明确布局层级
                    _editorScrollPosition = GUILayout.BeginScrollView(_editorScrollPosition);
                    EditorGUILayout.BeginVertical(GUI.skin.box); // 明确垂直布局容器
                    
                    if (_currentPropertyTree != null)
                    {
                        _currentPropertyTree.Draw(false);
                    }
                    
                    EditorGUILayout.EndVertical(); // 闭合垂直布局
                    GUILayout.EndScrollView();
                }
            }
            catch (System.Exception ex)
            {
                EditorGUILayout.HelpBox($"绘制配置时出错: {ex.Message}", MessageType.Error);
                Debug.LogError($"配置绘制错误: {ex}");
            }
        }
        
        GUILayout.EndArea();
    }

    // 窗口销毁时清理PropertyTree，避免内存泄漏
    private void OnDestroy()
    {
        if (_currentPropertyTree != null)
        {
            _currentPropertyTree.Dispose();
            _currentPropertyTree = null;
        }
    }
    #endregion
}
