// LogicLayer/Module/AttributeModule/AttributeEnum.cs
/// <summary>
/// 属性类型枚举
/// </summary>
public enum AttributeType
{
    // 基础属性（有最大值限制）
    Health,                 // 当前生命值
    MaxHealth,              // 最大生命值
    Shield,                 // 当前护盾值
    MaxShield,              // 最大护盾值
    Armor,                  // 护甲值
    MoveSpeed,              // 移动速度
    Level,                  // 等级
    
    // 伤害抗性（百分比）
    Resistance_Solid,       // 固体抗性
    Resistance_Liquid,      // 液体抗性
    Resistance_Gas,         // 气体抗性
    Resistance_Toxic,       // 毒素抗性
    Resistance_Fire,        // 火焰抗性
    Resistance_Ice,         // 冰冻抗性
    Resistance_Electric,    // 电击抗性
    
    Resilience,             // 韧性
    // 伤害加成（百分比）
    DamageOutPutRate,        // 伤害输出率 对应增伤区域，这里是对所有伤害都有加成
    
    
    // 派系（特殊处理）
    Faction,
    
    // === 玩家特有属性 ===
    Energy = 100,           // 当前能量值
    MaxEnergy,              // 最大能量值
    EnergyRegen,            // 能量恢复
    Luck,                   // 幸运值
    CooldownReduction,      // 冷却缩减
    ArmorPenetrationRate,   // 护甲穿透率
    SkillStrength,          // 技能强度
    SkillDuration,          // 技能持续时间
    SkillRange,             // 技能范围
    SkillEfficiency,        // 技能效率
    
    // === 怪物特有属性 ===
    AggroRange = 200,       // 仇恨范围
    MonsterRank,            // 怪物类别
    DropRate,               // 掉落率
    InGameGoldReward,       // 局内金币奖励
    OutGameCurrencyReward,  // 局外货币奖励
    DetectionRange,         // 侦测范围
}

/// <summary>
/// 属性修改类型
/// </summary>
public enum AttributeModifyType
{
    Set,        // 直接设置（最高优先级）
    Add,        // 加法
    Multiply,   // 乘法（连乘）
    Percentage  // 百分比（累加百分比）
}

/// <summary>
/// 元素类型
/// </summary>
public enum ElementType
{
    Ice,
    Fire,
    Poison, 
    Electric
}

/// <summary>
/// 派系枚举
/// </summary>
public enum FactionType
{
    Neutral,    // 中立
    Player,     // 玩家
    Enemy,      // 敌人
    Ally        // 盟友
}

public enum MonsterRank
{
    Normal,
    Elite,
    Boss
}