// NetworkLayer/AttributeSystem/ServerAttributeModule.cs
using Unity.Netcode;
using UnityEngine;
using System;
using System.Collections.Generic;
using Framework.LogicLayer.DamageCenter;
using Framework.NetworkLayer.NetworkObjectPool;
using Sirenix.OdinInspector;

/// <summary>
/// 服务器权威属性模块基类 - 保留完整的修改器系统
/// </summary>
public abstract class ServerAttributeModule : NetworkBehaviour, IAttributeProxy
{
    private const bool VerboseDamageLogs = false;
    private const float DamagePerCurrencyReward = 10f;

    #region NetworkVariables (核心属性同步)
    protected NetworkVariable<float> _networkHealth = new NetworkVariable<float>(100f);
    protected NetworkVariable<float> _networkMaxHealth = new NetworkVariable<float>(100f);
    protected NetworkVariable<float> _networkShield = new NetworkVariable<float>(50f);
    protected NetworkVariable<float> _networkMaxShield = new NetworkVariable<float>(50f);
    protected NetworkVariable<float> _networkArmor = new NetworkVariable<float>(0f);
    protected NetworkVariable<float> _networkMoveSpeed = new NetworkVariable<float>(5f);
    protected NetworkVariable<int> _networkLevel = new NetworkVariable<int>(1);
    
    // 抗性属性
    protected NetworkVariable<float> _networkResistanceSolid = new NetworkVariable<float>(0f);
    protected NetworkVariable<float> _networkResistanceLiquid = new NetworkVariable<float>(0f);
    protected NetworkVariable<float> _networkResistanceGas = new NetworkVariable<float>(0f);
    protected NetworkVariable<float> _networkResistanceToxic = new NetworkVariable<float>(0f);
    protected NetworkVariable<float> _networkResistanceFire = new NetworkVariable<float>(0f);
    protected NetworkVariable<float> _networkResistanceIce = new NetworkVariable<float>(0f);
    protected NetworkVariable<float> _networkResistanceElectric = new NetworkVariable<float>(0f);
    
    // 韧性
    protected NetworkVariable<float> _networkResilience = new NetworkVariable<float>(0f);
    
    // 伤害加成
    protected NetworkVariable<float> _networkDamageOutputRate = new NetworkVariable<float>(1f);
    
    // 派系
    protected NetworkVariable<int> _networkFaction = new NetworkVariable<int>(0);
    
    
    #endregion

    #region 保留原有AttributeModule的核心逻辑
    protected AttributeConfig _config;  // 特化配置类型
    protected Dictionary<AttributeType, AttributeData> _attributes = new Dictionary<AttributeType, AttributeData>();
    protected bool _isInitialized;

    public string EnemyName => _config != null ? _config.name : "???";
    #endregion

    #region 网络生命周期管理
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
    }

    public override void OnNetworkDespawn()
    {
        // 清理资源
        Cleanup();
        
        base.OnNetworkDespawn();
    }

    /// <summary>
    /// 从配置初始化
    /// </summary>
    protected virtual void InitializeFromConfig()
    {
        // 子类需要实现此方法来设置_config
        if (!_config)
        {
            Debug.LogError($"[ServerAttributeModule] 配置未设置，无法初始化");
            return;
        }
        
        InitializeAttributes();
        SyncBaseAttributesToNetwork();
        _isInitialized = true;
        // 客户端注册网络变量变化监听
        RegisterNetworkVariableCallbacks();
        DebugLog.Info("Network.ServerAttribute", $"[ServerAttributeModule] 初始化完成 - ObjectId: {NetworkObjectId}");
    }

    /// <summary>
    /// 注册网络变量回调
    /// </summary>
    protected virtual void RegisterNetworkVariableCallbacks()
    {
        _networkHealth.OnValueChanged += OnHealthChanged;
        _networkMaxHealth.OnValueChanged += OnMaxHealthChanged;
        _networkShield.OnValueChanged += OnShieldChanged;
        _networkMaxShield.OnValueChanged += OnMaxShieldChanged;
        _networkArmor.OnValueChanged += OnArmorChanged;
        _networkMoveSpeed.OnValueChanged += OnMoveSpeedChanged;
        _networkLevel.OnValueChanged += OnLevelChanged;
    
        // 抗性属性
        _networkResistanceSolid.OnValueChanged += OnResistanceSolidChanged;
        _networkResistanceLiquid.OnValueChanged += OnResistanceLiquidChanged;
        _networkResistanceGas.OnValueChanged += OnResistanceGasChanged;
        _networkResistanceToxic.OnValueChanged += OnResistanceToxicChanged;
        _networkResistanceFire.OnValueChanged += OnResistanceFireChanged;
        _networkResistanceIce.OnValueChanged += OnResistanceIceChanged;
        _networkResistanceElectric.OnValueChanged += OnResistanceElectricChanged;
        
        // 韧性
        _networkResilience.OnValueChanged += OnResilienceChanged;
    
        // 伤害加成
        _networkDamageOutputRate.OnValueChanged += OnDamageOutputRateChanged;
    
        // 派系
        _networkFaction.OnValueChanged += OnFactionChanged;
    }
    
    /// <summary>
    /// 清理资源
    /// </summary>
    protected virtual void Cleanup()
    {
        // 移除网络变量回调
        _networkHealth.OnValueChanged -= OnHealthChanged;
        _networkMaxHealth.OnValueChanged -= OnMaxHealthChanged;
        _networkShield.OnValueChanged -= OnShieldChanged;
        _networkMaxShield.OnValueChanged -= OnMaxShieldChanged;
        _networkArmor.OnValueChanged -= OnArmorChanged;
        _networkMoveSpeed.OnValueChanged -= OnMoveSpeedChanged;
        _networkLevel.OnValueChanged -= OnLevelChanged;
    
        // 抗性属性
        _networkResistanceSolid.OnValueChanged -= OnResistanceSolidChanged;
        _networkResistanceLiquid.OnValueChanged -= OnResistanceLiquidChanged;
        _networkResistanceGas.OnValueChanged -= OnResistanceGasChanged;
        _networkResistanceToxic.OnValueChanged -= OnResistanceToxicChanged;
        _networkResistanceFire.OnValueChanged -= OnResistanceFireChanged;
        _networkResistanceIce.OnValueChanged -= OnResistanceIceChanged;
        _networkResistanceElectric.OnValueChanged -= OnResistanceElectricChanged;
        
        // 韧性
        _networkResilience.OnValueChanged -= OnResilienceChanged;
    
        // 伤害加成
        _networkDamageOutputRate.OnValueChanged -= OnDamageOutputRateChanged;
    
        // 派系
        _networkFaction.OnValueChanged -= OnFactionChanged;

        _attributes?.Clear();
        _isInitialized = false;
    }
    #endregion

    #region 网络变量变化处理
    private void OnResilienceChanged(float previousValue, float newValue)
    {
        if (!IsServer) // 客户端处理
        {
            AttributeChangedEventTrigger(AttributeType.Resilience, previousValue, newValue);
        }
    }
    private void OnHealthChanged(float oldValue, float newValue)
    {
        if (!IsServer) // 客户端处理
        {
            AttributeChangedEventTrigger(AttributeType.Health, oldValue, newValue);
        }
    }

    private void OnMaxHealthChanged(float oldValue, float newValue)
    {
        if (!IsServer) // 客户端处理
        {
            AttributeChangedEventTrigger(AttributeType.MaxHealth, oldValue, newValue);
        }
    }

    private void OnShieldChanged(float oldValue, float newValue)
    {
        if (!IsServer) // 客户端处理
        {
            AttributeChangedEventTrigger(AttributeType.Shield, oldValue, newValue);

        }

    }
    
    private void OnMaxShieldChanged(float oldValue, float newValue)
    {
        if (!IsServer) // 客户端处理
        {
            AttributeChangedEventTrigger(AttributeType.MaxShield, oldValue, newValue);
        }
    }

    private void OnArmorChanged(float oldValue, float newValue)
    {
        if (!IsServer) // 客户端处理
        {
            AttributeChangedEventTrigger(AttributeType.Armor, oldValue, newValue);
        }
    }

    private void OnMoveSpeedChanged(float oldValue, float newValue)
    {
        if (!IsServer) // 客户端处理
        {
            AttributeChangedEventTrigger(AttributeType.MoveSpeed, oldValue, newValue);
        }
    }

    private void OnLevelChanged(int oldValue, int newValue)
    {
        if (!IsServer) // 客户端处理
        {
            AttributeChangedEventTrigger(AttributeType.Level, oldValue, newValue);
        }
    }

    // 抗性属性变化处理
    private void OnResistanceSolidChanged(float oldValue, float newValue)
    {
        if (!IsServer) // 客户端处理
        {
            AttributeChangedEventTrigger(AttributeType.Resistance_Solid, oldValue, newValue);
        }
    }

    private void OnResistanceLiquidChanged(float oldValue, float newValue)
    {
        if (!IsServer) // 客户端处理
        {
            AttributeChangedEventTrigger(AttributeType.Resistance_Liquid, oldValue, newValue);
        }
    }

    private void OnResistanceGasChanged(float oldValue, float newValue)
    {
        if (!IsServer) // 客户端处理
        {
            AttributeChangedEventTrigger(AttributeType.Resistance_Gas, oldValue, newValue);
        }
    }

    private void OnResistanceToxicChanged(float oldValue, float newValue)
    {
        if (!IsServer) // 客户端处理
        {
            AttributeChangedEventTrigger(AttributeType.Resistance_Toxic, oldValue, newValue);
        }
    }

    private void OnResistanceFireChanged(float oldValue, float newValue)
    {
        if (!IsServer) // 客户端处理
        {
            AttributeChangedEventTrigger(AttributeType.Resistance_Fire, oldValue, newValue);
        }
    }

    private void OnResistanceIceChanged(float oldValue, float newValue)
    {
        if (!IsServer) // 客户端处理
        {
            AttributeChangedEventTrigger(AttributeType.Resistance_Ice, oldValue, newValue);
        }
    }

    private void OnResistanceElectricChanged(float oldValue, float newValue)
    {
        if (!IsServer) // 客户端处理
        {
            AttributeChangedEventTrigger(AttributeType.Resistance_Electric, oldValue, newValue);
        }
    }

    // 伤害加成变化处理
    private void OnDamageOutputRateChanged(float oldValue, float newValue)
    {
        if (!IsServer) // 客户端处理
        {
            AttributeChangedEventTrigger(AttributeType.DamageOutPutRate, oldValue, newValue);
        }
    }

    // 派系变化处理
    private void OnFactionChanged(int oldValue, int newValue)
    {
        if (!IsServer) // 客户端处理
        {
            AttributeChangedEventTrigger(AttributeType.Faction, oldValue, newValue);
        }
    }
    
    protected void AttributeChangedEventTrigger(AttributeType attributeType, float oldValue, float newValue)
    {
        EventCenter.Instance.Trigger(EventName.AttributeChanged, new AttributeChangedEvt
        {
            unitId = NetworkObjectId,
            attributeType = attributeType,
            oldValue = oldValue,
            newValue = newValue
        });
    }
    #endregion
    
    #region 初始化方法

    /// <summary>
    /// 设置配置（需要在网络生成前调用）
    /// </summary>
    public abstract void SetConfig(AttributeConfig config);

    /// <summary>
    /// 初始化所有属性（保留原有逻辑）
    /// </summary>
    protected void InitializeAttributes()
    {
        _attributes.Clear();
        
        foreach (AttributeType type in Enum.GetValues(typeof(AttributeType)))
        {
            float baseValue = CalculateBaseValueFromConfig(type, _networkLevel.Value);
            bool hasCurrentValue = type is AttributeType.Health or AttributeType.Shield or AttributeType.Energy;
            
            _attributes[type] = new AttributeData(baseValue, hasCurrentValue);
        }
        
        // 特殊处理派系属性
        _attributes[AttributeType.Faction].BaseValue = (float)_config.baseFaction;
        _attributes[AttributeType.Faction].CurrentValue = (float)_config.baseFaction;
        _attributes[AttributeType.Faction].UpdateCache((float)_config.baseFaction);
    }

    /// <summary>
    /// 从配置计算基础值（保留原有逻辑）
    /// </summary>
    private float CalculateBaseValueFromConfig(AttributeType type, int level)
    {
        return _config.CalculateAttribute(type, level);
    }

    /// <summary>
    /// 同步基础属性到NetworkVariable
    /// </summary>
    protected virtual void SyncBaseAttributesToNetwork()
    {
        if (!IsServer) return;

        // 基础属性
        _networkMaxHealth.Value = GetBaseAttribute(AttributeType.MaxHealth);
        _networkMaxShield.Value = GetBaseAttribute(AttributeType.MaxShield);
        _networkHealth.Value = GetBaseAttribute(AttributeType.Health);
        _networkShield.Value = GetBaseAttribute(AttributeType.Shield);
        _networkArmor.Value = GetBaseAttribute(AttributeType.Armor);
        _networkMoveSpeed.Value = GetBaseAttribute(AttributeType.MoveSpeed);
        
        // 抗性属性
        _networkResistanceSolid.Value = GetBaseAttribute(AttributeType.Resistance_Solid);
        _networkResistanceLiquid.Value = GetBaseAttribute(AttributeType.Resistance_Liquid);
        _networkResistanceGas.Value = GetBaseAttribute(AttributeType.Resistance_Gas);
        _networkResistanceToxic.Value = GetBaseAttribute(AttributeType.Resistance_Toxic);
        _networkResistanceFire.Value = GetBaseAttribute(AttributeType.Resistance_Fire);
        _networkResistanceIce.Value = GetBaseAttribute(AttributeType.Resistance_Ice);
        _networkResistanceElectric.Value = GetBaseAttribute(AttributeType.Resistance_Electric);
        
        // 韧性
        _networkResilience.Value = GetBaseAttribute(AttributeType.Resilience);
        
        // 伤害加成
        _networkDamageOutputRate.Value = GetBaseAttribute(AttributeType.DamageOutPutRate);
        
        // 派系
        _networkFaction.Value = (int)GetBaseAttribute(AttributeType.Faction);
    }

    #endregion

    #region 属性访问（保留原有接口）
    public virtual float GetAttribute(AttributeType type)
    {
        if (!IsServer && TryGetNetworkAttributeValue(type, out float clientNetworkValue))
        {
            return clientNetworkValue;
        }

        if (_attributes.TryGetValue(type, out var data))
        {
            if (data.IsCacheDirty)
            {
                float finalValue = CalculateFinalValue(data);
                data.UpdateCache(finalValue);
            }
            return data.CachedValue;
        }

        return TryGetNetworkAttributeValue(type, out float networkValue) ? networkValue : 0f;
    }

    public virtual float GetBaseAttribute(AttributeType type)
    {
        return _attributes.TryGetValue(type, out var data) ? data.BaseValue : 0f;
    }

    protected bool TryGetNetworkAttributeValue(AttributeType type, out float value)
    {
        switch (type)
        {
            case AttributeType.Health:
                value = _networkHealth.Value;
                return true;
            case AttributeType.MaxHealth:
                value = _networkMaxHealth.Value;
                return true;
            case AttributeType.Shield:
                value = _networkShield.Value;
                return true;
            case AttributeType.MaxShield:
                value = _networkMaxShield.Value;
                return true;
            case AttributeType.Armor:
                value = _networkArmor.Value;
                return true;
            case AttributeType.MoveSpeed:
                value = _networkMoveSpeed.Value;
                return true;
            case AttributeType.Level:
                value = _networkLevel.Value;
                return true;
            case AttributeType.Resistance_Solid:
                value = _networkResistanceSolid.Value;
                return true;
            case AttributeType.Resistance_Liquid:
                value = _networkResistanceLiquid.Value;
                return true;
            case AttributeType.Resistance_Gas:
                value = _networkResistanceGas.Value;
                return true;
            case AttributeType.Resistance_Toxic:
                value = _networkResistanceToxic.Value;
                return true;
            case AttributeType.Resistance_Fire:
                value = _networkResistanceFire.Value;
                return true;
            case AttributeType.Resistance_Ice:
                value = _networkResistanceIce.Value;
                return true;
            case AttributeType.Resistance_Electric:
                value = _networkResistanceElectric.Value;
                return true;
            case AttributeType.Resilience:
                value = _networkResilience.Value;
                return true;
            case AttributeType.DamageOutPutRate:
                value = _networkDamageOutputRate.Value;
                return true;
            case AttributeType.Faction:
                value = _networkFaction.Value;
                return true;
            default:
                value = 0f;
                return false;
        }
    }

    /// <summary>
    /// 计算最终属性值（保留原有逻辑）
    /// </summary>
    protected virtual float CalculateFinalValue(AttributeData data)
    {
        // 保留原有的Set修改器优先级逻辑
        var setModifiers = data.Modifiers.FindAll(m => m.ModifyType == AttributeModifyType.Set);
        if (setModifiers.Count > 0)
        {
            return setModifiers[setModifiers.Count - 1].Value;
        }
        
        float addValue = 0f;
        float multiplyValue = 1f;
        float percentValue = 1f;
        
        foreach (var modifier in data.Modifiers)
        {
            switch (modifier.ModifyType)
            {
                case AttributeModifyType.Add:
                    addValue += modifier.Value * modifier.StackCount;
                    break;
                case AttributeModifyType.Multiply:
                    multiplyValue *= Mathf.Pow(modifier.Value, modifier.StackCount);
                    break;
                case AttributeModifyType.Percentage:
                    percentValue += modifier.Value * modifier.StackCount;
                    break;
            }
        }
        
        float finalValue = (data.BaseValue + addValue) * multiplyValue * (1 + (percentValue / 100f));
        
        if (data.HasCurrentValue)
        {
            return data.CurrentValue;
        }
        
        return finalValue;
    }
    #endregion

    #region 服务器权威的属性修改
    // 1) 服务器内部直接改（不走 RPC）——服务器权威逻辑都放这里
    protected void ModifyAttributeServerInternal(
        AttributeType type,
        float value,
        AttributeModifyType modifyType,
        ulong sourceId,
        int stacks = 1)
    {

        // ================== ！！！把你原 ModifyAttributeServerRpc 的“主体逻辑”全部搬到这里！！！ ==================
        // 例如（按你文件中已有的逻辑顺序）：
        // 1) oldValue = GetAttribute(type)
        // 2) _attributes.TryGetValue(type, out data)
        // 3) 查找 existing modifier -> 叠层/新增
        // 4) data.MarkCacheDirty / data.UpdateCache / ValidateAndClampAttribute 等
        // 5) newValue = GetAttribute(type)
        // 6) SyncCurrentWithMax / UpdateNetworkVariable
        // 7) SyncAttributeChangeClientRpc(type, oldValue, newValue)
        //
        // 注意：不要把 “owner/sender 校验” 搬进来；这是给 RPC 用的。
        // ===========================================================================================
        
        if (!IsServer)
        {
            DebugLog.Warning("Network.ServerAttribute", $"[ModifyAttributeServerInternal] 不在服务器端，直接返回");
            return;
        }

        float oldValue = GetAttribute(type);
        DebugLog.Info("Network.ServerAttribute", $"[ModifyAttributeServerInternal] 属性 {type} 当前值: {oldValue}");
    
        // 应用修改器
        if (_attributes.TryGetValue(type, out var data))
        {
            DebugLog.Info("Network.ServerAttribute", $"[ModifyAttributeServerInternal] 找到属性数据，当前修改器数量: {data.Modifiers.Count}");
            
            // ✔ 先检查是否已有同来源的modifier（按 ItemID / BuffID）
            var existing = data.Modifiers.Find(
                m => m.Source is ulong id 
                     && id == sourceId 
                     && m.ModifyType == modifyType 
                     && Mathf.Approximately(m.Value, value));

            if (existing != null)
            {
                // ✔ 已有 → 叠层
                DebugLog.Info("Network.ServerAttribute", $"[ModifyAttributeServerInternal] 找到已存在的修改器，叠层: {existing.StackCount} -> {existing.StackCount + stacks}");
                existing.StackCount += stacks;
            }
            else
            {
                // ✔ 新增
                DebugLog.Info("Network.ServerAttribute", $"[ModifyAttributeServerInternal] 创建新修改器: modifyType={modifyType}, value={value}, sourceId={sourceId}, stacks={stacks}");
                var modifier = new AttributeModifier(modifyType, value, sourceId);
                modifier.StackCount = stacks;
                data.Modifiers.Add(modifier);
                DebugLog.Info("Network.ServerAttribute", $"[ModifyAttributeServerInternal] 新修改器已添加，当前修改器数量: {data.Modifiers.Count}");
            }
            
            data.MarkCacheDirty();
        
            float newValue = GetAttribute(type);
            DebugLog.Info("Network.ServerAttribute", $"[ModifyAttributeServerInternal] ★ 属性 {type} 值变化: {oldValue} -> {newValue}");
        
            // 处理有当前值的属性
            if (data.HasCurrentValue)
            {
                if (modifyType == AttributeModifyType.Set)
                {
                    // Set模式：直接设置当前值
                    data.CurrentValue = ValidateAndClampAttribute(type, value);
                    // 立即更新缓存，确保GetAttribute返回最新值
                    data.UpdateCache(data.CurrentValue);
                    DebugLog.Info("Network.ServerAttribute", $"[ModifyAttributeServerInternal] Set模式：设置 {type} 当前值为 {data.CurrentValue}");
                }
                else if (modifyType == AttributeModifyType.Add)
                {
                    // Add模式：直接修改当前值（因为GetAttribute返回CurrentValue，不计算修改器）
                    // 注意：修改器仍然会被添加，但当前值会立即更新
                    float oldCurrentValue = data.CurrentValue;
                    float newCurrentValue = oldCurrentValue + value;
                    data.CurrentValue = ValidateAndClampAttribute(type, newCurrentValue);
                    // 立即更新缓存，确保GetAttribute返回最新值
                    data.UpdateCache(data.CurrentValue);
                    DebugLog.Info("Network.ServerAttribute", $"[ModifyAttributeServerInternal] Add模式：{type} 当前值 {oldCurrentValue} + {value} = {data.CurrentValue}");
                }
                else
                {
                    // 对于其他操作（Multiply、Percentage），需要重新计算
                    // 先应用修改器，然后从基础值+修改器计算新值
                    // 注意：对于有CurrentValue的属性，CalculateFinalValue会返回CurrentValue
                    // 所以我们需要手动计算最终值
                    float baseValue = GetBaseAttribute(type);
                    float addValue = 0f;
                    float multiplyValue = 1f;
                    float percentValue = 0f;
                    
                    foreach (var modifier in data.Modifiers)
                    {
                        switch (modifier.ModifyType)
                        {
                            case AttributeModifyType.Add:
                                addValue += modifier.Value * modifier.StackCount;
                                break;
                            case AttributeModifyType.Multiply:
                                multiplyValue *= Mathf.Pow(modifier.Value, modifier.StackCount);
                                break;
                            case AttributeModifyType.Percentage:
                                percentValue += modifier.Value * modifier.StackCount;
                                break;
                        }
                    }
                    
                    float calculatedValue = (baseValue + addValue) * multiplyValue * (1 + percentValue / 100f);
                    data.CurrentValue = ValidateAndClampAttribute(type, calculatedValue);
                    // 立即更新缓存，确保GetAttribute返回最新值
                    data.UpdateCache(data.CurrentValue);
                    DebugLog.Info("Network.ServerAttribute", $"[ModifyAttributeServerInternal] {modifyType}模式：{type} 计算值 = {calculatedValue}");
                }
            
                // 重新获取最终值（因为当前值可能被钳制）
                newValue = GetAttribute(type);
            }
            else
            {
                // 对于没有当前值的属性，直接验证最终值
                newValue = ValidateAndClampAttribute(type, newValue);
                // 更新缓存
                data.UpdateCache(newValue);
            }
            
            // ============================================================
            // 新增：如果是 MaxHealth / MaxShield / MaxEnergy，被道具/Buff修改，
            //       则联动对应的当前值（Health / Shield / Energy）
            // ============================================================
            if (type == AttributeType.MaxHealth ||
                type == AttributeType.MaxShield ||
                type == AttributeType.MaxEnergy)
            {
                SyncCurrentWithMax(type, oldValue, newValue);
            }

            // 更新NetworkVariable（自身的 MaxX）
            UpdateNetworkVariable(type, newValue);
        
            // 同步到客户端
            SyncAttributeChangeClientRpc(type, oldValue, newValue);
        }
    }
    
    /// <summary>
    /// 服务器权威的属性修改方法
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public virtual void ModifyAttributeServerRpc(
        AttributeType type,
        float value,
        AttributeModifyType modifyType,
        ulong sourceId,
        int stacks = 1,
        ServerRpcParams rpcParams = default)
    {
        DebugLog.Info("Network.ServerAttribute", $"[ModifyAttributeServerRpc] ★ 被调用: type={type}, value={value}, modifyType={modifyType}, sourceId={sourceId}, stacks={stacks}, IsServer={IsServer}");
        
        if (!IsServer)
        {
            DebugLog.Warning("Network.ServerAttribute", $"[ModifyAttributeServerRpc] 不在服务器端，直接返回");
            return;
        }

        // float oldValue = GetAttribute(type);
        // DebugLog.Info("Network.ServerAttribute", $"[ModifyAttributeServerRpc] 属性 {type} 当前值: {oldValue}");
        //
        // // 应用修改器
        // if (_attributes.TryGetValue(type, out var data))
        // {
        //     DebugLog.Info("Network.ServerAttribute", $"[ModifyAttributeServerRpc] 找到属性数据，当前修改器数量: {data.Modifiers.Count}");
        //     
        //     // ✔ 先检查是否已有同来源的modifier（按 ItemID / BuffID）
        //     var existing = data.Modifiers.Find(
        //         m => m.Source is ulong id 
        //              && id == sourceId 
        //              && m.ModifyType == modifyType 
        //              && Mathf.Approximately(m.Value, value));
        //
        //     if (existing != null)
        //     {
        //         // ✔ 已有 → 叠层
        //         DebugLog.Info("Network.ServerAttribute", $"[ModifyAttributeServerRpc] 找到已存在的修改器，叠层: {existing.StackCount} -> {existing.StackCount + stacks}");
        //         existing.StackCount += stacks;
        //     }
        //     else
        //     {
        //         // ✔ 新增
        //         DebugLog.Info("Network.ServerAttribute", $"[ModifyAttributeServerRpc] 创建新修改器: modifyType={modifyType}, value={value}, sourceId={sourceId}, stacks={stacks}");
        //         var modifier = new AttributeModifier(modifyType, value, sourceId);
        //         modifier.StackCount = stacks;
        //         data.Modifiers.Add(modifier);
        //         DebugLog.Info("Network.ServerAttribute", $"[ModifyAttributeServerRpc] 新修改器已添加，当前修改器数量: {data.Modifiers.Count}");
        //     }
        //     
        //     data.MarkCacheDirty();
        //
        //     float newValue = GetAttribute(type);
        //     DebugLog.Info("Network.ServerAttribute", $"[ModifyAttributeServerRpc] ★ 属性 {type} 值变化: {oldValue} -> {newValue}");
        //
        //     // 处理有当前值的属性
        //     if (data.HasCurrentValue)
        //     {
        //         if (modifyType == AttributeModifyType.Set)
        //         {
        //             // Set模式：直接设置当前值
        //             data.CurrentValue = ValidateAndClampAttribute(type, value);
        //             // 立即更新缓存，确保GetAttribute返回最新值
        //             data.UpdateCache(data.CurrentValue);
        //             DebugLog.Info("Network.ServerAttribute", $"[ModifyAttributeServerRpc] Set模式：设置 {type} 当前值为 {data.CurrentValue}");
        //         }
        //         else if (modifyType == AttributeModifyType.Add)
        //         {
        //             // Add模式：直接修改当前值（因为GetAttribute返回CurrentValue，不计算修改器）
        //             // 注意：修改器仍然会被添加，但当前值会立即更新
        //             float oldCurrentValue = data.CurrentValue;
        //             float newCurrentValue = oldCurrentValue + value;
        //             data.CurrentValue = ValidateAndClampAttribute(type, newCurrentValue);
        //             // 立即更新缓存，确保GetAttribute返回最新值
        //             data.UpdateCache(data.CurrentValue);
        //             DebugLog.Info("Network.ServerAttribute", $"[ModifyAttributeServerRpc] Add模式：{type} 当前值 {oldCurrentValue} + {value} = {data.CurrentValue}");
        //         }
        //         else
        //         {
        //             // 对于其他操作（Multiply、Percentage），需要重新计算
        //             // 先应用修改器，然后从基础值+修改器计算新值
        //             // 注意：对于有CurrentValue的属性，CalculateFinalValue会返回CurrentValue
        //             // 所以我们需要手动计算最终值
        //             float baseValue = GetBaseAttribute(type);
        //             float addValue = 0f;
        //             float multiplyValue = 1f;
        //             float percentValue = 0f;
        //             
        //             foreach (var modifier in data.Modifiers)
        //             {
        //                 switch (modifier.ModifyType)
        //                 {
        //                     case AttributeModifyType.Add:
        //                         addValue += modifier.Value * modifier.StackCount;
        //                         break;
        //                     case AttributeModifyType.Multiply:
        //                         multiplyValue *= Mathf.Pow(modifier.Value, modifier.StackCount);
        //                         break;
        //                     case AttributeModifyType.Percentage:
        //                         percentValue += modifier.Value * modifier.StackCount;
        //                         break;
        //                 }
        //             }
        //             
        //             float calculatedValue = (baseValue + addValue) * multiplyValue * (1 + percentValue / 100f);
        //             data.CurrentValue = ValidateAndClampAttribute(type, calculatedValue);
        //             // 立即更新缓存，确保GetAttribute返回最新值
        //             data.UpdateCache(data.CurrentValue);
        //             DebugLog.Info("Network.ServerAttribute", $"[ModifyAttributeServerRpc] {modifyType}模式：{type} 计算值 = {calculatedValue}");
        //         }
        //     
        //         // 重新获取最终值（因为当前值可能被钳制）
        //         newValue = GetAttribute(type);
        //     }
        //     else
        //     {
        //         // 对于没有当前值的属性，直接验证最终值
        //         newValue = ValidateAndClampAttribute(type, newValue);
        //         // 更新缓存
        //         data.UpdateCache(newValue);
        //     }
        //     
        //     // ============================================================
        //     // 新增：如果是 MaxHealth / MaxShield / MaxEnergy，被道具/Buff修改，
        //     //       则联动对应的当前值（Health / Shield / Energy）
        //     // ============================================================
        //     if (type == AttributeType.MaxHealth ||
        //         type == AttributeType.MaxShield ||
        //         type == AttributeType.MaxEnergy)
        //     {
        //         SyncCurrentWithMax(type, oldValue, newValue);
        //     }
        //
        //     // 更新NetworkVariable（自身的 MaxX）
        //     UpdateNetworkVariable(type, newValue);
        //
        //     // 同步到客户端
        //     SyncAttributeChangeClientRpc(type, oldValue, newValue);
        // }
        
        // 安全校验：只能改“自己拥有的 NetworkObject”的属性
        // 否则任何客户端都能调用这个 RPC 去改别人的属性（严重漏洞）
        if (rpcParams.Receive.SenderClientId != OwnerClientId)
        {
            Debug.LogWarning(
                $"[ModifyAttributeServerRpc] Reject: sender={rpcParams.Receive.SenderClientId}, owner={OwnerClientId}, type={type}");
            return;
        }

        ModifyAttributeServerInternal(type, value, modifyType, sourceId, stacks);
    }

    /// <summary>
    /// 移除特定来源的修改器
    /// </summary>
    /// <param name="type">修正类型</param>
    /// <param name="sourceId">来源ID，这里是指定的ID或者是某个游戏对象的ObjectID</param>
    /// <param name="stacks">默认为1指示删除几层，若为0则删除所有</param>
    [ServerRpc]
    public void RemoveModifiersServerRpc(
        AttributeType type,
        ulong sourceId,
        int stacks = 1)
    {
        if (!IsServer) return;

        RemoveModifiersServerInternal(type, sourceId, stacks);
    }

    protected void RemoveModifiersServerInternal(
        AttributeType type,
        ulong sourceId,
        int stacks = 1)
    {
        if (!IsServer) return;
        
        if (_attributes.TryGetValue(type, out var data))
        {
            float oldValue = GetAttribute(type);
        
            var modifier = data.Modifiers.Find(m => m.Source is ulong id && id == sourceId);
            
            if (stacks == 0)
            {
                // 删除所有
                data.Modifiers.RemoveAll(m => m.Source is ulong id && id == sourceId);
            }
            
            if (modifier != null)
            {
                modifier.StackCount -= stacks;

                if (modifier.StackCount <= 0)
                    data.Modifiers.Remove(modifier);
            }
            
            data.MarkCacheDirty();
        
            float newValue = GetAttribute(type);
        
            // 移除修改器后重新验证属性值
            if (data.HasCurrentValue)
            {
                data.CurrentValue = ValidateAndClampAttribute(type, data.CurrentValue);
                newValue = GetAttribute(type);
            }
            else
            {
                newValue = ValidateAndClampAttribute(type, newValue);
                data.UpdateCache(newValue);
            }
            
            // ============================================================
            // 新增：移除作用于 MaxHealth / MaxShield / MaxEnergy 的修改器时，
            //       同样需要联动当前值（避免 Max 掉了但当前值比例不对）
            // ============================================================
            if (type == AttributeType.MaxHealth ||
                type == AttributeType.MaxShield ||
                type == AttributeType.MaxEnergy)
            {
                SyncCurrentWithMax(type, oldValue, newValue);
            }

            // 更新NetworkVariable（MaxX 自身）
            UpdateNetworkVariable(type, newValue);

            AttributeChangedEventTrigger(type, oldValue, newValue);
            SyncAttributeChangeClientRpc(type, oldValue, newValue);
        }
    }
    
    /// <summary>
    /// 获取属性的有效范围
    /// </summary>
    protected virtual (float min, float max) GetAttributeRange(AttributeType type)
    {
        return type switch
        {
            // 通用属性范围
            AttributeType.Health => (0f, GetAttribute(AttributeType.MaxHealth)),
            AttributeType.Shield => (0f, GetAttribute(AttributeType.MaxShield)),
            AttributeType.Energy => (0f, GetAttribute(AttributeType.MaxEnergy)),
            AttributeType.MoveSpeed => (0f, float.MaxValue),
            AttributeType.Level => (1f, float.MaxValue),
        
            // 抗性属性范围 (-100% 到 100%)
            AttributeType.Resistance_Solid => (-1f, 1f),
            AttributeType.Resistance_Liquid => (-1f, 1f),
            AttributeType.Resistance_Gas => (-1f, 1f),
            AttributeType.Resistance_Toxic => (-1f, 1f),
            AttributeType.Resistance_Fire => (-1f, 1f),
            AttributeType.Resistance_Ice => (-1f, 1f),
            AttributeType.Resistance_Electric => (-1f, 1f),
            
        
            _ => (float.MinValue, float.MaxValue) // 默认无限制
        };
    }

    /// <summary>
    /// 验证并钳制属性值到有效范围
    /// </summary>
    protected virtual float ValidateAndClampAttribute(AttributeType type, float value)
    {
        var (min, max) = GetAttributeRange(type);
        return Mathf.Clamp(value, min, max);
    }
    
    protected void SyncCurrentWithMax(AttributeType maxType, float oldMax, float newMax)
    {
        // 没变就不用动
        if (Mathf.Approximately(oldMax, newMax))
            return;

        AttributeType currentType;
        switch (maxType)
        {
            case AttributeType.MaxHealth:
                currentType = AttributeType.Health;
                break;
            case AttributeType.MaxShield:
                currentType = AttributeType.Shield;
                break;
            case AttributeType.MaxEnergy:
                currentType = AttributeType.Energy;
                break;
            default:
                return;
        }

        if (!_attributes.TryGetValue(currentType, out var currentData))
            return;
        if (!currentData.HasCurrentValue)
            return;

        float oldCurrent = currentData.CurrentValue;

        // 设计：保持「当前值 / 最大值」的比例不变
        // oldMax <= 0 的极端情况：直接按满值处理
        float ratio = (oldMax > 0f)
            ? Mathf.Clamp01(oldCurrent / oldMax)
            : 1f;

        float target = newMax > 0f ? newMax * ratio : 0f;

        // 用统一的范围校验（Health/Shield/Energy 会走各自 MaxX 的范围）
        float newCurrent = ValidateAndClampAttribute(currentType, target);

        currentData.CurrentValue = newCurrent;
        currentData.MarkCacheDirty();
        currentData.UpdateCache(newCurrent);

        // 同步对应 NetworkVariable（玩家/怪物子类会在 override 里继续处理）
        UpdateNetworkVariable(currentType, newCurrent);

        // 触发事件 & 同步到客户端
        AttributeChangedEventTrigger(currentType, oldCurrent, newCurrent);
        SyncAttributeChangeClientRpc(currentType, oldCurrent, newCurrent);
    }
    
    #endregion

    #region 服务器权威的伤害处理
    /// <summary>
    /// 服务器处理伤害
    /// </summary>
    public void TakeDamage(DamageInfo damageInfo)
    {
        if (!IsServer) return;

        // 0. 找到自己身上的 BuffHandler（ServerBuffModule）
        var buffModule = GetComponent<ServerBuffModule>();
        var buffHandler = buffModule ? buffModule.Handler : null;

        // 1. 受击前回调（可以在这里做“减伤”、“无敌帧”等逻辑）
        buffHandler?.ApplyUponBeHurt(damageInfo);
        
        // 先读取当前护盾 / 生命
        float currentShield = GetAttribute(AttributeType.Shield);
        float currentHealth = GetAttribute(AttributeType.Health);
#if UNITY_EDITOR && false
        DebugLog.Info("Network.ServerAttribute", $"[TakeDamage] {gameObject.GetHashCode()} 受到伤害信息总量：{damageInfo.amount}");
#endif
        // 让 DamageCenter 按「毒穿盾 + 液体子弹双倍盾」规则，计算最终分配
        var damageResult = DamageCalculator.ApplyDamage(damageInfo, currentHealth, currentShield);

        // 应用护盾伤害
        if (damageResult.shieldDamage > 0f && _attributes.ContainsKey(AttributeType.Shield))
        {
            float newShield = Mathf.Max(0f, currentShield - damageResult.shieldDamage);
            _attributes[AttributeType.Shield].CurrentValue = newShield;
            _attributes[AttributeType.Shield].MarkCacheDirty();
            _networkShield.Value = newShield;
            AttributeChangedEventTrigger(AttributeType.Shield, currentShield, newShield);
            SyncAttributeChangeClientRpc(AttributeType.Shield, currentShield, newShield);
#if UNITY_EDITOR && false
        DebugLog.Info("Network.ServerAttribute", $"[TakeDamage] {gameObject.GetHashCode()} 受到护盾伤害结果总量：{damageResult.shieldDamage}，应用时间：{Time.time}，护盾变化：{currentShield} -> {newShield}");
#endif
        }

        // 应用生命值伤害
        if (damageResult.healthDamage > 0f && _attributes.ContainsKey(AttributeType.Health))
        {
            float newHealth = Mathf.Max(0f, currentHealth - damageResult.healthDamage);
            _attributes[AttributeType.Health].CurrentValue = newHealth;
            _attributes[AttributeType.Health].MarkCacheDirty();
            _networkHealth.Value = newHealth;
            damageResult.targetDied = newHealth <= 0f;
            AttributeChangedEventTrigger(AttributeType.Health, currentHealth, newHealth);
            SyncAttributeChangeClientRpc(AttributeType.Health, currentHealth, newHealth);
#if UNITY_EDITOR && false
        DebugLog.Info("Network.ServerAttribute", $"[TakeDamage] {gameObject.GetHashCode()} 受到生命伤害结果总量：{damageResult.healthDamage}，应用时间：{Time.time}，生命值变化：{currentHealth} -> {newHealth}");
#endif
        }

        OnDamageApplied(damageInfo, damageResult);

        // 6. 受击后回调（此时 damageResult 已经包含最终扣的血/盾）
        buffHandler?.ApplyOnBeHurt(damageInfo);
        
        // ★ 触发品质效果：受到伤害时（仅对玩家）
        var targetPlayerProxy = GetComponent<PlayerNetworkProxy>();
        if (targetPlayerProxy?.PlayerActor != null)
        {
            var attackerProxy = NetworkObjectManager.Instance.TryGetNetworkProxy<NetworkProxyBase>(
                damageInfo.sourceActorId, out var attacker) ? attacker : null;
            targetPlayerProxy.PlayerActor.QualityEffectModule?.RaiseOnHitReceived(
                attackerProxy, 
                damageResult.totalDamage
            );
        }

        if (damageResult.targetDied)
        {
            buffHandler?.ApplyOnDeath(damageInfo);

            // 同时给攻击方发 OnKill
            if (NetworkObjectManager.Instance.TryGetNetworkProxy<NetworkProxyBase>(damageInfo.sourceActorId, out var attackerProxy))
            {
                var attackerBuffModule = attackerProxy.GetComponent<ServerBuffModule>();
                var attackerHandler    = attackerBuffModule ? attackerBuffModule.Handler : null;
                attackerHandler?.ApplyOnKill(damageInfo);
                
                // ★ 触发品质效果：击杀敌人时（仅对玩家）
                var attackerPlayerProxy = attackerProxy as PlayerNetworkProxy;
                if (attackerPlayerProxy?.PlayerActor != null)
                {
                    var targetProxy = GetComponent<NetworkProxyBase>();
                    attackerPlayerProxy.PlayerActor.QualityEffectModule?.RaiseOnKill(targetProxy);
                }
            }
        }
        
        // 元素触发层数（由 DamageCalculator.CalculateDamage 已经算好）
        ApplyElementTriggers(damageResult, damageInfo);

        var contributionTracker = GetComponent<DamageContributionTracker>();
        if (contributionTracker != null &&
            TryResolveDamageContributorClientId(damageInfo, out var contributorClientId))
        {
            contributionTracker.RecordDamage(contributorClientId, damageResult.totalDamage);
        }

        // 触发受伤事件 / 同步客户端
        RewardCurrencyForEnemyDamage(damageInfo, damageResult);

        EventCenter.Instance.Trigger(EventName.UnitDamaged, new UnitDamagedEvt
        {
            targetId = NetworkObjectId,
            instigatorId = damageInfo.sourceActorId,
            damageResult = damageResult
        });
        
        // ============================================================
        // ★ 新增：构造 DamageVfxEvent（命中点在这里最权威）
        // ============================================================
        var vfxEvt = new DamageVfxEvent
        {
            targetId = damageInfo.targetActorId,
            sourceId = damageInfo.sourceActorId,
            hitWorldPos = damageInfo.hitWorldPos,
            damageResult = damageResult
        };
        
        var rpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new[] { damageInfo.instigator }
            }
        };
        // SyncDamageVfxClientRpc(vfxEvt);
        SyncDamageVfxClientRpc(vfxEvt, rpcParams);
        OnHitAnimation();

        SyncDamageResultClientRpc(damageResult);
        CheckDeathCondition();
    }

    private void RewardCurrencyForEnemyDamage(DamageInfo damageInfo, DamageResult damageResult)
    {
        if (!IsServer || damageResult.totalDamage <= 0f)
            return;

        if (this is not ServerEnemyAttributeModule)
            return;

        int reward = Mathf.FloorToInt(damageResult.totalDamage / DamagePerCurrencyReward);
        if (reward <= 0)
            return;

        if (NetworkObjectManager.Instance == null ||
            !NetworkObjectManager.Instance.TryGetNetworkProxy<PlayerNetworkProxy>(damageInfo.sourceActorId, out var playerProxy) ||
            playerProxy == null)
        {
            return;
        }

        var inventory = playerProxy.NetworkInventory != null
            ? playerProxy.NetworkInventory
            : playerProxy.GetComponent<NetworkInventory>();
        inventory?.AddCurrencyServerInternal(reward);
    }

    private static bool TryResolveDamageContributorClientId(DamageInfo damageInfo, out ulong contributorClientId)
    {
        contributorClientId = 0;

        if (NetworkObjectManager.Instance == null)
        {
            return false;
        }

        if (!NetworkObjectManager.Instance.TryGetNetworkProxy<PlayerNetworkProxy>(
                damageInfo.sourceActorId,
                out var playerProxy) ||
            playerProxy == null)
        {
            return false;
        }

        contributorClientId = playerProxy.OwnerClientId;
        return true;
    }

    protected virtual void OnHitAnimation()
    {
        // 子类（如 ServerEnemyAttributeModule）可重写以触发受击动画
    }

    protected virtual void OnDamageApplied(DamageInfo damageInfo, DamageResult damageResult)
    {
        // 子类可重写以响应服务端已经结算的实际伤害。
    }

    /// <summary>
    /// 应用元素触发。
    /// </summary>
    protected virtual void ApplyElementTriggers(DamageResult damageResult)
    {
        ApplyElementTriggers(damageResult, default);
    }

    protected virtual void ApplyElementTriggers(DamageResult damageResult, DamageInfo sourceDamageInfo)
    {
        if (!IsServer) return;

        var buffModule = GetComponent<ServerBuffModule>();
        if (buffModule == null)
        {
            return;
        }

        ulong applierObjectId = sourceDamageInfo.sourceActorId;
        ulong applierClientId = sourceDamageInfo.instigator;
        if (TryResolveDamageContributorClientId(sourceDamageInfo, out var contributorClientId))
        {
            applierClientId = contributorClientId;
        }

        ApplyElementTrigger(
            buffModule,
            ElementType.Ice,
            damageResult.iceTriggerLayer,
            damageResult.iceDamage,
            sourceDamageInfo,
            applierObjectId,
            applierClientId);

        ApplyElementTrigger(
            buffModule,
            ElementType.Fire,
            damageResult.fireTriggerLayer,
            damageResult.fireDamage,
            sourceDamageInfo,
            applierObjectId,
            applierClientId);

        ApplyElementTrigger(
            buffModule,
            ElementType.Poison,
            damageResult.poisonTriggerLayer,
            damageResult.poisonDamage,
            sourceDamageInfo,
            applierObjectId,
            applierClientId);

        ApplyElementTrigger(
            buffModule,
            ElementType.Electric,
            damageResult.electricTriggerLayer,
            damageResult.electricDamage,
            sourceDamageInfo,
            applierObjectId,
            applierClientId);
    }

    private void ApplyElementTrigger(
        ServerBuffModule buffModule,
        ElementType element,
        int layers,
        float elementDamageSnapshot,
        DamageInfo sourceDamageInfo,
        ulong applierObjectId,
        ulong applierClientId)
    {
        if (layers <= 0)
        {
            return;
        }

        var buffData = ElementBuffMappingAsset.Resolve(element);
        if (buffData == null)
        {
            DebugLog.Warning("Network.ServerAttribute", $"[ServerAttributeModule] 未找到 {element} 元素 BuffData，请配置 ElementBuffMappingAsset 或 BuffData tags/buffID。");
            return;
        }

        buffModule.ApplyBuff(
            buffData,
            layers,
            -1f,
            applierObjectId,
            applierClientId,
            sourceDamageInfo,
            element,
            Mathf.Max(0f, elementDamageSnapshot));
    }
    #endregion

    #region 客户端同步
    [ClientRpc]
    private void SyncAttributeChangeClientRpc(AttributeType type, float oldValue, float newValue)
    {
        if (IsServer && !IsHost) return;

        // 客户端处理属性变化（UI更新等）
        if (VerboseDamageLogs)
        {
            DebugLog.Info("Network.ServerAttribute", $"[Client] 属性变化 - Type: {type}, From: {oldValue}, To: {newValue}");
        }
    }

    [ClientRpc]
    private void SyncDamageResultClientRpc(DamageResult damageResult)
    {
        if (IsServer && !IsHost) return;

        // 客户端处理伤害效果（受击动画、跳字等）
        if (VerboseDamageLogs)
        {
            DebugLog.Info("Network.ServerAttribute", $"[Client] 受到伤害 - Total: {damageResult.totalDamage}");
        }
    }
    
    [ClientRpc]
    private void SyncDamageVfxClientRpc(DamageVfxEvent evt, ClientRpcParams clientRpcParams = default)
    {
        // DebugLog.Info("Network.ServerAttribute", $"[DamageVfx] RPC received. IsServer={IsServer}, IsClient={IsClient}, IsHost={IsHost}, pos={evt.hitWorldPos}");
        if (IsServer && !IsHost) return;
        Vector3 spawnPos = evt.hitWorldPos;

        // 极端兜底（比如命中点没算出来）
        if (spawnPos == Vector3.zero &&
            NetworkObjectManager.Instance
                .TryGetNetworkProxy<NetworkProxyBase>(evt.targetId, out var proxy))
        {
            spawnPos = proxy.transform.position;
        }
        // DebugLog.Info("Network.ServerAttribute", $"[DamageVfx] Manager={(DamageWorldTextManager.Instance ? "OK" : "NULL")}");
        DamageWorldTextManager.Instance.ShowDamageWorld(
            spawnPos,
            evt.damageResult
        );
    }
    #endregion

    #region 工具方法（保留原有逻辑）
    protected virtual AttributeType GetMaxAttributeType(AttributeType type)
    {
        return type switch
        {
            AttributeType.Health => AttributeType.MaxHealth,
            AttributeType.Shield => AttributeType.MaxShield,
            _ => type
        };
    }

    #region 死亡处理 - 回归对象池
    protected virtual void CheckDeathCondition()
    {
        if (GetAttribute(AttributeType.Health) <= 0)
        {
            HandleDeath();
        }
    }

    /// <summary>
    /// 处理死亡 - 回归对象池
    /// </summary>
    protected virtual void HandleDeath()
    {
        // 触发死亡事件
        EventCenter.Instance.Trigger(EventName.UnitDied, new UnitDiedEvt { unitId = NetworkObjectId });
        
        // ★ 触发品质效果：自身死亡时（仅对玩家）
        var playerProxy = GetComponent<PlayerNetworkProxy>();
        if (playerProxy?.PlayerActor != null)
        {
            playerProxy.PlayerActor.QualityEffectModule?.RaiseOnDeath();
        }
        
        // 延迟回归对象池，确保事件传播完成
        if (IsServer)
        {
            StartCoroutine(ReturnToPoolAfterDelay());
        }
    }

    private System.Collections.IEnumerator ReturnToPoolAfterDelay()
    {
        float delay = GetDeathReturnDelay();
        if (float.IsNaN(delay) || float.IsInfinity(delay) || delay < 0f)
        {
            DebugLog.Warning("Network.ServerAttribute", $"[ServerAttributeModule] Invalid death return delay {delay}, fallback to 1s. ObjectId={NetworkObjectId}", this);
            delay = 1f;
        }

        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        // 获取网络对象并回归对象池
        NetworkObject networkObject = GetComponent<NetworkObject>();
        if (networkObject == null)
        {
            DebugLog.Warning("Network.ServerAttribute", $"[ServerAttributeModule] Death recycle skipped because NetworkObject is missing. ObjectId={NetworkObjectId}", this);
            yield break;
        }

        if (!IsServer || !networkObject.IsSpawned)
        {
            yield break;
        }

        // 这里需要知道预制体路径，可以通过组件或配置获取
        string prefabPath = GetPrefabPath();
        try
        {
            if (!string.IsNullOrEmpty(prefabPath))
            {
                NetworkObjectPoolManager.Instance.DespawnAndRecycle(networkObject, prefabPath);
            }
            else
            {
                networkObject.Despawn();
            }
        }
        catch (Exception ex)
        {
            Debug.LogException(ex, this);
            if (networkObject != null && networkObject.IsSpawned)
            {
                networkObject.Despawn();
            }
        }
    }

    protected virtual float GetDeathReturnDelay()
    {
        return 1f;
    }
    /// <summary>
    /// 获取预制体路径（子类需要重写）
    /// </summary>
    protected virtual string GetPrefabPath()
    {
        // 子类需要实现此方法来返回正确的预制体路径
        return string.Empty;
    }
    #endregion

    // 基类只处理通用属性同步
    protected virtual void UpdateNetworkVariable(AttributeType type, float newValue)
    {
        if (!IsServer) return;

        switch (type)
        {
            // 只处理通用属性
            case AttributeType.Health:
                _networkHealth.Value = newValue;
                break;
            case AttributeType.MaxHealth:
                _networkMaxHealth.Value = newValue;
                break;
            case AttributeType.Shield:
                _networkShield.Value = newValue;
                break;
            case AttributeType.MaxShield:
                _networkMaxShield.Value = newValue;
                break;
            case AttributeType.Armor:
                _networkArmor.Value = newValue;
                break;
            case AttributeType.MoveSpeed:
                _networkMoveSpeed.Value = newValue;
                break;
            case AttributeType.Level:
                _networkLevel.Value = (int)newValue;
                break;
            
            // 抗性属性
            case AttributeType.Resistance_Solid:
                _networkResistanceSolid.Value = newValue;
                break;
            case AttributeType.Resistance_Liquid:
                _networkResistanceLiquid.Value = newValue;
                break;
            case AttributeType.Resistance_Gas:
                _networkResistanceGas.Value = newValue;
                break;
            case AttributeType.Resistance_Toxic:
                _networkResistanceToxic.Value = newValue;
                break;
            case AttributeType.Resistance_Fire:
                _networkResistanceFire.Value = newValue;
                break;
            case AttributeType.Resistance_Ice:
                _networkResistanceIce.Value = newValue;
                break;
            case AttributeType.Resistance_Electric:
                _networkResistanceElectric.Value = newValue;
                break;
            
            // 韧性
            case AttributeType.Resilience:
                _networkResilience.Value = newValue;
                break;
            
            // 伤害加成
            case AttributeType.DamageOutPutRate:
                _networkDamageOutputRate.Value = newValue;
                break;
            
            // 派系
            case AttributeType.Faction:
                _networkFaction.Value = (int)newValue;
                break;
        }
    }
    
    #endregion

    #region 原有的高级操作方法（服务器RPC版本）
    [ServerRpc]
    public void HealServerRpc(float amount)
    {
        if (!IsServer) return;

        if (!_attributes.ContainsKey(AttributeType.Health)) return;
            
        float currentHealth = GetAttribute(AttributeType.Health);
        float maxHealth = GetAttribute(AttributeType.MaxHealth);
        float newHealth = Mathf.Min(currentHealth + amount, maxHealth);
        float actualHeal = newHealth - currentHealth; // 实际治疗量
        
        _attributes[AttributeType.Health].CurrentValue = newHealth;
        _attributes[AttributeType.Health].MarkCacheDirty();
        _networkHealth.Value = newHealth;
        AttributeChangedEventTrigger(AttributeType.Health, currentHealth, newHealth);
        SyncAttributeChangeClientRpc(AttributeType.Health, currentHealth, newHealth);
        
        // ★ 触发品质效果：受到治疗时（仅对玩家，且实际治疗量大于0）
        var playerProxy = GetComponent<PlayerNetworkProxy>();
        if (playerProxy?.PlayerActor != null && actualHeal > 0f)
        {
            playerProxy.PlayerActor.QualityEffectModule?.RaiseOnHeal(actualHeal);
        }
    }

    [ServerRpc]
    public void RestoreShieldServerRpc(float amount)
    {
        RestoreShieldServerInternal(amount);
    }

    protected bool RestoreShieldServerInternal(float amount)
    {
        if (!IsServer) return false;
        if (amount <= 0f) return false;
        if (!_attributes.ContainsKey(AttributeType.Shield)) return false;

        float currentShield = GetAttribute(AttributeType.Shield);
        float maxShield = GetAttribute(AttributeType.MaxShield);
        if (maxShield <= 0f) return false;

        float newShield = Mathf.Min(currentShield + amount, maxShield);
        if (newShield <= currentShield) return false;

        _attributes[AttributeType.Shield].CurrentValue = newShield;
        _attributes[AttributeType.Shield].MarkCacheDirty();
        _networkShield.Value = newShield;
        AttributeChangedEventTrigger(AttributeType.Shield, currentShield, newShield);
        SyncAttributeChangeClientRpc(AttributeType.Shield, currentShield, newShield);
        return true;
    }

    [ServerRpc]
    public void SetLevelServerRpc(int level)
    {
        if (!IsServer) return;

        if (level < 1) level = 1;
        
        _networkLevel.Value = level;
        
        // 重新计算所有基础属性（保留原有逻辑）
        foreach (var kvp in _attributes)
        {
            float oldBaseValue = kvp.Value.BaseValue;
            float newBaseValue = CalculateBaseValueFromConfig(kvp.Key, level);
            
            kvp.Value.BaseValue = newBaseValue;
            
            if (kvp.Value.HasCurrentValue && oldBaseValue > 0)
            {
                float ratio = kvp.Value.CurrentValue / oldBaseValue;
                kvp.Value.CurrentValue = newBaseValue * ratio;
            }
            else if (kvp.Key == AttributeType.Level)
            {
                kvp.Value.BaseValue = level;
                kvp.Value.CurrentValue = level;
            }
            
            kvp.Value.MarkCacheDirty();
            
            float newValue = GetAttribute(kvp.Key);
            AttributeChangedEventTrigger(kvp.Key, newValue, newValue);
            SyncAttributeChangeClientRpc(kvp.Key, newValue, newValue);
        }
    }
    #endregion
    
    
    #region IAttributeProxy 实现（只是显式地把你原来预留的能力暴露为接口）

    public void AddModifier(AttributeType type, AttributeModifyType modifyType, float value, ulong sourceId, int stackDelta = 1)
    {
        if (IsServer)
        {
            ModifyAttributeServerInternal(type, value, modifyType, sourceId, stackDelta);
            return;
        }

        // 未 Spawn 时不发 RPC，防止 NGO 内部字典 KeyNotFoundException
        if (!IsSpawned)
        {
            Debug.LogWarning(
                $"[ServerAttributeModule] AddModifier 在未 Spawn 状态下被调用，" +
                $"跳过 RPC (type={type}, sourceId={sourceId})");
            return;
        }

        ModifyAttributeServerRpc(type, value, modifyType, sourceId, stackDelta);
    }

    public void RemoveModifiers(AttributeType type, ulong sourceId, int stackDelta = 1)
    {
        if (IsServer)
        {
            RemoveModifiersServerInternal(type, sourceId, stackDelta);
            return;
        }

        // 未 Spawn 时不发 RPC，防止 NGO 内部字典 KeyNotFoundException
        if (!IsSpawned)
        {
            Debug.LogWarning(
                $"[ServerAttributeModule] RemoveModifiers 在未 Spawn 状态下被调用，" +
                $"跳过 RPC (type={type}, sourceId={sourceId})");
            return;
        }

        RemoveModifiersServerRpc(type, sourceId, stackDelta);
    }

    #endregion
}

/// <summary>
/// 网络序列化属性集合
/// </summary>
[System.Serializable]
public struct NetworkSerializedAttributeSet : INetworkSerializable
{
    // 玩家特有属性
    public float energy;
    public float maxEnergy;
    public float energyRegen;
    public float luck;
    public float cooldownReduction;
    public float resilience;
    public float armorPenetrationRate;
    
    // 怪物特有属性
    public float aggroRange;
    public int monsterRank;
    public float dropRate;
    public float inGameGoldReward;
    public float outGameCurrencyReward;
    public float detectionRange;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        // 玩家属性
        serializer.SerializeValue(ref energy);
        serializer.SerializeValue(ref maxEnergy);
        serializer.SerializeValue(ref energyRegen);
        serializer.SerializeValue(ref luck);
        serializer.SerializeValue(ref cooldownReduction);
        serializer.SerializeValue(ref resilience);
        serializer.SerializeValue(ref armorPenetrationRate);
        
        // 怪物属性
        serializer.SerializeValue(ref aggroRange);
        serializer.SerializeValue(ref monsterRank);
        serializer.SerializeValue(ref dropRate);
        serializer.SerializeValue(ref inGameGoldReward);
        serializer.SerializeValue(ref outGameCurrencyReward);
        serializer.SerializeValue(ref detectionRange);
    }
}
