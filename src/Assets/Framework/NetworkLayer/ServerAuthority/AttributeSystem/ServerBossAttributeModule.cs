using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Boss 专属服务端属性模块。
/// 继承 ServerEnemyAttributeModule，复用完整的伤害管线。
/// 额外同步 Boss Phase，供客户端渲染层订阅。
/// 血量暂时硬编码为 1000。
/// </summary>
public class ServerBossAttributeModule : ServerEnemyAttributeModule
{
    private const float BossDefaultHealth = 1000f;

    private NetworkVariable<int> _networkPhase = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public int Phase => _networkPhase.Value;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        _networkPhase.OnValueChanged += OnPhaseChanged;
    }

    public override void OnNetworkDespawn()
    {
        _networkPhase.OnValueChanged -= OnPhaseChanged;
        base.OnNetworkDespawn();
    }

    /// <summary>
    /// 覆写初始化：在配置初始化完成后，将 Boss 血量设为 1000。
    /// </summary>
    protected override void InitializeFromConfig()
    {
        base.InitializeFromConfig();

        if (!IsServer) return;

        _networkHealth.Value = BossDefaultHealth;
        _networkMaxHealth.Value = BossDefaultHealth;

        if (_attributes != null)
        {
            if (_attributes.TryGetValue(AttributeType.Health, out var healthData))
            {
                healthData.CurrentValue = BossDefaultHealth;
                healthData.MarkCacheDirty();
                _attributes[AttributeType.Health] = healthData;
            }
            if (_attributes.TryGetValue(AttributeType.MaxHealth, out var maxHealthData))
            {
                maxHealthData.CurrentValue = BossDefaultHealth;
                maxHealthData.MarkCacheDirty();
                _attributes[AttributeType.MaxHealth] = maxHealthData;
            }
        }
    }

    public void SetPhase(int phase)
    {
        if (!IsServer) return;
        _networkPhase.Value = phase;
    }

    private void OnPhaseChanged(int prev, int next)
    {
        if (IsServer) return;
    }
}
