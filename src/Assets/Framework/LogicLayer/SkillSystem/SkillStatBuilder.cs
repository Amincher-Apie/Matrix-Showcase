// LogicLayer/SkillSystem/SkillStatBuilder.cs
using System;
using UnityEngine;

/// <summary>
/// 技能五维属性的构造工具：
/// 统一从 AttributeSystem 中把技能相关属性映射到 SkillStatSnapshot。
/// </summary>
public static class SkillStatBuilder
{
    /// <summary>
    /// 通过一个 AttributeType -> float 的委托来构造 SkillStatSnapshot。
    /// 这样既可以接 ServerPlayerAttributeModule，也可以接其它实现 GetAttribute 的对象。
    /// </summary>
    public static SkillStatSnapshot FromAttributeGetter(Func<AttributeType, float> getAttr)
    {
        if (getAttr == null)
        {
            // 兜底：全 1 倍，没有冷却缩减
            return new SkillStatSnapshot
            {
                strength = 1f,
                duration = 1f,
                range = 1f,
                efficiency = 1f,
                cooldownReduction = 0f
            };
        }

        // 原始属性（这里假设 PlayerAttributeConfig / 怪物配置里默认都配成 1）
        float rawStrength   = getAttr(AttributeType.SkillStrength);
        float rawDuration   = getAttr(AttributeType.SkillDuration);
        float rawRange      = getAttr(AttributeType.SkillRange);
        float rawEfficiency = getAttr(AttributeType.SkillEfficiency);
        float rawCdr        = getAttr(AttributeType.CooldownReduction);

        // 为了兼容“未配置=0”的情况：如果 <=0，就退回到 1 倍
        float strength   = rawStrength   <= 0f ? 1f : rawStrength;
        float duration   = rawDuration   <= 0f ? 1f : rawDuration;
        float range      = rawRange      <= 0f ? 1f : rawRange;
        float efficiency = rawEfficiency <= 0f ? 1f : rawEfficiency;

        var snapshot = new SkillStatSnapshot
        {
            strength   = Mathf.Max(0f, strength),
            duration   = Mathf.Max(0f, duration),
            range      = Mathf.Max(0f, range),
            // 效率倍率做个安全夹取，避免被配置成过于离谱的值
            efficiency = Mathf.Clamp(efficiency, 0f, 2f),
            // 冷却缩减只允许 0~1 之间
            cooldownReduction = Mathf.Clamp01(rawCdr)
        };

        return snapshot;
    }
}