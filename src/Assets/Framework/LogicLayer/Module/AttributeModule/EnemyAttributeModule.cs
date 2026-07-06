// LogicLayer/Module/AttributeModule/EnemyAttributeModule.cs
using UnityEngine;

/// <summary>
/// 逻辑层敌人属性模块 - 直接读取ServerEnemyAttributeModule的值
/// </summary>
public class EnemyAttributeModule : AttributeModule
{
    private ServerEnemyAttributeModule _serverEnemyAttributeModule;
    private EnemyAttributeConfig _attributeConfig;
    public override ulong ObjectId => _owner.ObjectId;
    
    public EnemyAttributeModule(LogicActor owner, EnemyAttributeConfig attributeConfig) : base(owner)
    {
        _attributeConfig = attributeConfig;
    }
    
    public override void LocalInit()
    {
        // 获取敌人专用的服务器属性模块
        _serverEnemyAttributeModule = _owner.GetComponent<ServerEnemyAttributeModule>();
        if (_serverEnemyAttributeModule == null)
        {
            Debug.LogError($"[EnemyAttributeModule] 找不到ServerEnemyAttributeModule组件 - {_owner.ObjectId}");
            return;
        }
        _serverEnemyAttributeModule.SetConfig(_attributeConfig);
        Debug.Log($"[EnemyAttributeModule] 初始化完成 - ObjectId: {ObjectId}");
    }
    
    public override float GetAttribute(AttributeType type)
    {
        if (_serverEnemyAttributeModule == null) return 0f;
        
        // 直接从服务器模块的NetworkVariable获取值
        return type switch
        {
            // 基础属性
            AttributeType.Health => _serverEnemyAttributeModule.GetAttribute(AttributeType.Health),
            AttributeType.MaxHealth => _serverEnemyAttributeModule.GetAttribute(AttributeType.MaxHealth),
            AttributeType.Shield => _serverEnemyAttributeModule.GetAttribute(AttributeType.Shield),
            AttributeType.MaxShield => _serverEnemyAttributeModule.GetAttribute(AttributeType.MaxShield),
            AttributeType.Armor => _serverEnemyAttributeModule.GetAttribute(AttributeType.Armor),
            AttributeType.MoveSpeed => _serverEnemyAttributeModule.GetAttribute(AttributeType.MoveSpeed),
            AttributeType.Level => _serverEnemyAttributeModule.GetAttribute(AttributeType.Level),
            
            // 抗性属性
            AttributeType.Resistance_Solid => _serverEnemyAttributeModule.GetAttribute(AttributeType.Resistance_Solid),
            AttributeType.Resistance_Liquid => _serverEnemyAttributeModule.GetAttribute(AttributeType.Resistance_Liquid),
            AttributeType.Resistance_Gas => _serverEnemyAttributeModule.GetAttribute(AttributeType.Resistance_Gas),
            AttributeType.Resistance_Toxic => _serverEnemyAttributeModule.GetAttribute(AttributeType.Resistance_Toxic),
            AttributeType.Resistance_Fire => _serverEnemyAttributeModule.GetAttribute(AttributeType.Resistance_Fire),
            AttributeType.Resistance_Ice => _serverEnemyAttributeModule.GetAttribute(AttributeType.Resistance_Ice),
            AttributeType.Resistance_Electric => _serverEnemyAttributeModule.GetAttribute(AttributeType.Resistance_Electric),
            
            // 伤害加成
            AttributeType.DamageOutPutRate => _serverEnemyAttributeModule.GetAttribute(AttributeType.DamageOutPutRate),
            
            // 派系
            AttributeType.Faction => _serverEnemyAttributeModule.GetAttribute(AttributeType.Faction),
            
            // 特有属性
            AttributeType.AggroRange => _serverEnemyAttributeModule.GetAttribute(AttributeType.AggroRange),
            AttributeType.MonsterRank => _serverEnemyAttributeModule.GetAttribute(AttributeType.MonsterRank),
            AttributeType.DropRate => _serverEnemyAttributeModule.GetAttribute(AttributeType.DropRate),
            AttributeType.InGameGoldReward => _serverEnemyAttributeModule.GetAttribute(AttributeType.InGameGoldReward),
            AttributeType.DetectionRange => _serverEnemyAttributeModule.GetAttribute(AttributeType.DetectionRange),
            
            _ => 0f
        };
    }
    
    #region 敌人特有方法
    /// <summary>
    /// 获取怪物等级
    /// </summary>
    public MonsterRank GetMonsterRank()
    {
        float rankValue = GetAttribute(AttributeType.MonsterRank);
        return (MonsterRank)Mathf.RoundToInt(rankValue);
    }
    
    /// <summary>
    /// 获取实际掉落率
    /// </summary>
    public float GetActualDropRate()
    {
        float baseDropRate = GetAttribute(AttributeType.DropRate);
        return Mathf.Clamp01(baseDropRate);
    }
    
    /// <summary>
    /// 检查是否在仇恨范围内
    /// </summary>
    public bool IsInAggroRange(Vector3 targetPosition)
    {
        float aggroRange = GetAttribute(AttributeType.AggroRange);
        float distance = Vector3.Distance(targetPosition, _owner.transform.position);
        return distance <= aggroRange;
    }
    
    /// <summary>
    /// 检查是否在侦测范围内
    /// </summary>
    public bool IsInDetectionRange(Vector3 targetPosition)
    {
        float detectionRange = GetAttribute(AttributeType.DetectionRange);
        float distance = Vector3.Distance(targetPosition, _owner.transform.position);
        return distance <= detectionRange;
    }
    #endregion
}