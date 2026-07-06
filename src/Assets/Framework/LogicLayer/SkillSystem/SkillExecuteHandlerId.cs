using UnityEngine;

/// <summary>
/// SkillDefinitionSO 可选择的技能执行器 ID。
/// 枚举名通过 ToHandlerIdString() 映射到 SkillExecuteRegistry 的字符串 key。
/// </summary>
public enum SkillExecuteHandlerId
{
    [InspectorName("未配置")]
    None = 0,

    [InspectorName("熔岩地狱 (BombardArea)")]
    BombardArea = 1,

    [InspectorName("穿刺射击 (PiercingShot)")]
    PiercingShot = 2,

    [InspectorName("动能增效 (KineticBoost)")]
    KineticBoost = 3,
}

public static class SkillExecuteHandlerIdExtensions
{
    public static string ToHandlerIdString(this SkillExecuteHandlerId handlerId)
    {
        return handlerId switch
        {
            SkillExecuteHandlerId.BombardArea => "BombardArea",
            SkillExecuteHandlerId.PiercingShot => "PiercingShot",
            SkillExecuteHandlerId.KineticBoost => "KineticBoost",
            _ => string.Empty
        };
    }

    public static SkillExecuteHandlerId FromHandlerIdString(string handlerId)
    {
        return handlerId switch
        {
            "BombardArea" => SkillExecuteHandlerId.BombardArea,
            "PiercingShot" => SkillExecuteHandlerId.PiercingShot,
            "KineticBoost" => SkillExecuteHandlerId.KineticBoost,
            _ => SkillExecuteHandlerId.None
        };
    }
}
