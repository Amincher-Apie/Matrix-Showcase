using System.Collections;
using System.Collections.Generic;
using Framework.LogicLayer.AttributeSystem;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using BehaviorDesigner.Runtime;

/// <summary>
/// Boss 网络代理。
/// 服务端权威：OnNetworkSpawn 时缓存 BT 引用。
/// 客户端只消费 ServerBossAttributeModule 同步过来的状态。
/// </summary>
public class BossNetworkProxy : NetworkProxyBase
{
    [field: SerializeField]
    public ServerBossAttributeModule ServerBossAttributeModule { get; private set; }

    public BossActor BossActor { get; private set; }

    private BehaviorTree _bt;
    private Coroutine _btTickRoutine;
    private bool _attributeConfigInitialized;
    private bool _hasRestartedAfterTargetBound;

    [Header("UI")]
    [SerializeField] private Transform _uiAnchor;
    [SerializeField] private Vector3 _fallbackUIAnchorOffset = new Vector3(0f, 3f, 0f);
    private Transform _runtimeUIAnchor;

    [Header("Attribute")]
    [SerializeField] private string _fallbackAttributeConfigId = "002";

    [Header("BT Tick")]
    [SerializeField] private float btTickInterval = 0.1f;
    [SerializeField] private float navMeshWarpSampleDistance = 6f;

    public override T GetServerAttributeModule<T>()
    {
        return ServerBossAttributeModule as T;
    }

    public override T GetServerWeaponRuntime<T>()
    {
        return null;
    }

    public override T GetServerCombatModule<T>()
    {
        return null;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        BossActor = GetComponent<BossActor>();
        if (ServerBossAttributeModule == null)
        {
            ServerBossAttributeModule = GetComponent<ServerBossAttributeModule>();
        }

        if (IsClient)
        {
            EnemyHealthBarManager.Instance.RegisterEnemy(NetworkObjectId, ResolveUIAnchor());
            InitializeBossAttributeConfig();
        }

        if (!IsServer) return;

        if (BossActor == null)
        {
            Debug.LogError("[BossNetworkProxy] BossActor not found on same GameObject.");
            return;
        }

        SetLogicObject(BossActor);

        _bt = BossActor.BehaviorTree;
        if (_bt == null)
        {
            Debug.LogError("[BossNetworkProxy] BehaviorTree not found on BossActor.");
            return;
        }

        InitializeBossAttributeConfig();
        BossActor.ActivateAfterSpawn();
        EnsureBossOnNavMesh();
        InitializeBehaviorTreeRuntime();
        _btTickRoutine = StartCoroutine(ServerBTTick());

        Debug.Log($"[BossNetworkProxy] Boss[{NetworkObjectId}] spawned.");
    }

    public override void OnNetworkDespawn()
    {
        if (IsClient)
        {
            EnemyHealthBarManager.Instance.UnregisterEnemy(NetworkObjectId);
        }

        StopBTTick();
        _bt = null;
        BossActor = null;
        _runtimeUIAnchor = null;
        _attributeConfigInitialized = false;
        _hasRestartedAfterTargetBound = false;

        base.OnNetworkDespawn();
    }

    private IEnumerator ServerBTTick()
    {
        while (IsServer && _bt != null)
        {
            TryBindAttackTarget();
            yield return new WaitForSeconds(btTickInterval);
        }
    }

    private void StopBTTick()
    {
        if (_btTickRoutine == null) return;
        StopCoroutine(_btTickRoutine);
        _btTickRoutine = null;
    }

    private void InitializeBossAttributeConfig()
    {
        if (_attributeConfigInitialized)
            return;

        if (ServerBossAttributeModule == null)
        {
            Debug.LogWarning($"[BossNetworkProxy] Boss[{NetworkObjectId}] 缺少 ServerBossAttributeModule，无法初始化 Boss 属性。");
            return;
        }

        var config = ResolveBossAttributeConfig(out var resolvedId, out var isFallback);
        if (config == null)
        {
            Debug.LogError($"[BossNetworkProxy] Boss[{NetworkObjectId}] 找不到可用属性配置。BossConfigId={BossActor?.BossConfigId}");
            return;
        }

        if (isFallback)
        {
            Debug.LogWarning($"[BossNetworkProxy] Boss[{NetworkObjectId}] 使用兜底属性配置 {resolvedId}，请补充正式 Boss 属性 SO 或在 BossActor 上设置有效 BossConfigId。");
        }

        ServerBossAttributeModule.SetConfig(config);
        _attributeConfigInitialized = true;
    }

    private EnemyAttributeConfig ResolveBossAttributeConfig(out string resolvedId, out bool isFallback)
    {
        resolvedId = string.Empty;
        isFallback = false;

        var manager = AttributeManager.Instance;
        var availableIds = manager.GetAllEnemyIds();
        if (availableIds == null || availableIds.Count == 0)
        {
            return null;
        }

        var candidates = new List<string>();
        AddAttributeCandidate(candidates, BossActor != null ? BossActor.BossConfigId : null);
        AddAttributeCandidate(candidates, ExtractAttributeId(BossActor != null ? BossActor.BossConfigId : null));
        AddAttributeCandidate(candidates, _fallbackAttributeConfigId);
        AddAttributeCandidate(candidates, ExtractAttributeId(_fallbackAttributeConfigId));
        AddAttributeCandidate(candidates, StripCloneSuffix(gameObject.name));
        AddAttributeCandidate(candidates, ExtractAttributeId(StripCloneSuffix(gameObject.name)));

        for (var i = 0; i < candidates.Count; i++)
        {
            var candidate = candidates[i];
            if (!ContainsId(availableIds, candidate))
                continue;

            var config = manager.GetEnemyAttributeConfig(candidate);
            if (config == null)
                continue;

            resolvedId = config.id;
            isFallback = !IsConfiguredBossId(candidate);
            return config;
        }

        for (var i = 0; i < availableIds.Count; i++)
        {
            var config = manager.GetEnemyAttributeConfig(availableIds[i]);
            if (config == null || config.baseMonsterRank != MonsterRank.Boss)
                continue;

            resolvedId = config.id;
            isFallback = true;
            return config;
        }

        availableIds.Sort(System.StringComparer.Ordinal);
        var fallbackId = availableIds[0];
        var fallbackConfig = manager.GetEnemyAttributeConfig(fallbackId);
        resolvedId = fallbackConfig != null ? fallbackConfig.id : fallbackId;
        isFallback = true;
        return fallbackConfig;
    }

    private bool IsConfiguredBossId(string candidate)
    {
        if (BossActor == null)
            return false;

        var bossConfigId = BossActor.BossConfigId;
        return string.Equals(candidate, bossConfigId, System.StringComparison.Ordinal) ||
               string.Equals(candidate, ExtractAttributeId(bossConfigId), System.StringComparison.Ordinal);
    }

    private static void AddAttributeCandidate(List<string> candidates, string candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
            return;

        candidate = candidate.Trim();
        if (!candidates.Contains(candidate))
        {
            candidates.Add(candidate);
        }
    }

    private static bool ContainsId(List<string> ids, string candidate)
    {
        for (var i = 0; i < ids.Count; i++)
        {
            if (string.Equals(ids[i], candidate, System.StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static string ExtractAttributeId(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return string.Empty;

        var trimmed = id.Trim();
        var separatorIndex = trimmed.LastIndexOfAny(new[] { '/', '\\' });
        return separatorIndex >= 0 && separatorIndex < trimmed.Length - 1
            ? trimmed.Substring(separatorIndex + 1)
            : trimmed;
    }

    private static string StripCloneSuffix(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return string.Empty;

        return objectName.Replace("(Clone)", string.Empty).Trim();
    }

    private void InitializeBehaviorTreeRuntime()
    {
        if (_bt == null || BossActor == null)
            return;

        _bt.SetVariableValue("self", BossActor.gameObject);
        _bt.SetVariableValue("death", false);
        _bt.SetVariableValue("isIdle", false);
        _hasRestartedAfterTargetBound = false;
        TryBindAttackTarget(false);

        if (!_bt.enabled)
        {
            _bt.enabled = true;
        }

        RestartBehaviorTree();
        _hasRestartedAfterTargetBound = HasValidAttackTarget();
    }

    private bool TryBindAttackTarget(bool restartOnFirstBind = true)
    {
        if (_bt == null || BossActor == null)
            return false;

        var currentTarget = _bt.GetVariable("attackTarget")?.GetValue() as GameObject;
        if (IsValidPlayerTarget(currentTarget))
        {
            _bt.SetVariableValue("targetPosition", currentTarget.transform.position);
            RestartAfterFirstTargetBind(restartOnFirstBind);
            return true;
        }

        var target = FindNearestPlayerTarget();
        if (target == null)
            return false;

        _bt.SetVariableValue("attackTarget", target.gameObject);
        _bt.SetVariableValue("targetPosition", target.transform.position);
        RestartAfterFirstTargetBind(restartOnFirstBind);
        return true;
    }

    private Transform FindNearestPlayerTarget()
    {
        Transform bestTarget = null;
        var bestSqrDistance = float.PositiveInfinity;
        var attackableManager = AttackableObjectManager.Instance;
        var registeredTargets = attackableManager != null ? attackableManager.GetAllRegistered() : null;

        for (var i = 0; registeredTargets != null && i < registeredTargets.Count; i++)
        {
            var candidate = registeredTargets[i];
            if (candidate == null ||
                candidate.TargetType != AttackableObjectType.Player ||
                !candidate.IsActiveForAI ||
                !candidate.IsAliveForAI ||
                candidate.TargetTransform == null)
            {
                continue;
            }

            var sqrDistance = (candidate.GetTargetPoint() - BossActor.transform.position).sqrMagnitude;
            if (sqrDistance >= bestSqrDistance)
                continue;

            bestSqrDistance = sqrDistance;
            bestTarget = candidate.TargetTransform;
        }

        if (bestTarget != null || NetworkManager.Singleton == null)
        {
            return bestTarget;
        }

        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            var playerObject = client.PlayerObject;
            if (playerObject == null)
                continue;

            var playerActor = playerObject.GetComponent<PlayerActor>();
            if (playerActor == null || !playerActor.IsActiveForAI || !playerActor.IsAliveForAI)
                continue;

            var sqrDistance = (playerActor.GetTargetPoint() - BossActor.transform.position).sqrMagnitude;
            if (sqrDistance >= bestSqrDistance)
                continue;

            bestSqrDistance = sqrDistance;
            bestTarget = playerActor.TargetTransform;
        }

        return bestTarget;
    }

    private void RestartAfterFirstTargetBind(bool restartOnFirstBind)
    {
        if (!restartOnFirstBind || _hasRestartedAfterTargetBound)
            return;

        _hasRestartedAfterTargetBound = true;
        RestartBehaviorTree();
    }

    private bool HasValidAttackTarget()
    {
        if (_bt == null)
            return false;

        var target = _bt.GetVariable("attackTarget")?.GetValue() as GameObject;
        return IsValidPlayerTarget(target);
    }

    private static bool IsValidPlayerTarget(GameObject target)
    {
        if (target == null || !target.activeInHierarchy)
            return false;

        var playerActor = target.GetComponentInParent<PlayerActor>();
        return playerActor != null && playerActor.IsActiveForAI && playerActor.IsAliveForAI;
    }

    private void RestartBehaviorTree()
    {
        if (_bt == null || !_bt.isActiveAndEnabled)
            return;

        _bt.DisableBehavior();
        _bt.EnableBehavior();
    }

    private void EnsureBossOnNavMesh()
    {
        var agent = BossActor != null ? BossActor.NavMeshAgent : null;
        if (agent == null || !agent.enabled || agent.isOnNavMesh)
            return;

        if (NavMesh.SamplePosition(agent.transform.position, out var hit, navMeshWarpSampleDistance, NavMesh.AllAreas))
        {
            agent.Warp(hit.position);
            Debug.Log($"[BossNetworkProxy] Boss[{NetworkObjectId}] Warp 到 NavMesh: {hit.position}");
        }
        else
        {
            Debug.LogWarning($"[BossNetworkProxy] Boss[{NetworkObjectId}] 附近 {navMeshWarpSampleDistance}m 内找不到 NavMesh，Boss 追踪可能失败。当前位置={agent.transform.position}");
        }
    }

    private Transform ResolveUIAnchor()
    {
        if (_uiAnchor != null)
        {
            return _uiAnchor;
        }

        if (_runtimeUIAnchor != null)
        {
            return _runtimeUIAnchor;
        }

        var namedAnchor = FindChildByName("UIAnchor", "UiAnchor", "HealthBarAnchor", "HpAnchor", "REAPER_ Head");
        if (namedAnchor != null)
        {
            return namedAnchor;
        }

        var anchorObject = new GameObject("BossHealthBarAnchor_Runtime");
        _runtimeUIAnchor = anchorObject.transform;
        _runtimeUIAnchor.SetParent(transform, false);
        _runtimeUIAnchor.localPosition = _fallbackUIAnchorOffset;
        return _runtimeUIAnchor;
    }

    private Transform FindChildByName(params string[] names)
    {
        var children = GetComponentsInChildren<Transform>(true);
        for (var i = 0; i < children.Length; i++)
        {
            var child = children[i];
            for (var j = 0; j < names.Length; j++)
            {
                if (child.name == names[j])
                {
                    return child;
                }
            }
        }

        return null;
    }
}
