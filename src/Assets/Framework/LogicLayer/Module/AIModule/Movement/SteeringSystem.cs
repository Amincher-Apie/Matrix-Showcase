using System.Collections.Generic;
using Framework.LogicLayer.Module.AIModule.Movement.Boids;
using UnityEngine;

/// <summary>
/// 敌人群体移动修正系统。
/// 该系统只负责在服务端对 AI 已经产出的移动方向做轻量修正，不参与状态机决策，也不直接执行位移。
/// 
/// 升级说明：
/// 自 Boids 优化版本起，本系统集成了 BoidsCentralController 的中央统一计算能力。
/// 当 Boids 启用时，优先使用 Boids 结果替代原有简单分离逻辑，获得更自然的群体行为效果。
/// 空间哈希查询结果由 BoidsCentralController 统一维护，SteeringSystem 无需重复构建。
/// </summary>
public class SteeringSystem
{
    /// <summary>
    /// 默认空间桶最小尺寸。
    /// 该常量用于兜底，避免配置异常时出现零或负数尺寸导致的桶划分错误。
    /// </summary>
    private const float MinSpatialBucketSize = 0.5f;

    /// <summary>
    /// Boids 结果与原有 steering 结果的混合权重。
    /// 0.0 表示完全使用原有 steering，1.0 表示完全使用 Boids 结果。
    /// </summary>
    private const float DefaultBoidsBlendWeight = 0.4f;

    /// <summary>
    /// 全局单例实例。
    /// 当前阶段继续采用轻量单例，便于在不改动现有模块装配方式的前提下平滑接入。
    /// </summary>
    public static SteeringSystem Instance { get; } = new SteeringSystem();

    /// <summary>
    /// 当前已注册到服务端群体修正系统中的敌人集合。
    /// </summary>
    private readonly HashSet<EnemyActor> _registeredEnemies = new HashSet<EnemyActor>();

    /// <summary>
    /// 用于临时收集邻居的缓冲区，避免频繁分配。
    /// </summary>
    private readonly List<EnemyActor> _neighborBuffer = new List<EnemyActor>(16);

    /// <summary>
    /// 当前帧构建出的空间桶索引。
    /// 该结构用于把邻居查询从全量扫描收口到局部桶查询，降低多人局中多敌人 steering 的常规开销。
    /// </summary>
    private readonly Dictionary<Vector2Int, List<EnemyActor>> _spatialBuckets = new Dictionary<Vector2Int, List<EnemyActor>>();

    /// <summary>
    /// 上一次构建空间桶时使用的帧号。
    /// </summary>
    private int _lastBucketBuildFrame = -1;

    /// <summary>
    /// 上一次构建空间桶时使用的桶尺寸。
    /// </summary>
    private float _lastBucketSize = -1f;

    /// <summary>
    /// 私有构造函数，禁止外部直接创建实例。
    /// </summary>
    private SteeringSystem()
    {
    }

    /// <summary>
    /// 将敌人注册到群体移动修正系统。
    /// 只有注册后的敌人才会参与彼此之间的分离计算。
    /// 同时会将敌人注册到 BoidsCentralController 以参与中央统一计算。
    /// </summary>
    /// <param name="enemy">需要参与群体修正的敌人对象。</param>
    public void RegisterEnemy(EnemyActor enemy)
    {
        if (enemy == null)
            return;

        _registeredEnemies.Add(enemy);
        InvalidateSpatialBuckets();

        if (BoidsCentralController.Instance != null)
        {
            BoidsCentralController.Instance.RegisterEnemy(enemy);
        }
    }

    /// <summary>
    /// 将敌人从群体移动修正系统中注销。
    /// 用于网络反生成、回收或销毁时避免旧引用残留。
    /// 同时会从 BoidsCentralController 注销。
    /// </summary>
    /// <param name="enemy">需要移除的敌人对象。</param>
    public void UnregisterEnemy(EnemyActor enemy)
    {
        if (enemy == null)
            return;

        _registeredEnemies.Remove(enemy);
        InvalidateSpatialBuckets();

        if (BoidsCentralController.Instance != null)
        {
            BoidsCentralController.Instance.UnregisterEnemy(enemy);
        }
    }

    /// <summary>
    /// 根据期望移动方向计算最终修正后的移动方向。
    /// 当前实现支持两种模式：
    /// 1. Boids 模式（默认）：优先从 BoidsCentralController 获取预计算的 Boids 结果，实现更自然的群体行为。
    /// 2. 兼容模式：当 Boids 不可用或未启用时，回退到原有的简单分离 + 避障逻辑。
    /// </summary>
    /// <param name="owner">发起本次修正的敌人对象。</param>
    /// <param name="config">该敌人使用的 AI 配置。</param>
    /// <param name="desiredDirection">状态机或路径系统给出的原始期望方向。</param>
    /// <param name="debugInfo">输出本次 steering 的调试快照。</param>
    /// <returns>返回已归一化的最终移动方向；若无法产生有效方向则返回零向量。</returns>
    public Vector3 ResolveMoveDirection(EnemyActor owner, EnemyAIConfig config, Vector3 desiredDirection, out AISteeringDebugInfo debugInfo)
    {
        debugInfo = default;
        if (owner == null)
            return Vector3.zero;

        var planarDesiredDirection = FlattenDirection(desiredDirection);
        debugInfo.desiredDirection = planarDesiredDirection;
        if (planarDesiredDirection.sqrMagnitude <= 1e-4f)
        {
            debugInfo.finalDirection = Vector3.zero;
            debugInfo.lastUpdateTime = Time.time;
            return Vector3.zero;
        }

        if (config == null || !config.enableSteeringCorrection)
        {
            debugInfo.finalDirection = planarDesiredDirection;
            debugInfo.lastUpdateTime = Time.time;
            return planarDesiredDirection;
        }

        var useBoids = ShouldUseBoids(config);
        if (useBoids)
        {
            return ResolveWithBoids(owner, config, planarDesiredDirection, ref debugInfo);
        }

        return ResolveWithLegacySteering(owner, config, planarDesiredDirection, ref debugInfo);
    }

    /// <summary>
    /// 判断当前是否应使用 Boids 计算。
    /// </summary>
    private bool ShouldUseBoids(EnemyAIConfig config)
    {
        if (config == null)
            return false;

        if (!config.enableSteeringCorrection)
            return false;

        if (config.boidsConfig == null || !config.boidsConfig.enableBoids)
            return false;

        if (BoidsCentralController.Instance == null)
            return false;

        return true;
    }

    /// <summary>
    /// 使用 Boids 中央控制器计算最终方向。
    /// 该路径通过 BoidsCentralController 获取预计算的分离/聚集/对齐合力，叠加避障修正后返回。
    /// </summary>
    private Vector3 ResolveWithBoids(EnemyActor owner, EnemyAIConfig config, Vector3 planarDesiredDirection, ref AISteeringDebugInfo debugInfo)
    {
        var boidsResult = Vector3.zero;
        var usedBoids = BoidsCentralController.Instance.TryGetBoidsResult(owner, planarDesiredDirection, out boidsResult);

        var boidsWeight = config.boidsBlendWeight;
        var legacySteeringResult = ResolveWithLegacySteeringOnlySeparation(owner, config, planarDesiredDirection, out var legacySeparationDirection, out var neighborCount, out var usedSpatialBuckets, out var usedSameRegionFilter);
        var obstacleAvoidanceDirection = CalculateObstacleAvoidanceDirection(owner, config, planarDesiredDirection, out var obstacleDetected);

        Vector3 combinedDirection;
        int totalNeighborCount = neighborCount;
        bool usedSpatial = usedSpatialBuckets || usedSameRegionFilter;

        if (usedBoids && boidsResult.sqrMagnitude > 1e-4f)
        {
            combinedDirection = planarDesiredDirection;
            combinedDirection += boidsResult * boidsWeight;
            combinedDirection += legacySteeringResult * (1f - boidsWeight) * 0.3f;
            combinedDirection += obstacleAvoidanceDirection * Mathf.Max(0f, config.steeringObstacleAvoidanceWeight);

            if (BoidsCentralController.Instance.TryGetAgentDebugInfo(owner.ObjectId, out var boidsDebug))
            {
                totalNeighborCount = boidsDebug.separationNeighbors + boidsDebug.cohesionNeighbors + boidsDebug.alignmentNeighbors;
            }
        }
        else
        {
            combinedDirection = legacySteeringResult;
            combinedDirection += obstacleAvoidanceDirection * Mathf.Max(0f, config.steeringObstacleAvoidanceWeight);
        }

        var finalDirection = FlattenDirection(combinedDirection);
        debugInfo.neighborCount = totalNeighborCount;
        debugInfo.usedSpatialBuckets = usedSpatial;
        debugInfo.usedSameRegionFilter = usedSameRegionFilter;
        debugInfo.separationDirection = usedBoids ? boidsResult : legacySeparationDirection;
        debugInfo.obstacleAvoidanceDirection = obstacleAvoidanceDirection;
        debugInfo.obstacleDetected = obstacleDetected;
        debugInfo.finalDirection = finalDirection;
        debugInfo.lastUpdateTime = Time.time;
        return finalDirection;
    }

    /// <summary>
    /// 仅计算分离力（不含避障），用于 Boids 混合模式。
    /// </summary>
    private Vector3 ResolveWithLegacySteeringOnlySeparation(
        EnemyActor owner,
        EnemyAIConfig config,
        Vector3 planarDesiredDirection,
        out Vector3 separationDirection,
        out int neighborCount,
        out bool usedSpatialBuckets,
        out bool usedSameRegionFilter)
    {
        separationDirection = CalculateSeparationDirection(
            owner,
            config,
            out neighborCount,
            out usedSpatialBuckets,
            out usedSameRegionFilter);

        var combined = planarDesiredDirection;
        combined += separationDirection * Mathf.Max(0f, config.steeringSeparationWeight);
        return FlattenDirection(combined);
    }

    /// <summary>
    /// 使用原有简单 steering 逻辑计算最终方向。
    /// 该路径包含分离 + 避障，用于 Boids 不可用时的回退。
    /// </summary>
    private Vector3 ResolveWithLegacySteering(
        EnemyActor owner,
        EnemyAIConfig config,
        Vector3 planarDesiredDirection,
        ref AISteeringDebugInfo debugInfo)
    {
        var separationDirection = CalculateSeparationDirection(
            owner,
            config,
            out var neighborCount,
            out var usedSpatialBuckets,
            out var usedSameRegionFilter);
        var obstacleAvoidanceDirection = CalculateObstacleAvoidanceDirection(
            owner,
            config,
            planarDesiredDirection,
            out var obstacleDetected);

        var combinedDirection = planarDesiredDirection;
        combinedDirection += separationDirection * Mathf.Max(0f, config.steeringSeparationWeight);
        combinedDirection += obstacleAvoidanceDirection * Mathf.Max(0f, config.steeringObstacleAvoidanceWeight);

        var finalDirection = FlattenDirection(combinedDirection);
        debugInfo.neighborCount = neighborCount;
        debugInfo.usedSpatialBuckets = usedSpatialBuckets;
        debugInfo.usedSameRegionFilter = usedSameRegionFilter;
        debugInfo.separationDirection = separationDirection;
        debugInfo.obstacleAvoidanceDirection = obstacleAvoidanceDirection;
        debugInfo.obstacleDetected = obstacleDetected;
        debugInfo.finalDirection = finalDirection;
        debugInfo.lastUpdateTime = Time.time;
        return finalDirection;
    }

    /// <summary>
    /// 根据期望移动方向计算最终修正后的移动方向。
    /// 该重载主要用于兼容当前未显式消费调试快照的调用方。
    /// </summary>
    /// <param name="owner">发起本次修正的敌人对象。</param>
    /// <param name="config">该敌人使用的 AI 配置。</param>
    /// <param name="desiredDirection">状态机或路径系统给出的原始期望方向。</param>
    /// <returns>返回已归一化的最终移动方向；若无法产生有效方向则返回零向量。</returns>
    public Vector3 ResolveMoveDirection(EnemyActor owner, EnemyAIConfig config, Vector3 desiredDirection)
    {
        return ResolveMoveDirection(owner, config, desiredDirection, out _);
    }

    /// <summary>
    /// 计算当前敌人应施加的分离修正方向。
    /// 当周围邻居过近时，系统会生成一个远离邻居中心的修正向量。
    /// </summary>
    /// <param name="owner">需要计算分离修正的敌人对象。</param>
    /// <param name="config">该敌人的 AI 配置。</param>
    /// <param name="neighborCount">输出本次命中的有效邻居数量。</param>
    /// <param name="usedSpatialBuckets">输出本次是否使用了空间桶查询。</param>
    /// <param name="usedSameRegionFilter">输出本次是否启用了同房间过滤。</param>
    /// <returns>返回已归一化的分离修正方向；若无需分离则返回零向量。</returns>
    private Vector3 CalculateSeparationDirection(
        EnemyActor owner,
        EnemyAIConfig config,
        out int neighborCount,
        out bool usedSpatialBuckets,
        out bool usedSameRegionFilter)
    {
        CollectNeighbors(
            owner,
            config.steeringNeighborRadius,
            config.steeringSpatialBucketSize,
            config.steeringLimitToSameRegionWhenPossible,
            out usedSpatialBuckets,
            out usedSameRegionFilter);

        neighborCount = _neighborBuffer.Count;
        if (_neighborBuffer.Count == 0)
            return Vector3.zero;

        var ownerPosition = owner.WorldPosition;
        var separation = Vector3.zero;

        for (var i = 0; i < _neighborBuffer.Count; i++)
        {
            var neighbor = _neighborBuffer[i];
            var offset = ownerPosition - neighbor.transform.position;
            offset.y = 0f;

            var distance = offset.magnitude;
            if (distance <= 1e-4f || distance > config.steeringSeparationRadius)
                continue;

            var normalizedDistance = 1f - (distance / Mathf.Max(config.steeringSeparationRadius, 1e-4f));
            separation += offset.normalized * normalizedDistance;
        }

        return FlattenDirection(separation);
    }

    /// <summary>
    /// 计算当前敌人的简单避障方向。
    /// 若正前方存在障碍，则依次尝试左右偏转方向，寻找一个当前帧更安全的移动朝向。
    /// 当正面和左右都被阻挡时，会尝试沿墙壁滑动。
    /// </summary>
    /// <param name="owner">需要进行避障检测的敌人对象。</param>
    /// <param name="config">该敌人的 AI 配置。</param>
    /// <param name="desiredDirection">原始期望前进方向。</param>
    /// <param name="obstacleDetected">输出本次是否检测到正前方障碍。</param>
    /// <returns>返回避障修正方向；若当前无需避障则返回零向量。</returns>
    private Vector3 CalculateObstacleAvoidanceDirection(
        EnemyActor owner,
        EnemyAIConfig config,
        Vector3 desiredDirection,
        out bool obstacleDetected)
    {
        obstacleDetected = false;
        if (!config.enableObstacleAvoidance)
            return Vector3.zero;

        if (config.steeringObstacleCheckDistance <= 0f)
            return Vector3.zero;

        var origin = owner.WorldPosition + Vector3.up * 0.5f;
        var checkDist = Mathf.Max(config.steeringObstacleCheckDistance, 1.5f); // 至少检测1.5米

        // 检查正前方
        if (!Physics.Raycast(origin, desiredDirection, out _, checkDist, config.obstacleLayerMask))
            return Vector3.zero;

        obstacleDetected = true;

        // 尝试左右方向
        var leftDirection = Quaternion.Euler(0f, -config.steeringAvoidanceProbeAngle, 0f) * desiredDirection;
        if (!Physics.Raycast(origin, leftDirection, out _, checkDist, config.obstacleLayerMask))
            return FlattenDirection(leftDirection);

        var rightDirection = Quaternion.Euler(0f, config.steeringAvoidanceProbeAngle, 0f) * desiredDirection;
        if (!Physics.Raycast(origin, rightDirection, out _, checkDist, config.obstacleLayerMask))
            return FlattenDirection(rightDirection);

        // 三个方向都被挡住，尝试沿墙壁滑动
        // 检测墙壁法线
        if (Physics.Raycast(origin, desiredDirection, out var hit, checkDist, config.obstacleLayerMask))
        {
            var wallNormal = hit.normal;
            wallNormal.y = 0f;
            if (wallNormal.sqrMagnitude > 1e-4f)
            {
                wallNormal.Normalize();

                // 计算沿墙滑动方向（墙壁法线的垂直方向）
                var slideDir = Vector3.Cross(Vector3.up, wallNormal).normalized;

                // 尝试左右两个滑动方向
                var leftSlide = -slideDir;
                var rightSlide = slideDir;

                // 选择更接近原始移动方向的那个
                var slideDir1 = Vector3.Dot(leftSlide, desiredDirection) > Vector3.Dot(rightSlide, desiredDirection) ? leftSlide : rightSlide;

                // 检测滑动方向是否畅通
                var extendedCheckDist = checkDist * 1.5f;
                if (!Physics.Raycast(origin, slideDir1, out _, extendedCheckDist, config.obstacleLayerMask))
                {
                    AIDebug.LogChannel("AI.Steering", $"[SteeringSystem] 墙壁滑动: {slideDir1}");
                    return FlattenDirection(slideDir1);
                }

                // 两个滑动方向都被堵，返回沿墙法线方向（尝试穿过）
                return FlattenDirection(wallNormal);
            }
        }

        // 完全无法移动，返回零向量
        AIDebug.LogWarning($"[SteeringSystem] 敌人 {owner.ObjectId} 被完全困住！");
        return Vector3.zero;
    }

    /// <summary>
    /// 收集当前敌人邻近范围内的其他敌人。
    /// 该方法会优先使用空间桶限制候选范围，并在可用时按房间拓扑过滤掉跨房间敌人。
    /// </summary>
    /// <param name="owner">本次查询的中心敌人对象。</param>
    /// <param name="neighborRadius">用于判定邻居的最大半径。</param>
    /// <param name="bucketSize">本次查询使用的空间桶尺寸。</param>
    /// <param name="limitToSameRegionWhenPossible">当房间拓扑可用时，是否仅对同房间敌人生效。</param>
    /// <param name="usedSpatialBuckets">输出本次是否使用了空间桶查询。</param>
    /// <param name="usedSameRegionFilter">输出本次是否启用了同房间过滤。</param>
    private void CollectNeighbors(
        EnemyActor owner,
        float neighborRadius,
        float bucketSize,
        bool limitToSameRegionWhenPossible,
        out bool usedSpatialBuckets,
        out bool usedSameRegionFilter)
    {
        _neighborBuffer.Clear();
        usedSpatialBuckets = false;
        usedSameRegionFilter = false;
        if (neighborRadius <= 0f)
            return;

        bucketSize = Mathf.Max(MinSpatialBucketSize, bucketSize);
        EnsureSpatialBuckets(bucketSize);
        usedSpatialBuckets = true;

        var ownerPosition = owner.WorldPosition;
        var maxSqrDistance = neighborRadius * neighborRadius;
        var ownerBucket = GetBucketKey(ownerPosition, bucketSize);
        var bucketRange = Mathf.CeilToInt(neighborRadius / bucketSize);
        var topologyService = PCGMapTopologyService.Instance;
        var ownerRegionId = -1;
        var canUseSameRegionFilter = limitToSameRegionWhenPossible
                                     && topologyService != null
                                     && topologyService.HasProvider()
                                     && topologyService.TryResolveRegion(ownerPosition, out ownerRegionId);
        usedSameRegionFilter = canUseSameRegionFilter;

        for (var x = ownerBucket.x - bucketRange; x <= ownerBucket.x + bucketRange; x++)
        {
            for (var y = ownerBucket.y - bucketRange; y <= ownerBucket.y + bucketRange; y++)
            {
                if (!_spatialBuckets.TryGetValue(new Vector2Int(x, y), out var bucketEnemies))
                    continue;

                for (var i = 0; i < bucketEnemies.Count; i++)
                {
                    var enemy = bucketEnemies[i];
                    if (enemy == null || enemy == owner || !enemy.isActiveAndEnabled)
                        continue;

                    var enemyRegionId = -1;
                    if (canUseSameRegionFilter
                        && !topologyService.TryResolveRegion(enemy.transform.position, out enemyRegionId))
                    {
                        continue;
                    }

                    if (canUseSameRegionFilter && enemyRegionId != ownerRegionId)
                        continue;

                    var offset = enemy.transform.position - ownerPosition;
                    offset.y = 0f;
                    if (offset.sqrMagnitude > maxSqrDistance)
                        continue;

                    _neighborBuffer.Add(enemy);
                }
            }
        }
    }

    /// <summary>
    /// 确保当前帧的空间桶索引已经构建完成。
    /// 当帧号或桶尺寸变化时会自动重建，保证 steering 查询读取到的是当前帧近似最新的位置分布。
    /// </summary>
    /// <param name="bucketSize">本次查询希望使用的桶尺寸。</param>
    private void EnsureSpatialBuckets(float bucketSize)
    {
        if (_lastBucketBuildFrame == Time.frameCount && Mathf.Approximately(_lastBucketSize, bucketSize))
            return;

        _spatialBuckets.Clear();
        _lastBucketBuildFrame = Time.frameCount;
        _lastBucketSize = bucketSize;

        foreach (var enemy in _registeredEnemies)
        {
            if (enemy == null || !enemy.isActiveAndEnabled)
                continue;

            var bucketKey = GetBucketKey(enemy.transform.position, bucketSize);
            if (!_spatialBuckets.TryGetValue(bucketKey, out var bucketEnemies))
            {
                bucketEnemies = new List<EnemyActor>(4);
                _spatialBuckets.Add(bucketKey, bucketEnemies);
            }

            bucketEnemies.Add(enemy);
        }
    }

    /// <summary>
    /// 使空间桶索引失效。
    /// 当敌人注册表发生变化时，主动让系统在下一次查询时重建桶索引，避免读取过期集合。
    /// </summary>
    private void InvalidateSpatialBuckets()
    {
        _lastBucketBuildFrame = -1;
        _lastBucketSize = -1f;
        _spatialBuckets.Clear();
    }

    /// <summary>
    /// 根据世界坐标计算所属空间桶键值。
    /// </summary>
    /// <param name="position">需要映射的世界坐标。</param>
    /// <param name="bucketSize">当前空间桶尺寸。</param>
    /// <returns>返回二维平面上的桶键值。</returns>
    private static Vector2Int GetBucketKey(Vector3 position, float bucketSize)
    {
        return new Vector2Int(
            Mathf.FloorToInt(position.x / bucketSize),
            Mathf.FloorToInt(position.z / bucketSize));
    }

    /// <summary>
    /// 将输入方向压平到水平面并归一化。
    /// 这样可以确保群体修正不会意外引入竖直方向位移。
    /// </summary>
    /// <param name="direction">需要压平处理的输入方向。</param>
    /// <returns>返回水平面上的归一化方向；若长度过小则返回零向量。</returns>
    private static Vector3 FlattenDirection(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude <= 1e-4f)
            return Vector3.zero;

        return direction.normalized;
    }
}
