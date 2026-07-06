// LogicLayer/Data/PlayerAttributeConfig.cs
using System;
using UnityEngine;

[Serializable]
[CreateAssetMenu(fileName = "PlayerAttributeConfig", menuName = "游戏配置/角色系统/创建配置/角色属性配置")]
public class PlayerAttributeConfig : AttributeConfig
{
    // 玩家特有属性的基础值
    public float baseEnergy;
    public float baseEnergyRegen;
    public float baseLuck;
    public float baseCooldownReduction;
    public float baseResilience;
    public float baseArmorPenetrationRate;
    public float baseSkillStrength;
    public float baseSkillDuration;
    public float baseSkillRange;
    public float baseSkillEfficiency;
    
    
    // 玩家特有属性的成长值
    public float energyGrowth;
    public float energyRegenGrowth;
    public float luckGrowth;
    public float cooldownReductionGrowth;
    public float resilienceGrowth;
    public float armorPenetrationRateGrowth;
    public float skillStrengthGrowth;
    public float skillDurationGrowth;
    public float skillRangeGrowth;
    public float skillEfficiencyGrowth;
    
    /// <summary>
    /// 重写计算方法，包含玩家特有属性
    /// </summary>
    public override float CalculateAttribute(AttributeType type, int level)
    {
        if (level < 1) level = 1;
        int levelBonus = level - 1;
        
        // 先调用基类处理基础属性
        float baseValue = base.CalculateAttribute(type, level);
        if (baseValue != 0f || type == AttributeType.Faction)
            return baseValue;
        
        // 处理玩家特有属性
        return type switch
        {
            // 玩家特有属性（有成长）
            AttributeType.Energy or AttributeType.MaxEnergy => baseEnergy + energyGrowth * levelBonus,
            AttributeType.EnergyRegen => baseEnergyRegen + energyRegenGrowth * levelBonus,
            AttributeType.Luck => baseLuck + luckGrowth * levelBonus,
            AttributeType.CooldownReduction => baseCooldownReduction + cooldownReductionGrowth * levelBonus,
            AttributeType.Resilience => baseResilience + resilienceGrowth * levelBonus,
            AttributeType.ArmorPenetrationRate => baseArmorPenetrationRate + armorPenetrationRateGrowth * levelBonus,
            AttributeType.SkillStrength => baseSkillStrength + skillStrengthGrowth * levelBonus,
            AttributeType.SkillDuration => baseSkillDuration + skillDurationGrowth * levelBonus,
            AttributeType.SkillRange => baseSkillRange + skillRangeGrowth * levelBonus,
            AttributeType.SkillEfficiency => baseSkillEfficiency + skillEfficiencyGrowth * levelBonus,
            
            // 默认值
            _ => 0f
        };
    }
}
