using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// 单列多值模式的子字段映射配置类
/// 用于定义"分割后的值"与"SO字段"的对应关系（如分割索引0→SO的param1字段）
/// 支持下拉选择SO字段，避免手动输入错误
/// </summary>
[Serializable]
public class MultiFieldItem
{
    /// <summary>
    /// 分割后的值的索引（从0开始）
    /// 对应Excel列中分割后的顺序（如"700#1000"中，700的索引为0，1000的索引为1）
    /// </summary>
    [LabelText("#号分割后的子字段索引"), MinValue(0), Tooltip("分割后的值的顺序（0开始，如第一个值填0）")]
    public int splitIndex; 
    
    /// <summary>
    /// 目标SO字段名
    /// 存储分割后的值需要赋值的SO字段名称（通过下拉选择，无需手动输入）
    /// </summary>
    [LabelText("目标SO对应字段的名称"), 
     Tooltip("从SO模板的字段中选择（需先配置SO模板）"),
     ValueDropdown("GetSoFieldNames")]
    public string targetSoFieldName;
    
    /// <summary>
    /// 动态获取SO模板的字段列表（用于下拉选择）
    /// 根据父级ColumnMapping→WorksheetConfig的SO模板，反射加载有效字段
    /// </summary>
    /// <returns>SO模板的有效字段名称列表，未找到则返回提示文本</returns>
    private IEnumerable<string> GetSoFieldNames()
    {
        // 步骤1：查找父级ColumnMapping（当前MultiFieldItem所属的列映射）
        var columnMapping = ParentFinder.FindParent<ColumnMapping>(this);
        if (columnMapping == null)
        {
            return new List<string> { "请保存配置后，再添加子字段" };
        }

        // 步骤2：查找祖父级WorksheetConfig（获取SO模板的来源）
        var worksheetConfig = ParentFinder.FindParent<WorksheetConfig>(columnMapping);
        if (worksheetConfig == null)
        {
            return new List<string> { "列映射未关联到工作表，请检查配置" };
        }

        // 步骤3：检查SO模板是否有效（无模板则无法获取字段）
        if (worksheetConfig.soTemplate == null)
        {
            return new List<string> { "工作表未配置SO模板，请先拖拽模板" };
        }

        // 步骤4：反射获取SO模板的有效字段（过滤Unity内置字段）
        Type soType = worksheetConfig.soTemplate.GetType();
        var validFields = soType.GetFields(BindingFlags.Public | BindingFlags.Instance)
            .Where(field => 
                field.DeclaringType != null && // 排除无声明类型的字段
                !ParentFinder.IsUnityBuiltInType(field.DeclaringType) && // 排除Unity内置字段
                field.FieldType != typeof(object) // 排除无意义的object类型字段
            )
            .Select(field => field.Name) // 提取字段名
            .OrderBy(name => name); // 按名称排序，提升查找效率

        // 步骤5：无有效字段时返回提示（避免空列表）
        return validFields.Any() ? validFields : new List<string> { "SO模板中无有效public字段" };
    }
}
