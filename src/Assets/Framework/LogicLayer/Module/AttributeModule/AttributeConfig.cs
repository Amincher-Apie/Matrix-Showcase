// LogicLayer/Data/AttributeConfig.cs
using System;
using UnityEngine;

[Serializable]
public abstract class AttributeConfig : ScriptableObject
{
    public string id;
    public string name;
    
    // 基础值（第一层）
    public float baseHealth;
    public float baseShield;
    public float baseArmor;
    public float baseMoveSpeed;
    
    // 成长值（每级增加）
    public float healthGrowth;
    public float shieldGrowth;
    public float armorGrowth;
    public float moveSpeedGrowth;
    
    // 基础抗性
    public float baseResistanceSolid;
    public float baseResistanceLiquid;
    public float baseResistanceGas;
    public float baseResistanceToxic;
    public float baseResistanceFire;
    public float baseResistanceIce;
    public float baseResistanceElectric;
    
    // 伤害输出率
    public float damageOutputRate;
    
    public FactionType baseFaction;
    
    /// <summary>
    /// 根据等级计算运行时基础值（第二层数据）
    /// </summary>
    public virtual float CalculateAttribute(AttributeType type, int level)
    {
        if (level < 1) level = 1;
        int levelBonus = level - 1; // 1级时没有加成
        
        return type switch
        {
            // 有成长属性的值
            AttributeType.MaxHealth or AttributeType.Health
                => baseHealth + healthGrowth * levelBonus,

            AttributeType.MaxShield or AttributeType.Shield
                => baseShield + shieldGrowth * levelBonus,
            AttributeType.Armor => baseArmor + armorGrowth * levelBonus,
            AttributeType.MoveSpeed => baseMoveSpeed + moveSpeedGrowth * levelBonus,
            
            // 固定值（无成长）
            AttributeType.Resistance_Solid => baseResistanceSolid,
            AttributeType.Resistance_Liquid => baseResistanceLiquid,
            AttributeType.Resistance_Gas => baseResistanceGas,
            AttributeType.Resistance_Toxic => baseResistanceToxic,
            AttributeType.Resistance_Fire => baseResistanceFire,
            AttributeType.Resistance_Ice => baseResistanceIce,
            AttributeType.Resistance_Electric => baseResistanceElectric,
            AttributeType.Faction => (float)baseFaction,
            AttributeType.DamageOutPutRate => damageOutputRate > 0 ? damageOutputRate : 1f,
            
            // 默认值
            _ => 0f
        };
    }
}