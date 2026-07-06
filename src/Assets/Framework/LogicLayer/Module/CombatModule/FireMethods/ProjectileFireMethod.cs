// 文件位置: LogicLayer/CombatModule/FireMethods/ProjectileFireMethod.cs

using System.Collections.Generic;

public class ProjectileFireMethod : IFireMethod
{
    public BulletKind GetBulletKind() => BulletKind.Projectile;

    public List<HitResult> ClientExecute(FireContext context, ServerWeaponRuntime weapon)
    {
        // 投射物在客户端只预测发射，不预测命中
        // 命中由服务器计算
        return new List<HitResult>();
    }

    public FireValidationResult ServerValidate(FireContext context, List<HitResult> clientHits, ServerWeaponRuntime weapon)
    {
        // 投射物开火总是有效，命中由投射物飞行过程中计算
        return new FireValidationResult 
        { 
            isValid = true, 
            validatedHits = new List<ValidatedHit>(),
            projectileInfo = new ProjectileInfo
            {
                origin = context.origin,
                direction = context.dir,
                speed = weapon.GetAttribute(WeaponAttributeType.BulletSpeed),
                range = weapon.GetAttribute(WeaponAttributeType.RangeMax),
                weaponId = weapon.WeaponSO.id,
                instigatorId = context.instigator,
                shotId = context.shotId
            }
        };
    }
}