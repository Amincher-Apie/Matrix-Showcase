using UnityEngine;

public static class SkillTargetingUtility
{
    public static bool TryGetProxy(Collider collider, out NetworkProxyBase proxy)
    {
        proxy = collider != null ? collider.GetComponentInParent<NetworkProxyBase>() : null;
        return proxy != null;
    }

    public static bool PassesFactionFilter(
        NetworkProxyBase caster,
        NetworkProxyBase target,
        FactionFilterType filter)
    {
        if (target == null)
        {
            return false;
        }

        bool isSelf = caster != null && ReferenceEquals(caster, target);
        if (filter == FactionFilterType.None)
        {
            return true;
        }

        if (filter == FactionFilterType.SelfOnly)
        {
            return isSelf;
        }

        if (isSelf)
        {
            return filter == FactionFilterType.AllyOnly;
        }

        if (filter == FactionFilterType.AllButSelf)
        {
            return true;
        }

        var casterAttr = caster != null ? caster.GetServerAttributeModule<ServerAttributeModule>() : null;
        var targetAttr = target.GetServerAttributeModule<ServerAttributeModule>();
        if (casterAttr == null || targetAttr == null)
        {
            return false;
        }

        var casterFaction = (FactionType)Mathf.RoundToInt(casterAttr.GetAttribute(AttributeType.Faction));
        var targetFaction = (FactionType)Mathf.RoundToInt(targetAttr.GetAttribute(AttributeType.Faction));
        bool sameFaction = casterFaction == targetFaction;

        return filter switch
        {
            FactionFilterType.EnemyOnly => !sameFaction,
            FactionFilterType.AllyOnly => sameFaction,
            _ => true
        };
    }

    public static void ApplyBuffRefs(
        in SkillRuntimeContext ctx,
        NetworkProxyBase casterProxy,
        NetworkProxyBase targetProxy,
        DamageInfo damageInfo)
    {
        if (ctx.definition == null || ctx.definition.buffRefs == null || targetProxy == null)
        {
            return;
        }

        var targetBuff = targetProxy.GetComponent<ServerBuffModule>();
        if (targetBuff == null || !targetBuff.IsServer)
        {
            return;
        }

        ulong applierObjectId = casterProxy != null ? casterProxy.NetworkObjectId : damageInfo.sourceActorId;
        ulong applierClientId = casterProxy != null ? casterProxy.OwnerClientId : damageInfo.instigator;

        foreach (var buffData in ctx.definition.buffRefs)
        {
            if (buffData == null)
            {
                continue;
            }

            var element = ElementBuffMappingAsset.InferElement(buffData, ElementType.Fire);
            float elementDamage = ElementBuffMappingAsset.ResolveElementDamage(damageInfo, element);
            if (elementDamage <= 0f)
            {
                elementDamage = damageInfo.amount;
            }

            targetBuff.ApplyBuff(
                buffData,
                1,
                -1f,
                applierObjectId,
                applierClientId,
                damageInfo,
                element,
                elementDamage);
        }
    }

    public static float ResolveAreaRadius(in SkillRuntimeContext ctx)
    {
        if (ctx.definition == null)
        {
            return Mathf.Max(0f, ctx.finalRange);
        }

        if (ctx.definition.splashRadius <= 0f)
        {
            return Mathf.Max(0f, ctx.finalRange);
        }

        float rangeMultiplier = ctx.definition.baseRange > 0f
            ? ctx.finalRange / ctx.definition.baseRange
            : 1f;
        return Mathf.Max(0f, ctx.definition.splashRadius * rangeMultiplier);
    }
}
