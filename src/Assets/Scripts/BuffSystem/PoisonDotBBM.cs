using UnityEngine;

[CreateAssetMenu(fileName = "PoisonDotBBM", menuName = "Buff/Element/PoisonDotBBM")]
public class PoisonDotBBM : ElementalBuffModuleBase
{
    [Range(0f, 5f)]
    public float tickDamageRatio = 0.3f;

    public override void Apply(BuffInfo buffInfo, DamageInfo damageInfo = default)
    {
        var targetAttr = GetOwnerAttribute(buffInfo);
        var targetProxy = GetOwnerProxy(buffInfo);
        float damage = buffInfo.elementDamageSnapshot * Mathf.Max(1, buffInfo.currentStack) * tickDamageRatio;
        ApplyElementDamage(buffInfo, targetAttr, targetProxy, ElementType.Poison, damage);
    }
}