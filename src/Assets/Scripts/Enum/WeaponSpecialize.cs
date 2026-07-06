using System;

public enum BulletKind { Projectile, HitScan }
public enum FireMode { Semi, Auto, Charge }

[Serializable]
public sealed class DamageProfile
{
    public int solid;   // 固体
    public int liquid;  // 液体
    public int gas;     // 气体
    public int ice;     // 冰
    public int fire;    // 火
    public int toxic;   // 毒
    public int electric;// 电
}