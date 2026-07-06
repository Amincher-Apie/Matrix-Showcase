using Sirenix.OdinInspector;

/// <summary>
/// 道具品质分层
/// </summary>
public enum EnumQualityLevel
{
    [LabelText("普通 白色")]
    Common,    // 普通（白）
    [LabelText("罕见 绿色")]
    Uncommon,  // 罕见（绿）
    [LabelText("稀有 蓝色")]
    Rare,      // 稀有（蓝）
    [LabelText("史诗 紫色")]
    Epic,      // 史诗（紫）
    [LabelText("传奇 红色")]
    Legendary  // 传奇（红）
}

