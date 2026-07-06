using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ModifyAttributeBBM", menuName = "Buff/ModifyAttributeBBM")]
public class ModifyAttributeBBM : BaseBuffModule
{
    public ModifyAttributeTemplate ModifyTemplates = new();
    public override void Apply(BuffInfo buffInfo, DamageInfo damageInfo = default)
    {
        if (!buffInfo.reverse)
        {
            var owner = buffInfo.Owner.AttributeProxy;
            owner.AddModifier(ModifyTemplates.type, ModifyTemplates.modifyType, ModifyTemplates.value, buffInfo.RuntimeSourceId);
        }
        else
        {
            var owner = buffInfo.Owner.AttributeProxy;
            owner.RemoveModifiers(ModifyTemplates.type, buffInfo.RuntimeSourceId);
        }
    }
}
[Serializable]
public class ModifyAttributeTemplate
{
    public AttributeType type;
    public AttributeModifyType modifyType;
    public float value;
}
