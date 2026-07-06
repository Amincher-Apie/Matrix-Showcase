/// <summary>
/// 武器修改类型
/// </summary>
public enum WeaponModifyType
{
    TotalDamage,        // 总伤害
    SpecificElement,    // 特定元素伤害
    CritChance,         // 暴击率
    CritMultiplier,     // 暴击伤害
    ProcChance,         // 触发几率
    FireRate,           // 射速
    MagazineSize,       // 弹匣容量
    ReloadSpeed,        // 装填速度
    BulletSpeed,        // 子弹速度
    Spread,             // 散布
    Range               // 射程
}

/// <summary>
/// 武器属性类型（用于修改器）
/// </summary>
public enum WeaponAttributeType
{
    // 基础属性
    SolidDamage,
    LiquidDamage,
    GasDamage,
    IceDamage,
    FireDamage,
    ToxicDamage,
    ElectricDamage,
    
    // 武器属性
    CritChance,
    CritMultiplier,
    ProcChance,
    FireRate,
    MagazineSize,
    ReloadTime,
    ChargeTime,
    BulletSpeed,
    Spread,
    RangeMin,
    RangeMax
}