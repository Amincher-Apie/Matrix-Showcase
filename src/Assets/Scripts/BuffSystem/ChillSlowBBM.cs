using UnityEngine;

[CreateAssetMenu(fileName = "ChillSlowBBM", menuName = "Buff/Element/ChillSlowBBM")]
public class ChillSlowBBM : ElementalBuffModuleBase
{
    [Tooltip("每层 MoveSpeed 百分比修改量。-10 表示每层减速 10%。")]
    public float slowPercentPerStack = -10f;

    public override void Apply(BuffInfo buffInfo, DamageInfo damageInfo = default)
    {
        var owner = buffInfo?.Owner?.AttributeProxy;
        if (owner == null)
        {
            return;
        }

        if (!buffInfo.reverse)
        {
            owner.AddModifier(
                AttributeType.MoveSpeed,
                AttributeModifyType.Percentage,
                slowPercentPerStack,
                buffInfo.RuntimeSourceId,
                1);
        }
        else
        {
            owner.RemoveModifiers(AttributeType.MoveSpeed, buffInfo.RuntimeSourceId, 1);
        }
    }
}
