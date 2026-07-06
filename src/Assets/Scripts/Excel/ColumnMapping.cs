using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Excel列与SO字段的映射配置类
/// 定义单个Excel列的解析规则，支持"单列单值"和"单列多值"两种模式
/// 动态加载SO字段列表，避免手动输入字段名导致的错误
/// </summary>
[Serializable]
public class ColumnMapping
{
    /// <summary>
    /// Excel列号（从1开始）
    /// 对应Excel中的实际列位置（如A列=1，B列=2，以此类推）
    /// </summary>
    [LabelText("Excel列号(1开始)"), MinValue(1), Tooltip("对应Excel的列号（A=1，B=2，以此类推）")]
    public int excelColumnIndex;
    
    /// <summary>
    /// 列映射类型
    /// 选择当前列的解析模式（单列单值/单列多值）
    /// </summary>
    [LabelText("列映射类型"), Tooltip("选择当前列是单列单值还是单列多值")]
    public ColumnMappingType columnMappingType = ColumnMappingType.SingleField;

    #region 单列单值配置（仅当类型为SingleField时显示）
    /// <summary>
    /// 单列单值模式的SO字段名
    /// 存储当前Excel列需要赋值的SO字段名称（通过下拉选择）
    /// </summary>
    [ShowIf("columnMappingType", ColumnMappingType.SingleField), 
     LabelText("SO字段名"), 
     Tooltip("从SO模板的字段中选择（需先配置SO模板）"),
     ValueDropdown("GetSoFieldNames")]
    public string singleSoFieldName;
    #endregion

    #region 单列多值配置（仅当类型为MultiField时显示）
    /// <summary>
    /// 单列多值模式的分隔符
    /// 用于分割Excel列中的多值数据（如#、,、|，需与Excel中的符号一致）
    /// </summary>
    [ShowIf("columnMappingType", ColumnMappingType.MultiField), 
     LabelText("多参数分隔符"), 
     Tooltip("分割列值的符号（如#、,、|，区分中英文）")]
    public string separator = "#";
    
    /// <summary>
    /// 单列多值模式的子字段映射列表
    /// 存储"分割后的值索引"与"SO字段"的对应关系（如索引0→param1）
    /// </summary>
    [ShowIf("columnMappingType", ColumnMappingType.MultiField), 
     LabelText("子字段映射列表"), 
     TableList(ShowPaging = false, DrawScrollView = true, MaxScrollViewHeight = 150), 
     Tooltip("配置分割后的值与SO字段的对应关系")]
    public List<MultiFieldItem> multiFieldItems = new List<MultiFieldItem>();
    #endregion

    /// <summary>
    /// 动态获取SO模板的字段列表（用于下拉选择）
    /// 根据父级WorksheetConfig的SO模板，反射加载有效字段，支持两种映射模式
    /// </summary>
    /// <returns>SO模板的有效字段名称列表，未找到则返回提示文本</returns>
    private IEnumerable<string> GetSoFieldNames()
    {
        // 步骤1：查找父级WorksheetConfig（获取SO模板的来源）
        var worksheetConfig = ParentFinder.FindParent<WorksheetConfig>(this);
        if (worksheetConfig == null)
        {
            return new List<string> { "请先保存配置，再添加列映射" };
        }

        // 步骤2：检查SO模板是否有效（无模板则无法获取字段）
        if (worksheetConfig.soTemplate == null)
        {
            return new List<string> { "请先为工作表配置SO模板" };
        }

        // 步骤3：反射获取SO模板的有效字段（过滤无效字段）
        Type soType = worksheetConfig.soTemplate.GetType();
        var validFields = soType.GetFields(BindingFlags.Public | BindingFlags.Instance)
            .Where(field => 
                field.DeclaringType != null && // 排除无声明类型的字段
                !ParentFinder.IsUnityBuiltInType(field.DeclaringType) && // 排除Unity内置字段
                field.FieldType != typeof(object) // 排除无意义的object类型字段
            )
            .Select(field => field.Name) // 提取字段名
            .OrderBy(name => name); // 按名称排序，提升查找效率

        // 步骤4：无有效字段时返回提示（避免空列表）
        return validFields.Any() ? validFields : new List<string> { "SO模板中无有效public字段" };
    }
}
