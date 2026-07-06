using System;
using System.Collections.Generic;
using Matrix.Missions;
using Matrix.PCG;
using Unity.Netcode;
using UnityEngine;

namespace Framework.LogicLayer.Module.SpawnSystem
{
    /// <summary>
    /// Monster spawn controller. Server-only.
    ///
    /// Lifecycle:
    ///   1. Call InitializeWithMapResult() with the PCG generation result.
    ///   2. Call UpdatePlayerCount() / UpdateActiveTaskCount() / UpdateOccupiedRegions() each frame.
    ///   3. Every spawnTickInterval seconds OnSpawnTick() distributes spawn budget by region weights
    ///      and spawns monsters at available points.
    ///
    /// Death tracking is handled via EventCenter.UnitDied - no direct coupling to enemy components.
    /// Object pooling is handled by EnemySpawnService -> NetworkObjectPoolManager.
    /// </summary>
    public sealed class MonsterSpawnManager : MonoBehaviour, Matrix.SpawnSystem.IMonsterSpawnSystem
    {
        #region Serialized Fields

        [Header("References")]
        [SerializeField] internal MissionManager missionManager;
        [SerializeField] internal EnemySpawnService enemySpawnService;

        [Header("Config")]
        [SerializeField] internal Matrix.SpawnSystem.MonsterSpawnConfig config;
        [SerializeField] internal string monsterEnemyId = "001";

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs;

        #endregion

        #region Runtime State

        private Dictionary<int, List<SpawnPointData>> _regionSpawnPoints;
        private RoomGraph _cachedGraph;
        private HashSet<int> _occupiedRegions;
        private int _currentPlayerCount = 1;
        private int _activeTaskCount;
        private int _dynamicCap;
        private float _tickTimer;
        private bool _isRunning;
        private bool _registeredDeathListener;
        private PcgGenerationProfile _currentProfile;
        private List<string> _availableEnemyIds;

        public static MonsterSpawnManager Instance { get; private set; }

        /// <summary>RunManager 注入的敌人生成/销毁回调，用于统一路由事件。</summary>
        internal System.Action<ulong, NetworkObject> OnEnemySpawned;
        internal System.Action<ulong> OnEnemyDespawned;

        #endregion

        #region IMonsterSpawnSystem

        public int ActiveMonsterCount => MonsterRegistry.Instance != null
            ? MonsterRegistry.Instance.ActiveMonsterCount : 0;

        public int TotalSpawnedCount => MonsterRegistry.Instance != null
            ? MonsterRegistry.Instance.TotalSpawnedCount : 0;

        public int TotalKilledCount => MonsterRegistry.Instance != null
            ? MonsterRegistry.Instance.TotalKilledCount : 0;

        public int DynamicCap => _dynamicCap;

        public float CurrentSpawnRate => ComputeCurrentSpawnRate();

        public void InitializeWithMapResult(object result, object profile)
        {
            InitializeWithMapResult(result as PcgMapGenerationResult, profile as PcgGenerationProfile);
        }

        public void UpdatePlayerCount(int count)
        {
            int clamped = Mathf.Clamp(count, 1, 4);
            if (_currentPlayerCount == clamped) return;
            _currentPlayerCount = clamped;
            RefreshDynamicCap();
        }

        public void UpdateActiveTaskCount(int count)
        {
            if (_activeTaskCount == count) return;
            _activeTaskCount = Mathf.Max(0, count);
            RefreshDynamicCap();
        }

        public void UpdateOccupiedRegions(List<int> occupiedNodeIds)
        {
            _occupiedRegions.Clear();
            if (occupiedNodeIds != null)
            {
                foreach (int id in occupiedNodeIds)
                    _occupiedRegions.Add(id);
            }
        }

        public void Shutdown()
        {
            _isRunning = false;
            MonsterRegistry.Instance?.Clear();
            _regionSpawnPoints?.Clear();
            _cachedGraph = null;
        }

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[MonsterSpawn] Another instance exists. Destroying this.", this);
                Destroy(gameObject);
                return;
            }
            Instance = this;
            _regionSpawnPoints = new Dictionary<int, List<SpawnPointData>>();
            _occupiedRegions = new HashSet<int>();
        }

        private void OnEnable()
        {
            RegisterDeathListener();
        }

        private void OnDisable()
        {
            UnregisterDeathListener();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void FixedUpdate()
        {
            if (!_isRunning) return;
            PollMissionState();
            TickSpawn();
        }

        #endregion

        #region Initialization

        private void InitializeWithMapResult(PcgMapGenerationResult result, PcgGenerationProfile profile)
        {
            if (result == null)
            {
                Debug.LogError("[MonsterSpawn] InitializeWithMapResult received null result.", this);
                return;
            }

            _currentProfile = profile;
            MonsterRegistry.Create();

            // 从 Profile 的敌人池拉取可用 prefab 地址，Profile 为空时回退到 monsterEnemyId
            _availableEnemyIds = new List<string>();
            if (_currentProfile != null && _currentProfile.AvailableEnemies != null)
            {
                foreach (var enemySO in _currentProfile.AvailableEnemies)
                {
                    if (enemySO != null && !string.IsNullOrEmpty(enemySO.id) && enemySO.prefab != null)
                        _availableEnemyIds.Add($"{enemySO.rank}/{enemySO.id}");
                }
            }
            if (_availableEnemyIds.Count == 0)
                _availableEnemyIds.Add($"Normal/{monsterEnemyId}");
            MonsterRegistry.Instance.Clear();

            _regionSpawnPoints.Clear();
            _occupiedRegions.Clear();

            for (int i = 0; i < result.SpawnPoints.Count; i++)
            {
                var sp = result.SpawnPoints[i];
                if (sp.Category != SpawnPointCategory.NormalEnemy) continue;
                if (sp.PointTransform == null) continue;

                if (!_regionSpawnPoints.TryGetValue(sp.NodeId, out var list))
                {
                    list = new List<SpawnPointData>();
                    _regionSpawnPoints[sp.NodeId] = list;
                }
                list.Add(new SpawnPointData(sp.NodeId, sp.PointTransform));
            }

            _cachedGraph = result.Graph;
            RefreshDynamicCap();
            _tickTimer = 0f;
            _isRunning = true;

            if (enableDebugLogs)
            {
                int totalPoints = 0;
                foreach (var l in _regionSpawnPoints.Values) totalPoints += l.Count;
                Debug.Log($"[MonsterSpawn] Initialized. Regions={_regionSpawnPoints.Count}, Points={totalPoints}", this);
            }
        }

        #endregion

        #region Dynamic Cap & Rate

        private void RefreshDynamicCap()
        {
            if (_regionSpawnPoints == null || _currentProfile == null)
            {
                _dynamicCap = config != null ? config.globalMaxActiveMonsters : 200;
                return;
            }

            float playerMult = GetPlayerCapMultiplier(_currentPlayerCount);
            float taskMult = ComputeCapTaskMultiplier(_activeTaskCount);
            int baseCap = _currentProfile.MonsterSpawn.baseCapPerRegion;
            int regionCount = _regionSpawnPoints.Count;
            int profileMax = _currentProfile.MonsterSpawn.globalMaxCap;

            int dynamic = Mathf.CeilToInt(baseCap * regionCount * playerMult * taskMult);
            _dynamicCap = Mathf.Min(dynamic, profileMax);
            _dynamicCap = Mathf.Min(_dynamicCap, config != null ? config.globalMaxActiveMonsters : 200);
        }

        private float ComputeCurrentSpawnRate()
        {
            if (_currentProfile == null) return 0f;
            float playerMult = GetPlayerRateMultiplier(_currentPlayerCount);
            float taskMult = ComputeRateTaskMultiplier(_activeTaskCount);
            return _currentProfile.MonsterSpawn.baseSpawnRatePerTick * playerMult * taskMult;
        }

        private float ComputeCapTaskMultiplier(int taskCount)
        {
            if (taskCount <= 0 || config == null) return 1f;
            float raw = config.firstTaskCapMultiplier + (taskCount - 1) * config.additionalTaskCapMultiplier;
            return Mathf.Min(raw, config.maxTaskCapMultiplier);
        }

        private float ComputeRateTaskMultiplier(int taskCount)
        {
            if (taskCount <= 0 || config == null) return 1f;
            float raw = config.firstTaskRateMultiplier + (taskCount - 1) * config.additionalTaskRateMultiplier;
            return Mathf.Min(raw, config.maxTaskRateMultiplier);
        }

        private static float GetPlayerCapMultiplier(int count)
        {
            return count switch { 1 => 1.0f, 2 => 1.5f, 3 => 2.0f, 4 => 2.5f, _ => 1.0f };
        }

        private static float GetPlayerRateMultiplier(int count)
        {
            return count switch { 1 => 1.0f, 2 => 1.2f, 3 => 1.5f, 4 => 2.0f, _ => 1.0f };
        }

        #endregion

        #region Spawn Tick

        private void TickSpawn()
        {
            float interval = config != null ? config.spawnTickInterval : 2f;
            _tickTimer += Time.fixedDeltaTime;
            if (_tickTimer < interval) return;
            _tickTimer -= interval;
            OnSpawnTick();
        }

        private void OnSpawnTick()
        {
            if (ActiveMonsterCount >= _dynamicCap) return;

            int budget = Mathf.FloorToInt(ComputeCurrentSpawnRate());
            if (budget <= 0) return;

            var regionWeights = ComputeRegionWeights();
            if (regionWeights.Count == 0) return;

            float totalWeight = 0f;
            foreach (float w in regionWeights.Values) totalWeight += w;

            var allocation = DistributeBudget(budget, regionWeights, totalWeight);

            foreach (var kvp in allocation)
            {
                int nodeId = kvp.Key;
                int count = kvp.Value;
                if (count <= 0) continue;
                if (!_regionSpawnPoints.TryGetValue(nodeId, out var points) || points.Count == 0) continue;

                int perPoint = count / points.Count;
                int remainder = count % points.Count;

                for (int i = 0; i < points.Count; i++)
                {
                    int toSpawn = perPoint + (i < remainder ? 1 : 0);
                    for (int j = 0; j < toSpawn; j++)
                        SpawnMonsterAt(points[i]);
                }
            }
        }

        #endregion

        #region Interest Point Algorithm

        private Dictionary<int, float> ComputeRegionWeights()
        {
            var weights = new Dictionary<int, float>();
            foreach (int nodeId in _regionSpawnPoints.Keys)
            {
                int dist = GetMinPlayerDistance(nodeId);
                float weight = GetWeightForDistance(dist);
                if (weight > 0f) weights[nodeId] = weight;
            }
            return weights;
        }

        private float GetWeightForDistance(int distance)
        {
            if (distance <= 0 || distance == int.MaxValue)
                return 0f;

            if (config == null)
                return distance switch { 1 => 0.5f, 2 => 1.0f, 3 => 0.5f, _ => 0f };

            return distance switch
            {
                1 => config.distance1Weight,
                2 => config.distance2Weight,
                3 => config.distance3Weight,
                _ => config.distance4PlusWeight
            };
        }

        private int GetMinPlayerDistance(int targetNodeId)
        {
            if (_cachedGraph == null || _occupiedRegions.Count == 0) return int.MaxValue;
            int minDist = int.MaxValue;
            foreach (int occupied in _occupiedRegions)
            {
                int dist = _cachedGraph.GetShortestDistance(occupied, targetNodeId);
                if (dist < minDist) minDist = dist;
            }
            return minDist;
        }

        private Dictionary<int, int> DistributeBudget(int totalBudget, Dictionary<int, float> weights, float totalWeight)
        {
            var result = new Dictionary<int, int>();
            if (totalWeight <= 0f) return result;

            foreach (var kvp in weights)
            {
                float ratio = kvp.Value / totalWeight;
                result[kvp.Key] = Mathf.FloorToInt(totalBudget * ratio);
            }

            int allocated = 0;
            foreach (int v in result.Values) allocated += v;

            int remainder = totalBudget - allocated;
            if (remainder > 0)
            {
                int maxKey = 0;
                float maxWeight = float.MinValue;
                foreach (var kvp in weights)
                    if (kvp.Value > maxWeight) { maxWeight = kvp.Value; maxKey = kvp.Key; }
                result[maxKey] += remainder;
            }

            return result;
        }

        #endregion

        #region Spawning

        private void SpawnMonsterAt(SpawnPointData pointData)
        {
            if (ActiveMonsterCount >= _dynamicCap) return;
            if (enemySpawnService == null)
            {
                if (enableDebugLogs) Debug.LogWarning("[MonsterSpawn] EnemySpawnService not assigned.", this);
                return;
            }

            string enemyId = _availableEnemyIds[UnityEngine.Random.Range(0, _availableEnemyIds.Count)];
            var monsterNetObj = enemySpawnService.SpawnEnemy(enemyId, pointData.WorldPosition, Quaternion.identity);
            if (monsterNetObj == null)
            {
                if (enableDebugLogs) Debug.Log($"[MonsterSpawn] Spawn failed at {pointData.WorldPosition}", this);
                return;
            }

            // 根据当前难度等级设置怪物等级（难度 0 → 等级 1，每级 +1）
            int monsterLevel = 1 + _difficultyTier;
            var serverAttr = monsterNetObj.GetComponent<ServerEnemyAttributeModule>();
            if (serverAttr != null)
                serverAttr.SetLevelServerRpc(monsterLevel);

            MonsterRegistry.Instance.Register(monsterNetObj);
            OnEnemySpawned?.Invoke(monsterNetObj.NetworkObjectId, monsterNetObj);
            if (enableDebugLogs)
                Debug.Log($"[MonsterSpawn] Spawned Lv.{monsterLevel} at {pointData.WorldPosition}. Active={ActiveMonsterCount}/{_dynamicCap}", this);
        }

        #endregion

        #region EventCenter Death Tracking

        private void RegisterDeathListener()
        {
            if (_registeredDeathListener) return;
            EventCenter.Instance.AddListener<UnitDiedEvt>(EventName.UnitDied, OnUnitDied);
            _registeredDeathListener = true;
        }

        private void UnregisterDeathListener()
        {
            if (!_registeredDeathListener) return;
            EventCenter.Instance.RemoveListener<UnitDiedEvt>(EventName.UnitDied, OnUnitDied);
            _registeredDeathListener = false;
        }

        private void OnUnitDied(UnitDiedEvt evt)
        {
            MonsterRegistry.Instance?.ReportKillByNetworkId(evt.unitId);
            OnEnemyDespawned?.Invoke(evt.unitId);
            if (enableDebugLogs)
                Debug.Log($"[MonsterSpawn] Monster died (unitId={evt.unitId}). Active={ActiveMonsterCount}", this);
        }

        #endregion

        #region Mission Integration

        private int _difficultyTier;

        private void PollMissionState()
        {
            if (missionManager == null) return;

            // 从 RunManager 读取当前难度等级（难度系数控制函数）
            var runManager = FindObjectOfType<Matrix.RunSystem.RunManager>();
            _difficultyTier = runManager != null ? runManager.GetDifficultyTier() : 0;

            UpdatePlayerCount(GetCurrentPlayerCount());
            UpdateActiveTaskCount(GetActiveSideTaskCount());
            UpdateOccupiedRegions(GetCurrentPlayerOccupiedRegionIds());
            RefreshDynamicCap(); // 难度等级变化时重算
        }

        private int GetCurrentPlayerCount()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.ConnectedClientsList != null)
                return NetworkManager.Singleton.ConnectedClientsList.Count;
            return 1;
        }

        private int GetActiveSideTaskCount()
        {
            if (missionManager?.RuntimeMissions == null) return 0;
            int count = 0;
            for (int i = 0; i < missionManager.RuntimeMissions.Count; i++)
            {
                var m = missionManager.RuntimeMissions[i];
                if (m != null && m.State == Matrix.Missions.MissionState.Active &&
                    m.Config != null && m.Config.MissionCategory == Matrix.Missions.MissionCategory.Secondary)
                    count++;
            }
            return count;
        }

        private List<int> GetCurrentPlayerOccupiedRegionIds()
        {
            var result = new List<int>();
            if (missionManager?.CurrentMapResult == null || missionManager.CurrentMapResult.PlacedRooms == null)
                return result;
            if (AttackableObjectManager.Instance == null) return result;

            IReadOnlyList<IAttackableObject> allObjects = AttackableObjectManager.Instance.GetAllRegistered();
            for (int i = 0; i < allObjects.Count; i++)
            {
                IAttackableObject obj = allObjects[i];
                if (obj == null || obj is not PlayerActor player) continue;
                Vector3 pos = player.TargetTransform != null ? player.TargetTransform.position : player.transform.position;
                int nodeId = FindPlayerRoomNode(pos);
                if (nodeId >= 0) result.Add(nodeId);
            }
            return result;
        }

        private int FindPlayerRoomNode(Vector3 worldPosition)
        {
            if (missionManager.CurrentMapResult == null) return -1;
            int footprintNodeId = FindPlayerRoomNodeByFootprint(worldPosition);
            if (footprintNodeId >= 0) return footprintNodeId;

            int bestNodeId = -1;
            float bestDist = float.MaxValue;

            for (int i = 0; i < missionManager.CurrentMapResult.PlacedRooms.Count; i++)
            {
                var room = missionManager.CurrentMapResult.PlacedRooms[i];
                if (room == null || room.RoomInstance == null) continue;
                if (!room.RoomInstance.TryGetWorldBounds(out Bounds bounds, out _)) continue;

                if (bounds.Contains(worldPosition)) return room.NodeId;
                float dist = bounds.SqrDistance(worldPosition);
                if (dist < bestDist) { bestDist = dist; bestNodeId = room.NodeId; }
            }
            return bestNodeId;
        }

        private int FindPlayerRoomNodeByFootprint(Vector3 worldPosition)
        {
            int bestNodeId = -1;
            float bestDist = float.MaxValue;

            for (int i = 0; i < missionManager.CurrentMapResult.PlacedRooms.Count; i++)
            {
                var room = missionManager.CurrentMapResult.PlacedRooms[i];
                var boundsProvider = room?.RoomInstance != null ? room.RoomInstance.RoomBounds : null;
                if (boundsProvider == null) continue;
                if (!boundsProvider.TryGetWorldFootprintCorners(
                        out Vector3 minXMinZ,
                        out Vector3 maxXMinZ,
                        out Vector3 maxXMaxZ,
                        out Vector3 minXMaxZ))
                    continue;

                if (!IsPointInsideQuadXZ(worldPosition, minXMinZ, maxXMinZ, maxXMaxZ, minXMaxZ))
                    continue;

                Vector3 center = (minXMinZ + maxXMinZ + maxXMaxZ + minXMaxZ) * 0.25f;
                float dist = (new Vector2(worldPosition.x, worldPosition.z) - new Vector2(center.x, center.z)).sqrMagnitude;
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestNodeId = room.NodeId;
                }
            }

            return bestNodeId;
        }

        private static bool IsPointInsideQuadXZ(Vector3 point, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            Vector2 p = new Vector2(point.x, point.z);
            Vector2 va = new Vector2(a.x, a.z);
            Vector2 vb = new Vector2(b.x, b.z);
            Vector2 vc = new Vector2(c.x, c.z);
            Vector2 vd = new Vector2(d.x, d.z);

            bool hasNegative = false;
            bool hasPositive = false;
            AccumulateEdgeSign(p, va, vb, ref hasNegative, ref hasPositive);
            AccumulateEdgeSign(p, vb, vc, ref hasNegative, ref hasPositive);
            AccumulateEdgeSign(p, vc, vd, ref hasNegative, ref hasPositive);
            AccumulateEdgeSign(p, vd, va, ref hasNegative, ref hasPositive);

            return !(hasNegative && hasPositive);
        }

        private static void AccumulateEdgeSign(Vector2 point, Vector2 edgeStart, Vector2 edgeEnd, ref bool hasNegative, ref bool hasPositive)
        {
            float cross = (edgeEnd.x - edgeStart.x) * (point.y - edgeStart.y) -
                          (edgeEnd.y - edgeStart.y) * (point.x - edgeStart.x);

            if (cross < -0.001f) hasNegative = true;
            else if (cross > 0.001f) hasPositive = true;
        }

        #endregion
    }
}
