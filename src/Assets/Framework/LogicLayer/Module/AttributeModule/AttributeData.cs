using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 属性修改器
/// </summary>
public class AttributeModifier
{
    public AttributeModifyType ModifyType { get; set; }
    public float Value { get; set; }
    public object Source { get; set; } // 修改来源（技能、道具等）
    public int StackCount { get; set; } = 1; // 堆叠次数
        
    public AttributeModifier(AttributeModifyType modifyType, float value, object source = null)
    {
        ModifyType = modifyType;
        Value = value;
        Source = source;
    }
}
    
/// <summary>
/// 属性数据
/// </summary>
public class AttributeData
{
    // 基础值（从配置计算得出）
    public float BaseValue { get; set; }
        
    // 当前值（对于有最大值的属性）
    public float CurrentValue { get; set; }
        
    // 修改器列表
    public List<AttributeModifier> Modifiers { get; } = new List<AttributeModifier>();
        
    // 是否具有当前值（生命值、护盾值等）
    public bool HasCurrentValue { get; set; }
        
    // 缓存值
    public float CachedValue { get; private set; }
        
    // 缓存是否脏（需要重新计算）
    public bool IsCacheDirty { get; private set; } = true;
        
    public AttributeData(float baseValue, bool hasCurrentValue = false)
    {
        BaseValue = baseValue;
        CurrentValue = baseValue;
        HasCurrentValue = hasCurrentValue;
        CachedValue = baseValue;
    }
        
    /// <summary>
    /// 标记缓存为脏
    /// </summary>
    public void MarkCacheDirty()
    {
        IsCacheDirty = true;
    }
        
    /// <summary>
    /// 更新缓存值
    /// </summary>
    public void UpdateCache(float newValue)
    {
        CachedValue = newValue;
        IsCacheDirty = false;
    }
}
