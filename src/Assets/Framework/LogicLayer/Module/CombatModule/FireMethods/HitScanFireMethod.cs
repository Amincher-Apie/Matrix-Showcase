// 文件位置: LogicLayer/CombatModule/FireMethods/HitScanFireMethod.cs

using System.Collections.Generic;
using UnityEngine;

public class HitScanFireMethod : IFireMethod
{
    private readonly IHitScanService _hitScanService;

    public HitScanFireMethod(IHitScanService hitScanService)
    {
        _hitScanService = hitScanService;
    }

    public BulletKind GetBulletKind() => BulletKind.HitScan;

    public List<HitResult> ClientExecute(FireContext context, ServerWeaponRuntime weapon)
    {
        var hits = _hitScanService.Raycast(context.origin, context.dir, 
            weapon.GetAttribute(WeaponAttributeType.RangeMax), 
            Mathf.Max(1, (int)weapon.WeaponSO.bulletCount));
#if UNITY_EDITOR
        float range = weapon.GetAttribute(WeaponAttributeType.RangeMax);
        DebugLog.Info("Combat.HitScan", $"[HitScanFireMethod] client RangeMax = {range}");
#endif
        var hitResults = new List<HitResult>();
        foreach (var hit in hits)
        {
            // 过滤掉自己
            if (hit.targetId == context.shooterObjectId)
                continue;
            hitResults.Add(new HitResult
            {
                targetId = hit.targetId,
                point = hit.point,
                normal = hit.normal,
                distance = hit.distance
            });
        }

        return hitResults;
    }

    public FireValidationResult ServerValidate(FireContext context, List<HitResult> clientHits, ServerWeaponRuntime weapon)
    {
        var result = new FireValidationResult { isValid = true, validatedHits = new List<ValidatedHit>() };

        // 服务器重新进行视线检测
        var serverHits = _hitScanService.Raycast(context.origin, context.dir, 
            weapon.GetAttribute(WeaponAttributeType.RangeMax), 
            Mathf.Max(1, (int)weapon.WeaponSO.bulletCount));

        // 验证客户端命中结果
        foreach (var clientHit in clientHits)
        {
            bool isValidHit = false;
            
            // 检查服务器结果中是否有匹配的命中
            foreach (var serverHit in serverHits)
            {
                if (serverHit.targetId == clientHit.targetId && 
                    Vector3.Distance(serverHit.point, clientHit.point) < 1.0f) // 允许1米的位置误差
                {
#if UNITY_EDITOR
                    DebugLog.Warning("Combat.HitScan", $"[HitScanFireMethod] 服务器验证命中 - 目标ID: {serverHit.targetId}, 服务器点: {serverHit.point}, 客户端点: {clientHit.point}, 距离差: {Vector3.Distance(serverHit.point, clientHit.point)}");
#endif
                    isValidHit = true;
                    result.validatedHits.Add(new ValidatedHit
                    {
                        targetId = serverHit.targetId,
                        point = serverHit.point,
                        normal = serverHit.normal,
                        distance = serverHit.distance
                    });
                    break;
                }
                DebugLog.Warning("Combat.HitScan", $"[HitScanFireMethod] 命中验证失败 - 服务器目标ID: {serverHit.targetId}, 客户端目标ID: {clientHit.targetId}, 点距离: {Vector3.Distance(serverHit.point, clientHit.point)}");
            }

            if (!isValidHit)
            {
                
                // 客户端发送了无效命中，标记为可疑
                result.isValid = false;
            }
        }

        return result;
    }
}