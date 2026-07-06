using Unity.Netcode;
using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// 服务器权威的武器运行时组件
/// </summary>
public class ServerWeaponRuntime : NetworkBehaviour
{
    #region NetworkVariables (武器属性同步)
    // 基础属性
    private readonly NetworkVariable<float> _networkSolidDamage = new NetworkVariable<float>();
    private readonly NetworkVariable<float> _networkLiquidDamage = new NetworkVariable<float>();
    private readonly NetworkVariable<float> _networkGasDamage = new NetworkVariable<float>();
    private readonly NetworkVariable<float> _networkIceDamage = new NetworkVariable<float>();
    private readonly NetworkVariable<float> _networkFireDamage = new NetworkVariable<float>();
    private readonly NetworkVariable<float> _networkToxicDamage = new NetworkVariable<float>();
    private readonly NetworkVariable<float> _networkElectricDamage = new NetworkVariable<float>();
    
    // 武器属性
    private readonly NetworkVariable<float> _networkCritChance = new NetworkVariable<float>();
    private readonly NetworkVariable<float> _networkCritMultiplier = new NetworkVariable<float>();
    private readonly NetworkVariable<float> _networkProcChance = new NetworkVariable<float>();
    private readonly NetworkVariable<float> _networkFireRate = new NetworkVariable<float>();
    private readonly NetworkVariable<float> _networkMagazineSize = new NetworkVariable<float>();
    private readonly NetworkVariable<float> _networkReloadTime = new NetworkVariable<float>();
    private readonly NetworkVariable<float> _networkChargeTime = new NetworkVariable<float>();
    private readonly NetworkVariable<float> _networkBulletSpeed = new NetworkVariable<float>();
    private readonly NetworkVariable<float> _networkSpread = new NetworkVariable<float>();
    private readonly NetworkVariable<float> _networkRangeMin = new NetworkVariable<float>();
    private readonly NetworkVariable<float> _networkRangeMax = new NetworkVariable<float>();
    
    // 武器状态
    private readonly NetworkVariable<int> _networkAmmoInMag = new NetworkVariable<int>();
    #endregion

    #region 服务器端私有字段
    private WeaponSO _weaponSO; 
    private Dictionary<WeaponAttributeType, WeaponAttributeData> _attributes = new Dictionary<WeaponAttributeType, WeaponAttributeData>();

    #endregion

    #region 公共属性
    public WeaponSO WeaponSO => _weaponSO;
    public int AmmoInMag => _networkAmmoInMag.Value;

    
    /// <summary>
    /// 面板总伤害（计算所有元素伤害之和）
    /// </summary>
    public float TotalDamage
    {
        get
        {
            float total = 0f;
            total += GetAttribute(WeaponAttributeType.SolidDamage);
            total += GetAttribute(WeaponAttributeType.LiquidDamage);
            total += GetAttribute(WeaponAttributeType.GasDamage);
            total += GetAttribute(WeaponAttributeType.IceDamage);
            total += GetAttribute(WeaponAttributeType.FireDamage);
            total += GetAttribute(WeaponAttributeType.ToxicDamage);
            total += GetAttribute(WeaponAttributeType.ElectricDamage);
            return total;
        }
    }
    #endregion

    #region 网络生命周期管理
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        if (IsServer)
        {
            Debug.Log($"[ServerWeaponRuntime] 服务器端武器运行时初始化 - ObjectId: {NetworkObjectId}");
        }
        else
        {
            Debug.Log($"[ServerWeaponRuntime] 客户端武器运行时初始化 - ObjectId: {NetworkObjectId}");
        }
        
        // 注册网络变量变化回调
        RegisterNetworkVariableCallbacks();
    }

    public override void OnNetworkDespawn()
    {
        // 清理资源
        Cleanup();
        base.OnNetworkDespawn();
    }

    /// <summary>
    /// 注册网络变量变化回调
    /// </summary>
    private void RegisterNetworkVariableCallbacks()
    {
        // 基础伤害属性
        _networkSolidDamage.OnValueChanged += OnSolidDamageChanged;
        _networkLiquidDamage.OnValueChanged += OnLiquidDamageChanged;
        _networkGasDamage.OnValueChanged += OnGasDamageChanged;
        _networkIceDamage.OnValueChanged += OnIceDamageChanged;
        _networkFireDamage.OnValueChanged += OnFireDamageChanged;
        _networkToxicDamage.OnValueChanged += OnToxicDamageChanged;
        _networkElectricDamage.OnValueChanged += OnElectricDamageChanged;
        
        // 武器属性
        _networkCritChance.OnValueChanged += OnCritChanceChanged;
        _networkCritMultiplier.OnValueChanged += OnCritMultiplierChanged;
        _networkProcChance.OnValueChanged += OnProcChanceChanged;
        _networkFireRate.OnValueChanged += OnFireRateChanged;
        _networkMagazineSize.OnValueChanged += OnMagazineSizeChanged;
        _networkReloadTime.OnValueChanged += OnReloadTimeChanged;
        _networkChargeTime.OnValueChanged += OnChargeTimeChanged;
        _networkBulletSpeed.OnValueChanged += OnBulletSpeedChanged;
        _networkSpread.OnValueChanged += OnSpreadChanged;
        _networkRangeMin.OnValueChanged += OnRangeMinChanged;
        _networkRangeMax.OnValueChanged += OnRangeMaxChanged;
        
        // 武器状态
        _networkAmmoInMag.OnValueChanged += OnAmmoInMagChanged;
    }

    /// <summary>
    /// 清理资源
    /// </summary>
    private void Cleanup()
    {
        // 移除网络变量回调
        _networkSolidDamage.OnValueChanged -= OnSolidDamageChanged;
        _networkLiquidDamage.OnValueChanged -= OnLiquidDamageChanged;
        _networkGasDamage.OnValueChanged -= OnGasDamageChanged;
        _networkIceDamage.OnValueChanged -= OnIceDamageChanged;
        _networkFireDamage.OnValueChanged -= OnFireDamageChanged;
        _networkToxicDamage.OnValueChanged -= OnToxicDamageChanged;
        _networkElectricDamage.OnValueChanged -= OnElectricDamageChanged;
        
        _networkCritChance.OnValueChanged -= OnCritChanceChanged;
        _networkCritMultiplier.OnValueChanged -= OnCritMultiplierChanged;
        _networkProcChance.OnValueChanged -= OnProcChanceChanged;
        _networkFireRate.OnValueChanged -= OnFireRateChanged;
        _networkMagazineSize.OnValueChanged -= OnMagazineSizeChanged;
        _networkReloadTime.OnValueChanged -= OnReloadTimeChanged;
        _networkChargeTime.OnValueChanged -= OnChargeTimeChanged;
        _networkBulletSpeed.OnValueChanged -= OnBulletSpeedChanged;
        _networkSpread.OnValueChanged -= OnSpreadChanged;
        _networkRangeMin.OnValueChanged -= OnRangeMinChanged;
        _networkRangeMax.OnValueChanged -= OnRangeMaxChanged;
        
        _networkAmmoInMag.OnValueChanged -= OnAmmoInMagChanged;

        _attributes?.Clear();
    }
    #endregion

    #region 网络变量变化处理
    private void OnSolidDamageChanged(float oldValue, float newValue) => TriggerWeaponAttributeModified(WeaponAttributeType.SolidDamage, newValue);
    private void OnLiquidDamageChanged(float oldValue, float newValue) => TriggerWeaponAttributeModified(WeaponAttributeType.LiquidDamage, newValue);
    private void OnGasDamageChanged(float oldValue, float newValue) => TriggerWeaponAttributeModified(WeaponAttributeType.GasDamage, newValue);
    private void OnIceDamageChanged(float oldValue, float newValue) => TriggerWeaponAttributeModified(WeaponAttributeType.IceDamage, newValue);
    private void OnFireDamageChanged(float oldValue, float newValue) => TriggerWeaponAttributeModified(WeaponAttributeType.FireDamage, newValue);
    private void OnToxicDamageChanged(float oldValue, float newValue) => TriggerWeaponAttributeModified(WeaponAttributeType.ToxicDamage, newValue);
    private void OnElectricDamageChanged(float oldValue, float newValue) => TriggerWeaponAttributeModified(WeaponAttributeType.ElectricDamage, newValue);
    
    private void OnCritChanceChanged(float oldValue, float newValue) => TriggerWeaponAttributeModified(WeaponAttributeType.CritChance, newValue);
    private void OnCritMultiplierChanged(float oldValue, float newValue) => TriggerWeaponAttributeModified(WeaponAttributeType.CritMultiplier, newValue);
    private void OnProcChanceChanged(float oldValue, float newValue) => TriggerWeaponAttributeModified(WeaponAttributeType.ProcChance, newValue);
    private void OnFireRateChanged(float oldValue, float newValue) => TriggerWeaponAttributeModified(WeaponAttributeType.FireRate, newValue);
    private void OnMagazineSizeChanged(float oldValue, float newValue) => TriggerWeaponAttributeModified(WeaponAttributeType.MagazineSize, newValue);
    private void OnReloadTimeChanged(float oldValue, float newValue) => TriggerWeaponAttributeModified(WeaponAttributeType.ReloadTime, newValue);
    private void OnChargeTimeChanged(float oldValue, float newValue) => TriggerWeaponAttributeModified(WeaponAttributeType.ChargeTime, newValue);
    private void OnBulletSpeedChanged(float oldValue, float newValue) => TriggerWeaponAttributeModified(WeaponAttributeType.BulletSpeed, newValue);
    private void OnSpreadChanged(float oldValue, float newValue) => TriggerWeaponAttributeModified(WeaponAttributeType.Spread, newValue);
    private void OnRangeMinChanged(float oldValue, float newValue) => TriggerWeaponAttributeModified(WeaponAttributeType.RangeMin, newValue);
    private void OnRangeMaxChanged(float oldValue, float newValue) => TriggerWeaponAttributeModified(WeaponAttributeType.RangeMax, newValue);
    
    private void OnAmmoInMagChanged(int oldValue, int newValue) { /* 弹药变化处理 */ }
    private void OnNextFireTimeChanged(float oldValue, float newValue) { /* 下次射击时间变化处理 */ }
    private void OnChargeStartTimeChanged(float oldValue, float newValue) { /* 蓄力开始时间变化处理 */ }

    /// <summary>
    /// 触发武器属性修改事件
    /// </summary>
    private void TriggerWeaponAttributeModified(WeaponAttributeType attributeType, float value)
    {
        EventCenter.Instance.Trigger(EventName.WeaponAttributeModified, new WeaponAttributeModifiedEvt
        {
            ownerId = NetworkObjectId,
            value = value,
            source = attributeType.ToString()
        });
    }
    #endregion

    #region 服务器初始化方法
    /// <summary>
    /// 服务器初始化武器
    /// </summary>
    private void InitializeWeapon()
    {
        if (!IsServer) return;
        
        if (_weaponSO == null)
        {
            Debug.LogError($"[ServerWeaponRuntime] 武器SO未设置，无法初始化");
            return;
        }

        InitializeAttributes();
        SyncBaseAttributesToNetwork();
        
        // 初始化状态
        _networkAmmoInMag.Value = (int)_weaponSO.magazineSize;
        
        
        Debug.Log($"[ServerWeaponRuntime] 武器初始化完成 - SO: {_weaponSO.id}");
    }

    /// <summary>
    /// 设置武器SO（服务器端调用）
    /// </summary>
    public void SetWeaponSO(WeaponSO weaponSO)
    {
        if (!IsServer)
        {
            Debug.LogError($"[ServerWeaponRuntime] 只能在服务器端设置武器SO");
            return;
        }

        _weaponSO = weaponSO;

        // ✅ 无论是否已 Spawn，直接初始化：
        InitializeWeapon();
    }
    
    public void ClientSetWeaponSO(WeaponSO weaponSO)
    {
        if (IsServer)
        {
            // 服务器已经用 SetWeaponSO 配过了，不走这个接口
            return;
        }

        _weaponSO = weaponSO;
    }

    /// <summary>
    /// 初始化所有属性
    /// </summary>
    private void InitializeAttributes()
    {
        _attributes.Clear();

        // 从WeaponSO初始化基础值
        _attributes[WeaponAttributeType.SolidDamage] = new WeaponAttributeData(_weaponSO.solidDamage);
        _attributes[WeaponAttributeType.LiquidDamage] = new WeaponAttributeData(_weaponSO.liquidDamage);
        _attributes[WeaponAttributeType.GasDamage] = new WeaponAttributeData(_weaponSO.gasDamage);
        _attributes[WeaponAttributeType.IceDamage] = new WeaponAttributeData(_weaponSO.iceDamage);
        _attributes[WeaponAttributeType.FireDamage] = new WeaponAttributeData(_weaponSO.fireDamage);
        _attributes[WeaponAttributeType.ToxicDamage] = new WeaponAttributeData(_weaponSO.poisonDamage);
        _attributes[WeaponAttributeType.ElectricDamage] = new WeaponAttributeData(_weaponSO.lightningDamage);
        
        _attributes[WeaponAttributeType.CritChance] = new WeaponAttributeData(_weaponSO.criticalRate);
        _attributes[WeaponAttributeType.CritMultiplier] = new WeaponAttributeData(_weaponSO.criticalDamage);
        _attributes[WeaponAttributeType.ProcChance] = new WeaponAttributeData(_weaponSO.triggerRate);
        _attributes[WeaponAttributeType.FireRate] = new WeaponAttributeData(_weaponSO.fireRate);
        _attributes[WeaponAttributeType.MagazineSize] = new WeaponAttributeData(_weaponSO.magazineSize);
        _attributes[WeaponAttributeType.ReloadTime] = new WeaponAttributeData(_weaponSO.reloadTime);
        _attributes[WeaponAttributeType.ChargeTime] = new WeaponAttributeData(_weaponSO.chargeTime);
        _attributes[WeaponAttributeType.BulletSpeed] = new WeaponAttributeData(_weaponSO.bulletSpeed);
        _attributes[WeaponAttributeType.Spread] = new WeaponAttributeData(0f); // 需要从WeaponSO获取
        _attributes[WeaponAttributeType.RangeMin] = new WeaponAttributeData(0f); // 需要从WeaponSO获取
        _attributes[WeaponAttributeType.RangeMax] = new WeaponAttributeData(_weaponSO.effectiveRange);
    }

    /// <summary>
    /// 同步基础属性到NetworkVariable
    /// </summary>
    private void SyncBaseAttributesToNetwork()
    {
        if (!IsServer) return;

        // 基础伤害属性
        _networkSolidDamage.Value = GetBaseAttribute(WeaponAttributeType.SolidDamage);
        _networkLiquidDamage.Value = GetBaseAttribute(WeaponAttributeType.LiquidDamage);
        _networkGasDamage.Value = GetBaseAttribute(WeaponAttributeType.GasDamage);
        _networkIceDamage.Value = GetBaseAttribute(WeaponAttributeType.IceDamage);
        _networkFireDamage.Value = GetBaseAttribute(WeaponAttributeType.FireDamage);
        _networkToxicDamage.Value = GetBaseAttribute(WeaponAttributeType.ToxicDamage);
        _networkElectricDamage.Value = GetBaseAttribute(WeaponAttributeType.ElectricDamage);
        
        // 武器属性
        _networkCritChance.Value = GetBaseAttribute(WeaponAttributeType.CritChance);
        _networkCritMultiplier.Value = GetBaseAttribute(WeaponAttributeType.CritMultiplier);
        _networkProcChance.Value = GetBaseAttribute(WeaponAttributeType.ProcChance);
        _networkFireRate.Value = GetBaseAttribute(WeaponAttributeType.FireRate);
        _networkMagazineSize.Value = GetBaseAttribute(WeaponAttributeType.MagazineSize);
        _networkReloadTime.Value = GetBaseAttribute(WeaponAttributeType.ReloadTime);
        _networkChargeTime.Value = GetBaseAttribute(WeaponAttributeType.ChargeTime);
        _networkBulletSpeed.Value = GetBaseAttribute(WeaponAttributeType.BulletSpeed);
        _networkSpread.Value = GetBaseAttribute(WeaponAttributeType.Spread);
        _networkRangeMin.Value = GetBaseAttribute(WeaponAttributeType.RangeMin);
        _networkRangeMax.Value = GetBaseAttribute(WeaponAttributeType.RangeMax);
    }
    #endregion

    #region 属性访问方法
    /// <summary>
    /// 获取属性值（带修改器计算）
    /// </summary>
    public float GetAttribute(WeaponAttributeType type)
    {
        if (IsServer)
        {
            // 服务器端：使用完整的修改器计算
            if (_attributes.TryGetValue(type, out var data))
            {
                if (data.IsCacheDirty)
                {
                    float finalValue = CalculateFinalValue(data);
                    data.UpdateCache(finalValue);
                }
                return data.CachedValue;
            }
            return 0f;
        }
        else
        {
            // 客户端：直接从NetworkVariable读取
            return GetAttributeFromNetwork(type);
        }
    }

    /// <summary>
    /// 从NetworkVariable获取属性值
    /// </summary>
    private float GetAttributeFromNetwork(WeaponAttributeType type)
    {
        return type switch
        {
            WeaponAttributeType.SolidDamage => _networkSolidDamage.Value,
            WeaponAttributeType.LiquidDamage => _networkLiquidDamage.Value,
            WeaponAttributeType.GasDamage => _networkGasDamage.Value,
            WeaponAttributeType.IceDamage => _networkIceDamage.Value,
            WeaponAttributeType.FireDamage => _networkFireDamage.Value,
            WeaponAttributeType.ToxicDamage => _networkToxicDamage.Value,
            WeaponAttributeType.ElectricDamage => _networkElectricDamage.Value,
            WeaponAttributeType.CritChance => _networkCritChance.Value,
            WeaponAttributeType.CritMultiplier => _networkCritMultiplier.Value,
            WeaponAttributeType.ProcChance => _networkProcChance.Value,
            WeaponAttributeType.FireRate => _networkFireRate.Value,
            WeaponAttributeType.MagazineSize => _networkMagazineSize.Value,
            WeaponAttributeType.ReloadTime => _networkReloadTime.Value,
            WeaponAttributeType.ChargeTime => _networkChargeTime.Value,
            WeaponAttributeType.BulletSpeed => _networkBulletSpeed.Value,
            WeaponAttributeType.Spread => _networkSpread.Value,
            WeaponAttributeType.RangeMin => _networkRangeMin.Value,
            WeaponAttributeType.RangeMax => _networkRangeMax.Value,
            _ => 0f
        };
    }

    /// <summary>
    /// 获取基础属性值（无修改器影响）
    /// </summary>
    public float GetBaseAttribute(WeaponAttributeType type)
    {
        return _attributes.TryGetValue(type, out var data) ? data.BaseValue : 0f;
    }
    
    public PhysicalBulletType GetPhysicalBulletType()
    {
        return WeaponSO.activeDamageType;
    }
    #endregion

    #region 服务器权威的修改器管理
    /// <summary>
    /// 添加修改器（服务器RPC）
    /// </summary>
    [ServerRpc]
    public void AddModifierServerRpc(WeaponModifyType modifyType, WeaponAttributeType attributeType, 
        float value, WeaponModifyOperator op, ulong sourceId, ElementType elementType = ElementType.Fire)
    {
        if (!IsServer) return;

        if (!_attributes.TryGetValue(attributeType, out var data))
            return;

        var modifier = new WeaponModifier(modifyType, attributeType, value, op, sourceId, elementType);
        data.Modifiers.Add(modifier);
        data.MarkCacheDirty();

        // 重新计算并同步属性
        float newValue = GetAttribute(attributeType);
        UpdateNetworkVariable(attributeType, newValue);
        
        // 特殊处理：暴击率上限和转换
        HandleCritSpecialRules();

        Debug.Log($"[ServerWeaponRuntime] 添加修改器 - Type: {attributeType}, Value: {value}, Operator: {op}");
    }

    /// <summary>
    /// 移除特定来源的修改器（服务器RPC）
    /// </summary>
    [ServerRpc]
    public void RemoveModifiersFromSourceServerRpc(WeaponAttributeType type, ulong sourceId)
    {
        if (!IsServer) return;

        if (_attributes.TryGetValue(type, out var data))
        {
            data.Modifiers.RemoveAll(m => m.Source is ulong id && id == sourceId);
            data.MarkCacheDirty();

            // 重新计算并同步属性
            float newValue = GetAttribute(type);
            UpdateNetworkVariable(type, newValue);
            
            // 特殊处理：暴击率上限和转换
            HandleCritSpecialRules();

            Debug.Log($"[ServerWeaponRuntime] 移除修改器 - Type: {type}, Source: {sourceId}");
        }
    }

    /// <summary>
    /// 强制刷新所有缓存（服务器RPC）
    /// </summary>
    [ServerRpc]
    public void RefreshAllCachesServerRpc()
    {
        if (!IsServer) return;

        foreach (var kvp in _attributes)
        {
            kvp.Value.MarkCacheDirty();
            float newValue = GetAttribute(kvp.Key);
            UpdateNetworkVariable(kvp.Key, newValue);
        }
        
        HandleCritSpecialRules();
    }
    #endregion

    #region 服务器权威的状态管理

    /// <summary>
    /// 设置蓄力开始时间（服务器RPC）
    /// </summary>
    [ServerRpc]
    public void SetChargeStartTimeServerRpc(float chargeStartTime)
    {
        if (!IsServer) return;
    }

    /// <summary>
    /// 消耗弹药（服务器RPC）
    /// </summary>
    [ServerRpc]
    public void ConsumeAmmoServerRpc(int amount = 1)
    {
        if (!IsServer) return;
        _networkAmmoInMag.Value = Mathf.Max(0, _networkAmmoInMag.Value - amount);
    }

    /// <summary>
    /// 重新装填（服务器RPC）
    /// </summary>
    [ServerRpc]
    public void ReloadServerRpc()
    {
        if (!IsServer) return;
        _networkAmmoInMag.Value = (int)GetAttribute(WeaponAttributeType.MagazineSize);
    }
    #endregion

    #region 核心计算方法（保留原有逻辑）
    /// <summary>
    /// 计算属性的最终值
    /// </summary>
    private float CalculateFinalValue(WeaponAttributeData data)
    {
        // 如果有Set修改器，直接返回最后一个Set的值
        var setModifiers = data.Modifiers.FindAll(m => m.Operator == WeaponModifyOperator.Set);
        if (setModifiers.Count > 0)
        {
            return setModifiers[setModifiers.Count - 1].Value;
        }

        float addValue = 0f;
        float multiplyValue = 1f;
        float percentValue = 0f; // 百分比是累加

        // 分别计算不同类型的修改器
        foreach (var modifier in data.Modifiers)
        {
            switch (modifier.Operator)
            {
                case WeaponModifyOperator.Add:
                    addValue += modifier.Value * modifier.StackCount;
                    break;
                case WeaponModifyOperator.Multiply:
                    multiplyValue *= Mathf.Pow(modifier.Value, modifier.StackCount);
                    break;
                case WeaponModifyOperator.Percent:
                    percentValue += modifier.Value * modifier.StackCount;
                    break;
            }
        }

        // 最终计算：(基础值 + 加法) × (1 + 百分比) × 乘法
        float finalValue = (data.BaseValue + addValue) * (1 + percentValue / 100f) * multiplyValue;
        
        return finalValue;
    }

    /// <summary>
    /// 处理暴击特殊规则
    /// </summary>
    private void HandleCritSpecialRules()
    {
        float critChance = GetAttribute(WeaponAttributeType.CritChance);
        
        // 暴击率上限为100%，超出部分转换为暴击伤害
        if (critChance > 100f)
        {
            float overflowCrit = critChance - 100f;
            float currentCritMultiplier = GetBaseAttribute(WeaponAttributeType.CritMultiplier);
            
            // 溢出部分转换为暴击伤害：每1%溢出增加0.01倍暴击伤害
            float bonusMultiplier = overflowCrit * 0.01f * currentCritMultiplier;
            
            // 添加一个临时的暴击伤害修改器（使用特殊source标记）
            var critMultiplierModifier = new WeaponModifier(
                WeaponModifyType.CritMultiplier,
                WeaponAttributeType.CritMultiplier,
                bonusMultiplier,
                WeaponModifyOperator.Add,
                ulong.MaxValue // 使用特殊ID标记溢出转换
            );
            
            // 确保只添加一次
            RemoveModifiersFromSourceServerRpc(WeaponAttributeType.CritMultiplier, ulong.MaxValue);
            AddModifierServerRpc(WeaponModifyType.CritMultiplier, WeaponAttributeType.CritMultiplier, 
                bonusMultiplier, WeaponModifyOperator.Add, ulong.MaxValue);
            
            // 限制暴击率为100%
            var critChanceModifier = new WeaponModifier(
                WeaponModifyType.CritChance,
                WeaponAttributeType.CritChance,
                100f,
                WeaponModifyOperator.Set,
                ulong.MaxValue - 1 // 使用另一个特殊ID标记上限
            );
            
            RemoveModifiersFromSourceServerRpc(WeaponAttributeType.CritChance, ulong.MaxValue - 1);
            AddModifierServerRpc(WeaponModifyType.CritChance, WeaponAttributeType.CritChance, 
                100f, WeaponModifyOperator.Set, ulong.MaxValue - 1);
        }
    }

    /// <summary>
    /// 更新NetworkVariable
    /// </summary>
    private void UpdateNetworkVariable(WeaponAttributeType type, float newValue)
    {
        if (!IsServer) return;

        switch (type)
        {
            case WeaponAttributeType.SolidDamage:
                _networkSolidDamage.Value = newValue;
                break;
            case WeaponAttributeType.LiquidDamage:
                _networkLiquidDamage.Value = newValue;
                break;
            case WeaponAttributeType.GasDamage:
                _networkGasDamage.Value = newValue;
                break;
            case WeaponAttributeType.IceDamage:
                _networkIceDamage.Value = newValue;
                break;
            case WeaponAttributeType.FireDamage:
                _networkFireDamage.Value = newValue;
                break;
            case WeaponAttributeType.ToxicDamage:
                _networkToxicDamage.Value = newValue;
                break;
            case WeaponAttributeType.ElectricDamage:
                _networkElectricDamage.Value = newValue;
                break;
            case WeaponAttributeType.CritChance:
                _networkCritChance.Value = newValue;
                break;
            case WeaponAttributeType.CritMultiplier:
                _networkCritMultiplier.Value = newValue;
                break;
            case WeaponAttributeType.ProcChance:
                _networkProcChance.Value = newValue;
                break;
            case WeaponAttributeType.FireRate:
                _networkFireRate.Value = newValue;
                break;
            case WeaponAttributeType.MagazineSize:
                _networkMagazineSize.Value = newValue;
                break;
            case WeaponAttributeType.ReloadTime:
                _networkReloadTime.Value = newValue;
                break;
            case WeaponAttributeType.ChargeTime:
                _networkChargeTime.Value = newValue;
                break;
            case WeaponAttributeType.BulletSpeed:
                _networkBulletSpeed.Value = newValue;
                break;
            case WeaponAttributeType.Spread:
                _networkSpread.Value = newValue;
                break;
            case WeaponAttributeType.RangeMin:
                _networkRangeMin.Value = newValue;
                break;
            case WeaponAttributeType.RangeMax:
                _networkRangeMax.Value = newValue;
                break;
        }
    }
    #endregion

    #region 工具方法
    /// <summary>
    /// 获取伤害面板
    /// </summary>
    public DamageProfile GetModifiedDamageProfile()
    {
        return new DamageProfile
        {
            solid = (int)GetAttribute(WeaponAttributeType.SolidDamage),
            liquid = (int)GetAttribute(WeaponAttributeType.LiquidDamage),
            gas = (int)GetAttribute(WeaponAttributeType.GasDamage),
            ice = (int)GetAttribute(WeaponAttributeType.IceDamage),
            fire = (int)GetAttribute(WeaponAttributeType.FireDamage),
            toxic = (int)GetAttribute(WeaponAttributeType.ToxicDamage),
            electric = (int)GetAttribute(WeaponAttributeType.ElectricDamage)
        };
    }
    

    /// <summary>
    /// 检查是否可以重新装填
    /// </summary>
    public bool CanReload()
    {
        return AmmoInMag < GetAttribute(WeaponAttributeType.MagazineSize);
    }
    #endregion


}