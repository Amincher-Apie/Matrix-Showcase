using UnityEngine;

public abstract class ElementalBuffModuleBase : BaseBuffModule
{
    protected ServerAttributeModule GetOwnerAttribute(BuffInfo buffInfo)
    {
        return buffInfo?.Owner?.AttributeProxy as ServerAttributeModule;
    }

    protected NetworkProxyBase GetOwnerProxy(BuffInfo buffInfo)
    {
        if (buffInfo?.Owner == null || NetworkObjectManager.Instance == null)
        {
            return null;
        }

        return NetworkObjectManager.Instance.TryGetNetworkProxy<NetworkProxyBase>(
            buffInfo.Owner.NetworkObjectId,
            out var proxy)
            ? proxy
            : null;
    }

    protected NetworkProxyBase GetApplierProxy(BuffInfo buffInfo)
    {
        if (buffInfo == null || buffInfo.applierObjectId == 0 || NetworkObjectManager.Instance == null)
        {
            return null;
        }

        return NetworkObjectManager.Instance.TryGetNetworkProxy<NetworkProxyBase>(
            buffInfo.applierObjectId,
            out var proxy)
            ? proxy
            : null;
    }

    protected void ApplyElementDamage(
        BuffInfo buffInfo,
        ServerAttributeModule targetAttr,
        NetworkProxyBase targetProxy,
        ElementType element,
        float amount)
    {
        if (buffInfo == null || targetAttr == null || amount <= 0f)
        {
            return;
        }

        ulong sourceId = buffInfo.applierObjectId != 0
            ? buffInfo.applierObjectId
            : buffInfo.Owner.NetworkObjectId;

        var damageInfo = new DamageInfo(PhysicalBulletType.Solid, sourceId, targetAttr.NetworkObjectId)
        {
            amount = amount,
            instigator = buffInfo.applierClientId,
            isSkill = buffInfo.sourceDamageInfo.isSkill,
            hasHitWorldPos = targetProxy != null,
            hitWorldPos = targetProxy != null ? targetProxy.transform.position : Vector3.zero
        };

        switch (element)
        {
            case ElementType.Fire:
                damageInfo.fireDamage = amount;
                break;
            case ElementType.Ice:
                damageInfo.iceDamage = amount;
                break;
            case ElementType.Poison:
                damageInfo.poisonDamage = amount;
                break;
            case ElementType.Electric:
                damageInfo.electricDamage = amount;
                break;
        }

        targetAttr.TakeDamage(damageInfo);
    }

    protected bool PassesEnemyFilter(NetworkProxyBase applierProxy, NetworkProxyBase candidate)
    {
        if (candidate == null || applierProxy == null || ReferenceEquals(applierProxy, candidate))
        {
            return false;
        }

        var applierAttr = applierProxy.GetServerAttributeModule<ServerAttributeModule>();
        var candidateAttr = candidate.GetServerAttributeModule<ServerAttributeModule>();
        if (applierAttr == null || candidateAttr == null)
        {
            return false;
        }

        var applierFaction = (FactionType)Mathf.RoundToInt(applierAttr.GetAttribute(AttributeType.Faction));
        var candidateFaction = (FactionType)Mathf.RoundToInt(candidateAttr.GetAttribute(AttributeType.Faction));
        return applierFaction != candidateFaction;
    }
}
