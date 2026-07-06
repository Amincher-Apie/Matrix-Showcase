using UnityEngine;

public enum SkillCostType
{
    CooldownOnly,     // 只冷却
    EnergyOnly,       // 只消耗能量（充能类）
    CooldownAndEnergy // 冷却 + 蓝耗
}

public enum SkillTargetType
{
    None,
    Self,
    Direction,   // 朝向射线 / 扇形等
    Point,       // 地面点
    Actor        // 锁定单位
}

public enum SkillPhaseState
{
    Idle,
    Precast,   // 前摇（吟唱）
    Casting,   // 释放动画阶段
    Postcast   // 后摇
}

public enum FactionFilterType
{
    None,
    EnemyOnly,
    AllButSelf,
    AllyOnly,
    SelfOnly
}

/// <summary>
/// 五维属性在某一时刻的快照，全部是“倍率”概念：1 = 100%
/// 冷却缩减因为已经写在 AttributeSystem 里，这里只是留一个字段方便你以后用；
/// 当前实现里冷却直接走 ServerPlayerAttributeModule.GetReducedCooldown。
/// </summary>
[System.Serializable]
public struct SkillStatSnapshot
{
    [Range(0f, 10f)] public float strength;          // 强度倍率，默认 1
    [Range(0f, 10f)] public float duration;          // 持续时间倍率，默认 1
    [Range(0f, 10f)] public float range;             // 范围倍率，默认 1
    [Range(0f, 2f)]  public float efficiency;        // 效率倍率，默认 1，公式里会做 0~1.75 限制
    [Range(0f, 1f)]  public float cooldownReduction; // 冷却缩减，0-1，实际冷却 = base * (1 - cdr)
}
