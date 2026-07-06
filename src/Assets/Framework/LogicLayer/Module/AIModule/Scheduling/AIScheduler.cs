using System.Collections.Generic;
using Framework.Singleton;
using UnityEngine;

/// <summary>
/// AI 服务端调度器。
/// 当前阶段提供最小可用的仿真 LOD 骨架，根据玩家距离、兴趣热点、近期战斗和怪物等级决定敌人 Tick 频率。
/// </summary>
public class AIScheduler : SingletonBase<AIScheduler>
{
    /// <summary>
    /// 全局 AI Debug 日志开关。
    /// 设为 true 时，所有 AI 相关模块的 Debug.Log 会输出；设为 false 时抑制日志。
    /// </summary>
    public static bool GlobalDebugLogEnabled = false;

    /// <summary>
    /// 当前服务端 AI 调度配置。
    /// 该配置在调度器初始化时加载，若没有资源文件则自动创建默认值实例。
    /// </summary>
    private AISchedulerConfig _config;

    /// <summary>
    /// 调度状态缓存。
    /// 该状态按敌人个体保存，用于实现滞后阈值、最短保持时间和战斗锁定时间。
    /// </summary>
    private sealed class SchedulerState
    {
        /// <summary>
        /// 当前仿真级别。
        /// </summary>
        public AISimulationLevel currentLevel = AISimulationLevel.Full;

        /// <summary>
        /// 上一次级别切换时间。
        /// </summary>
        public float lastLevelChangeTime = float.NegativeInfinity;

        /// <summary>
        /// 战斗锁定截止时间。
        /// 在锁定期内即使目标短暂丢失，也仍然维持较高仿真级别。
        /// </summary>
        public float combatLockUntil = float.NegativeInfinity;

        /// <summary>
        /// 最近一次受击或造成伤害的时间戳。
        /// 该时间用于实现“最近受击”带来的短时活跃窗口。
        /// </summary>
        public float lastDamagedTime = float.NegativeInfinity;

        /// <summary>
        /// 最近一次评估得到的期望仿真级别。
        /// 该字段主要用于调试输出与观测。
        /// </summary>
        public AISimulationLevel lastDesiredLevel = AISimulationLevel.Full;

        /// <summary>
        /// 最近一次评估时记录的最近目标距离。
        /// </summary>
        public float lastNearestTargetDistance = float.PositiveInfinity;

        /// <summary>
        /// 最近一次评估时是否命中了附近兴趣热点。
        /// </summary>
        public bool lastHadNearbyInterest;

        /// <summary>
        /// 最近一次评估时是否被视为处于战斗中。
        /// </summary>
        public bool lastWasInCombat;

        /// <summary>
        /// 最近一次评估时是否被视为高优先级敌人。
        /// </summary>
        public bool lastWasPriorityEnemy;

        /// <summary>
        /// 最近一次评估时是否命中最近受击窗口。
        /// </summary>
        public bool lastWasRecentlyDamaged;

        /// <summary>
        /// 最近一次计算出的服务端 Tick 间隔。
        /// </summary>
        public float lastComputedTickInterval = 0.1f;

        /// <summary>
        /// 最近一次命中的主要判定原因。
        /// 当前使用稳定的 ASCII 原因码，避免不同终端编码下日志或调试面板出现乱码。
        /// </summary>
        public string lastDecisionReason = "NotEvaluated";
    }

    /// <summary>
    /// 已注册敌人的调度状态表。
    /// </summary>
    private readonly Dictionary<ulong, SchedulerState> _states = new Dictionary<ulong, SchedulerState>();

    /// <summary>
    /// 初始化调度器。
    /// 当前阶段主要负责加载调度配置，并订阅统一受击事件用于驱动战斗热点和近期受击活跃窗口。
    /// </summary>
    protected override void Initialize()
    {
        _config = AISchedulerConfig.LoadOrCreate();
        EventCenter.Instance.AddListener<UnitDamagedEvt>(EventName.UnitDamaged, OnUnitDamaged);
    }

    /// <summary>
    /// 释放调度器资源。
    /// 当前阶段主要负责解除事件监听，避免重复初始化或热重载后出现重复处理受击事件的问题。
    /// </summary>
    public override void Release()
    {
        EventCenter.Instance.RemoveListener<UnitDamagedEvt>(EventName.UnitDamaged, OnUnitDamaged);
        _states.Clear();
        base.Release();
    }

    /// <summary>
    /// 注册一个敌人到调度系统。
    /// </summary>
    /// <param name="enemyActor">要注册的敌人对象。</param>
    public void RegisterEnemy(EnemyActor enemyActor)
    {
        if (enemyActor == null)
            return;

        if (_states.ContainsKey(enemyActor.ObjectId))
        {
            AIDebug.Log($"RegisterEnemy: Enemy[{enemyActor.ObjectId}] 已注册，跳过");
            return;
        }

        _states.Add(enemyActor.ObjectId, new SchedulerState
        {
            currentLevel = AISimulationLevel.Full,
            lastLevelChangeTime = Time.time,
            combatLockUntil = float.NegativeInfinity,
            lastDamagedTime = float.NegativeInfinity,
            lastDesiredLevel = AISimulationLevel.Full,
            lastNearestTargetDistance = float.PositiveInfinity,
            lastComputedTickInterval = _config != null ? _config.fullTickInterval : 0.1f,
            lastDecisionReason = "InitialRegistration"
        });

        AIDebug.Log($"RegisterEnemy: Enemy[{enemyActor.ObjectId}] 注册成功，当前调度器管理 {_states.Count} 个敌人");
    }

    /// <summary>
    /// 从调度系统中注销一个敌人。
    /// </summary>
    /// <param name="enemyActor">要注销的敌人对象。</param>
    public void UnregisterEnemy(EnemyActor enemyActor)
    {
        if (enemyActor == null)
            return;

        var removed = _states.Remove(enemyActor.ObjectId);
        AIDebug.Log($"UnregisterEnemy: Enemy[{enemyActor.ObjectId}] 注销 {(removed ? "成功" : "失败(未找到)")}，剩余 {_states.Count} 个敌人");
    }

    /// <summary>
    /// 获取指定敌人当前应使用的服务端 Tick 间隔。
    /// </summary>
    /// <param name="enemyActor">待调度的敌人对象。</param>
    /// <param name="aiModule">敌人的 AI 模块。</param>
    /// <param name="fallbackInterval">调度失败时使用的回退间隔。</param>
    /// <returns>返回本次 AI Tick 后应等待的秒数。</returns>
    public float GetTickInterval(EnemyActor enemyActor, EnemyAIModule aiModule, float fallbackInterval)
    {
        if (enemyActor == null || aiModule == null)
            return Mathf.Max(0.02f, fallbackInterval);

        EnsureConfigLoaded();
        RegisterEnemy(enemyActor);

        if (!_states.TryGetValue(enemyActor.ObjectId, out var state))
            return Mathf.Max(0.02f, fallbackInterval);

        var desiredLevel = EvaluateDesiredLevel(enemyActor, aiModule, state);
        if (ShouldChangeLevel(state, desiredLevel))
        {
            var previousLevel = state.currentLevel;
            state.currentLevel = desiredLevel;
            state.lastLevelChangeTime = Time.time;

            if (_config.logLevelChanges)
            {
                AIDebug.Log($"Enemy[{enemyActor.ObjectId}] 仿真级别切换: {previousLevel} -> {desiredLevel}");
            }
        }

        state.lastDesiredLevel = desiredLevel;
        state.lastComputedTickInterval = GetIntervalByLevel(state.currentLevel);
        return state.lastComputedTickInterval;
    }

    /// <summary>
    /// 获取指定敌人当前实际生效的仿真级别。
    /// 该接口主要供外部调试工具、同步层或后续可视化面板读取。
    /// </summary>
    /// <param name="enemyActor">要查询的敌人对象。</param>
    /// <returns>返回当前缓存的仿真级别；若未注册则回退为 Full。</returns>
    public AISimulationLevel GetCurrentSimulationLevel(EnemyActor enemyActor)
    {
        if (enemyActor == null)
            return AISimulationLevel.Full;

        if (_states.TryGetValue(enemyActor.ObjectId, out var state))
        {
            return state.currentLevel;
        }

        return AISimulationLevel.Full;
    }

    /// <summary>
    /// 尝试获取指定敌人的调度调试快照。
    /// 该接口不会推进仿真，只读取上一轮评估缓存，用于避免调试查询反向影响权威逻辑。
    /// </summary>
    /// <param name="enemyActor">要查询的敌人对象。</param>
    /// <param name="debugInfo">输出的调度调试信息。</param>
    /// <returns>返回 true 表示当前敌人已经注册且存在可读的调度状态。</returns>
    public bool TryGetDebugInfo(EnemyActor enemyActor, out AISchedulerDebugInfo debugInfo)
    {
        debugInfo = default;
        if (enemyActor == null)
            return false;

        if (!_states.TryGetValue(enemyActor.ObjectId, out var state))
            return false;

        debugInfo = new AISchedulerDebugInfo
        {
            isRegistered = true,
            currentLevel = state.currentLevel,
            desiredLevel = state.lastDesiredLevel,
            currentTickInterval = state.lastComputedTickInterval,
            nearestTargetDistance = state.lastNearestTargetDistance,
            hasNearbyInterest = state.lastHadNearbyInterest,
            isInCombat = state.lastWasInCombat,
            isPriorityEnemy = state.lastWasPriorityEnemy,
            wasRecentlyDamaged = state.lastWasRecentlyDamaged,
            lastLevelChangeTime = state.lastLevelChangeTime,
            combatLockUntil = state.combatLockUntil,
            lastDamagedTime = state.lastDamagedTime,
            decisionReason = state.lastDecisionReason
        };
        return true;
    }

    /// <summary>
    /// 评估指定敌人当前期望的仿真级别。
    /// </summary>
    /// <param name="enemyActor">待评估的敌人对象。</param>
    /// <param name="aiModule">敌人的 AI 模块。</param>
    /// <param name="state">敌人的调度状态缓存。</param>
    /// <returns>返回本轮评估得到的目标仿真级别。</returns>
    private AISimulationLevel EvaluateDesiredLevel(EnemyActor enemyActor, EnemyAIModule aiModule, SchedulerState state)
    {
        EnsureConfigLoaded();

        var now = Time.time;
        var isInCombat = IsEnemyInCombat(aiModule);
        var wasRecentlyDamaged = WasRecentlyDamaged(state, now);
        var hasNearbyInterest = false;
        var nearestTargetDistance = float.PositiveInfinity;
        var isPriorityEnemy = false;

        if (isInCombat)
        {
            state.combatLockUntil = now + _config.combatLockDuration;
            CacheEvaluation(state, AISimulationLevel.Full, nearestTargetDistance, hasNearbyInterest, isInCombat, false, wasRecentlyDamaged, "CombatTargetOrRecentMemory");
            return AISimulationLevel.Full;
        }

        if (state.combatLockUntil > now)
        {
            CacheEvaluation(state, AISimulationLevel.Full, nearestTargetDistance, hasNearbyInterest, isInCombat, false, wasRecentlyDamaged, "CombatLockActive");
            return AISimulationLevel.Full;
        }

        if (wasRecentlyDamaged)
        {
            CacheEvaluation(state, AISimulationLevel.Full, nearestTargetDistance, hasNearbyInterest, isInCombat, false, wasRecentlyDamaged, "RecentDamageWindow");
            return AISimulationLevel.Full;
        }

        hasNearbyInterest = InterestRegionManager.Instance.HasInterestNear(enemyActor.WorldPosition, _config.interestQueryRadius);
        if (hasNearbyInterest)
        {
            CacheEvaluation(state, AISimulationLevel.Full, nearestTargetDistance, hasNearbyInterest, isInCombat, false, wasRecentlyDamaged, "NearbyInterestRegion");
            return AISimulationLevel.Full;
        }

        nearestTargetDistance = AttackableObjectManager.Instance.GetNearestTargetDistance(enemyActor.WorldPosition);
        if (_config.logLevelChanges)
        {
            AIDebug.Log($"Enemy[{enemyActor.ObjectId}] Nearest={nearestTargetDistance}, Position={enemyActor.WorldPosition}");
        }
        if (ShouldStayFull(state.currentLevel, nearestTargetDistance))
        {
            CacheEvaluation(state, AISimulationLevel.Full, nearestTargetDistance, hasNearbyInterest, isInCombat, false, wasRecentlyDamaged, "WithinFullDistanceThreshold");
            return AISimulationLevel.Full;
        }

        if (ShouldStayReduced(state.currentLevel, nearestTargetDistance))
        {
            CacheEvaluation(state, AISimulationLevel.Reduced, nearestTargetDistance, hasNearbyInterest, isInCombat, false, wasRecentlyDamaged, "WithinReducedDistanceThreshold");
            return AISimulationLevel.Reduced;
        }

        isPriorityEnemy = IsPriorityEnemy(enemyActor);
        if (isPriorityEnemy)
        {
            CacheEvaluation(state, AISimulationLevel.Reduced, nearestTargetDistance, hasNearbyInterest, isInCombat, isPriorityEnemy, wasRecentlyDamaged, "PriorityEnemyMinimumReduced");
            return AISimulationLevel.Reduced;
        }

        CacheEvaluation(state, AISimulationLevel.Dormant, nearestTargetDistance, hasNearbyInterest, isInCombat, isPriorityEnemy, wasRecentlyDamaged, "NoActiveConditionDormant");
        return AISimulationLevel.Dormant;
    }

    /// <summary>
    /// 判断当前敌人是否处于战斗态。
    /// 当前阶段使用“当前有目标”或“仍有近期目标记忆”作为最小判断依据。
    /// </summary>
    /// <param name="aiModule">待判断的 AI 模块。</param>
    /// <returns>返回 true 表示该敌人当前应视为战斗中。</returns>
    private static bool IsEnemyInCombat(EnemyAIModule aiModule)
    {
        if (aiModule.GetCurrentTarget() != null)
            return true;

        return aiModule.HasRecentTargetMemory();
    }

    /// <summary>
    /// 判断该敌人是否属于高优先级个体。
    /// 当前阶段将精英与 Boss 至少保持在 Reduced 级别，避免完全休眠。
    /// </summary>
    /// <param name="enemyActor">待判断的敌人对象。</param>
    /// <returns>返回 true 表示该敌人属于高优先级个体。</returns>
    private static bool IsPriorityEnemy(EnemyActor enemyActor)
    {
        var attributeModule = enemyActor.AIModule?.GetAttributeModule();
        if (attributeModule == null)
            return false;

        var rank = attributeModule.GetMonsterRank();
        return rank == MonsterRank.Elite || rank == MonsterRank.Boss;
    }

    /// <summary>
    /// 判断当前是否应维持或进入 Full 级别。
    /// </summary>
    /// <param name="currentLevel">当前仿真级别。</param>
    /// <param name="nearestTargetDistance">距最近可攻击对象的距离。</param>
    /// <returns>返回 true 表示应处于 Full 级别。</returns>
    private static bool ShouldStayFull(AISimulationLevel currentLevel, float nearestTargetDistance)
    {
        var config = Instance.GetConfig();
        var threshold = currentLevel == AISimulationLevel.Full ? config.fullExitDistance : config.fullEnterDistance;
        return nearestTargetDistance <= threshold;
    }

    /// <summary>
    /// 判断当前是否应维持或进入 Reduced 级别。
    /// </summary>
    /// <param name="currentLevel">当前仿真级别。</param>
    /// <param name="nearestTargetDistance">距最近可攻击对象的距离。</param>
    /// <returns>返回 true 表示应处于 Reduced 级别。</returns>
    private static bool ShouldStayReduced(AISimulationLevel currentLevel, float nearestTargetDistance)
    {
        var config = Instance.GetConfig();
        var threshold = currentLevel == AISimulationLevel.Dormant ? config.reducedEnterDistance : config.reducedExitDistance;
        return nearestTargetDistance <= threshold;
    }

    /// <summary>
    /// 判断本轮是否允许切换仿真级别。
    /// </summary>
    /// <param name="state">敌人的调度状态缓存。</param>
    /// <param name="desiredLevel">本轮期望级别。</param>
    /// <returns>返回 true 表示本轮允许切换级别。</returns>
    private static bool ShouldChangeLevel(SchedulerState state, AISimulationLevel desiredLevel)
    {
        if (state.currentLevel == desiredLevel)
            return false;

        if (Time.time - state.lastLevelChangeTime < Instance.GetConfig().minLevelHoldDuration)
            return false;

        return true;
    }

    /// <summary>
    /// 将仿真级别映射为实际 Tick 间隔。
    /// </summary>
    /// <param name="level">当前仿真级别。</param>
    /// <returns>返回对应的服务端 Tick 间隔。</returns>
    private static float GetIntervalByLevel(AISimulationLevel level)
    {
        var config = Instance.GetConfig();
        return level switch
        {
            AISimulationLevel.Full => config.fullTickInterval,
            AISimulationLevel.Reduced => config.reducedTickInterval,
            AISimulationLevel.Dormant => config.dormantTickInterval,
            _ => config.fullTickInterval
        };
    }

    /// <summary>
    /// 处理统一受击事件。
    /// 当前阶段在这里将战斗热点写入 InterestRegionManager，并把相关敌人标记为“最近受击/活跃”。
    /// </summary>
    /// <param name="evt">服务端属性模块抛出的受击事件参数。</param>
    private void OnUnitDamaged(UnitDamagedEvt evt)
    {
        EnsureConfigLoaded();

        RegisterCombatHotspot(evt);
        MarkEnemyRecentlyDamaged(evt.targetId);
        MarkEnemyRecentlyDamaged(evt.instigatorId);
    }

    /// <summary>
    /// 注册或刷新战斗热点。
    /// 当前阶段使用受击双方的世界坐标估算热点中心，为多人局的仿真唤醒提供统一公共来源。
    /// </summary>
    /// <param name="evt">服务端受击事件。</param>
    private void RegisterCombatHotspot(UnitDamagedEvt evt)
    {
        var hasTargetPosition = TryGetWorldPosition(evt.targetId, out var targetPosition);
        var hasInstigatorPosition = TryGetWorldPosition(evt.instigatorId, out var instigatorPosition);
        if (!hasTargetPosition && !hasInstigatorPosition)
            return;

        var hotspotCenter = hasTargetPosition && hasInstigatorPosition
            ? (targetPosition + instigatorPosition) * 0.5f
            : hasTargetPosition ? targetPosition : instigatorPosition;

        var sourceObjectId = evt.targetId != 0 ? evt.targetId : evt.instigatorId;
        InterestRegionManager.Instance.RegisterCombatRegion(
            hotspotCenter,
            _config.combatHotspotRadius,
            _config.combatHotspotDuration,
            sourceObjectId,
            "UnitDamaged");
    }

    /// <summary>
    /// 将指定网络对象对应的敌人标记为最近受击。
    /// 这里同时覆盖“被打到的敌人”和“造成伤害的敌人”，确保双方在短时间内保持高仿真活跃度。
    /// </summary>
    /// <param name="networkObjectId">事件中的网络对象 ID。</param>
    private void MarkEnemyRecentlyDamaged(ulong networkObjectId)
    {
        if (!TryResolveEnemyActor(networkObjectId, out var enemyActor))
            return;

        RegisterEnemy(enemyActor);
        if (_states.TryGetValue(enemyActor.ObjectId, out var state))
        {
            state.lastDamagedTime = Time.time;
            state.combatLockUntil = Mathf.Max(state.combatLockUntil, Time.time + _config.combatLockDuration);
        }
    }

    /// <summary>
    /// 判断指定敌人是否仍位于最近受击活跃窗口内。
    /// </summary>
    /// <param name="state">敌人的调度状态缓存。</param>
    /// <param name="now">当前时间戳。</param>
    /// <returns>返回 true 表示当前仍应视为最近受击状态。</returns>
    private bool WasRecentlyDamaged(SchedulerState state, float now)
    {
        return now - state.lastDamagedTime <= _config.recentDamageMemoryDuration;
    }

    /// <summary>
    /// 缓存最近一次调度评估的判定结果。
    /// 这样调试查询可以读取上轮真实评估结果，而不会为了取数再次触发一轮逻辑计算。
    /// </summary>
    /// <param name="state">要写入的调度状态缓存。</param>
    /// <param name="desiredLevel">本轮期望的仿真级别。</param>
    /// <param name="nearestTargetDistance">最近目标距离。</param>
    /// <param name="hasNearbyInterest">是否存在附近热点。</param>
    /// <param name="isInCombat">是否处于战斗中。</param>
    /// <param name="isPriorityEnemy">是否为高优先级敌人。</param>
    /// <param name="wasRecentlyDamaged">是否仍处于最近受击窗口。</param>
    /// <param name="decisionReason">本轮命中的主要判定原因。</param>
    private static void CacheEvaluation(
        SchedulerState state,
        AISimulationLevel desiredLevel,
        float nearestTargetDistance,
        bool hasNearbyInterest,
        bool isInCombat,
        bool isPriorityEnemy,
        bool wasRecentlyDamaged,
        string decisionReason)
    {
        state.lastDesiredLevel = desiredLevel;
        state.lastNearestTargetDistance = nearestTargetDistance;
        state.lastHadNearbyInterest = hasNearbyInterest;
        state.lastWasInCombat = isInCombat;
        state.lastWasPriorityEnemy = isPriorityEnemy;
        state.lastWasRecentlyDamaged = wasRecentlyDamaged;
        state.lastDecisionReason = decisionReason;
    }

    /// <summary>
    /// 尝试通过网络对象 ID 解析世界坐标。
    /// 当前阶段优先从 NetworkObjectManager 中解析网络代理，避免 AI 系统直接依赖具体场景对象枚举。
    /// </summary>
    /// <param name="networkObjectId">要解析的网络对象 ID。</param>
    /// <param name="position">输出的世界坐标。</param>
    /// <returns>返回 true 表示成功解析到世界坐标。</returns>
    private static bool TryGetWorldPosition(ulong networkObjectId, out Vector3 position)
    {
        position = default;
        if (networkObjectId == 0)
            return false;

        if (!NetworkObjectManager.Instance.TryGetNetworkProxy<NetworkProxyBase>(networkObjectId, out var proxy) || proxy == null)
            return false;

        position = proxy.transform.position;
        return true;
    }

    /// <summary>
    /// 尝试通过网络对象 ID 解析出敌人逻辑对象。
    /// 该步骤用于把统一战斗事件重新映射回单个敌人的局部调度状态。
    /// </summary>
    /// <param name="networkObjectId">事件中携带的网络对象 ID。</param>
    /// <param name="enemyActor">输出的敌人逻辑对象。</param>
    /// <returns>返回 true 表示该网络对象对应的是敌人。</returns>
    private static bool TryResolveEnemyActor(ulong networkObjectId, out EnemyActor enemyActor)
    {
        enemyActor = null;
        if (networkObjectId == 0)
            return false;

        if (!NetworkObjectManager.Instance.TryGetLogicObject<EnemyActor>(networkObjectId, out enemyActor))
            return false;

        return enemyActor != null;
    }

    /// <summary>
    /// 获取当前调度器正在使用的配置。
    /// 该接口主要供内部计算和后续调试工具读取统一参数。
    /// </summary>
    /// <returns>返回当前服务端 AI 调度配置。</returns>
    private AISchedulerConfig GetConfig()
    {
        EnsureConfigLoaded();
        return _config;
    }

    /// <summary>
    /// 确保调度配置已经完成加载。
    /// 之所以保留该保护，是为了兼容单例可能被不同入口首次触发访问的情况。
    /// </summary>
    private void EnsureConfigLoaded()
    {
        if (_config == null)
        {
            _config = AISchedulerConfig.LoadOrCreate();
        }
    }
}
