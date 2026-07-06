using System.Collections;
using System.Collections.Generic;
using Framework.LogicLayer.DamageCenter;
using UnityEngine;

/// <summary>
/// 示例1：
/// - 冷却12秒
/// - 持续2秒
/// - 每0.5s 对范围内敌人造成 武器伤害100% 的范围伤害
/// 对应你的描述：强度影响 100% 与 0.5s（频率），持续时间影响 2s，范围影响半径/最大指定距离。
/// </summary>
public class BombardAreaSkillExecutor : ISkillExecute
{
    public string Id => "BombardArea";

    public void ClientPredictExecute(in SkillRuntimeContext ctx)
    {
        // TODO：在客户端生成一个范围提示圈、播放预警特效等
        Debug.Log("[BombardArea] ClientPredictExecute");
    }

    public void ServerExecute(in SkillRuntimeContext ctx)
    {
        var caster = ctx.caster;
        if (caster == null || caster.networkProxy == null) return;

        // 这里简单直接启动一个协程在服务器上执行多次伤害 tick
        var runner = caster.networkProxy.GetComponent<MonoBehaviour>();
        if (runner == null)
        {
            Debug.LogError("[BombardArea] caster 上没有 MonoBehaviour 容器");
            return;
        }

        runner.StartCoroutine(DoBombardCoroutine(ctx));
    }

    private IEnumerator DoBombardCoroutine(SkillRuntimeContext ctx)
    {
        float duration = ctx.finalDuration;     // 已经包含持续时间倍率
        float tickIntervalBase = 0.5f;
        float strength = Mathf.Max(ctx.stats.strength, 0.01f);

        // 强度影响 tick 频率：示例公式  tickInterval = base / strength
        float tickInterval = tickIntervalBase / strength;

        if (tickInterval <= 0.05f) tickInterval = 0.05f;

        float elapsed = 0f;

        if (duration <= 0f)
        {
            ApplyOneBombardTick(ctx);
            yield break;
        }

        while (elapsed < duration)
        {
            ApplyOneBombardTick(ctx);
            yield return new WaitForSeconds(tickInterval);
            elapsed += tickInterval;
        }
    }

    public void ApplyOneBombardTick(SkillRuntimeContext ctx)
    {
        // 0. 取施放者 / 施放者 NetworkObjectId
        var casterActor = ctx.caster;
        if (!casterActor || !casterActor.networkProxy) return;

        var casterProxy = casterActor.networkProxy;
        ulong casterId = casterProxy.NetworkObjectId;

        // 1. 基础伤害快照已经在 ctx.baseDamageProfile 里准备好了
        var baseProfile = ctx.baseDamageProfile;
        var bulletType  = ctx.bulletType;

        // 2. 在技能的当前中心做一次 Physics.OverlapSphere，找到进圈的敌人（你原来就有这段）
        var currentCenter = ctx.castContext.point;
        var currentRadius = SkillTargetingUtility.ResolveAreaRadius(ctx);
        Collider[] colliders = Physics.OverlapSphere(currentCenter, currentRadius);
        var visited = new HashSet<ulong>();
        int hitCount = 0;

        foreach (var col in colliders)
        {
            if (!SkillTargetingUtility.TryGetProxy(col, out var proxy)) continue;
            if (!visited.Add(proxy.NetworkObjectId)) continue;
            if (!SkillTargetingUtility.PassesFactionFilter(casterProxy, proxy, ctx.definition.factionFilter)) continue;

            ulong targetId = proxy.NetworkObjectId;

            // 3. 调用 DamageCenter 的“技能版”入口：
            var damageInfo = DamageCalculator.CalculateDamageFromProfile(
                casterId,
                targetId,
                baseProfile,                 // 技能快照（例如固体 1000、毒 0 ...）
                bulletType,                  // 决定护甲 / 护盾规则
                enableCrit: ctx.definition.enableCrit,
                extraCritChance: ctx.definition.extraCritChance,
                extraCritMulti: ctx.definition.extraCritMulti,
                procChance: ctx.definition.skillProcChance
            );

            damageInfo.instigator = casterProxy.OwnerClientId;
            damageInfo.hasHitWorldPos = true;
            damageInfo.hitWorldPos = col.ClosestPoint(currentCenter);

            // 4. 把这份 DamageInfo 扔给目标的 AttributeModule 处理护盾/血量分配
            var targetAttr = proxy.GetServerAttributeModule<ServerAttributeModule>();
            if (targetAttr)
            {
                targetAttr.TakeDamage(damageInfo);
                SkillTargetingUtility.ApplyBuffRefs(ctx, casterProxy, proxy, damageInfo);
                 
                // ★ 触发品质效果：技能命中时
                if (casterActor?.QualityEffectModule != null)
                {
                    casterActor.QualityEffectModule.RaiseOnSkillHit(
                        proxy, 
                        damageInfo.amount
                    );
                }
            }

            hitCount++;
            if (ctx.definition.maxTargets > 0 && hitCount >= ctx.definition.maxTargets)
            {
                break;
            }
        }
    }
}
