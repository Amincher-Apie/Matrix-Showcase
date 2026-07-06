using System;



[Serializable]
public sealed class WeaponConfig
{
    public string id;
    public string displayName;
    public string category;

    public DamageProfile baseDamage;
    public float critChance;     // 暴击几率（可>100%按层级暴击）
    public float critMultiplier; // 暴击伤害倍率
    public float procChance;     // 触发异常层数的几率

    public int rpm;              // 射速（发/分钟）
    public float rangeMin;       // 有效最小射程（米）
    public float rangeMax;       // 有效最大射程（米）

    public int pelletCount;      // 弹头数（霰弹>1）
    public float bulletSpeed;    // m/s（Projectile使用）
    public float spread;         // 0~1（1=全屏随机）
    public int magazineSize;     // 弹匣容量
    public float reloadTime;     // s
    public float chargeTime;     // s（蓄力武器）
    public FireMode fireMode;
    public BulletKind bulletKind;
}

