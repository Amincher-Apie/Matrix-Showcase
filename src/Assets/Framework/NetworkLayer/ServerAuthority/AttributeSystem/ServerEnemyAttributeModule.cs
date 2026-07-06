// NetworkLayer/AttributeSystem/ServerEnemyAttributeModule.cs

using Framework.LogicLayer.Module.AIModule.Movement;
using Framework.NetworkLayer.NetworkObjectPool;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 服务器权威敌人属性模块 - 保留EnemyAttributeModule的所有特化功能
/// </summary>
public class ServerEnemyAttributeModule : ServerAttributeModule
{
    #region 敌人特有NetworkVariables
    private NetworkVariable<float> _networkAggroRange = new NetworkVariable<float>(10f);
    private NetworkVariable<int> _networkMonsterRank = new NetworkVariable<int>(0);
    private NetworkVariable<float> _networkDropRate = new NetworkVariable<float>(0.1f);
    private NetworkVariable<float> _networkInGameGoldReward = new NetworkVariable<float>(10f);
    private NetworkVariable<float> _networkDetectionRange = new NetworkVariable<float>(15f);
    #endregion
    
    #region 预制体路径配置
    [Header("Pool Recycle")]
    [SerializeField] private string _prefabPath; // "Prefab/Enemy/001"

    [Header("Death")]
    [SerializeField] private float _deathAnimDuration = 1.2f; // 按你动画实际时长调
    private bool _deathHandled;
    private const float FallbackDeathReturnDelay = 1.2f;

    public void SetPrefabPath(string prefabPath) => _prefabPath = prefabPath;
    
    #endregion

    #region 网络生命周期
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        _deathHandled = false;

        if (!_config && this is ServerBossAttributeModule)
        {
            Debug.Log($"[ServerEnemyAttributeModule] Boss 属性配置尚未注入，等待 BossNetworkProxy.SetConfig 后初始化。ObjectId={NetworkObjectId}");
            return;
        }

        InitializeFromConfig();
        Debug.Log($"[ServerEnemyAttributeModule] 网络对象 {NetworkObjectId} 生成完成");
        
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
    }
    
    /// <summary>
    /// 从配置初始化
    /// </summary>
    protected override void InitializeFromConfig()
    {
        // 子类需要实现此方法来设置_config
        if (!_config || !(_config is EnemyAttributeConfig))
        {
            Debug.LogError($"[ServerEnemyAttributeModule] 配置未设置，无法初始化");
            return;
        }

        if (_isInitialized)
        {
            Cleanup();
        }

        InitializeAttributes();
        SyncBaseAttributesToNetwork();
        _isInitialized = true;
        // 客户端注册网络变量变化监听
        RegisterNetworkVariableCallbacks();
        Debug.Log($"[ServerEnemyAttributeModule] 初始化完成 - ObjectId: {NetworkObjectId}");
    }

    /// <summary>
    /// 注册网络变量回调
    /// </summary>
    protected override void RegisterNetworkVariableCallbacks()
    {
        base.RegisterNetworkVariableCallbacks();
        _networkAggroRange.OnValueChanged += OnAggroRangeChanged;
        _networkMonsterRank.OnValueChanged += OnMonsterRankChanged;
        _networkDropRate.OnValueChanged += OnDropRateChanged;
        _networkInGameGoldReward.OnValueChanged += OnInGameGoldRewardChanged;
        _networkDetectionRange.OnValueChanged += OnDetectionRangeChanged;
        
    }
    
    /// <summary>
    /// 清理资源
    /// </summary>
    protected override void Cleanup()
    {
        _networkAggroRange.OnValueChanged -= OnAggroRangeChanged;
        _networkMonsterRank.OnValueChanged -= OnMonsterRankChanged;
        _networkDropRate.OnValueChanged -= OnDropRateChanged;
        _networkInGameGoldReward.OnValueChanged -= OnInGameGoldRewardChanged;
        _networkDetectionRange.OnValueChanged -= OnDetectionRangeChanged;

        _attributes?.Clear();
        _isInitialized = false;
        _deathHandled = false;

        base.Cleanup();
    }
    #endregion
    
    #region 网络变量变化处理
    private void OnAggroRangeChanged(float previousValue, float newValue)
    {
        if (!IsServer) // 客户端处理
        {
            AttributeChangedEventTrigger(AttributeType.AggroRange, previousValue, newValue);
        }
    }
    private void OnMonsterRankChanged(int previousValue, int newValue)
    {
        if (!IsServer) // 客户端处理
        {
            AttributeChangedEventTrigger(AttributeType.MonsterRank, previousValue, newValue);
        }
    }
    private void OnDropRateChanged(float previousValue, float newValue)
    {
        if (!IsServer) // 客户端处理
        {
            AttributeChangedEventTrigger(AttributeType.DropRate, previousValue, newValue);
        }
    }
    private void OnInGameGoldRewardChanged(float previousValue, float newValue)
    {
        if (!IsServer) // 客户端处理
        {
            AttributeChangedEventTrigger(AttributeType.InGameGoldReward, previousValue, newValue);
        }
    }
    private void OnDetectionRangeChanged(float previousValue, float newValue)
    {
        if (!IsServer) // 客户端处理
        {
            AttributeChangedEventTrigger(AttributeType.DetectionRange, previousValue, newValue);
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
            Debug.LogError($"[ServerEnemyAttributeModule] 尝试设置空配置");
            return;
        }
    
        if (config is not EnemyAttributeConfig)
        {
            Debug.LogError($"[ServerEnemyAttributeModule] 配置类型错误，期望 EnemyAttributeConfig，实际 {config.GetType()}");
            return;
        }
    
        _config = config;
        
        // 如果已经网络生成，立即初始化；客户端初始化只注册 NetworkVariable 回调，不写入同步值。
        if (IsSpawned)
        {
            InitializeFromConfig();
        }
    }
    
    protected override void SyncBaseAttributesToNetwork()
    {
        if (!IsServer) return;
        base.SyncBaseAttributesToNetwork();
        _networkAggroRange.Value = GetAttribute(AttributeType.AggroRange);
        _networkMonsterRank.Value = (int)GetAttribute(AttributeType.MonsterRank);
        _networkDropRate.Value = GetAttribute(AttributeType.DropRate);
        _networkInGameGoldReward.Value = GetAttribute(AttributeType.InGameGoldReward);
        _networkDetectionRange.Value = GetAttribute(AttributeType.DetectionRange);
    }
    
    protected override void UpdateNetworkVariable(AttributeType type, float newValue)
    {
        base.UpdateNetworkVariable(type, newValue);
        
        if (!IsServer) return;

        switch (type)
        {
            case AttributeType.AggroRange:
                _networkAggroRange.Value = newValue;
                break;
            case AttributeType.MonsterRank:
                _networkMonsterRank.Value = (int)newValue;
                break;
            case AttributeType.DropRate:
                _networkDropRate.Value = newValue;
                break;
            case AttributeType.InGameGoldReward:
                _networkInGameGoldReward.Value = newValue;
                break;
            case AttributeType.DetectionRange:
                _networkDetectionRange.Value = newValue;
                break;
        }
    }
    #endregion

    #region 敌人特有方法
    /// <summary>
    /// 获取怪物等级（服务器计算）
    /// </summary>
    public MonsterRank GetMonsterRank()
    {
        float rankValue = GetAttribute(AttributeType.MonsterRank);
        return (MonsterRank)Mathf.RoundToInt(rankValue);
    }
    
    /// <summary>
    /// 获取实际掉落率（服务器计算）
    /// </summary>
    public float GetActualDropRate()
    {
        float baseDropRate = GetAttribute(AttributeType.DropRate);
        return Mathf.Clamp01(baseDropRate);
    }
    
    /// <summary>
    /// 获取奖励（服务器计算）
    /// </summary>
    public void GetReward()
    {
        // return new EnemyReward
        // {
        //     inGameGold = GetAttribute(AttributeType.InGameGoldReward),
        //     outGameCurrency = GetAttribute(AttributeType.OutGameCurrencyReward),
        //     dropRate = GetActualDropRate()
        // };
    }
    
    /// <summary>
    /// 检查是否在仇恨范围内（服务器计算）
    /// </summary>
    public bool IsInAggroRange(Vector3 targetPosition, Vector3 enemyPosition)
    {
        float aggroRange = GetAttribute(AttributeType.AggroRange);
        float distance = Vector3.Distance(targetPosition, enemyPosition);
        return distance <= aggroRange;
    }
    
    /// <summary>
    /// 检查是否在侦测范围内（服务器计算）
    /// </summary>
    public bool IsInDetectionRange(Vector3 targetPosition, Vector3 enemyPosition)
    {
        float detectionRange = GetAttribute(AttributeType.DetectionRange);
        float distance = Vector3.Distance(targetPosition, enemyPosition);
        return distance <= detectionRange;
    }
    #endregion

    #region 事件处理
    
    /// <summary>
    /// 处理敌人死亡
    /// </summary>
    private void HandleEnemyDeath(ILogicObject enemy)
    {
        // 敌人死亡时触发奖励掉落
        // EventCenter.Instance.Trigger();
        
        // 可以在这里添加敌人死亡特有逻辑
        Debug.Log($"Enemy died, reward dropped!");
        
        // 同步奖励信息到客户端
        // var reward = GetReward();
        // SyncEnemyRewardClientRpc(reward);
    }
    #endregion

    #region 客户端同步方法
    /// <summary>
    /// 点对点客户端同步奖励信息
    /// </summary>
    [ClientRpc]
    private void SyncEnemyRewardClientRpc()
    {
        if (IsServer) return;

        // 客户端处理敌人奖励显示
        // Debug.Log($"[Client] 敌人奖励 - Gold: {reward.inGameGold}, Currency: {reward.outGameCurrency}");
    }
    #endregion
    
    #region 父类方法重写
    
    public override float GetAttribute(AttributeType type)
    {
        float enemyValue = GetEnemySpecificAttribute(type);
        if (!Mathf.Approximately(enemyValue, float.MinValue))
            return enemyValue;
        
        return base.GetAttribute(type);
    }

    private float GetEnemySpecificAttribute(AttributeType type)
    {
        return type switch
        {
            AttributeType.AggroRange => _networkAggroRange.Value,
            AttributeType.MonsterRank => _networkMonsterRank.Value,
            AttributeType.DropRate => _networkDropRate.Value,
            AttributeType.InGameGoldReward => _networkInGameGoldReward.Value,
            AttributeType.DetectionRange => _networkDetectionRange.Value,
            _ => float.MinValue
        };
    }
    
    protected override (float min, float max) GetAttributeRange(AttributeType type)
    {
        var enemyRange = type switch
        {
            AttributeType.DropRate => (0f, 1f),
            AttributeType.InGameGoldReward => (0f, float.MaxValue),
            _ => (float.MinValue, float.MaxValue)
        };
    
        if (!Mathf.Approximately(enemyRange.Item1, float.MinValue) || !Mathf.Approximately(enemyRange.Item2, float.MaxValue))
            return enemyRange;
    
        return base.GetAttributeRange(type);
    }

    protected override void HandleDeath()
    {
        if (_deathHandled)
            return;

        _deathHandled = true;

        var anim = GetComponent<Animator>();
        if (anim != null)
        {
            anim.SetBool("IsDead", true);
            anim.SetTrigger("Die");
        }

        GetComponent<EnemyNavAgentController>()?.RetireForDeath();

        base.HandleDeath();
    }

    protected override void OnHitAnimation()
    {
        GetComponent<Animator>()?.SetTrigger("Hit");
    }

    protected override float GetDeathReturnDelay()
    {
        float delay = _deathAnimDuration;
        if (float.IsNaN(delay) || float.IsInfinity(delay) || delay < 0f)
        {
            Debug.LogWarning($"[ServerEnemyAttributeModule] Invalid death animation duration {delay}, fallback to {FallbackDeathReturnDelay}s. ObjectId={NetworkObjectId}", this);
            return FallbackDeathReturnDelay;
        }

        return delay;
    }

    protected override string GetPrefabPath()
    {
        return _prefabPath;
    }
    
    private void TrySpawnDropsOnServer()
    {
        if (_config is not EnemyAttributeConfig enemyCfg) return;
        if (enemyCfg.dropTable == null) return;

        Vector3 basePos = transform.position + Vector3.up * 0.2f;
        ulong? ownerClientId = null;

        foreach (var e in enemyCfg.dropTable.entries)
        {
            if (e == null) continue;
            if (string.IsNullOrEmpty(e.itemSoId)) continue;

            // 概率判定：Guaranteed 跳过，Random 按 chance 判定
            if (e.category == EnemyDropTableSO.DropCategory.Random)
            {
                if (Random.value > Mathf.Clamp01(e.chance)) continue;
            }

            int count = Random.Range(e.minCount, e.maxCount + 1);
            if (count <= 0) continue;

            string prefabPath = !string.IsNullOrEmpty(e.dropPrefabPath)
                ? e.dropPrefabPath
                : enemyCfg.defaultDropPrefabPath;

            var go = NetworkObjectPoolManager.Instance.GetAndSpawn(
                prefabPath,
                basePos + Random.insideUnitSphere * 0.35f,
                Quaternion.identity);

            if (go == null) continue;

            var drop = go.GetComponent<NetworkDropItem>();
            if (drop != null)
            {
                drop.ServerInit(prefabPath, e.itemSoId, count, e.isSharedForAllPlayers, ownerClientId);
            }
        }
    }

    #endregion 
}
