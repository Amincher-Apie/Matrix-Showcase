using UnityEngine;

[CreateAssetMenu(fileName = "ElectricArcBBM", menuName = "Buff/Element/ElectricArcBBM")]
public class ElectricArcBBM : ElementalBuffModuleBase
{
    [Min(0f)]
    public float arcRadius = 3f;

    [Min(0)]
    public int maxExtraTargets = 3;

    [Range(0f, 5f)]
    public float arcDamageRatio = 0.5f;

    public bool hitPrimaryTarget = true;

    public override void Apply(BuffInfo buffInfo, DamageInfo damageInfo = default)
    {
        var ownerProxy = GetOwnerProxy(buffInfo);
        var ownerAttr = GetOwnerAttribute(buffInfo);
        var applierProxy = GetApplierProxy(buffInfo);
        if (ownerProxy == null || ownerAttr == null)
        {
            return;
        }

        float damage = buffInfo.elementDamageSnapshot * Mathf.Max(1, buffInfo.currentStack) * arcDamageRatio;
        if (hitPrimaryTarget)
        {
            ApplyElementDamage(buffInfo, ownerAttr, ownerProxy, ElementType.Electric, damage);
        }

        if (arcRadius <= 0f || maxExtraTargets == 0)
        {
            return;
        }

        int hitCount = 0;
        var colliders = Physics.OverlapSphere(ownerProxy.transform.position, arcRadius);
        foreach (var col in colliders)
        {
            var candidate = col.GetComponentInParent<NetworkProxyBase>();
            if (candidate == null || ReferenceEquals(candidate, ownerProxy))
            {
                continue;
            }

            if (applierProxy != null && !PassesEnemyFilter(applierProxy, candidate))
            {
                continue;
            }

            var targetAttr = candidate.GetServerAttributeModule<ServerAttributeModule>();
            if (targetAttr == null)
            {
                continue;
            }

            ApplyElementDamage(buffInfo, targetAttr, candidate, ElementType.Electric, damage);
            hitCount++;

            if (maxExtraTargets > 0 && hitCount >= maxExtraTargets)
            {
                break;
            }
        }
    }
}
