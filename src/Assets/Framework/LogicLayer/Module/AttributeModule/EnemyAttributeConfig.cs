// LogicLayer/Data/EnemyAttributeConfig.cs
using System;
using UnityEngine;

[Serializable]
[CreateAssetMenu(fileName = "EnemyAttributeConfig_Template", menuName = "游戏配置/怪物系统/创建配置/怪物属性配置")]
public class EnemyAttributeConfig : AttributeConfig
{
    // 怪物特有属性的基础值
    public float baseAggroRange;
    public float baseDropRate;
    public float baseInGameGoldReward;
    public float baseOutGameCurrencyReward;
    public float baseDetectionRange;
    public MonsterRank baseMonsterRank;
    
    // 怪物特有属性的成长值
    public float aggroRangeGrowth;
    public float dropRateGrowth;
    public float inGameGoldRewardGrowth;
    public float outGameCurrencyRewardGrowth;
    public float detectionRangeGrowth;
    
    [Header("掉落配置")]
    public EnemyDropTableSO dropTable;

    [Tooltip("没填 DropEntry.dropPrefabPath 时用这个")]
    public string defaultDropPrefabPath = "NetworkPrefabs/Drop/DropItem";
    
    /// <summary>
    /// 重写计算方法，包含怪物特有属性
    /// </summary>
    public override float CalculateAttribute(AttributeType type, int level)
    {
        if (level < 1) level = 1;
        int levelBonus = level - 1;
        
        // 先调用基类处理基础属性
        float baseValue = base.CalculateAttribute(type, level);
        if (baseValue != 0f || type == AttributeType.Faction)
            return baseValue;
        
        // 处理怪物特有属性
        return type switch
        {
            // 怪物特有属性（有成长）
            AttributeType.AggroRange => baseAggroRange + aggroRangeGrowth * levelBonus,
            AttributeType.DropRate => baseDropRate + dropRateGrowth * levelBonus,
            AttributeType.InGameGoldReward => baseInGameGoldReward + inGameGoldRewardGrowth * levelBonus,
            AttributeType.OutGameCurrencyReward => baseOutGameCurrencyReward + outGameCurrencyRewardGrowth * levelBonus,
            AttributeType.DetectionRange => baseDetectionRange + detectionRangeGrowth * levelBonus,
            AttributeType.MonsterRank => (float)baseMonsterRank,
            
            // 默认值
            _ => 0f
        };
    }
}