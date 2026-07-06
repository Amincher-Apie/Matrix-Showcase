using System;
using System.Collections.Generic;

/// <summary>
/// 武器修改器
/// </summary>
public class WeaponModifier
{
    public WeaponModifyType ModifyType { get; set; }
    public WeaponAttributeType AttributeType { get; set; }
    public ElementType ElementType { get; set; }
    public float Value { get; set; }
    public WeaponModifyOperator Operator { get; set; }
    public object Source { get; set; }
    public int StackCount { get; set; } = 1;

    public WeaponModifier(WeaponModifyType modifyType, WeaponAttributeType attributeType, 
        float value, WeaponModifyOperator op, object source = null, 
        ElementType elementType = ElementType.Fire)
    {
        ModifyType = modifyType;
        AttributeType = attributeType;
        Value = value;
        Operator = op;
        Source = source;
        ElementType = elementType;
    }
}

/// <summary>
/// 武器修改操作符
/// </summary>
public enum WeaponModifyOperator
{
    Set,        // 直接设置
    Add,        // 加法
    Multiply,   // 乘法（叠乘）
    Percent     // 百分比（累加）
}

/// <summary>
/// 武器属性数据
/// </summary>
public class WeaponAttributeData
{
    public float BaseValue { get; set; }
    public float CachedValue { get; private set; }
    public bool IsCacheDirty { get; private set; } = true;
    public List<WeaponModifier> Modifiers { get; } = new List<WeaponModifier>();

    public WeaponAttributeData(float baseValue)
    {
        BaseValue = baseValue;
        CachedValue = baseValue;
    }

    public void MarkCacheDirty()
    {
        IsCacheDirty = true;
    }

    public void UpdateCache(float newValue)
    {
        CachedValue = newValue;
        IsCacheDirty = false;
    }
}