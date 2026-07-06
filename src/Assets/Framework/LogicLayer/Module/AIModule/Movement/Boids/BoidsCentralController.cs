using System.Collections.Generic;
using Framework.Singleton;
using UnityEngine;
using UnityEngine.Profiling;

namespace Framework.LogicLayer.Module.AIModule.Movement.Boids
{
    /// <summary>
    /// Boids 群体行为算法的中央控制器。
    /// 
    /// 设计目标：
    /// 1. 中央统一计算 —— 所有敌人的 Boids 力由中央控制器批量计算，避免每个敌人都独立遍历邻居列表，
    ///    从而将 O(N^2) 的邻居查询次数从每帧 N 次降低到仅需要构建一次空间哈希桶。
    /// 2. 空间哈希加速 —— 采用均匀网格哈希（Uniform Spatial Hashing）将邻居查询范围从全场景收窄到
    ///    周边若干个网格单元，适合服务器端批量更新。
    /// 3. 与现有 SteeringSystem 无缝集成 —— 不替换现有 steering 逻辑，而是将 Boids 结果注入到
    ///    SteeringSystem 的 ResolveMoveDirection 流程中。
    /// 4. 调度感知 —— 能够根据 AIScheduler 的仿真级别（Full/Reduced/Dormant）动态跳过或简化 Boids 计算。
    /// 
    /// 算法说明：
    /// - 分离 (Separation)：每个个体主动远离过近的邻居，防止堆积和相互穿透。
    /// - 聚集 (Cohesion)：每个个体倾向移动到本地邻居群体的几何中心，自然形成集群。
    /// - 对齐 (Alignment)：每个个体的速度方向趋向于邻居的平均方向，产生方向一致的群体流动。
    /// 
    /// 使用方式：
    /// 1. 在服务端初始化阶段调用 BoidsCentralController.Instance.Initialize()（若未自动初始化）。
    /// 2. 在每个 AI Tick 前（或每帧服务端更新中），调用 BatchComputeBoidsForAllRegistered()。
    /// 3. SteeringSystem 在执行分离/对齐/聚集时，优先从 BoidsCentralController 读取预计算结果，
    ///    而非自行遍历邻居。
    /// </summary>
    public class BoidsCentralController : SingletonBase<BoidsCentralController>
    {
        /// <summary>
        /// 默认空间哈希网格最小尺寸，防止除零或过小导致的性能问题。
        /// </summary>
        private const float MinSpatialHashCellSize = 0.25f;

        /// <summary>
        /// 分离缓冲区默认初始容量。
        /// </summary>
        private const int DefaultSeparationBufferCapacity = 16;

        /// <summary>
        /// 聚集/对齐缓冲区默认初始容量。
        /// </summary>
        private const int DefaultCohesionAlignmentBufferCapacity = 16;

        /// <summary>
        /// 区域 ID 未分配时的默认值。
        /// </summary>
        private const int UnassignedRegionId = -1;

        /// <summary>
        /// 全局 Boids 配置。
        /// 当前阶段使用统一配置，后续可扩展为按敌人类型分配不同配置。
        /// </summary>
        private BoidsConfig _config;

        /// <summary>
        /// 已注册参与 Boids 计算的敌人集合。
        /// </summary>
        private readonly HashSet<EnemyActor> _registeredEnemies = new HashSet<EnemyActor>();

        /// <summary>
        /// 敌人 ID 到单帧数据的映射。
        /// </summary>
        private readonly Dictionary<ulong, BoidsAgentData> _agentDataMap = new Dictionary<ulong, BoidsAgentData>();

        /// <summary>
        /// 敌人 ID 到调试快照的映射。
        /// </summary>
        private readonly Dictionary<ulong, BoidsAgentDebugInfo> _agentDebugInfoMap = new Dictionary<ulong, BoidsAgentDebugInfo>();

        /// <summary>
        /// 空间哈希桶：网格键 -> 该桶内所有敌人的列表。
        /// </summary>
        private readonly Dictionary<Vector2Int, List<EnemyActor>> _spatialHashGrid = new Dictionary<Vector2Int, List<EnemyActor>>();

        /// <summary>
        /// 空间哈希键缓存，用于避免每帧重复分配 Vector2Int。
        /// </summary>
        private readonly Dictionary<Vector2Int, Vector2Int> _cachedHashKeys = new Dictionary<Vector2Int, Vector2Int>();

        /// <summary>
        /// 分离力计算的临时缓冲区（预分配避免 GC）。
        /// </summary>
        private readonly List<EnemyActor> _separationNeighborBuffer = new List<EnemyActor>(DefaultSeparationBufferCapacity);

        /// <summary>
        /// 聚集力计算的临时缓冲区（预分配避免 GC）。
        /// </summary>
        private readonly List<EnemyActor> _cohesionNeighborBuffer = new List<EnemyActor>(DefaultCohesionAlignmentBufferCapacity);

        /// <summary>
        /// 对齐力计算的临时缓冲区（预分配避免 GC）。
        /// </summary>
        private readonly List<EnemyActor> _alignmentNeighborBuffer = new List<EnemyActor>(DefaultCohesionAlignmentBufferCapacity);

        /// <summary>
        /// 上一帧构建空间哈希时使用的网格尺寸。
        /// 尺寸变化时需要强制重建。
        /// </summary>
        private float _lastCellSize = -1f;

        /// <summary>
        /// 上一帧构建空间哈希时使用的帧号，用于检测本帧是否已构建。
        /// </summary>
        private int _lastBuildFrame = -1;

        /// <summary>
        /// 是否已完成首次初始化。
        /// </summary>
        private bool _isInitialized = false;

        /// <summary>
        /// 当前帧处理的个体数量（用于调试）。
        /// </summary>
        private int _processedCount = 0;

        /// <summary>
        /// 当前帧总邻居查询次数（用于调试）。
        /// </summary>
        private int _totalNeighborQueries = 0;

        /// <summary>
        /// 是否命中了空间哈希缓存。
        /// </summary>
        private bool _usedSpatialHashCache = false;

        /// <summary>
        /// 全局 Boids 调试信息快照。
        /// </summary>
        private BoidsDebugInfo _debugInfo;

        /// <summary>
        /// 最后一次计算耗时（毫秒）。
        /// </summary>
        private float _lastComputeTimeMs = 0f;

        protected override void Initialize()
        {
            _config = BoidsConfig.CreateVisualFirst();
            _isInitialized = true;

            _separationNeighborBuffer.Clear();
            _cohesionNeighborBuffer.Clear();
            _alignmentNeighborBuffer.Clear();
            _spatialHashGrid.Clear();
            _agentDataMap.Clear();
            _agentDebugInfoMap.Clear();
            _registeredEnemies.Clear();

            _debugInfo = BoidsDebugInfo.CreateDefault();
            _debugInfo.boidsEnabled = _config.enableBoids;

            _lastCellSize = -1f;
            _lastBuildFrame = -1;
        }

        /// <summary>
        /// 获取当前全局 Boids 配置。
        /// </summary>
        public BoidsConfig GetConfig()
        {
            return _config;
        }

        /// <summary>
        /// 设置全局 Boids 配置。
        /// 配置变更后，下一帧会自动反映。
        /// </summary>
        /// <param name="config">新的 Boids 配置。</param>
        public void SetConfig(BoidsConfig config)
        {
            if (config == null)
            {
                AIDebug.LogWarning("传入的 config 为 null，使用默认禁用配置。");
                _config = BoidsConfig.CreateDisabled();
                return;
            }

            _config = config;
            _debugInfo.boidsEnabled = _config.enableBoids;
            InvalidateSpatialHash();
        }

        /// <summary>
        /// 设置性能优先配置。
        /// </summary>
        public void ApplyPerformanceFirstConfig()
        {
            SetConfig(BoidsConfig.CreatePerformanceFirst());
        }

        /// <summary>
        /// 设置视觉优先配置。
        /// </summary>
        public void ApplyVisualFirstConfig()
        {
            SetConfig(BoidsConfig.CreateVisualFirst());
        }

        /// <summary>
        /// 将敌人注册到 Boids 中央控制器。
        /// 注册后的敌人才会参与群体行为计算。
        /// </summary>
        /// <param name="enemy">要注册的敌人对象。</param>
        public void RegisterEnemy(EnemyActor enemy)
        {
            if (enemy == null)
                return;

            if (_registeredEnemies.Add(enemy))
            {
                InvalidateSpatialHash();
                CacheAgentDataIfNeeded(enemy);
            }
        }

        /// <summary>
        /// 将敌人从 Boids 系统中注销。
        /// 注销后该敌人不再参与群体行为计算。
        /// </summary>
        /// <param name="enemy">要注销的敌人对象。</param>
        public void UnregisterEnemy(EnemyActor enemy)
        {
            if (enemy == null)
                return;

            if (_registeredEnemies.Remove(enemy))
            {
                var id = enemy.ObjectId;
                _agentDataMap.Remove(id);
                _agentDebugInfoMap.Remove(id);
                InvalidateSpatialHash();
            }
        }

        /// <summary>
        /// 获取指定敌人的 Boids 计算结果。
        /// 若该敌人未注册或本帧尚未计算，则返回零向量。
        /// </summary>
        /// <param name="enemy">要查询的敌人对象。</param>
        /// <param name="baseDirection">该敌人当前的基础移动方向（用于 layerOnBaseDirection 模式）。</param>
        /// <param name="result">输出的 Boids 合力方向（归一化）。</param>
        /// <returns>返回 true 表示成功获取到有效结果。</returns>
        public bool TryGetBoidsResult(EnemyActor enemy, Vector3 baseDirection, out Vector3 result)
        {
            result = Vector3.zero;
            if (!_config.enableBoids || enemy == null)
                return false;

            if (!_agentDataMap.TryGetValue(enemy.ObjectId, out var data))
                return false;

            if (!data.isActive)
                return false;

            result = data.finalBoidsDirection;

            if (_config.layerOnBaseDirection && baseDirection.sqrMagnitude > 1e-4f)
            {
                result = BlendWithBaseDirection(result, baseDirection);
            }

            return result.sqrMagnitude > 1e-4f;
        }

        /// <summary>
        /// 尝试获取指定敌人的单帧 Boids 数据。
        /// </summary>
        /// <param name="objectId">敌人的网络对象 ID。</param>
        /// <param name="data">输出的 Boids 数据。</param>
        /// <returns>返回 true 表示成功获取。</returns>
        public bool TryGetAgentData(ulong objectId, out BoidsAgentData data)
        {
            return _agentDataMap.TryGetValue(objectId, out data);
        }

        /// <summary>
        /// 尝试获取指定敌人的调试快照。
        /// </summary>
        /// <param name="objectId">敌人的网络对象 ID。</param>
        /// <param name="debugInfo">输出的调试信息。</param>
        /// <returns>返回 true 表示成功获取。</returns>
        public bool TryGetAgentDebugInfo(ulong objectId, out BoidsAgentDebugInfo debugInfo)
        {
            return _agentDebugInfoMap.TryGetValue(objectId, out debugInfo);
        }

        /// <summary>
        /// 获取全局调试信息快照。
        /// </summary>
        public BoidsDebugInfo GetDebugInfo()
        {
            return _debugInfo;
        }

        /// <summary>
        /// 批量计算所有已注册敌人的 Boids 力。
        /// 该方法应在服务端每帧 AI Tick 之前调用一次，由 AIScheduler 或服务端主更新循环驱动。
        /// 
        /// 调用流程：
        /// 1. 根据当前配置和调度级别决定是否执行计算。
        /// 2. 构建空间哈希桶（仅本帧首次调用时）。
        /// 3. 遍历所有已注册的敌人，分别计算其分离、聚集、对齐力。
        /// 4. 汇总各力得到最终方向，写入 BoidsAgentData。
        /// </summary>
        /// <param name="currentSimulationLevel">当前 AIScheduler 决定的仿真级别。</param>
        public void BatchComputeBoidsForAllRegistered(AISimulationLevel currentSimulationLevel = AISimulationLevel.Full)
        {
            if (!_isInitialized)
                Initialize();

            _debugInfo.boidsEnabled = _config.enableBoids;
            _debugInfo.currentSimulationLevel = currentSimulationLevel;
            _debugInfo.lastUpdateTime = Time.time;

            if (!_config.enableBoids)
            {
                _debugInfo.activeAgentCount = _registeredEnemies.Count;
                _debugInfo.processedAgentCount = 0;
                return;
            }

            if (_registeredEnemies.Count == 0)
            {
                _debugInfo.activeAgentCount = 0;
                _debugInfo.processedAgentCount = 0;
                return;
            }

            if (_config.skipBoidsWhenReducedSimulation && currentSimulationLevel == AISimulationLevel.Dormant)
            {
                _debugInfo.activeAgentCount = _registeredEnemies.Count;
                _debugInfo.processedAgentCount = 0;
                return;
            }

            Profiler.BeginSample("[Boids] BatchCompute");

            var sw = new System.Diagnostics.Stopwatch();
            sw.Start();

            _processedCount = 0;
            _totalNeighborQueries = 0;
            _usedSpatialHashCache = false;

            _debugInfo.activeAgentCount = _registeredEnemies.Count;

            var cellSize = Mathf.Max(MinSpatialHashCellSize, _config.spatialHashCellSize);
            BuildSpatialHashGrid(cellSize, currentSimulationLevel);
            _debugInfo.spatialHashBuiltThisFrame = true;

            foreach (var enemy in _registeredEnemies)
            {
                if (enemy == null || !enemy.isActiveAndEnabled)
                    continue;

                if (_config.maxDistanceToActivateBoids > 0f)
                {
                    var nearestPlayerPos = GetNearestPlayerPosition(enemy.transform.position);
                    if (nearestPlayerPos.HasValue)
                    {
                        var dist = Vector3.Distance(enemy.transform.position, nearestPlayerPos.Value);
                        if (dist > _config.maxDistanceToActivateBoids)
                            continue;
                    }
                }

                ComputeBoidsForAgent(enemy, cellSize);
                _processedCount++;
            }

            sw.Stop();
            _lastComputeTimeMs = (float)sw.Elapsed.TotalMilliseconds;
            _debugInfo.estimatedComputeTimeMs = (float)_lastComputeTimeMs;
            _debugInfo.processedAgentCount = _processedCount;
            _debugInfo.totalNeighborQueries = _totalNeighborQueries;
            _debugInfo.spatialHashHitRate = _processedCount > 0 ? (float)_totalNeighborQueries / (_processedCount * 3f) : 0f;

            if (_processedCount > 0)
            {
                var lastId = GetLastProcessedObjectId();
                if (_agentDebugInfoMap.TryGetValue(lastId, out var representativeDebug))
                {
                    _debugInfo.lastSeparationForce = representativeDebug.separationForce;
                    _debugInfo.lastCohesionForce = representativeDebug.cohesionForce;
                    _debugInfo.lastAlignmentForce = representativeDebug.alignmentForce;
                    _debugInfo.lastFinalDirection = representativeDebug.finalDirection;
                }
            }

            Profiler.EndSample();
        }

        /// <summary>
        /// 为单个敌人计算 Boids 行为力。
        /// 该方法由 BatchComputeBoidsForAllRegistered 内部调用，不应单独调用。
        /// </summary>
        /// <param name="enemy">目标敌人。</param>
        /// <param name="cellSize">当前帧使用的空间哈希网格尺寸。</param>
        private void ComputeBoidsForAgent(EnemyActor enemy, float cellSize)
        {
            if (enemy == null)
                return;

            CacheAgentDataIfNeeded(enemy);
            if (!_agentDataMap.TryGetValue(enemy.ObjectId, out var data))
                return;

            data.Reset();
            data.objectId = enemy.ObjectId;
            data.position = enemy.transform.position;
            var aiModule = enemy.GetModule<EnemyAIModule>();
            var navController = aiModule != null ? aiModule.GetCachedNavController() : null;
            data.velocity = navController != null && navController.IsOnNavMesh
                ? navController.Velocity.normalized
                : (aiModule != null ? aiModule.MoveIntent.direction : enemy.transform.forward);
            data.forward = enemy.transform.forward;
            data.regionId = GetEnemyRegionId(enemy);
            data.isActive = true;
            data.isInCombat = AIScheduler.Instance.GetCurrentSimulationLevel(enemy) != AISimulationLevel.Dormant;
            data.originalBaseDirection = data.velocity;

            _separationNeighborBuffer.Clear();
            _cohesionNeighborBuffer.Clear();
            _alignmentNeighborBuffer.Clear();

            if (_config.enableSpatialHashing)
            {
                CollectNeighborsWithSpatialHash(enemy, cellSize);
            }
            else
            {
                CollectNeighborsWithoutSpatialHash(enemy);
            }

            data.separationNeighborCount = _separationNeighborBuffer.Count;
            data.cohesionNeighborCount = _cohesionNeighborBuffer.Count;
            data.alignmentNeighborCount = _alignmentNeighborBuffer.Count;
            data.totalNeighborQueries = _separationNeighborBuffer.Count + _cohesionNeighborBuffer.Count + _alignmentNeighborBuffer.Count;
            data.usedSpatialHashCache = _config.enableSpatialHashing;

            _totalNeighborQueries += data.totalNeighborQueries;

            if (_config.enableSeparation)
            {
                data.separationForce = CalculateSeparationForce(enemy, data, _separationNeighborBuffer);
            }

            if (_config.enableCohesion)
            {
                data.cohesionForce = CalculateCohesionForce(enemy, data, _cohesionNeighborBuffer);
            }

            if (_config.enableAlignment)
            {
                data.alignmentForce = CalculateAlignmentForce(enemy, data, _alignmentNeighborBuffer);
            }

            data.finalBoidsDirection = CombineForces(data);

            if (_config.enableSpatialHashing)
            {
                _debugInfo.spatialHashHitRate = 1f;
            }

            _agentDataMap[enemy.ObjectId] = data;
            WriteDebugInfo(enemy.ObjectId, data);
        }

        /// <summary>
        /// 根据空间哈希收集邻居个体。
        /// </summary>
        private void CollectNeighborsWithSpatialHash(EnemyActor enemy, float cellSize)
        {
            var pos = enemy.transform.position;
            var ownerBucket = GetBucketKey(pos, cellSize);
            var separationRange = Mathf.CeilToInt(_config.separationRadius / cellSize);
            var cohesionRange = Mathf.CeilToInt(_config.cohesionRadius / cellSize);
            var alignmentRange = Mathf.CeilToInt(_config.alignmentRadius / cellSize);
            var maxSepSqr = _config.separationRadius * _config.separationRadius;
            var maxCohSqr = _config.cohesionRadius * _config.cohesionRadius;
            var maxAliSqr = _config.alignmentRadius * _config.alignmentRadius;
            var ownerRegionId = GetEnemyRegionId(enemy);
            var canFilterByRegion = _config.limitToSameRegion && ownerRegionId != UnassignedRegionId;

            for (var x = ownerBucket.x - Mathf.Max(separationRange, Mathf.Max(cohesionRange, alignmentRange));
                 x <= ownerBucket.x + Mathf.Max(separationRange, Mathf.Max(cohesionRange, alignmentRange));
                 x++)
            {
                for (var z = ownerBucket.y - Mathf.Max(separationRange, Mathf.Max(cohesionRange, alignmentRange));
                     z <= ownerBucket.y + Mathf.Max(separationRange, Mathf.Max(cohesionRange, alignmentRange));
                     z++)
                {
                    if (!_spatialHashGrid.TryGetValue(new Vector2Int(x, z), out var bucketEnemies))
                        continue;

                    for (var i = 0; i < bucketEnemies.Count; i++)
                    {
                        var neighbor = bucketEnemies[i];
                        if (neighbor == null || neighbor == enemy || !neighbor.isActiveAndEnabled)
                            continue;

                        if (canFilterByRegion)
                        {
                            var neighborRegionId = GetEnemyRegionId(neighbor);
                            if (neighborRegionId != UnassignedRegionId && neighborRegionId != ownerRegionId)
                                continue;
                        }

                        var offset = neighbor.transform.position - pos;
                        offset.y = 0f;
                        var sqrDist = offset.sqrMagnitude;

                        if (sqrDist <= maxSepSqr)
                        {
                            _separationNeighborBuffer.Add(neighbor);
                        }

                        if (sqrDist <= maxCohSqr)
                        {
                            _cohesionNeighborBuffer.Add(neighbor);
                        }

                        if (sqrDist <= maxAliSqr)
                        {
                            _alignmentNeighborBuffer.Add(neighbor);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 不使用空间哈希的邻居收集（暴力遍历）。
        /// 仅在敌人数量极少（< 5）且空间哈希未启用时使用。
        /// </summary>
        private void CollectNeighborsWithoutSpatialHash(EnemyActor enemy)
        {
            var pos = enemy.transform.position;
            var ownerRegionId = GetEnemyRegionId(enemy);
            var canFilterByRegion = _config.limitToSameRegion && ownerRegionId != UnassignedRegionId;
            var maxSepSqr = _config.separationRadius * _config.separationRadius;
            var maxCohSqr = _config.cohesionRadius * _config.cohesionRadius;
            var maxAliSqr = _config.alignmentRadius * _config.alignmentRadius;

            foreach (var neighbor in _registeredEnemies)
            {
                if (neighbor == null || neighbor == enemy || !neighbor.isActiveAndEnabled)
                    continue;

                if (canFilterByRegion)
                {
                    var neighborRegionId = GetEnemyRegionId(neighbor);
                    if (neighborRegionId != UnassignedRegionId && neighborRegionId != ownerRegionId)
                        continue;
                }

                var offset = neighbor.transform.position - pos;
                offset.y = 0f;
                var sqrDist = offset.sqrMagnitude;

                if (sqrDist <= maxSepSqr)
                {
                    _separationNeighborBuffer.Add(neighbor);
                }

                if (sqrDist <= maxCohSqr)
                {
                    _cohesionNeighborBuffer.Add(neighbor);
                }

                if (sqrDist <= maxAliSqr)
                {
                    _alignmentNeighborBuffer.Add(neighbor);
                }
            }
        }

        /// <summary>
        /// 计算分离力。
        /// 分离力的方向指向远离所有邻居中心的反方向，大小与距离成反比。
        /// </summary>
        private Vector3 CalculateSeparationForce(EnemyActor owner, BoidsAgentData ownerData, List<EnemyActor> neighbors)
        {
            if (neighbors.Count == 0)
                return Vector3.zero;

            var separation = Vector3.zero;
            var pos = owner.transform.position;

            for (var i = 0; i < neighbors.Count; i++)
            {
                var neighbor = neighbors[i];
                if (neighbor == null || !neighbor.isActiveAndEnabled)
                    continue;

                var offset = pos - neighbor.transform.position;
                offset.y = 0f;
                var distance = offset.magnitude;

                if (distance < 1e-4f)
                    continue;

                var weight = 1f - (distance / _config.separationRadius);
                separation += offset.normalized * weight;
            }

            separation = Flatten(separation);

            if (_config.clampSeparationForce && separation.sqrMagnitude > 1e-4f)
            {
                if (separation.magnitude > _config.maxSeparationMagnitude)
                {
                    separation = separation.normalized * _config.maxSeparationMagnitude;
                }
            }

            return separation * _config.separationWeight;
        }

        /// <summary>
        /// 计算聚集力。
        /// 聚集力指向当前邻居群体的几何中心，引导个体向邻居聚集。
        /// 当分离邻居数超过阈值时，按比例降低聚集力以打破门口瓶颈死锁。
        /// </summary>
        private Vector3 CalculateCohesionForce(EnemyActor owner, BoidsAgentData ownerData, List<EnemyActor> neighbors)
        {
            if (neighbors.Count == 0)
                return Vector3.zero;

            var centerOfMass = Vector3.zero;
            var pos = owner.transform.position;
            var count = 0;

            for (var i = 0; i < neighbors.Count; i++)
            {
                var neighbor = neighbors[i];
                if (neighbor == null || !neighbor.isActiveAndEnabled)
                    continue;

                centerOfMass += neighbor.transform.position;
                count++;
            }

            if (count == 0)
                return Vector3.zero;

            centerOfMass /= count;

            var cohesionDirection = centerOfMass - pos;
            cohesionDirection.y = 0f;
            cohesionDirection = Flatten(cohesionDirection);

            // 密度感知抑制：当周围太挤时自动降低聚集力，防止门口死锁
            var effectiveWeight = _config.cohesionWeight;
            var sepCount = ownerData.separationNeighborCount;
            if (sepCount > _config.densityNeighborThreshold)
            {
                var excess = sepCount - _config.densityNeighborThreshold;
                var reduction = 1f - Mathf.Min(excess * _config.densityCohesionReductionPerNeighbor, 1f);
                effectiveWeight *= Mathf.Max(reduction, 0f);
            }

            return cohesionDirection.normalized * effectiveWeight;
        }

        /// <summary>
        /// 计算对齐力。
        /// 对齐力使个体的朝向趋近于邻居的平均朝向，产生方向一致的群体流动。
        /// </summary>
        private Vector3 CalculateAlignmentForce(EnemyActor owner, BoidsAgentData ownerData, List<EnemyActor> neighbors)
        {
            if (neighbors.Count == 0)
                return Vector3.zero;

            var averageForward = Vector3.zero;
            var count = 0;

            for (var i = 0; i < neighbors.Count; i++)
            {
                var neighbor = neighbors[i];
                if (neighbor == null || !neighbor.isActiveAndEnabled)
                    continue;

                var neighborData = default(BoidsAgentData);
                if (_agentDataMap.TryGetValue(neighbor.ObjectId, out neighborData) && neighborData.velocity.sqrMagnitude > 1e-4f)
                {
                    averageForward += neighborData.velocity.normalized;
                }
                else
                {
                    averageForward += neighbor.transform.forward;
                }

                count++;
            }

            if (count == 0)
                return Vector3.zero;

            averageForward /= count;
            averageForward = Flatten(averageForward);

            return averageForward.normalized * _config.alignmentWeight;
        }

        /// <summary>
        /// 合并分离、聚集、对齐三个力，得到最终的 Boids 方向。
        /// </summary>
        private Vector3 CombineForces(BoidsAgentData data)
        {
            var combined = data.separationForce + data.cohesionForce + data.alignmentForce;
            combined = Flatten(combined);

            if (combined.sqrMagnitude <= 1e-4f)
                return Vector3.zero;

            return combined.normalized;
        }

        /// <summary>
        /// 将 Boids 结果与基础移动方向混合。
        /// 当 layerOnBaseDirection 开启时，最终方向 = 基础方向 * (1-w) + Boids方向 * w。
        /// </summary>
        private Vector3 BlendWithBaseDirection(Vector3 boidsDirection, Vector3 baseDirection)
        {
            if (baseDirection.sqrMagnitude <= 1e-4f || boidsDirection.sqrMagnitude <= 1e-4f)
                return boidsDirection.sqrMagnitude > 1e-4f ? boidsDirection : baseDirection;

            var blendWeight = 0.6f;
            var blended = baseDirection.normalized * blendWeight + boidsDirection * (1f - blendWeight);
            blended = Flatten(blended);

            return blended.sqrMagnitude > 1e-4f ? blended.normalized : Vector3.zero;
        }

        /// <summary>
        /// 构建空间哈希桶网格。
        /// 每帧仅在首次调用 BatchCompute 时构建一次。
        /// </summary>
        private void BuildSpatialHashGrid(float cellSize, AISimulationLevel simulationLevel)
        {
            if (_lastBuildFrame == Time.frameCount && Mathf.Approximately(_lastCellSize, cellSize))
            {
                _usedSpatialHashCache = true;
                return;
            }

            _spatialHashGrid.Clear();
            _lastCellSize = cellSize;
            _lastBuildFrame = Time.frameCount;

            foreach (var enemy in _registeredEnemies)
            {
                if (enemy == null || !enemy.isActiveAndEnabled)
                    continue;

                var bucketKey = GetBucketKey(enemy.transform.position, cellSize);
                if (!_spatialHashGrid.TryGetValue(bucketKey, out var bucket))
                {
                    bucket = new List<EnemyActor>(4);
                    _spatialHashGrid[bucketKey] = bucket;
                }

                bucket.Add(enemy);
            }
        }

        /// <summary>
        /// 根据世界坐标计算空间哈希桶键。
        /// </summary>
        private static Vector2Int GetBucketKey(Vector3 position, float cellSize)
        {
            return new Vector2Int(
                Mathf.FloorToInt(position.x / cellSize),
                Mathf.FloorToInt(position.z / cellSize));
        }

        /// <summary>
        /// 使空间哈希缓存失效，强制下一帧重建。
        /// </summary>
        private void InvalidateSpatialHash()
        {
            _lastBuildFrame = -1;
            _lastCellSize = -1f;
            _spatialHashGrid.Clear();
        }

        /// <summary>
        /// 若指定敌人的数据尚未缓存，则创建空记录。
        /// </summary>
        private void CacheAgentDataIfNeeded(EnemyActor enemy)
        {
            if (enemy != null && !_agentDataMap.ContainsKey(enemy.ObjectId))
            {
                _agentDataMap[enemy.ObjectId] = new BoidsAgentData { objectId = enemy.ObjectId };
            }
        }

        /// <summary>
        /// 获取敌人的区域 ID。
        /// 若拓扑服务不可用或查询失败，返回 UnassignedRegionId。
        /// </summary>
        private static int GetEnemyRegionId(EnemyActor enemy)
        {
            if (enemy == null)
                return UnassignedRegionId;

            var topologyService = PCGMapTopologyService.Instance;
            if (topologyService != null && topologyService.HasProvider())
            {
                if (topologyService.TryResolveRegion(enemy.transform.position, out var regionId))
                {
                    return regionId;
                }
            }

            return UnassignedRegionId;
        }

        /// <summary>
        /// 获取最近一个已处理的敌人的 ObjectId（用于调试输出）。
        /// </summary>
        private ulong GetLastProcessedObjectId()
        {
            foreach (var enemy in _registeredEnemies)
            {
                if (enemy != null && enemy.isActiveAndEnabled)
                    return enemy.ObjectId;
            }

            return 0;
        }

        /// <summary>
        /// 获取最近的有效玩家位置。
        /// 若没有玩家，返回 null。
        /// </summary>
        private Vector3? GetNearestPlayerPosition(Vector3 from)
        {
            var nearestDist = float.PositiveInfinity;
            Vector3? nearest = null;

            var manager = AttackableObjectManager.Instance;
            if (manager == null)
                return nearest;

            var buffer = _tempCandidateBuffer;
            buffer.Clear();
            const float searchRadius = 500f;
            manager.GetCandidates(from, searchRadius, buffer);

            for (var i = 0; i < buffer.Count; i++)
            {
                if (buffer[i] is PlayerActor player && player != null && player.isActiveAndEnabled)
                {
                    var dist = Vector3.Distance(from, player.transform.position);
                    if (dist < nearestDist)
                    {
                        nearestDist = dist;
                        nearest = player.transform.position;
                    }
                }
            }

            return nearest;
        }

        private readonly List<IAttackableObject> _tempCandidateBuffer = new List<IAttackableObject>(16);

        /// <summary>
        /// 将方向向量压平到水平面（Y=0）并归一化。
        /// </summary>
        private static Vector3 Flatten(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude <= 1e-4f)
                return Vector3.zero;

            return direction.normalized;
        }

        /// <summary>
        /// 写入调试信息快照。
        /// </summary>
        private void WriteDebugInfo(ulong objectId, BoidsAgentData data)
        {
            var debugInfo = new BoidsAgentDebugInfo
            {
                objectId = objectId,
                originalDirection = data.originalBaseDirection,
                separationForce = data.separationForce,
                cohesionForce = data.cohesionForce,
                alignmentForce = data.alignmentForce,
                finalDirection = data.finalBoidsDirection,
                separationNeighbors = data.separationNeighborCount,
                cohesionNeighbors = data.cohesionNeighborCount,
                alignmentNeighbors = data.alignmentNeighborCount,
                usedSpatialHash = data.usedSpatialHashCache,
                wasProcessed = data.isActive
            };

            _agentDebugInfoMap[objectId] = debugInfo;
        }

        /// <summary>
        /// 获取当前已注册敌人的总数。
        /// </summary>
        public int GetRegisteredCount()
        {
            return _registeredEnemies.Count;
        }

        /// <summary>
        /// 获取当前已激活（参与本帧计算）的敌人数量。
        /// </summary>
        public int GetActiveCount()
        {
            var count = 0;
            foreach (var enemy in _registeredEnemies)
            {
                if (enemy != null && enemy.isActiveAndEnabled)
                    count++;
            }

            return count;
        }

        /// <summary>
        /// 获取最近一次批量计算的耗时（毫秒）。
        /// </summary>
        public float GetLastComputeTimeMs()
        {
            return _lastComputeTimeMs;
        }

        /// <summary>
        /// 调试用：绘制本帧 Boids 辅助 Gizmos。
        /// 在 Editor 中勾选 Config 的 drawDebugGizmos 并开启 Gizmos 绘制后自动调用。
        /// </summary>
        public void OnDrawGizmos()
        {
            if (!_config.enableBoids || !_config.drawDebugGizmos)
                return;

            if (!_config.drawDebugGizmos)
                return;

            Gizmos.color = Color.cyan;
            foreach (var enemy in _registeredEnemies)
            {
                if (enemy == null || !enemy.isActiveAndEnabled)
                    continue;

                if (!_agentDataMap.TryGetValue(enemy.ObjectId, out var data) || !data.isActive)
                    continue;

                var pos = enemy.transform.position;

                if (data.separationNeighborCount > 0 && data.separationForce.sqrMagnitude > 1e-4f)
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawRay(pos + Vector3.up * 0.5f, data.separationForce * _config.debugForceScale);
                }

                if (data.cohesionNeighborCount > 0 && data.cohesionForce.sqrMagnitude > 1e-4f)
                {
                    Gizmos.color = Color.blue;
                    Gizmos.DrawRay(pos + Vector3.up * 0.5f, data.cohesionForce * _config.debugForceScale);
                }

                if (data.alignmentNeighborCount > 0 && data.alignmentForce.sqrMagnitude > 1e-4f)
                {
                    Gizmos.color = Color.red;
                    Gizmos.DrawRay(pos + Vector3.up * 0.5f, data.alignmentForce * _config.debugForceScale);
                }

                if (data.finalBoidsDirection.sqrMagnitude > 1e-4f)
                {
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawRay(pos + Vector3.up * 0.5f, data.finalBoidsDirection * _config.debugArrowLength);
                }
            }
        }
    }
}
