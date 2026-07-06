using System.Collections.Generic;
using Sirenix.OdinInspector;

/// <summary>
/// 品质效果系统常量定义
/// 用于在Inspector中提供下拉列表选择，避免手动输入字符串导致拼写错误
/// </summary>
public static class QualityEffectConstants
{
    #region 中英文对照映射
    
    /// <summary>
    /// 触发器ID的中英文对照
    /// </summary>
    public static readonly Dictionary<string, string> TriggerIdNames = new Dictionary<string, string>
    {
        { "OnHitDealt", "造成伤害时" },
        { "OnHitReceived", "受到伤害时" },
        { "OnCrit", "造成暴击时" },
        { "OnKill", "击杀敌人时" },
        { "OnDeath", "自身死亡时" },
        { "OnSkillCast", "释放技能时" },
        { "OnSkillHit", "技能命中时" },
        { "OnHeal", "受到治疗时" },
        { "OnEquip", "装备道具时" },
        { "OnUnequip", "卸下道具时" },
        { "OnStackChanged", "堆叠层数变化时" },
        { "OnEnterArea", "进入区域时" },
        { "OnUseItem", "使用主动道具时" },
    };
    
    /// <summary>
    /// 条件ID的中英文对照
    /// </summary>
    public static readonly Dictionary<string, string> ConditionIdNames = new Dictionary<string, string>
    {
        { "RandomChance", "随机几率" },
        { "HPRatioLessThan", "血量比例低于阈值" },
        { "HPRatioGreaterThan", "血量比例高于阈值" },
        { "HasStatus", "拥有特定状态" },
        { "NoStatus", "不拥有特定状态" },
        { "IsElite", "目标是精英/Boss" },
        { "StackGreaterThan", "层数大于等于阈值" },
        { "RecentKills", "最近N秒内击杀数≥阈值" },
        { "DistanceLessThan", "距离小于阈值" },
        { "IsFacing", "面向目标" },
        { "AttributeGreater", "自身属性≥目标属性" },
    };
    
    /// <summary>
    /// 动作ID的中英文对照
    /// </summary>
    public static readonly Dictionary<string, string> ActionIdNames = new Dictionary<string, string>
    {
        { "AddStat", "增加属性" },
        { "AddWeaponStat", "增加武器属性" },
        { "ApplyDot", "施加持续伤害" },
        { "Summon", "召唤单位" },
        { "ApplyHoT", "施加持续治疗" },
        { "RemoveStat", "移除属性" },
        { "RemoveWeaponStat", "移除武器属性" },
        { "ApplyBuff", "施加增益状态" },
        { "ApplyDebuff", "施加减益状态" },
        { "AddDamage", "造成瞬时伤害" },
        { "Heal", "瞬时治疗" },
        { "Teleport", "瞬移" },
        { "ModifyCooldown", "修改技能冷却" },
        { "DropItem", "掉落物品" },
    };
    
    /// <summary>
    /// 参数键名的中英文对照
    /// </summary>
    public static readonly Dictionary<string, string> ParamKeyNames = new Dictionary<string, string>
    {
        // 浮点数参数
        { "chance", "概率" },
        { "chance.base", "概率-基础值" },
        { "chance.perStack", "概率-每层增加" },
        { "chance.perQuality", "概率-每品质增加" },
        { "chance.mult", "概率-倍数" },
        { "duration", "持续时间" },
        { "duration.base", "持续时间-基础值" },
        { "duration.perStack", "持续时间-每层增加" },
        { "duration.perQuality", "持续时间-每品质增加" },
        { "duration.mult", "持续时间-倍数" },
        { "amount", "数值" },
        { "amount.base", "数值-基础值" },
        { "amount.perStack", "数值-每层增加" },
        { "amount.perQuality", "数值-每品质增加" },
        { "amount.mult", "数值-倍数" },
        { "damage", "伤害" },
        { "damage.base", "伤害-基础值" },
        { "damage.perStack", "伤害-每层增加" },
        { "damagePctOfBase", "伤害百分比(基于基础伤害)" },
        { "damagePct", "伤害百分比" },
        { "range", "范围" },
        { "range.base", "范围-基础值" },
        { "range.perStack", "范围-每层增加" },
        { "threshold", "阈值" },
        { "count", "数量" },
        { "window", "时间窗口(秒)" },
        { "angle", "角度" },
        { "distance", "距离" },
        { "slowPct", "减速百分比" },
        { "moveSpeedPct", "移速百分比" },
        { "attackSpeedPct", "攻速百分比" },
        { "armorReduction", "护甲削减" },
        { "reduceAmount", "减少数值" },
        { "dropChance", "掉落概率" },
        { "modifyType", "武器修改类型" },
        { "operator", "修改操作符" },
        { "elementType", "元素类型" },
        // 字符串参数
        { "stat", "属性名" },
        { "buffId", "增益状态ID" },
        { "debuffId", "减益状态ID" },
        { "statusId", "状态ID" },
        { "target", "目标" },
        { "type", "类型" },
        { "unitId", "单位ID" },
        { "skillId", "技能ID" },
        { "itemId", "物品ID" },
        { "areaId", "区域ID" },
        { "damageType", "伤害类型" },
        { "direction", "方向" },
        { "ownerAttr", "自身属性" },
        { "targetAttr", "目标属性" },
        { "center", "中心点" },
    };
    
    #endregion
    
    #region 触发器ID
    
    /// <summary>
    /// 所有可用的触发器ID列表
    /// </summary>
    public static readonly List<string> TriggerIds = new List<string>
    {
        "OnHitDealt",      // 造成伤害时
        "OnHitReceived",   // 受到伤害时
        "OnCrit",          // 造成暴击时
        "OnKill",          // 击杀敌人时
        "OnDeath",         // 自身死亡时
        "OnSkillCast",     // 释放技能时
        "OnSkillHit",      // 技能命中时
        "OnHeal",          // 受到治疗时
        "OnEquip",         // 装备道具时
        "OnUnequip",       // 卸下道具时
        "OnStackChanged",  // 堆叠层数变化时
        "OnEnterArea",     // 进入区域时
        "OnUseItem",       // 使用主动道具时
    };
    
    /// <summary>
    /// 定时触发器前缀（需要配合数值使用，如 "OnTick:1.0"）
    /// </summary>
    public const string TickTriggerPrefix = "OnTick:";
    
    #endregion
    
    #region 条件ID
    
    /// <summary>
    /// 所有可用的条件ID列表
    /// </summary>
    public static readonly List<string> ConditionIds = new List<string>
    {
        "RandomChance",        // 随机几率
        "HPRatioLessThan",     // 血量比例低于阈值
        "HPRatioGreaterThan",  // 血量比例高于阈值
        "HasStatus",           // 拥有特定状态
        "NoStatus",            // 不拥有特定状态
        "IsElite",             // 目标是精英/Boss
        "StackGreaterThan",    // 层数大于等于阈值
        "RecentKills",         // 最近N秒内击杀数≥阈值
        "DistanceLessThan",    // 距离小于阈值
        "IsFacing",            // 面向目标
        "AttributeGreater",    // 自身属性≥目标属性
    };
    
    #endregion
    
    #region 动作ID
    
    /// <summary>
    /// 所有可用的动作ID列表
    /// </summary>
    public static readonly List<string> ActionIds = new List<string>
    {
        "AddStat",         // 增加属性
        "AddWeaponStat",   // 增加武器属性
        "ApplyDot",         // 施加持续伤害
        "Summon",           // 召唤单位
        "ApplyHoT",          // 施加持续治疗
        "RemoveStat",       // 移除属性
        "RemoveWeaponStat", // 移除武器属性
        "ApplyBuff",        // 施加增益状态
        "ApplyDebuff",      // 施加减益状态
        "AddDamage",        // 造成瞬时伤害
        "Heal",             // 瞬时治疗
        "Teleport",         // 瞬移
        "ModifyCooldown",   // 修改技能冷却
        "DropItem",         // 掉落物品
    };
    
    #endregion
    
    #region 参数键名常量（常用参数）
    
    /// <summary>
    /// 常用浮点数参数键名
    /// </summary>
    public static class FloatParams
    {
        // 通用参数
        public const string Chance = "chance";
        public const string ChanceBase = "chance.base";
        public const string ChancePerStack = "chance.perStack";
        public const string ChancePerQuality = "chance.perQuality";
        public const string ChanceMult = "chance.mult";
        
        public const string Duration = "duration";
        public const string DurationBase = "duration.base";
        public const string DurationPerStack = "duration.perStack";
        public const string DurationPerQuality = "duration.perQuality";
        public const string DurationMult = "duration.mult";
        
        public const string Amount = "amount";
        public const string AmountBase = "amount.base";
        public const string AmountPerStack = "amount.perStack";
        public const string AmountPerQuality = "amount.perQuality";
        public const string AmountMult = "amount.mult";
        
        public const string Damage = "damage";
        public const string DamageBase = "damage.base";
        public const string DamagePerStack = "damage.perStack";
        
        public const string DamagePctOfBase = "damagePctOfBase";
        public const string DamagePct = "damagePct";
        
        public const string Range = "range";
        public const string RangeBase = "range.base";
        public const string RangePerStack = "range.perStack";
        
        public const string Threshold = "threshold";
        public const string Count = "count";
        public const string Window = "window";
        public const string Angle = "angle";
        public const string Distance = "distance";
        
        public const string SlowPct = "slowPct";
        public const string MoveSpeedPct = "moveSpeedPct";
        public const string AttackSpeedPct = "attackSpeedPct";
        public const string ArmorReduction = "armorReduction";
        public const string ReduceAmount = "reduceAmount";
        public const string DropChance = "dropChance";
    }
    
    /// <summary>
    /// 常用字符串参数键名
    /// </summary>
    public static class StringParams
    {
        public const string Stat = "stat";
        public const string BuffId = "buffId";
        public const string DebuffId = "debuffId";
        public const string StatusId = "statusId";
        public const string Target = "target";
        public const string Type = "type";
        public const string UnitId = "unitId";
        public const string SkillId = "skillId";
        public const string ItemId = "itemId";
        public const string AreaId = "areaId";
        public const string DamageType = "damageType";
        public const string Direction = "direction";
        public const string OwnerAttr = "ownerAttr";
        public const string TargetAttr = "targetAttr";
        public const string Center = "center";
        public const string ModifyType = "modifyType";
        public const string Operator = "operator";
        public const string ElementType = "elementType";
    }
    
    #endregion
    
    #region 堆叠规则名称
    
    /// <summary>
    /// 预设堆叠规则名称列表（用于 Inspector 下拉框显示）
    /// </summary>
    public static readonly List<string> StackingRuleNames = new List<string>
    {
        "Add",
        "Max",
        "Min",
        "NoStack",
        "Average",
        "StackByQuality"
    };
    
    #endregion
    
    #region 条件ID和动作ID到参数列表的映射
    
    /// <summary>
    /// 条件ID到可用参数列表的映射（只包含该条件实际需要的参数）
    /// </summary>
    public static readonly Dictionary<string, List<string>> ConditionParamMap = new Dictionary<string, List<string>>
    {
        { "RandomChance", new List<string> { "chance", "chance.base", "chance.perStack", "chance.perQuality", "chance.mult" } },
        { "HPRatioLessThan", new List<string> { "threshold", "target" } },
        { "HPRatioGreaterThan", new List<string> { "threshold", "target" } },
        { "HasStatus", new List<string> { "statusId", "target" } },
        { "NoStatus", new List<string> { "statusId", "target" } },
        { "IsElite", new List<string> { "type" } },
        { "StackGreaterThan", new List<string> { "threshold" } },
        { "RecentKills", new List<string> { "count", "window" } },
        { "DistanceLessThan", new List<string> { "range", "range.base", "range.perStack" } },
        { "IsFacing", new List<string> { "angle" } },
        { "AttributeGreater", new List<string> { "ownerAttr", "targetAttr" } },
    };
    
    /// <summary>
    /// 动作ID到可用参数列表的映射（只包含该动作实际需要的参数）
    /// </summary>
    public static readonly Dictionary<string, List<string>> ActionParamMap = new Dictionary<string, List<string>>
    {
        { "AddStat", new List<string> { "stat", "amount", "amount.base", "amount.perStack", "amount.perQuality", "amount.mult" } },
        { "AddWeaponStat", new List<string> { "stat", "amount", "amount.base", "amount.perStack", "amount.perQuality", "amount.mult", "modifyType", "operator", "elementType" } },
        { "ApplyDot", new List<string> { "damagePctOfBase", "duration", "duration.base", "duration.perStack", "duration.perQuality", "duration.mult" } },
        { "Summon", new List<string> { "unitId", "damageMultiplier", "duration", "duration.base", "duration.perStack", "duration.perQuality", "duration.mult" } },
        { "ApplyHoT", new List<string> { "amount", "amount.base", "amount.perStack", "amount.perQuality", "amount.mult", "duration", "duration.base", "duration.perStack", "duration.perQuality", "duration.mult" } },
        { "RemoveStat", new List<string> { "stat", "amount", "amount.base", "amount.perStack", "amount.perQuality", "amount.mult" } },
        { "RemoveWeaponStat", new List<string> { "stat" } },
        { "ApplyBuff", new List<string> { "buffId", "duration", "duration.base", "duration.perStack", "duration.perQuality", "duration.mult", "moveSpeedPct", "attackSpeedPct", "damagePct" } },
        { "ApplyDebuff", new List<string> { "debuffId", "duration", "duration.base", "duration.perStack", "duration.perQuality", "duration.mult", "slowPct", "armorReduction" } },
        { "AddDamage", new List<string> { "damage", "damage.base", "damage.perStack", "damageType" } },
        { "Heal", new List<string> { "amount", "amount.base", "amount.perStack", "amount.perQuality", "amount.mult" } },
        { "Teleport", new List<string> { "distance", "direction" } },
        { "ModifyCooldown", new List<string> { "skillId", "reduceAmount" } },
        { "DropItem", new List<string> { "itemId", "count", "dropChance" } },
    };
    
    #endregion
}

