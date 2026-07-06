using System;
using System.Collections.Generic;
using Framework.LogicLayer.DamageCenter;
using UnityEngine;

/// <summary>
/// 直线穿透射击：沿施法方向命中多个目标，每穿透一个目标伤害递减 20%。
/// </summary>
public class PiercingShotSkillExecutor : ISkillExecute
{
    public string Id => "PiercingShot";

    public void ClientPredictExecute(in SkillRuntimeContext ctx)
    {
        // TODO: 客户端生成本地子弹轨迹 / 锁定线等纯表现
        Debug.Log("[PiercingShot] ClientPredictExecute");
    }

    public void ServerExecute(in SkillRuntimeContext ctx)
    {
        var caster = ctx.caster;
        if (caster == null || caster.networkProxy == null) return;

        var casterProxy = caster.networkProxy;
        Vector3 origin = ctx.castContext.origin;
        if (origin == Vector3.zero)
        {
            origin = casterProxy.transform.position + Vector3.up * 1.4f;
        }

        Vector3 direction = ctx.castContext.direction.sqrMagnitude > 0.0001f
            ? ctx.castContext.direction.normalized
            : casterProxy.transform.forward;

        float range = Mathf.Max(0.1f, ctx.finalRange);
        RaycastHit[] hits = ctx.definition.spreadRadius > 0f
            ? Physics.SphereCastAll(origin, ctx.definition.spreadRadius, direction, range)
            : Physics.RaycastAll(origin, direction, range);

        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        var visited = new HashSet<ulong>();
        int hitIndex = 0;
        int maxTargets = ctx.definition.maxTargets > 0 ? ctx.definition.maxTargets : int.MaxValue;

        foreach (var hit in hits)
        {
            if (!SkillTargetingUtility.TryGetProxy(hit.collider, out var targetProxy)) continue;
            if (!visited.Add(targetProxy.NetworkObjectId)) continue;
            if (!SkillTargetingUtility.PassesFactionFilter(casterProxy, targetProxy, ctx.definition.factionFilter)) continue;

            var targetAttr = targetProxy.GetServerAttributeModule<ServerAttributeModule>();
            if (targetAttr == null) continue;

            float damageScale = Mathf.Max(0f, 1f - 0.2f * hitIndex);
            var scaledProfile = ScaleProfile(ctx.baseDamageProfile, damageScale);

            var damageInfo = DamageCalculator.CalculateDamageFromProfile(
                casterProxy.NetworkObjectId,
                targetProxy.NetworkObjectId,
                scaledProfile,
                ctx.bulletType,
                enableCrit: ctx.definition.enableCrit,
                extraCritChance: ctx.definition.extraCritChance,
                extraCritMulti: ctx.definition.extraCritMulti,
                procChance: ctx.definition.skillProcChance);

            damageInfo.instigator = casterProxy.OwnerClientId;
            damageInfo.hasHitWorldPos = true;
            damageInfo.hitWorldPos = hit.point;

            targetAttr.TakeDamage(damageInfo);
            SkillTargetingUtility.ApplyBuffRefs(ctx, casterProxy, targetProxy, damageInfo);

            caster.QualityEffectModule?.RaiseOnSkillHit(targetProxy, damageInfo.amount);

            hitIndex++;
            if (hitIndex >= maxTargets)
            {
                break;
            }
        }
    }

    private static DamageProfile ScaleProfile(DamageProfile profile, float scale)
    {
        return new DamageProfile
        {
            solid = Mathf.RoundToInt(profile.solid * scale),
            liquid = Mathf.RoundToInt(profile.liquid * scale),
            gas = Mathf.RoundToInt(profile.gas * scale),
            ice = Mathf.RoundToInt(profile.ice * scale),
            fire = Mathf.RoundToInt(profile.fire * scale),
            toxic = Mathf.RoundToInt(profile.toxic * scale),
            electric = Mathf.RoundToInt(profile.electric * scale),
        };
    }
}
