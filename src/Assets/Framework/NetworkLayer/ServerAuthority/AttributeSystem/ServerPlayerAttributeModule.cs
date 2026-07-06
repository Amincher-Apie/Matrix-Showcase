// NetworkLayer/AttributeSystem/ServerPlayerAttributeModule.cs
using Framework.LogicLayer.DamageCenter;
using Unity.Netcode;
using UnityEngine;

/// <summary>玩家生命状态。</summary>
public enum PlayerLifeState
{
    Alive = 0,       // 正常存活
    Downed = 1,      // 倒地，可被救助或自救
    Dead = 2,        // 真死，不可复活
    Spectating = 3   // 观战
}

/// <summary>
/// 服务器权威玩家属性模块 - 保留PlayerAttributeModule的所有特化功能
/// </summary>
public sealed class ServerPlayerAttributeModule : ServerAttributeModule
{
    #region 玩家特有NetworkVariables
    private NetworkVariable<float> _networkEnergy = new NetworkVariable<float>(100f);
    private NetworkVariable<float> _networkMaxEnergy = new NetworkVariable<float>(100f);
    private NetworkVariable<float> _networkEnergyRegen = new NetworkVariable<float>(0f);
    private NetworkVariable<float> _networkLuck = new NetworkVariable<float>(0f);
    private NetworkVariable<float> _networkCooldownReduction = new NetworkVariable<float>(0f);
    private NetworkVariable<float> _networkArmorPenetrationRate = new NetworkVariable<float>(0f);
    private NetworkVariable<float> _networkSkillStrength = new NetworkVariable<float>(1f);
    private NetworkVariable<float> _networkSkillDuration = new NetworkVariable<float>(1f);
    private NetworkVariable<float> _networkSkillRange = new NetworkVariable<float>(1f);
    private NetworkVariable<float> _networkSkillEfficiency = new NetworkVariable<float>(1f);
    #endregion

    #region 玩家生命状态
    /// <summary>生命状态同步（Alive/Downed/Dead/Spectating）。</summary>
    private readonly NetworkVariable<int> _lifeState = new((int)PlayerLifeState.Alive,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    /// <summary>剩余复活次数（每局 3 次）。</summary>
    private readonly NetworkVariable<int> _reviveTokens = new(3,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public PlayerLifeState CurrentLifeState => (PlayerLifeState)_lifeState.Value;
    public int ReviveTokens => _reviveTokens.Value;
    #endregion

    #region 预制体路径配置
    [SerializeField] private string _prefabPath = "Players/"; // 在Inspector中配置
    #endregion

    #region 护盾自动回复
    [SerializeField, Min(0f)] private float _shieldRegenDelay = 5f;
    [SerializeField, Min(0f)] private float _shieldRegenRate = 10f;
    [SerializeField, Min(0.02f)] private float _shieldRegenTickInterval = 0.2f;

    private float _lastDamageTime = float.NegativeInfinity;
    private float _shieldRegenAccumulator;
    #endregion

    private bool _networkCallbacksRegistered;
    
    #region 网络生命周期
    public override void OnNetworkSpawn()
    {
        InitializeFromConfig();
        base.OnNetworkSpawn();

        if (IsServer)
        {
            _lastDamageTime = Time.time;
            _shieldRegenAccumulator = 0f;
        }
    }

    public override void OnNetworkDespawn()
    {
        Cleanup();
        base.OnNetworkDespawn();
    }
    
    /// <summary>
    /// 从配置初始化
    /// </summary>
    protected override void InitializeFromConfig()
    {
        if (!_config)
        {
            TryResolveConfigFromHeroSO();
        }

        if (!_config)
        {
            RegisterNetworkVariableCallbacks();
            _isInitialized = true;

            if (IsServer)
            {
                Debug.LogError($"[ServerPlayerAttributeModule] 配置未设置，无法初始化");
            }
            else
            {
                Debug.LogWarning($"[ServerPlayerAttributeModule] 客户端未持有属性配置，仅注册 NetworkVariable 回调用于 UI 同步。ObjectId: {NetworkObjectId}");
            }

            return;
        }

        if (_config is not PlayerAttributeConfig)
        {
            Debug.LogError($"[ServerPlayerAttributeModule] 配置类型错误，期望 PlayerAttributeConfig，实际 {_config.GetType()}");
            return;
        }
        
        InitializeAttributes();
        SyncBaseAttributesToNetwork();
        _isInitialized = true;
        // 客户端注册网络变量变化监听
        RegisterNetworkVariableCallbacks();
        Debug.Log($"[ServerPlayerAttributeModule] 初始化完成 - ObjectId: {NetworkObjectId}");
    }

    /// <summary>
    /// 注册网络变量回调
    /// </summary>
    protected override void RegisterNetworkVariableCallbacks()
    {
        if (_networkCallbacksRegistered) return;

        base.RegisterNetworkVariableCallbacks();
        _networkEnergy.OnValueChanged += OnEnergyChanged;
        _networkMaxEnergy.OnValueChanged += OnMaxEnergyChanged;
        _networkEnergyRegen.OnValueChanged += OnEnergyRegenChanged;
        _networkLuck.OnValueChanged += OnLuckChanged;
        _networkCooldownReduction.OnValueChanged += OnCooldownReductionChanged;
        _networkArmorPenetrationRate.OnValueChanged += OnArmorPenetrationRateChanged;
        _networkSkillStrength.OnValueChanged += OnSkillStrengthChanged;
        _networkSkillDuration.OnValueChanged += OnSkillDurationChanged;
        _networkSkillRange.OnValueChanged += OnSkillRangeChanged;
        _networkSkillEfficiency.OnValueChanged += OnSkillEfficiencyChanged;

        _networkCallbacksRegistered = true;
    }
    
    /// <summary>
    /// 清理资源
    /// </summary>
    protected override void Cleanup()
    {
        if (_networkCallbacksRegistered)
        {
            _networkEnergy.OnValueChanged -= OnEnergyChanged;
            _networkMaxEnergy.OnValueChanged -= OnMaxEnergyChanged;
            _networkEnergyRegen.OnValueChanged -= OnEnergyRegenChanged;
            _networkLuck.OnValueChanged -= OnLuckChanged;
            _networkCooldownReduction.OnValueChanged -= OnCooldownReductionChanged;
            _networkArmorPenetrationRate.OnValueChanged -= OnArmorPenetrationRateChanged;
            _networkSkillStrength.OnValueChanged -= OnSkillStrengthChanged;
            _networkSkillDuration.OnValueChanged -= OnSkillDurationChanged;
            _networkSkillRange.OnValueChanged -= OnSkillRangeChanged;
            _networkSkillEfficiency.OnValueChanged -= OnSkillEfficiencyChanged;
            _networkCallbacksRegistered = false;
        }
        
        base.Cleanup();
        _attributes?.Clear();
        _isInitialized = false;
    }
    #endregion
    
    #region 玩家特有网络变量处理
    private void OnEnergyChanged(float oldValue, float newValue)
    {
        if (!IsServer)
        {
            AttributeChangedEventTrigger(AttributeType.Energy, oldValue, newValue);
            // 能量耗尽事件
            if (newValue <= 0)
            {
                EventCenter.Instance.Trigger(EventName.PlayerEnergyExhaust, new PlayerEnergyExhaustEvt() { unitId = NetworkObjectId });
            }
        }
    }
    
    private void OnMaxEnergyChanged(float previousValue, float newValue)
    {
        if (!IsServer) // 客户端处理
        {
            AttributeChangedEventTrigger(AttributeType.MaxEnergy, previousValue, newValue);
        }
    }
    
    private void OnEnergyRegenChanged(float oldValue, float newValue)
    {
        if (!IsServer)
        {
            AttributeChangedEventTrigger(AttributeType.EnergyRegen, oldValue, newValue);
        }
    }
    
    private void OnLuckChanged(float oldValue, float newValue)
    {
        if (!IsServer)
        {
            AttributeChangedEventTrigger(AttributeType.Luck, oldValue, newValue);
        }
    }
    
    private void OnCooldownReductionChanged(float oldValue, float newValue)
    {
        if (!IsServer)
        {
            AttributeChangedEventTrigger(AttributeType.CooldownReduction, oldValue, newValue);
        }
    }
    
    private void OnArmorPenetrationRateChanged(float oldValue, float newValue)
    {
        if (!IsServer)
        {
            AttributeChangedEventTrigger(AttributeType.ArmorPenetrationRate, oldValue, newValue);
        }
    }
    
    private void OnSkillStrengthChanged(float oldValue, float newValue)
    {
        if (!IsServer)
        {
            AttributeChangedEventTrigger(AttributeType.SkillStrength, oldValue, newValue);
        }
    }
    
    private void OnSkillDurationChanged(float oldValue, float newValue)
    {
        if (!IsServer)
        {
            AttributeChangedEventTrigger(AttributeType.SkillDuration, oldValue, newValue);
        }
    }
    
    private void OnSkillRangeChanged(float oldValue, float newValue)
    {
        if (!IsServer)
        {
            AttributeChangedEventTrigger(AttributeType.SkillRange, oldValue, newValue);
        }
    }
    
    private void OnSkillEfficiencyChanged(float oldValue, float newValue)
    {
        if (!IsServer)
        {
            AttributeChangedEventTrigger(AttributeType.SkillEfficiency, oldValue, newValue);
        }
    }
    
    #endregion
    
    #region 初始化
    /// <summary>
    /// 设置配置（需要在网络生成前调用）
    /// </summary>
    public override void SetConfig(AttributeConfig config)
    {
        // 拆分判定并添加对应日志
        if (config == null)
        {
            Debug.LogError($"[ServerPlayerAttributeModule] 尝试设置空配置");
            return;
        }
    
        if (config is not PlayerAttributeConfig)
        {
            Debug.LogError($"[ServerPlayerAttributeModule] 配置类型错误，期望 PlayerAttributeConfig，实际 {config.GetType()}");
            return;
        }
    
        _config = config;
        
        // 如果已经网络生成，立即初始化
        if (IsSpawned)
        {
            InitializeFromConfig();
        }
    }

    private bool TryResolveConfigFromHeroSO()
    {
        var playerActor = GetComponent<PlayerActor>();
        var heroAttributeConfig = playerActor != null ? playerActor.HeroSO?.attributeConfig : null;
        if (heroAttributeConfig == null)
        {
            return false;
        }

        _config = heroAttributeConfig;
        Debug.Log($"[ServerPlayerAttributeModule] 已从 PlayerActor.HeroSO 解析属性配置：{heroAttributeConfig.id}");
        return true;
    }
    
    protected override void SyncBaseAttributesToNetwork()
    {
        if (!IsServer) return;
        base.SyncBaseAttributesToNetwork();
        _networkEnergy.Value = GetBaseAttribute(AttributeType.Energy);
        _networkMaxEnergy.Value = GetBaseAttribute(AttributeType.MaxEnergy);
        _networkEnergyRegen.Value = GetBaseAttribute(AttributeType.EnergyRegen);
        _networkLuck.Value = GetBaseAttribute(AttributeType.Luck);
        _networkCooldownReduction.Value = GetBaseAttribute(AttributeType.CooldownReduction);
        _networkArmorPenetrationRate.Value = GetBaseAttribute(AttributeType.ArmorPenetrationRate);
        _networkSkillStrength.Value = GetBaseAttribute(AttributeType.SkillStrength);
        _networkSkillDuration.Value = GetBaseAttribute(AttributeType.SkillDuration);
        _networkSkillRange.Value = GetBaseAttribute(AttributeType.SkillRange);
        _networkSkillEfficiency.Value = GetBaseAttribute(AttributeType.SkillEfficiency);
    }
    #endregion

    #region 护盾自动回复
    private void Update()
    {
        TickShieldRegen(Time.deltaTime);
    }

    protected override void OnDamageApplied(DamageInfo damageInfo, DamageResult damageResult)
    {
        base.OnDamageApplied(damageInfo, damageResult);

        if (!IsServer || damageResult.totalDamage <= 0f)
        {
            return;
        }

        _lastDamageTime = Time.time;
        _shieldRegenAccumulator = 0f;
    }

    private void TickShieldRegen(float deltaTime)
    {
        if (!IsServer || !_isInitialized || deltaTime <= 0f)
        {
            _shieldRegenAccumulator = 0f;
            return;
        }

        if ((PlayerLifeState)_lifeState.Value != PlayerLifeState.Alive)
        {
            _shieldRegenAccumulator = 0f;
            return;
        }

        if (_shieldRegenRate <= 0f || !_attributes.ContainsKey(AttributeType.Shield))
        {
            _shieldRegenAccumulator = 0f;
            return;
        }

        float currentShield = GetAttribute(AttributeType.Shield);
        float maxShield = GetAttribute(AttributeType.MaxShield);
        if (maxShield <= 0f || currentShield >= maxShield)
        {
            _shieldRegenAccumulator = 0f;
            return;
        }

        if (Time.time - _lastDamageTime < _shieldRegenDelay)
        {
            _shieldRegenAccumulator = 0f;
            return;
        }

        _shieldRegenAccumulator += deltaTime;
        float tickInterval = Mathf.Max(0.02f, _shieldRegenTickInterval);
        if (_shieldRegenAccumulator < tickInterval)
        {
            return;
        }

        float regenAmount = _shieldRegenRate * _shieldRegenAccumulator;
        _shieldRegenAccumulator = 0f;
        RestoreShieldServerInternal(regenAmount);
    }
    #endregion

    #region 玩家特有方法（服务器RPC版本）
    [ServerRpc]
    public void ConsumeEnergyServerRpc(float amount)
    {
        if (!IsServer) return;

        float currentEnergy = GetAttribute(AttributeType.Energy);
        if (currentEnergy < amount) return;

        float newEnergy = currentEnergy - amount;
        ModifyAttributeServerRpc(AttributeType.Energy, newEnergy, AttributeModifyType.Set, NetworkObjectId);
    }

    /// <summary>
    /// 服务器内部能量扣除（不走 RPC，由 ServerSkillModule 直接调用）。
    /// 仅在 IsServer 时执行；返回 false = 能量不足。
    /// </summary>
    public bool TryConsumeEnergyServerInternal(float amount)
    {
        if (!IsServer)
        {
            Debug.LogError("[ServerPlayerAttributeModule] TryConsumeEnergyServerInternal 只能在服务端调用！");
            return false;
        }

        float currentEnergy = GetAttribute(AttributeType.Energy);
        if (currentEnergy < amount) return false;

        float newEnergy = currentEnergy - amount;
        // 直接调基类的内部方法，绕过 RPC 层和 SenderClientId 校验
        ModifyAttributeServerInternal(AttributeType.Energy, newEnergy, AttributeModifyType.Set, NetworkObjectId);
        return true;
    }

    [ServerRpc]
    public void RestoreEnergyServerRpc(float amount)
    {
        if (!IsServer) return;

        float currentEnergy = GetAttribute(AttributeType.Energy);
        float maxEnergy = GetAttribute(AttributeType.MaxEnergy);
        float newEnergy = Mathf.Min(currentEnergy + amount, maxEnergy);
        
        ModifyAttributeServerRpc(AttributeType.Energy, newEnergy, AttributeModifyType.Set, NetworkObjectId);
    }

    /// <summary>
    /// 获取冷却缩减后的时间（服务器计算）
    /// </summary>
    public float GetReducedCooldown(float baseCooldown)
    {
        float cooldownReduction = GetAttribute(AttributeType.CooldownReduction) / 100f;
        return baseCooldown * (1 - Mathf.Clamp(cooldownReduction, 0f, 0.8f));
    }

    /// <summary>
    /// 获取幸运加成的掉落率（服务器计算）
    /// </summary>
    public float GetLuckyDropRate(float baseDropRate)
    {
        float luck = GetAttribute(AttributeType.Luck);
        float luckBonus = luck * 0.01f;
        return baseDropRate * (1 + luckBonus);
    }
    #endregion

    #region 重写属性修改方法（保留玩家特有验证）
    protected override string GetPrefabPath()
    {
        return _prefabPath;
    }

    /// <summary>
    /// 覆写死亡处理：玩家血量归零后直接进入 Dead，并交给 RunManager 判定失败。
    /// </summary>
    protected override void HandleDeath()
    {
        if (!IsServer) return;

        var currentState = (PlayerLifeState)_lifeState.Value;
        if (currentState == PlayerLifeState.Dead || currentState == PlayerLifeState.Spectating)
            return; // 已是终态

        var ownerParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { OwnerClientId } }
        };

        _lifeState.Value = (int)PlayerLifeState.Dead;
        PlayDieAnimClientRpc(dead: true, ownerParams);
        EventCenter.Instance.Trigger(EventName.UnitDied, new UnitDiedEvt { unitId = NetworkObjectId });
    }

    protected override void OnHitAnimation()
    {
        var ownerParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { OwnerClientId } }
        };
        PlayHitAnimClientRpc(ownerParams);
    }

    [ClientRpc]
    private void PlayHitAnimClientRpc(ClientRpcParams rpcParams = default)
    {
        if (IsOwner)
        {
            var anim = GetComponent<Animator>();
            if (anim != null)
                anim.SetTrigger("Hit");
        }
    }

    [ClientRpc]
    private void PlayDieAnimClientRpc(bool dead, ClientRpcParams rpcParams = default)
    {
        if (IsOwner)
        {
            var anim = GetComponent<Animator>();
            if (anim == null) return;

            if (dead)
            {
                anim.SetBool("IsDead", true);
                anim.SetTrigger("Die");
            }
            else
            {
                anim.SetTrigger("Die");
            }
        }
    }

    /// <summary>
    /// 服务端：消耗复活次数原地复活。
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void SelfReviveServerRpc()
    {
        if (!IsServer) return;
        if (_reviveTokens.Value <= 0) return;
        if ((PlayerLifeState)_lifeState.Value != PlayerLifeState.Downed) return;

        _reviveTokens.Value--;
        _lifeState.Value = (int)PlayerLifeState.Alive;
        HealServerRpc(GetAttribute(AttributeType.MaxHealth));           // 回复满 HP
        RestoreShieldServerRpc(GetAttribute(AttributeType.MaxShield));  // 回复满护盾
    }

    /// <summary>
    /// 服务端：队友救助成功。
    /// </summary>
    public void RescueByTeammate()
    {
        if (!IsServer) return;
        if ((PlayerLifeState)_lifeState.Value != PlayerLifeState.Downed) return;

        _lifeState.Value = (int)PlayerLifeState.Alive;
        HealServerRpc(GetAttribute(AttributeType.MaxHealth));           // 回复满 HP
        RestoreShieldServerRpc(GetAttribute(AttributeType.MaxShield));  // 回复满护盾
    }

    protected override void UpdateNetworkVariable(AttributeType type, float newValue)
    {
        base.UpdateNetworkVariable(type, newValue);
        
        if (!IsServer) return;

        switch (type)
        {
            case AttributeType.Energy:
                _networkEnergy.Value = newValue;
                break;
            case AttributeType.MaxEnergy:
                _networkMaxEnergy.Value = newValue;
                break;
            case AttributeType.EnergyRegen:
                _networkEnergyRegen.Value = newValue;
                break;
            case AttributeType.Luck:
                _networkLuck.Value = newValue;
                break;
            case AttributeType.CooldownReduction:
                _networkCooldownReduction.Value = newValue;
                break;
            case AttributeType.ArmorPenetrationRate:
                _networkArmorPenetrationRate.Value = newValue;
                break;  
            case AttributeType.SkillStrength:
                _networkSkillStrength.Value = newValue;
                break;
            case AttributeType.SkillDuration:
                _networkSkillDuration.Value = newValue;
                break;
            case AttributeType.SkillRange:
                _networkSkillRange.Value = newValue;
                break;
            case AttributeType.SkillEfficiency:
                _networkSkillEfficiency.Value = newValue;
                break;
        }
    }
    
    protected override (float min, float max) GetAttributeRange(AttributeType type)
    {
        // 先检查玩家特有属性的范围
        var playerRange = type switch
        {
            AttributeType.Energy => (0f, GetAttribute(AttributeType.MaxEnergy)),
            AttributeType.CooldownReduction => (0f, 0.8f),
            AttributeType.Luck => (0f, 100f),
            AttributeType.ArmorPenetrationRate => (0f, 1f),
            _ => (float.MinValue, float.MaxValue)
        };
    
        // 如果玩家范围有定义，使用玩家范围，否则使用基类范围
        if (!Mathf.Approximately(playerRange.Item1, float.MinValue) || !Mathf.Approximately(playerRange.Item2, float.MaxValue))
            return playerRange;
    
        return base.GetAttributeRange(type);
    }
    #endregion

    #region 属性访问重写
    public override float GetAttribute(AttributeType type)
    {
        float playerValue = GetPlayerSpecificAttribute(type);
        if (!Mathf.Approximately(playerValue, float.MinValue))
            return playerValue;
        
        return base.GetAttribute(type);
    }

    private float GetPlayerSpecificAttribute(AttributeType type)
    {
        return type switch
        {
            AttributeType.Energy => _networkEnergy.Value,
            AttributeType.MaxEnergy => _networkMaxEnergy.Value,
            AttributeType.EnergyRegen => _networkEnergyRegen.Value,
            AttributeType.Luck => _networkLuck.Value,
            AttributeType.CooldownReduction => _networkCooldownReduction.Value,
            AttributeType.ArmorPenetrationRate => _networkArmorPenetrationRate.Value,
            AttributeType.SkillStrength => _networkSkillStrength.Value,
            AttributeType.SkillDuration => _networkSkillDuration.Value,
            AttributeType.SkillRange => _networkSkillRange.Value,
            AttributeType.SkillEfficiency => _networkSkillEfficiency.Value,
            _ => float.MinValue
        };
    }
    #endregion

}
