using Framework.LogicLayer.Module.AIModule.Movement;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 敌人 AI 模块。
/// 该模块负责维护单个敌人的状态机、感知系统、目标记忆与移动意图，
/// 并把真实 AI Tick 收口到服务端网络入口。
/// </summary>
public class EnemyAIModule : IModule
{
    /// <summary>
    /// 敌人 AI 所属的逻辑对象。
    /// </summary>
    private readonly EnemyActor _owner;

    /// <summary>
    /// 敌人 AI 使用的配置数据。
    /// </summary>
    private readonly EnemyAIConfig _config;

    /// <summary>
    /// 敌人的状态机实例。
    /// </summary>
    private AIStateMachine _stateMachine;

    /// <summary>
    /// 敌人的感知系统实例。
    /// </summary>
    private PerceptionSystem _perceptionSystem;

    /// <summary>
    /// 敌人的属性模块引用。
    /// </summary>
    private EnemyAttributeModule _attributeModule;

    /// <summary>
    /// 敌人的战斗模块引用。
    /// </summary>
    private EnemyCombatModule _combatModule;

    /// <summary>
    /// 敌人当前锁定的目标。
    /// </summary>
    private IAttackableObject _currentTarget;

    /// <summary>
    /// 当前目标锁定到期时间。
    /// 在到期前保留当前目标，避免防御任务中敌人频繁切换攻击对象。
    /// </summary>
    private float _targetLockedUntil = float.NegativeInfinity;

    /// <summary>
    /// 敌人最后一次确认目标时记录的位置。
    /// </summary>
    private Vector3 _lastKnownTargetPosition;

    /// <summary>
    /// 敌人最后一次确认目标时记录的时间戳。
    /// </summary>
    private float _lastSeenTargetTime = float.NegativeInfinity;

    /// <summary>
    /// 当前是否已经拥有有效的目标记忆。
    /// </summary>
    private bool _hasLastKnownTargetPosition;

    /// <summary>
    /// 敌人的初始出生位置，用于回归与巡逻中心计算。
    /// </summary>
    private Vector3 _initialPosition;

    /// <summary>
    /// 最近一次群体移动修正的调试快照。
    /// 该字段只用于开发期观测 steering 是否触发，不参与任何权威行为判断。
    /// </summary>
    private AISteeringDebugInfo _lastSteeringDebugInfo;

    /// <summary>
    /// NavMeshAgent 控制器引用。
    /// 重构后，移动执行完全交由 NavMeshAgent，EnemyAIModule 只负责调用其接口。
    /// </summary>
    private EnemyNavAgentController _navController;

    /// <summary>
    /// 获取缓存的 NavMeshAgent 控制器引用。
    /// 用于外部系统（如 BoidsCentralController）在不触发 GetComponent 的情况下访问 NavController。
    /// </summary>
    internal EnemyNavAgentController GetCachedNavController() => _navController;

    /// <summary>
    /// 缓存的 NavMeshAgent 速度，用于节流比较。
    /// </summary>
    private float _cachedAgentSpeed;

    [Header("Debug")]
    [Tooltip("是否输出详细调试日志。")]
    [SerializeField]
    private bool verboseLog;

    /// <summary>
    /// 获取当前逻辑对象 ID。
    /// </summary>
    public ulong ObjectId => _owner.ObjectId;

    /// <summary>
    /// 构造敌人 AI 模块。
    /// </summary>
    /// <param name="owner">拥有该 AI 的敌人逻辑对象。</param>
    /// <param name="config">敌人 AI 使用的配置数据。</param>
    public EnemyAIModule(EnemyActor owner, EnemyAIConfig config)
    {
        _owner = owner;
        _config = config;
    }

    /// <summary>
    /// 当前帧 AI 输出给移动执行层的移动意图。
    /// </summary>
    public AIMoveIntent MoveIntent { get; private set; }

    /// <summary>
    /// 清空当前移动意图。
    /// 用于避免旧状态残留移动指令继续被执行。
    /// </summary>
    public void ClearMoveIntent()
    {
        MoveIntent = default;
    }


    /// <summary>
    /// 对状态机或路径系统给出的原始方向施加服务端群体移动修正。
    /// </summary>
    /// <param name="desiredDirection">原始期望移动方向。</param>
    /// <param name="debugInfo">输出的调试信息。</param>
    /// <returns>返回修正后的最终方向；若无法移动则返回零向量。</returns>
    [System.Obsolete("Steering 修正已由 NavMeshAgent 内置绕障承担，该方法仅保留用于调试信息收集。")]
    private Vector3 ApplySteeringCorrection(Vector3 desiredDirection, out AISteeringDebugInfo debugInfo)
    {
        if (_owner == null)
        {
            debugInfo = default;
            return Vector3.zero;
        }

        return SteeringSystem.Instance.ResolveMoveDirection(_owner, _config, desiredDirection, out debugInfo);
    }

    /// <summary>
    /// 初始化 AI 模块运行依赖，并创建初始状态机。
    /// 当前阶段不再在这里直接注册全局 Update，避免客户端或双入口驱动真实 AI。
    /// </summary>
    public void LocalInit()
    {
        _attributeModule = _owner.GetModule<EnemyAttributeModule>();
        _combatModule = _owner.GetModule<EnemyCombatModule>();
        _initialPosition = _owner.WorldPosition;
        _navController = _owner.GetComponent<EnemyNavAgentController>();

        if (_navController != null)
        {
            _cachedAgentSpeed = GetCurrentMoveSpeed();
        }

        _perceptionSystem = new PerceptionSystem(_owner, _config);
        _stateMachine = new AIStateMachine(_owner, _config);

        AIStateBase initialState;
        if (_config.startWithPatrol)
        {
            initialState = new PatrolState(_stateMachine, _config);
        }
        else
        {
            initialState = new IdleState(_stateMachine, _config);
        }

        _stateMachine.ChangeState(initialState);
        AIDebug.Log(ObjectId, "初始化完成，等待服务端网络入口驱动");
    }

    /// <summary>
    /// 模块激活钩子。
    /// 当前阶段不在此处启动权威 AI，仅保留生命周期接口兼容性。
    /// </summary>
    public void OnActivate()
    {
    }

    /// <summary>
    /// 销毁 AI 模块内部状态，并清空本地缓存的目标、记忆与移动意图。
    /// </summary>
    public void LocalDestroy()
    {
        _stateMachine?.Cleanup();
        _stateMachine = null;
        _perceptionSystem = null;
        _currentTarget = null;
        _targetLockedUntil = float.NegativeInfinity;
        _hasLastKnownTargetPosition = false;
        _lastSeenTargetTime = float.NegativeInfinity;
        _lastKnownTargetPosition = default;
        _lastSteeringDebugInfo = default;
        ClearMoveIntent();
    }

    /// <summary>
    /// 服务端权威 AI Tick。
    /// 只有在网络对象已生成且当前实例是服务端时才会真正推进状态机。
    /// </summary>
    public void ServerTick()
    {
        if (_stateMachine == null)
        {
            AIDebug.LogWarning(_owner?.ObjectId ?? 0, "ServerTick: _stateMachine 为空，跳过");
            return;
        }

        if (!CanRunServerAuthorityLogic())
        {
            AIDebug.LogWarning(_owner?.ObjectId ?? 0, "ServerTick: CanRunServerAuthorityLogic=false，跳过");
            return;
        }

        var state = _stateMachine.GetCurrentStateName();
        var simLevel = GetCurrentSimulationLevel();
        AIDebug.Log(_owner?.ObjectId ?? 0, $"ServerTick 当前状态={state} 仿真级别={simLevel}");

        UpdateNavControllerSimulationLevel();
        UpdatePerception();
        _stateMachine.OnUpdate();
    }

    /// <summary>
    /// 根据当前仿真级别更新 NavMeshAgent 控制器的寻路节流参数。
    /// </summary>
    private void UpdateNavControllerSimulationLevel()
    {
        if (_navController == null)
            return;

        var level = GetCurrentSimulationLevel();
        _navController.ApplySimulationLevel(level);
    }

    /// <summary>
    /// 判断当前是否允许执行服务端权威 AI 逻辑。
    /// 这里做硬保护，是为了把 AI 从“约定只在服务端跑”收口成“代码层面只允许在服务端跑”。
    /// </summary>
    /// <returns>返回 true 表示当前网络生命周期允许执行真实 AI。</returns>
    private bool CanRunServerAuthorityLogic()
    {
        if (_owner == null || !_owner.enabled)
            return false;

        var networkManager = NetworkManager.Singleton;
        if (networkManager == null || !networkManager.IsServer)
            return false;

        var networkObject = _owner.GetComponent<NetworkObject>();
        if (networkObject == null || !networkObject.IsSpawned)
            return false;

        return true;
    }

    /// <summary>
    /// 更新感知系统缓存的当前目标。
    /// 若本次成功感知到目标，则同步刷新目标记忆；若未感知到目标，则只清空当前目标，不丢弃记忆。
    /// </summary>
    private void UpdatePerception()
    {
        if (_perceptionSystem == null)
            return;

        if (Time.frameCount % _config.perceptionUpdateInterval != 0)
            return;

        if (IsCurrentTargetLockedAndValid())
        {
            UpdateTargetMemory(_currentTarget.GetTargetPoint());
            return;
        }

        var detectedTarget = _perceptionSystem.DetectTarget();
        if (detectedTarget != null)
        {
            SetTarget(detectedTarget);
        }
        else
        {
            ClearTarget();
        }
    }

    /// <summary>
    /// 获取当前 AI 目标。
    /// </summary>
    /// <returns>返回当前锁定目标；若没有目标则返回 null。</returns>
    public IAttackableObject GetCurrentTarget()
    {
        return _currentTarget;
    }

    /// <summary>
    /// 设置当前 AI 目标，并同步刷新个体级目标记忆。
    /// </summary>
    /// <param name="target">要设置为当前目标的可攻击对象。</param>
    public void SetTarget(IAttackableObject target)
    {
        _currentTarget = target;

        if (target != null)
        {
            UpdateTargetMemory(target.GetTargetPoint());
            _targetLockedUntil = Time.time + Mathf.Max(0f, _config.targetSwitchCooldown);
        }
    }

    /// <summary>
    /// 外部强制切换目标入口。
    /// 后续嘲讽、仇恨技能可以通过该方法覆盖当前锁定目标。
    /// </summary>
    /// <param name="newTarget">要切换到的新目标。</param>
    /// <returns>返回 true 表示切换成功。</returns>
    public bool TrySwitchTarget(IAttackableObject newTarget)
    {
        if (!IsTargetUsable(newTarget))
        {
            return false;
        }

        SetTarget(newTarget);
        return true;
    }

    /// <summary>
    /// 清空当前 AI 目标。
    /// 注意：这里不清空最后目标点记忆。
    /// </summary>
    public void ClearTarget()
    {
        _currentTarget = null;
    }

    private bool IsCurrentTargetLockedAndValid()
    {
        return _currentTarget != null &&
               Time.time < _targetLockedUntil &&
               IsTargetUsable(_currentTarget);
    }

    private static bool IsTargetUsable(IAttackableObject target)
    {
        return target != null &&
               target.TargetTransform != null &&
               target.IsActiveForAI &&
               target.IsAliveForAI;
    }

    /// <summary>
    /// 更新目标记忆。
    /// 该记忆用于目标短暂丢失后继续追向最后目标点，而不是立即退回待机。
    /// </summary>
    /// <param name="targetPosition">本次确认到的目标位置。</param>
    public void UpdateTargetMemory(Vector3 targetPosition)
    {
        _lastKnownTargetPosition = targetPosition;
        _lastSeenTargetTime = Time.time;
        _hasLastKnownTargetPosition = true;
    }

    /// <summary>
    /// 判断当前是否仍然拥有有效的近期目标记忆。
    /// </summary>
    /// <returns>返回 true 表示最后目标点仍处于有效记忆时间窗内。</returns>
    public bool HasRecentTargetMemory()
    {
        if (!_hasLastKnownTargetPosition)
            return false;

        return Time.time - _lastSeenTargetTime <= _config.targetMemoryDuration;
    }

    /// <summary>
    /// 获取最后记录的目标位置。
    /// </summary>
    /// <returns>返回最后一次见到目标时记录的位置。</returns>
    public Vector3 GetLastKnownTargetPosition()
    {
        return _lastKnownTargetPosition;
    }

    /// <summary>
    /// 获取最后一次见到目标的时间戳。
    /// </summary>
    /// <returns>返回最后一次目击时间，单位为秒。</returns>
    public float GetLastSeenTargetTime()
    {
        return _lastSeenTargetTime;
    }

    /// <summary>
    /// 获取敌人的初始出生位置。
    /// </summary>
    /// <returns>返回用于回归和巡逻参考的世界坐标。</returns>
    public Vector3 GetInitialPosition()
    {
        return _initialPosition;
    }

    /// <summary>
    /// 判断指定世界位置是否仍处于当前敌人的追击活动半径内。
    /// 只比较水平距离，避免地形高度差影响 leash 判定。
    /// </summary>
    public bool IsWithinChaseLeash(Vector3 worldPosition, float extraMargin = 0f)
    {
        var offset = worldPosition - _initialPosition;
        offset.y = 0f;

        var leash = Mathf.Max(0f, _config.maxChaseDistance + extraMargin);
        return offset.sqrMagnitude <= leash * leash;
    }

    /// <summary>
    /// 获取敌人属性模块。
    /// </summary>
    /// <returns>返回敌人属性模块引用。</returns>
    public EnemyAttributeModule GetAttributeModule()
    {
        return _attributeModule;
    }

    /// <summary>
    /// 获取敌人战斗模块。
    /// </summary>
    /// <returns>返回敌人战斗模块引用。</returns>
    public EnemyCombatModule GetCombatModule()
    {
        return _combatModule;
    }

    /// <summary>
    /// 获取当前敌人应使用的移动速度。
    /// 若属性模块可用，则优先使用实时属性值；否则回退到 AI 配置默认速度。
    /// </summary>
    /// <returns>返回当前应使用的移动速度。</returns>
    public float GetCurrentMoveSpeed()
    {
        return _attributeModule?.GetAttribute(AttributeType.MoveSpeed) ?? _config.defaultMoveSpeed;
    }

    /// <summary>
    /// 直接命令 NavMeshAgent 朝目标位置移动（重构后的新寻路接口）。
    ///
    /// 与旧版 TryMoveTowardsPosition 的区别：
    /// - 不再调用 PathService.QueryPath()
    /// - 不再设置 MoveIntent 方向
    /// - 直接通过 EnemyNavAgentController.SetDestinationThrottled() 驱动 NavMeshAgent
    ///
    /// NavMeshAgent 自己负责绕障、穿门、跨房间。
    /// Steering 修正也由 NavMeshAgent 内置绕障承担。
    /// </summary>
    /// <param name="targetPosition">目标世界坐标。</param>
    /// <param name="arrivalDistance">到达判定距离。</param>
    /// <returns>返回 true 表示本帧触发了实际寻路；返回 false 表示已到达或无法移动。</returns>
    public bool MoveToPosition(Vector3 targetPosition, float arrivalDistance)
    {
        if (_navController == null)
        {
            if (verboseLog) AIDebug.LogWarning("MoveToPosition: _navController 为空");
            return false;
        }

        _navController.SetStoppingDistance(arrivalDistance);

        if (_navController.HasArrived(arrivalDistance))
        {
            AIDebug.Log(_owner?.ObjectId ?? 0, $"MoveToPosition: 已到达(arrivalDistance={arrivalDistance:F2}m)，停止移动");
            _navController.Stop();
            return false;
        }

        if (_navController.TryRecoverFromStuck())
        {
            AIDebug.Log(_owner?.ObjectId ?? 0, "MoveToPosition: 触发卡死恢复");
            return true;
        }

        AIDebug.Log(_owner?.ObjectId ?? 0, $"MoveToPosition: 未到达，调用SetDestinationThrottled 目标={targetPosition}");
        ClearMoveIntent();
        var result = _navController.SetDestinationThrottled(targetPosition);
        AIDebug.Log(_owner?.ObjectId ?? 0, $"MoveToPosition: SetDestinationThrottled返回={result}");
        return result;
    }

    /// <summary>
    /// 停止 NavMeshAgent 当前路径。
    /// </summary>
    public void StopMoving()
    {
        if (_navController != null)
        {
            _navController.Stop();
            if (verboseLog) AIDebug.Log(_owner?.ObjectId ?? 0, "StopMoving");
        }

        ClearMoveIntent();
    }

    /// <summary>
    /// 获取 NavMeshAgent 控制器的当前速度（用于调试）。
    /// </summary>
    public Vector3 GetNavAgentVelocity()
    {
        return _navController?.Velocity ?? Vector3.zero;
    }

    /// <summary>
    /// 判断 NavMeshAgent 当前是否在 NavMesh 上。
    /// </summary>
    public bool IsNavAgentOnMesh()
    {
        return _navController?.IsOnNavMesh ?? false;
    }

    /// <summary>
    /// 尝试将 NavMeshAgent 传送到最近的 NavMesh 可走面上。
    /// 用于出生点修正或强制位置同步。
    /// </summary>
    public bool TryWarpToNavMesh()
    {
        return _navController?.TryWarpToNavMesh() ?? false;
    }

    /// <summary>
    /// 同步 NavMeshAgent 速度与当前 AI 配置。
    /// 当属性模块的速度发生变化时调用此方法更新 Agent 速度。
    /// </summary>
    public void SyncNavAgentSpeed()
    {
        if (_navController == null)
            return;

        var speed = GetCurrentMoveSpeed();
        if (Mathf.Abs(_cachedAgentSpeed - speed) > 0.01f)
        {
            _cachedAgentSpeed = speed;
            _navController.SetSpeed(speed);
        }
    }

    /// <summary>
    /// 获取当前状态名，主要用于调试与后续状态同步。
    /// </summary>
    /// <returns>返回当前状态类名；若状态机为空则返回 None。</returns>
    public string GetCurrentStateName()
    {
        return _stateMachine?.GetCurrentStateName() ?? "None";
    }

    /// <summary>
    /// 获取当前敌人在服务端调度器中的仿真级别。
    /// 该接口主要供调试工具、状态复制层和后续可视化面板读取，不会主动推进 AI 逻辑。
    /// </summary>
    /// <returns>返回当前缓存的服务端仿真级别。</returns>
    public AISimulationLevel GetCurrentSimulationLevel()
    {
        return AIScheduler.Instance.GetCurrentSimulationLevel(_owner);
    }

    /// <summary>
    /// 尝试获取当前敌人的服务端调度调试快照。
    /// 该快照反映的是上一轮真实评估后的缓存结果，可用于查看敌人为何处于当前仿真级别。
    /// </summary>
    /// <param name="debugInfo">输出的调度调试信息。</param>
    /// <returns>返回 true 表示当前敌人已注册进调度器且存在可读快照。</returns>
    public bool TryGetSchedulerDebugInfo(out AISchedulerDebugInfo debugInfo)
    {
        return AIScheduler.Instance.TryGetDebugInfo(_owner, out debugInfo);
    }

    /// <summary>
    /// 获取最近一次群体移动修正的调试快照。
    /// 该接口主要提供给调试面板读取，不会主动触发新的 steering 计算。
    /// </summary>
    /// <param name="debugInfo">输出最近一次缓存的 steering 调试信息。</param>
    /// <returns>返回 true 表示当前存在可读的 steering 调试快照。</returns>
    public bool TryGetLastSteeringDebugInfo(out AISteeringDebugInfo debugInfo)
    {
        debugInfo = _lastSteeringDebugInfo;
        return _lastSteeringDebugInfo.lastUpdateTime > 0f;
    }
}
