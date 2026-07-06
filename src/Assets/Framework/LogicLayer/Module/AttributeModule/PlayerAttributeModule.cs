// LogicLayer/Module/AttributeModule/PlayerAttributeModule.cs
using UnityEngine;

/// <summary>
/// 逻辑层玩家属性模块 - 直接读取ServerPlayerAttributeModule的值
/// </summary>
public class PlayerAttributeModule : AttributeModule
{
    private ServerPlayerAttributeModule _serverPlayerAttributeModule;
    private PlayerAttributeConfig _config;
    public override ulong ObjectId => _owner.ObjectId;
    
    public PlayerAttributeModule(LogicActor owner, PlayerAttributeConfig config) : base(owner)
    {
        _config = config;
    }
    
    public override void LocalInit()
    {
        // 获取玩家专用的服务器属性模块
        _serverPlayerAttributeModule = _owner.GetComponent<ServerPlayerAttributeModule>();
        if (_serverPlayerAttributeModule == null)
        {
            Debug.LogError($"[PlayerAttributeModule] 找不到ServerPlayerAttributeModule组件 - {_owner.ObjectId}");
            return;
        }
        _serverPlayerAttributeModule.SetConfig(_config);
        
        
        Debug.Log($"[PlayerAttributeModule] 初始化完成 - ObjectId: {ObjectId}");
    }
    
    public override float GetAttribute(AttributeType type)
    {
        if (_serverPlayerAttributeModule == null) return 0f;
        
        // 直接从服务器模块的NetworkVariable获取值
        return type switch
        {
            // 基础属性
            AttributeType.Health => _serverPlayerAttributeModule.GetAttribute(AttributeType.Health),
            AttributeType.MaxHealth => _serverPlayerAttributeModule.GetAttribute(AttributeType.MaxHealth),
            AttributeType.Shield => _serverPlayerAttributeModule.GetAttribute(AttributeType.Shield),
            AttributeType.MaxShield => _serverPlayerAttributeModule.GetAttribute(AttributeType.MaxShield),
            AttributeType.Armor => _serverPlayerAttributeModule.GetAttribute(AttributeType.Armor),
            AttributeType.MoveSpeed => _serverPlayerAttributeModule.GetAttribute(AttributeType.MoveSpeed),
            AttributeType.Level => _serverPlayerAttributeModule.GetAttribute(AttributeType.Level),
            
            // 抗性属性
            AttributeType.Resistance_Solid => _serverPlayerAttributeModule.GetAttribute(AttributeType.Resistance_Solid),
            AttributeType.Resistance_Liquid => _serverPlayerAttributeModule.GetAttribute(AttributeType.Resistance_Liquid),
            AttributeType.Resistance_Gas => _serverPlayerAttributeModule.GetAttribute(AttributeType.Resistance_Gas),
            AttributeType.Resistance_Toxic => _serverPlayerAttributeModule.GetAttribute(AttributeType.Resistance_Toxic),
            AttributeType.Resistance_Fire => _serverPlayerAttributeModule.GetAttribute(AttributeType.Resistance_Fire),
            AttributeType.Resistance_Ice => _serverPlayerAttributeModule.GetAttribute(AttributeType.Resistance_Ice),
            AttributeType.Resistance_Electric => _serverPlayerAttributeModule.GetAttribute(AttributeType.Resistance_Electric),
            
            // 伤害加成
            AttributeType.DamageOutPutRate => _serverPlayerAttributeModule.GetAttribute(AttributeType.DamageOutPutRate),
            
            // 派系
            AttributeType.Faction => _serverPlayerAttributeModule.GetAttribute(AttributeType.Faction),
            
            // 玩家特有属性
            AttributeType.Energy => _serverPlayerAttributeModule.GetAttribute(AttributeType.Energy),
            AttributeType.MaxEnergy => _serverPlayerAttributeModule.GetAttribute(AttributeType.MaxEnergy),
            AttributeType.EnergyRegen => _serverPlayerAttributeModule.GetAttribute(AttributeType.EnergyRegen),
            AttributeType.Luck => _serverPlayerAttributeModule.GetAttribute(AttributeType.Luck),
            AttributeType.CooldownReduction => _serverPlayerAttributeModule.GetAttribute(AttributeType.CooldownReduction),
            AttributeType.ArmorPenetrationRate => _serverPlayerAttributeModule.GetAttribute(AttributeType.ArmorPenetrationRate), 
            
            _ => 0f
        };
    }
    
    #region 玩家特有方法
    /// <summary>
    /// 检查能量是否足够
    /// </summary>
    public bool HasEnoughEnergy(float amount)
    {
        return GetAttribute(AttributeType.Energy) >= amount;
    }
    
    /// <summary>
    /// 获取冷却缩减后的时间
    /// </summary>
    public float GetReducedCooldown(float baseCooldown)
    {
        float cooldownReduction = GetAttribute(AttributeType.CooldownReduction) / 100f;
        return baseCooldown * (1 - Mathf.Clamp(cooldownReduction, 0f, 0.8f));
    }
    
    /// <summary>
    /// 获取幸运加成的掉落率
    /// </summary>
    public float GetLuckyDropRate(float baseDropRate)
    {
        float luck = GetAttribute(AttributeType.Luck);
        float luckBonus = luck * 0.01f;
        return baseDropRate * (1 + luckBonus);
    }
    
    /// <summary>
    /// 检查是否能量耗尽
    /// </summary>
    public bool IsEnergyExhausted()
    {
        return GetAttribute(AttributeType.Energy) <= 0;
    }
    #endregion
}