using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 动能增效：给自身和范围内同阵营玩家刷新一段 DamageOutPutRate 增益。
/// </summary>
public class KineticBoostSkillExecutor : ISkillExecute
{
    private const ulong SourcePrefix = 0x4B494E4554494300UL;
    private readonly Dictionary<ulong, Coroutine> _activeRoutines = new();
    private readonly Dictionary<ulong, List<ServerAttributeModule>> _activeTargets = new();

    public string Id => "KineticBoost";

    public void ClientPredictExecute(in SkillRuntimeContext ctx)
    {
        Debug.Log("[KineticBoost] ClientPredictExecute");
    }

    public void ServerExecute(in SkillRuntimeContext ctx)
    {
        var caster = ctx.caster;
        if (caster == null || caster.networkProxy == null) return;

        var runner = caster.networkProxy.GetComponent<MonoBehaviour>();
        if (runner == null) return;

        ulong casterId = caster.networkProxy.NetworkObjectId;
        if (_activeRoutines.TryGetValue(casterId, out var oldRoutine) && oldRoutine != null)
        {
            runner.StopCoroutine(oldRoutine);
            if (_activeTargets.TryGetValue(casterId, out var oldTargets))
            {
                RemoveBoost(casterId, oldTargets);
            }
        }

        var targets = CollectTargets(ctx);
        _activeTargets[casterId] = targets;
        foreach (var target in targets)
        {
            target.RemoveModifiers(AttributeType.DamageOutPutRate, BuildSourceId(casterId), 0);
            target.AddModifier(
                AttributeType.DamageOutPutRate,
                AttributeModifyType.Add,
                1f,
                BuildSourceId(casterId),
                1);
        }

        _activeRoutines[casterId] = runner.StartCoroutine(RemoveAfterDuration(casterId, targets, ctx.finalDuration));
    }

    private List<ServerAttributeModule> CollectTargets(in SkillRuntimeContext ctx)
    {
        var result = new List<ServerAttributeModule>();
        var casterProxy = ctx.caster?.networkProxy;
        if (casterProxy == null) return result;

        float radius = Mathf.Max(0f, ctx.finalRange);
        var colliders = Physics.OverlapSphere(casterProxy.transform.position, radius);
        var visited = new HashSet<ulong>();

        AddTargetIfValid(casterProxy, casterProxy, visited, result);
        foreach (var col in colliders)
        {
            if (!SkillTargetingUtility.TryGetProxy(col, out var proxy)) continue;
            AddTargetIfValid(casterProxy, proxy, visited, result);
        }

        return result;
    }

    private static void AddTargetIfValid(
        NetworkProxyBase casterProxy,
        NetworkProxyBase proxy,
        HashSet<ulong> visited,
        List<ServerAttributeModule> result)
    {
        if (proxy == null || !visited.Add(proxy.NetworkObjectId)) return;
        if (!SkillTargetingUtility.PassesFactionFilter(casterProxy, proxy, FactionFilterType.AllyOnly)) return;

        if (proxy is PlayerNetworkProxy playerProxy)
        {
            var playerAttr = playerProxy.GetServerAttributeModule<ServerPlayerAttributeModule>();
            if (playerAttr == null || playerAttr.CurrentLifeState != PlayerLifeState.Alive)
            {
                return;
            }

            result.Add(playerAttr);
            return;
        }

        var attr = proxy.GetServerAttributeModule<ServerAttributeModule>();
        if (attr != null)
        {
            result.Add(attr);
        }
    }

    private IEnumerator RemoveAfterDuration(
        ulong casterId,
        List<ServerAttributeModule> targets,
        float duration)
    {
        if (duration > 0f)
        {
            yield return new WaitForSeconds(duration);
        }

        RemoveBoost(casterId, targets);
        _activeRoutines.Remove(casterId);
        _activeTargets.Remove(casterId);
    }

    private static void RemoveBoost(ulong casterId, List<ServerAttributeModule> targets)
    {
        ulong sourceId = BuildSourceId(casterId);
        foreach (var target in targets)
        {
            if (target != null)
            {
                target.RemoveModifiers(AttributeType.DamageOutPutRate, sourceId, 0);
            }
        }
    }

    private static ulong BuildSourceId(ulong casterId)
    {
        return SourcePrefix ^ casterId;
    }
}
