using System.Collections.Generic;
using Matrix.PCG;
using Matrix.RunSystem;
using Unity.Netcode;
using UnityEngine;

namespace Matrix.SpawnSystem
{
    /// <summary>
    /// 玩家生成管理器。服务端权威。
    /// 持有 playerPrefab 引用，在 PCG 完成后为所有已连接客户端生成玩家对象。
    /// </summary>
    public sealed class PlayerSpawnManager : MonoBehaviour
    {
        [Header("Config")]
        [Tooltip("回退玩家 Prefab（HeroSO.heroPrefab 为空时使用）。须挂载 NetworkObject + PlayerActor + PlayerNetworkProxy + PlayerInitializer。")]
        [SerializeField] internal GameObject playerPrefab;

        [Tooltip("默认英雄数据模板。优先使用其 heroPrefab 生成玩家。")]
        [SerializeField] internal HeroSO defaultHeroSO;

        [Tooltip("同一出生点周围多玩家时的空间步长。")]
        [SerializeField] private float spawnOffsetStep = 2.5f;

        [Header("Debug")]
        [SerializeField] private bool verboseLog = true;

        private NetworkManager _networkManager;
        private readonly Dictionary<ulong, NetworkObject> _spawnedPlayers = new Dictionary<ulong, NetworkObject>();
        private PcgMapGenerationResult _cachedMapResult;
        private int _cachedBirthNodeId = -1;
        private bool _mapReady;
        private bool _initialSpawnDone;
        private RunManager _runManager;
        private Queue<ulong> _pendingLateJoinClients;
        private bool _networkCallbacksRegistered;

        /// <summary>RunManager 注入的玩家生成回调，用于统一路由事件。</summary>
        internal System.Action<ulong, NetworkObject> OnPlayerSpawned;
        internal System.Action<ulong> OnPlayerDespawned;

        // ── Public API ──

        public void SetRunManager(RunManager runManager) => _runManager = runManager;

        /// <summary>RunManager 在 PCG 完成后调用。</summary>
        public void OnMapReady(PcgMapGenerationResult mapResult, int birthNodeId)
        {
            if (!IsServer()) return;

            if (!EnsureNetworkManager())
            {
                LogError("NetworkManager 未就绪，无法生成玩家。");
                return;
            }

            _cachedMapResult = mapResult;
            _cachedBirthNodeId = birthNodeId;
            _mapReady = true;

            SpawnAllConnectedPlayers();

            while (_pendingLateJoinClients != null && _pendingLateJoinClients.Count > 0)
            {
                ulong clientId = _pendingLateJoinClients.Dequeue();
                SpawnPlayerForClient(clientId);
            }
        }

        // ── Unity Lifecycle ──

        private void Awake()
        {
            _pendingLateJoinClients = new Queue<ulong>();
        }

        private void Start()
        {
            EnsureNetworkManager();
        }

        private void OnDestroy()
        {
            if (_networkManager != null && _networkCallbacksRegistered)
            {
                _networkManager.OnClientConnectedCallback -= OnClientConnected;
                _networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
                _networkCallbacksRegistered = false;
            }
        }

        // ── Spawn Logic ──

        private void SpawnAllConnectedPlayers()
        {
            if (!EnsureNetworkManager() || !_networkManager.IsServer) return;

            var toSpawn = new List<ulong>();
            foreach (ulong clientId in _networkManager.ConnectedClientsIds)
            {
                if (!_spawnedPlayers.ContainsKey(clientId))
                    toSpawn.Add(clientId);
            }

            for (int i = 0; i < toSpawn.Count; i++)
            {
                SpawnPlayerForClientInternal(toSpawn[i], i, toSpawn.Count);
            }

            _initialSpawnDone = true;
            if (toSpawn.Count > 0)
                Log($"初始玩家生成完成: {toSpawn.Count} 人");
        }

        private void SpawnPlayerForClient(ulong clientId)
        {
            if (_spawnedPlayers.ContainsKey(clientId))
            {
                LogWarning($"客户端 {clientId} 已有玩家对象，跳过。");
                return;
            }

            int playerIndex = _spawnedPlayers.Count;
            SpawnPlayerForClientInternal(clientId, playerIndex, playerIndex + 1);
        }

        private void SpawnPlayerForClientInternal(ulong clientId, int playerIndex, int totalPlayers)
        {
            Vector3 spawnPos = ResolveSpawnPosition(playerIndex, totalPlayers);
            GameObject prefabToUse = defaultHeroSO != null && defaultHeroSO.heroPrefab != null
                ? defaultHeroSO.heroPrefab
                : playerPrefab;

            if (prefabToUse == null)
            {
                LogError("无可用的玩家 Prefab（HeroSO.heroPrefab 和 playerPrefab 均为空）。");
                return;
            }

            GameObject instance = Instantiate(prefabToUse, spawnPos, Quaternion.identity);
            instance.name = $"[Player_{clientId}]";

            // 注入 HeroSO（英雄数据 + 技能/被动）
            var initializer = instance.GetComponent<PlayerInitializer>();
            if (initializer != null && defaultHeroSO != null)
            {
                initializer.SetHeroSO(defaultHeroSO);
            }

            var no = instance.GetComponent<NetworkObject>();
            if (no == null)
            {
                LogError("playerPrefab 缺少 NetworkObject 组件！");
                Destroy(instance);
                return;
            }

            no.SpawnAsPlayerObject(clientId, destroyWithScene: true);
            _spawnedPlayers[clientId] = no;
            OnPlayerSpawned?.Invoke(clientId, no);
            Log($"玩家已为客户端 {clientId} 生成于 {spawnPos} (第 {playerIndex + 1}/{totalPlayers} 人)");
        }

        // ── Position Resolution ──

        private Vector3 ResolveSpawnPosition(int playerIndex, int totalPlayers)
        {
            List<Vector3> spawnPoints = CollectStartRoomSpawnPoints();
            if (spawnPoints.Count > 0)
            {
                int pointIdx = playerIndex % spawnPoints.Count;
                int samePointOffset = playerIndex / spawnPoints.Count;
                return spawnPoints[pointIdx] + new Vector3(samePointOffset * spawnOffsetStep, 0f, 0f);
            }

            Vector3 roomCenter = GetStartRoomCenter();
            if (roomCenter != Vector3.zero)
            {
                int row = playerIndex / 4;
                int col = playerIndex % 4;
                return roomCenter
                     + new Vector3(col * spawnOffsetStep, 0f, row * spawnOffsetStep)
                     - new Vector3(1.5f * spawnOffsetStep, 0f, 0.5f * spawnOffsetStep);
            }

            return Vector3.zero;
        }

        private List<Vector3> CollectStartRoomSpawnPoints()
        {
            var results = new List<Vector3>();
            if (_cachedMapResult?.SpawnPoints == null) return results;

            foreach (var sp in _cachedMapResult.SpawnPoints)
            {
                if (sp == null || sp.PointTransform == null) continue;
                if (sp.NodeId == _cachedBirthNodeId && sp.Category == SpawnPointCategory.NormalEnemy)
                    results.Add(sp.PointTransform.position);
            }
            return results;
        }

        private Vector3 GetStartRoomCenter()
        {
            if (_cachedMapResult?.PlacedRooms == null) return Vector3.zero;

            foreach (var room in _cachedMapResult.PlacedRooms)
            {
                if (room == null || room.NodeId != _cachedBirthNodeId || room.RoomInstance == null) continue;

                if (room.RoomInstance.TryGetWorldBounds(out Bounds bounds, out _))
                    return bounds.center;
                return room.RoomInstance.transform.position;
            }
            return Vector3.zero;
        }

        // ── Network Callbacks ──

        private void OnClientConnected(ulong clientId)
        {
            if (!IsServer()) return;
            EnsureNetworkManager();

            if (clientId == NetworkManager.ServerClientId && !_initialSpawnDone)
                return;

            if (_spawnedPlayers.ContainsKey(clientId))
                return;

            if (!_mapReady)
            {
                _pendingLateJoinClients.Enqueue(clientId);
                Log($"地图未就绪，客户端 {clientId} 入队等待。");
                return;
            }

            SpawnPlayerForClient(clientId);
        }

        private void OnClientDisconnected(ulong clientId)
        {
            if (!IsServer()) return;
            EnsureNetworkManager();

            if (_spawnedPlayers.TryGetValue(clientId, out NetworkObject no))
            {
                if (no != null && no.IsSpawned)
                    no.Despawn(destroy: true);
                _spawnedPlayers.Remove(clientId);
                OnPlayerDespawned?.Invoke(clientId);
                Log($"客户端 {clientId} 断开，玩家对象已清理。");
            }
        }

        // ── Helpers ──

        private bool EnsureNetworkManager()
        {
            if (_networkManager == null)
                _networkManager = NetworkManager.Singleton;

            if (_networkManager == null)
                return false;

            if (!_networkCallbacksRegistered)
            {
                _networkManager.OnClientConnectedCallback += OnClientConnected;
                _networkManager.OnClientDisconnectCallback += OnClientDisconnected;
                _networkCallbacksRegistered = true;
            }

            return true;
        }

        private bool IsServer() => NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;

        private void Log(string msg)
        { if (verboseLog) Debug.Log($"[PlayerSpawnManager] {msg}"); }

        private void LogWarning(string msg)
        { Debug.LogWarning($"[PlayerSpawnManager] {msg}"); }

        private void LogError(string msg)
        { Debug.LogError($"[PlayerSpawnManager] {msg}"); }
    }
}
