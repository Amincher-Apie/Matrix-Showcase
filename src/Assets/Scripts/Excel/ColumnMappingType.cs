using Sirenix.OdinInspector;

/// <summary>
/// 定义Excel列与SO字段的映射类型枚举
/// 用于区分不同的列数据解析方式
/// </summary>
public enum ColumnMappingType 
{
    /// <summary>
    /// 单列单值模式：1个Excel列对应SO的1个字段
    /// 适用于常规配置（如"名称列"→SO的name字段）
    /// </summary>
    [LabelText("单列单值（1列→1字段）")]
    SingleField,
    
    /// <summary>
    /// 单列多值模式：1个Excel列用分隔符分割后对应SO的多个字段
    /// 适用于参数打包场景（如"700#1000"→SO的param1和param2字段）
    /// </summary>
    [LabelText("单列多值（1列→多字段，分隔符分割）")]
    MultiField
}