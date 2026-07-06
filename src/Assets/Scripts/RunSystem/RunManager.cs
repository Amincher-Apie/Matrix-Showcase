using System;
using System.Collections.Generic;
using Matrix.Missions;
using Matrix.PCG;
using Matrix.SpawnSystem;
using Unity.Netcode;
using UnityEngine;

namespace Matrix.RunSystem
{
    /// <summary>
    /// Run 生命周期状态机。服务端权威驱动，通过 NetworkVariable/NetworkList 同步到客户端。
    /// 仿 MissionManager 的 NetworkBehaviour 模式。
    /// </summary>
    public sealed class RunManager : NetworkBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] internal PcgMapGenerator pcgMapGenerator;
        [SerializeField] internal MissionManager missionManager;
        [SerializeField] internal Framework.LogicLayer.Module.SpawnSystem.MonsterSpawnManager monsterSpawnManager;
        [SerializeField] internal PlayerSpawnManager playerSpawnManager;
        [SerializeField] internal RunSummaryCalculator runSummaryCalculator;
        [SerializeField] internal RunConfig runConfig;

        [Header("Debug")]
        [SerializeField] private bool verboseLog = true;
        [SerializeField] private float victoryPollInterval = 2f;   // 服务端保底轮询间隔（秒）

        // ── Network Synced State ──
        private readonly NetworkVariable<int> _currentState = new NetworkVariable<int>(
            (int)RunState.MainMenu,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> _currentRoomNodeId = new NetworkVariable<int>(
            -1,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> _runSeed = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> _difficultyLevel = new NetworkVariable<int>(
            (int)RunDifficulty.Normal,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private NetworkList<RoomNetState> _roomStates;

        // ── Runtime ──
        private RunContext _runContext;
        private RunSessionData _sessionData;
        private readonly Dictionary<int, RoomRunState> _roomRunStateCache = new Dictionary<int, RoomRunState>();
        private readonly Dictionary<RunState, Action> _stateEnterHandlers = new Dictionary<RunState, Action>();
        private bool _bootstrapped;
        private bool _registeredDeathListener;
        private bool _registeredVictoryListener;
        private float _victoryPollTimer;   // 保底轮询计时器

        // ── Public Accessors ──
        public RunState CurrentState => (RunState)_currentState.Value;
        public int CurrentRoomNodeId => _currentRoomNodeId.Value;
        public int RunSeed => _runSeed.Value;
        public RunDifficulty Difficulty => (RunDifficulty)_difficultyLevel.Value;
        public RunContext Context => _runContext;
        public RunSessionData Session => _sessionData;
        public int RoomStateCount => _roomStates.Count;

        public bool TryGetRoomState(int nodeId, out RoomNetState state)
        {
            for (int i = 0; i < _roomStates.Count; i++)
            {
                state = _roomStates[i];
                if (state.RoomNodeId == nodeId)
                    return true;
            }

            state = default;
            return false;
        }

        /// <summary>仅服务端调用：推进到下一个状态。</summary>
        public bool TransitionTo(RunState nextState)
        {
            if (!IsServer)
            {
                Debug.LogWarning($"[RunManager] TransitionTo called from non-server. State={nextState}");
                return false;
            }

            RunState oldState = CurrentState;
            if (!CanTransition(oldState, nextState))
            {
                Debug.LogWarning($"[RunManager] Invalid transition: {oldState} -> {nextState}");
                return false;
            }

            Log($"{oldState} -> {nextState}");
            _currentState.Value = (int)nextState;
            EnterState(nextState);
            return true;
        }

        /// <summary>设置选中英雄（P0 默认值）。</summary>
        public void SetSelectedHero(string heroId, string loadoutId)
        {
            if (_sessionData == null) _sessionData = new RunSessionData();
            _sessionData.SelectedHeroId = heroId ?? "DefaultHero";
            _sessionData.SelectedLoadoutId = loadoutId;
        }

        // ── NetworkBehaviour Lifecycle ──

        private void Awake()
        {
            _roomStates = new NetworkList<RoomNetState>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            _currentState.OnValueChanged += OnCurrentStateChanged;
            _currentRoomNodeId.OnValueChanged += OnCurrentRoomNodeIdChanged;
            _roomStates.OnListChanged += OnRoomStatesChanged;

            if (IsServer)
            {
                InitStateHandlers();
                Log("RunManager spawned on server. Starting at MainMenu.");
            }
        }

        public override void OnNetworkDespawn()
        {
            _currentState.OnValueChanged -= OnCurrentStateChanged;
            _currentRoomNodeId.OnValueChanged -= OnCurrentRoomNodeIdChanged;
            if (_roomStates != null)
            {
                _roomStates.OnListChanged -= OnRoomStatesChanged;
            }
            UnregisterDeathListener();
            UnregisterVictoryListener();
            _roomStates?.Dispose();
            base.OnNetworkDespawn();
        }

        public override void OnDestroy()
        {
            _roomStates?.Dispose();
            base.OnDestroy();
        }

        private void Update()
        {
            if (!IsServer) return;
            TickCurrentState();
        }

        // ── State Change Callbacks (client + server) ──

        private void OnCurrentStateChanged(int previous, int current)
        {
            EventCenter.Instance.Trigger<RunStateChangedEvt>(EventName.RunStateChanged,
                new RunStateChangedEvt
                {
                    OldState = previous,
                    NewState = current,
                    RoomNodeId = _currentRoomNodeId.Value
                });
        }

        private void OnCurrentRoomNodeIdChanged(int previous, int current)
        {
            if (current >= 0 && _sessionData != null && !_sessionData.RoomChain.Contains(current))
                _sessionData.RoomChain.Add(current);
        }

        private void OnRoomStatesChanged(NetworkListEvent<RoomNetState> changeEvent)
        {
            // 客户端同步缓存
            for (int i = 0; i < _roomStates.Count; i++)
            {
                RoomNetState state = _roomStates[i];
                _roomRunStateCache[state.RoomNodeId] = state.State;
            }
        }

        // ── Server-Side State Handlers ──

        private void InitStateHandlers()
        {
            _stateEnterHandlers[RunState.Lobby] = EnterLobby;
            _stateEnterHandlers[RunState.HeroSelect] = EnterHeroSelect;
            _stateEnterHandlers[RunState.RunInit] = EnterRunInit;
            _stateEnterHandlers[RunState.RoomEnter] = EnterRoomEnter;
            _stateEnterHandlers[RunState.Exploring] = EnterExploring;
            _stateEnterHandlers[RunState.BossFight] = EnterBossFight;
            _stateEnterHandlers[RunState.RunVictory] = EnterRunVictory;
            _stateEnterHandlers[RunState.RunDefeat] = EnterRunDefeat;
            _stateEnterHandlers[RunState.RunSummary] = EnterRunSummary;
        }

        private void EnterState(RunState state)
        {
            if (_stateEnterHandlers.TryGetValue(state, out Action handler))
                handler?.Invoke();
        }

        private void TickCurrentState()
        {
            TickVictoryPoll();
        }

        /// <summary>
        /// 服务端保底轮询：每隔一定时间检查主任务是否已完成，完成则直接宣胜。
        /// 用于兜底 BossDeath → BossMission → BossDefeated 链路中任何可能的断裂。
        /// </summary>
        private void TickVictoryPoll()
        {
            if (!IsServer) return;

            RunState state = CurrentState;
            // 只在局内活跃状态轮询（Exploring / BossFight）
            if (state != RunState.Exploring && state != RunState.BossFight) return;

            _victoryPollTimer += Time.deltaTime;
            if (_victoryPollTimer < victoryPollInterval) return;
            _victoryPollTimer = 0f;

            if (missionManager == null) return;
            var runtimeMissions = missionManager.RuntimeMissions;
            if (runtimeMissions == null) return;

            for (int i = 0; i < runtimeMissions.Count; i++)
            {
                var m = runtimeMissions[i];
                if (m == null || m.Config == null) continue;
                if (m.Config.MissionCategory != MissionCategory.Primary) continue;
                if (m.State == MissionState.Completed)
                {
                    Log($"Victory poll detected Primary mission Completed. Slot={m.SlotIndex} Type={m.Config.MissionType}");
                    UnregisterDeathListener();
                    if (_sessionData != null) _sessionData.IsVictory = true;
                    TransitionTo(RunState.RunVictory);
                    return;
                }
            }
        }

        // ── Enter Handlers ──

        private void EnterLobby()
        {
            Log("Enter Lobby (P0: auto-advance)");

            if (runConfig != null && runConfig.SkipLobbyForTesting)
            {
                if (runConfig.SkipHeroSelectForTesting)
                {
                    SetSelectedHero("DefaultHero", null);
                    TransitionTo(RunState.RunInit);
                }
                else
                {
                    TransitionTo(RunState.HeroSelect);
                }
            }
        }

        private void EnterHeroSelect()
        {
            Log("Enter HeroSelect (P0: auto-select default)");
            SetSelectedHero("DefaultHero", null);
            TransitionTo(RunState.RunInit);
        }

        private void EnterRunInit()
        {
            RegisterVictoryListener();

            int seed = ResolveRunSeed();
            _difficultyLevel.Value = (int)(runConfig != null ? runConfig.DefaultDifficulty : RunDifficulty.Normal);

            string styleKey = MissionSessionConfig.HasSessionConfig
                ? MissionSessionConfig.StyleKey
                : (runConfig != null ? runConfig.DefaultStyleKey : "Default");

            if (runConfig != null)
                Log($"RunInit | Difficulty={Difficulty}, Style={styleKey}");

            // 从 MissionManager 拉取任务输入
            MapTaskInput taskInput;
            if (missionManager == null || !missionManager.TryBuildCurrentPcgTaskInput(out taskInput))
            {
                taskInput = CreateDefaultTaskInput();
            }

            string selectedHeroId = MissionSessionConfig.HasSessionConfig &&
                                    !string.IsNullOrEmpty(MissionSessionConfig.SelectedHeroId)
                ? MissionSessionConfig.SelectedHeroId
                : (_sessionData != null && !string.IsNullOrEmpty(_sessionData.SelectedHeroId)
                    ? _sessionData.SelectedHeroId
                    : "DefaultHero");

            string selectedLoadoutId = MissionSessionConfig.HasSessionConfig
                ? MissionSessionConfig.SelectedWeaponId
                : _sessionData?.SelectedLoadoutId;

            _sessionData = new RunSessionData
            {
                Seed = 0, // filled after successful generation
                Difficulty = Difficulty,
                StartTimeUtc = DateTime.UtcNow,
                SelectedHeroId = selectedHeroId,
                SelectedLoadoutId = selectedLoadoutId
            };

            // 订阅生成完成事件
            pcgMapGenerator.OnGenerationCompleted += OnMapGenerationCompleted;

            // ★ 种子重试循环：当前 seed 失败则换随机种子重试
            PcgMapGenerationResult result = null;
            int maxSeedAttempts = runConfig != null ? runConfig.MaxPcgSeedAttempts : 10;
            int seedAttempt = 0;

            while (seedAttempt < maxSeedAttempts)
            {
                var package = new PcgGeneratePackage
                {
                    StyleKey = styleKey,
                    Seed = seed,
                    TaskInput = taskInput,
                    RequestSource = $"RunManager|SeedAttempt={seedAttempt}"
                };

                result = pcgMapGenerator.Generate(package);
                if (result != null) break;

                Debug.LogWarning($"[RunManager] PCG failed with seed={seed}, attempt={seedAttempt + 1}/{maxSeedAttempts}. Trying next seed...", this);
                seed = UnityEngine.Random.Range(1, int.MaxValue);
                seedAttempt++;
            }

            if (result == null)
            {
                Debug.LogError($"[RunManager] PCG exhausted all {maxSeedAttempts} seed attempts. Aborting.", this);
                pcgMapGenerator.OnGenerationCompleted -= OnMapGenerationCompleted;
                return;
            }

            // ★ 成功后才同步种子给客户端
            _runSeed.Value = result.Seed;
            _sessionData.Seed = result.Seed;
            Log($"RunInit | Seed={result.Seed}, Style={styleKey}, SeedAttempt={seedAttempt}");

            _runContext = new RunContext(this, pcgMapGenerator, missionManager, monsterSpawnManager, runConfig);
        }

        private void OnMapGenerationCompleted(PcgMapGenerationResult result)
        {
            pcgMapGenerator.OnGenerationCompleted -= OnMapGenerationCompleted;

            _sessionData.MapResult = result;
            BuildRoomStates(result.Graph);

            // 初始化怪物刷新管理器
            if (monsterSpawnManager != null)
                monsterSpawnManager.InitializeWithMapResult(result, pcgMapGenerator.DefaultProfile);

            // 从 PCG 结果中解析出生房间（不再依赖硬编码 BirthRoomNodeId）
            int birthNodeId = ResolveBirthNodeId(result);
            _currentRoomNodeId.Value = birthNodeId;

            EventCenter.Instance.Trigger<int>(EventName.RunSeedFinalized, result.Seed);

            if (playerSpawnManager != null)
            {
                playerSpawnManager.SetRunManager(this);
                playerSpawnManager.OnPlayerSpawned += (clientId, netObj) =>
                    EventCenter.Instance.Trigger(EventName.PlayerSpawned,
                        new PlayerSpawnedEvt { NetworkObjectId = netObj.NetworkObjectId, ClientId = clientId });
                playerSpawnManager.OnPlayerDespawned += (clientId) =>
                    EventCenter.Instance.Trigger(EventName.PlayerDespawned,
                        new PlayerDespawnedEvt { NetworkObjectId = 0 });

                playerSpawnManager.OnMapReady(result, birthNodeId);
            }

            if (monsterSpawnManager != null)
            {
                monsterSpawnManager.OnEnemySpawned += (netId, netObj) =>
                    EventCenter.Instance.Trigger(EventName.EnemySpawned,
                        new EnemySpawnedEvt { NetworkObjectId = netObj.NetworkObjectId });
                monsterSpawnManager.OnEnemyDespawned += (netId) =>
                    EventCenter.Instance.Trigger(EventName.EnemyDespawned,
                        new EnemyDespawnedEvt { NetworkObjectId = netId });
            }

            TransitionTo(RunState.RoomEnter);
        }

        private void EnterRoomEnter()
        {
            int nodeId = _currentRoomNodeId.Value;
            if (_sessionData?.MapResult?.Graph == null)
            {
                Debug.LogError("[RunManager] EnterRoomEnter with no map graph.", this);
                return;
            }

            RoomGraphNode node = _sessionData.MapResult.Graph.GetNode(nodeId);
            if (node == null)
            {
                Debug.LogWarning($"[RunManager] Room node {nodeId} not found in graph.", this);
                TransitionTo(RunState.Exploring);
                return;
            }

            Log($"RoomEnter | NodeId={nodeId}, Role={node.AssignedRole}");

            EventCenter.Instance.Trigger<RoomEnteredEvt>(EventName.RoomEntered,
                new RoomEnteredEvt
                {
                    RoomNodeId = nodeId,
                    RoomRole = (int)node.AssignedRole,
                    EnteredByClientId = NetworkManager?.LocalClientId ?? 0
                });

            // 按房间角色路由：仅 Boss 房间有特殊流程，其余一律进自由探索
            if (node.AssignedRole == RoomRole.Boss)
                TransitionTo(RunState.BossFight);
            else
                TransitionTo(RunState.Exploring);
        }

        private void EnterExploring()
        {
            RegisterDeathListener();
            // MonsterSpawnManager 在 FixedUpdate 中自动根据玩家位置刷怪，无需额外触发
        }

        private void EnterBossFight()
        {
            int nodeId = _currentRoomNodeId.Value;
            _sessionData.CurrentRoomCombatStartUtc = DateTime.UtcNow;
            RegisterDeathListener();

            string bossId = "Boss_" + nodeId;

            EventCenter.Instance.Trigger<BossFightStartedEvt>(EventName.BossFightStarted,
                new BossFightStartedEvt { RoomNodeId = nodeId, BossId = bossId });
        }

        private void OnBossDefeated(BossDefeatedEvt evt)
        {
            UnregisterDeathListener();
            _sessionData.IsVictory = true;
            TransitionTo(RunState.RunVictory);
        }

        private void RegisterVictoryListener()
        {
            if (_registeredVictoryListener) return;
            EventCenter.Instance.AddListener<BossDefeatedEvt>(EventName.BossDefeated, OnBossDefeated);
            _registeredVictoryListener = true;
        }

        private void UnregisterVictoryListener()
        {
            if (!_registeredVictoryListener) return;
            EventCenter.Instance.RemoveListener<BossDefeatedEvt>(EventName.BossDefeated, OnBossDefeated);
            _registeredVictoryListener = false;
        }

        private void EnterRunVictory()
        {
            Log("RunVictory!");
            _sessionData.IsVictory = true;
            PublishRunResultEvent(EventName.RunVictory);
            TransitionTo(RunState.RunSummary);
        }

        private void EnterRunDefeat()
        {
            Log("RunDefeat!");
            _sessionData.IsVictory = false;
            UnregisterDeathListener();
            UnregisterVictoryListener();
            PublishRunResultEvent(EventName.RunDefeat);
            TransitionTo(RunState.RunSummary);
        }

        private void EnterRunSummary()
        {
            UnregisterDeathListener();
            UnregisterVictoryListener();
            Log("RunSummary — calculating results.");

            var summary = new RunSummaryData
            {
                IsVictory = _sessionData.IsVictory,
                Seed = _sessionData.Seed,
                Difficulty = _sessionData.Difficulty,
                TotalDuration = _sessionData.TotalDuration,
                TotalKills = Framework.LogicLayer.Module.SpawnSystem.MonsterRegistry.Instance?.TotalKilledCount ?? 0,
                RoomsCleared = _sessionData.RoomsCleared,
                TotalRooms = _sessionData.MapResult?.Graph?.NodeCount ?? 0,
                MapStyle = runConfig != null ? runConfig.DefaultStyleKey : "Default"
            };

            // 统计任务结果
            if (missionManager != null)
            {
                int sideCompleted = 0;
                int sideFailed = 0;
                _sessionData.MissionResults.Clear();
                var runtimeMissions = missionManager.RuntimeMissions;
                if (runtimeMissions != null)
                {
                    foreach (var m in runtimeMissions)
                    {
                        if (m == null || m.Config == null) continue;
                        _sessionData.MissionResults.Add(new MissionResultRecord
                        {
                            MissionType = m.Config.MissionType.ToString(),
                            IsSuccess = m.State == MissionState.Completed
                        });
                        if (m.Config.MissionCategory != MissionCategory.Secondary) continue;
                        if (m.State == MissionState.Completed) sideCompleted++;
                        else if (m.State == MissionState.Failed) sideFailed++;
                    }
                }
                _sessionData.SideTasksCompleted = sideCompleted;
                _sessionData.SideTasksFailed = sideFailed;
                _sessionData.TotalCurrencyEarned = 0;
                foreach (var m in runtimeMissions)
                {
                    if (m == null || m.Config == null) continue;
                    if (m.State == MissionState.Completed)
                        _sessionData.TotalCurrencyEarned += m.Config.CurrencyReward;
                }
            }

            // 写入 ArchiveManager
            if (runSummaryCalculator != null)
                runSummaryCalculator.CalculateAndRecord(_sessionData, runConfig);

            PublishRunResultEvent(EventName.RunSummaryReady);
        }

        private void PublishRunResultEvent(EventName eventName)
        {
            RunResultEvt evt = BuildRunResultEvt();
            EventCenter.Instance.Trigger<RunResultEvt>(eventName, evt);
            BroadcastRunResultEventToRemoteClients(eventName, evt);
        }

        private void BroadcastRunResultEventToRemoteClients(EventName eventName, RunResultEvt evt)
        {
            if (!IsServer || NetworkManager == null || NetworkManager.ConnectedClientsIds == null)
                return;

            ulong serverClientId = Unity.Netcode.NetworkManager.ServerClientId;
            var targetClientIds = new List<ulong>();
            foreach (ulong clientId in NetworkManager.ConnectedClientsIds)
            {
                if (clientId == serverClientId)
                    continue;

                targetClientIds.Add(clientId);
            }

            if (targetClientIds.Count == 0)
                return;

            var rpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = targetClientIds.ToArray()
                }
            };

            DispatchRunResultEventClientRpc(
                (int)eventName,
                evt.IsVictory,
                evt.Seed,
                evt.Difficulty,
                evt.TotalDurationSeconds,
                evt.TotalKills,
                evt.RoomsCleared,
                rpcParams);
        }

        [ClientRpc]
        private void DispatchRunResultEventClientRpc(
            int eventNameValue,
            bool isVictory,
            int seed,
            int difficulty,
            double totalDurationSeconds,
            int totalKills,
            int roomsCleared,
            ClientRpcParams rpcParams = default)
        {
            EventName eventName = (EventName)eventNameValue;
            if (eventName != EventName.RunVictory &&
                eventName != EventName.RunDefeat &&
                eventName != EventName.RunSummaryReady)
            {
                return;
            }

            EventCenter.Instance.Trigger<RunResultEvt>(eventName,
                new RunResultEvt
                {
                    IsVictory = isVictory,
                    Seed = seed,
                    Difficulty = difficulty,
                    TotalDurationSeconds = totalDurationSeconds,
                    TotalKills = totalKills,
                    RoomsCleared = roomsCleared
                });
        }

        // ── ServerRpc ──

        /// <summary>客户端通知服务端玩家进入房间（RunRoomTrigger → ServerRpc）。</summary>
        [ServerRpc(RequireOwnership = false)]
        public void ReportRoomEnteredServerRpc(int roomNodeId, ServerRpcParams rpcParams = default)
        {
            if (!IsServer) return;

            if (roomNodeId != _currentRoomNodeId.Value)
            {
                Log($"Player moved from Room {_currentRoomNodeId.Value} to Room {roomNodeId}");
                _currentRoomNodeId.Value = roomNodeId;
                TransitionTo(RunState.RoomEnter);
            }
        }

        // ── Death Detection ──

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
            if (!IsServer) return;

            // 仅检查玩家死亡
            int aliveCount = _runContext != null ? _runContext.GetAlivePlayerCount() : 1;
            if (aliveCount <= 0)
            {
                Log("All players dead!");
                EventCenter.Instance.Trigger(EventName.AllPlayersDead);
                UnregisterDeathListener();
                TransitionTo(RunState.RunDefeat);
            }
        }

        // ── Room State Helpers ──

        private void BuildRoomStates(RoomGraph graph)
        {
            _roomStates.Clear();
            _roomRunStateCache.Clear();

            for (int i = 0; i < graph.NodeCount; i++)
            {
                RoomGraphNode node = graph.GetNode(i);
                var state = new RoomNetState
                {
                    RoomNodeId = node.Id,
                    State = RoomRunState.Locked,
                    Role = node.AssignedRole,
                    EnemyCountAlive = 0
                };
                _roomStates.Add(state);
                _roomRunStateCache[node.Id] = RoomRunState.Locked;
            }

            // 出生房间初始设为 Available（从 PCG 结果中解析，不再依赖硬编码）
            int birthNodeId = ResolveBirthNodeIdFromGraph(graph);
            if (birthNodeId >= 0 && birthNodeId < graph.NodeCount)
            {
                UpdateRoomState(birthNodeId, RoomRunState.Available);
            }
        }

        private void UpdateRoomState(int nodeId, RoomRunState newState)
        {
            _roomRunStateCache[nodeId] = newState;

            for (int i = 0; i < _roomStates.Count; i++)
            {
                RoomNetState state = _roomStates[i];
                if (state.RoomNodeId == nodeId)
                {
                    state.State = newState;
                    _roomStates[i] = state;
                    return;
                }
            }
        }

        private RoomRunState GetRoomRunState(int nodeId)
        {
            _roomRunStateCache.TryGetValue(nodeId, out RoomRunState state);
            return state;
        }

        private int GetRoomRole(int nodeId)
        {
            RoomGraphNode node = _sessionData?.MapResult?.Graph?.GetNode(nodeId);
            return node != null ? (int)node.AssignedRole : -1;
        }

        // ── Validation ──

        private static bool CanTransition(RunState from, RunState to)
        {
            // 允许从任何状态进入 RunDefeat（全员死亡是突发条件）
            if (to == RunState.RunDefeat)
                return true;

            // 保底宣胜：允许从 Exploring 或 BossFight 进入 RunVictory
            if (to == RunState.RunVictory)
                return from == RunState.BossFight || from == RunState.Exploring;

            switch (from)
            {
                case RunState.MainMenu:
                    return to == RunState.Lobby;
                case RunState.Lobby:
                    return to == RunState.HeroSelect || to == RunState.RunInit;
                case RunState.HeroSelect:
                    return to == RunState.RunInit;
                case RunState.RunInit:
                    return to == RunState.RoomEnter;
                case RunState.RoomEnter:
                    return to == RunState.BossFight || to == RunState.Exploring;
                case RunState.Exploring:
                    return to == RunState.RoomEnter || to == RunState.RunVictory;
                case RunState.BossFight:
                    return to == RunState.RunVictory;
                case RunState.RunVictory:
                case RunState.RunDefeat:
                    return to == RunState.RunSummary;
                case RunState.RunSummary:
                    return to == RunState.MainMenu || to == RunState.Lobby;
                default:
                    return false;
            }
        }

        public static bool IsCombatRoom(RoomRole role)
        {
            return role == RoomRole.SideElimination ||
                   role == RoomRole.SideDefense ||
                   role == RoomRole.SideCapture ||
                   role == RoomRole.SideDestroy ||
                   role == RoomRole.Connector; // Connector rooms can also have combat
        }

        // ── Helpers ──

        /// <summary>
        /// 从 PCG 结果中解析出生房间的 NodeId。优先查 PlacedRooms（已实例化的房间），
        /// 找不到则回退到 Graph 中的 AssignedRole。
        /// </summary>
        private static int ResolveBirthNodeId(PcgMapGenerationResult result)
        {
            if (result == null) return -1;

            // 优先：已实例化的 Start 房间
            if (result.PlacedRooms != null)
            {
                foreach (var room in result.PlacedRooms)
                {
                    if (room != null && room.IsPhysicallyPlaced && room.Role == RoomRole.Start)
                        return room.NodeId;
                }
            }

            // 回退：图结构中标记为 Start 的节点
            return ResolveBirthNodeIdFromGraph(result.Graph);
        }

        /// <summary>
        /// 从 RoomGraph 中查找 AssignedRole == Start 的节点。
        /// </summary>
        private static int ResolveBirthNodeIdFromGraph(RoomGraph graph)
        {
            if (graph == null) return -1;

            for (int i = 0; i < graph.NodeCount; i++)
            {
                RoomGraphNode node = graph.GetNode(i);
                if (node != null && node.AssignedRole == RoomRole.Start)
                    return node.Id;
            }

            return -1;
        }

        private int ResolveRunSeed()
        {
            unchecked
            {
                return (int)DateTime.UtcNow.Ticks ^ UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            }
        }

        private static MapTaskInput CreateDefaultTaskInput()
        {
            return new MapTaskInput
            {
                PrimaryTask = new PrimaryTaskInput { TaskType = PrimaryTaskType.BossBattle },
                SideTasks = new List<SideTaskInput>
                {
                    new SideTaskInput { TaskType = SideTaskType.Elimination },
                    new SideTaskInput { TaskType = SideTaskType.Elimination }
                },
                TaskProvider = "RunManager.Default"
            };
        }

        private RunResultEvt BuildRunResultEvt()
        {
            if (_sessionData == null)
                return new RunResultEvt();

            return new RunResultEvt
            {
                IsVictory = _sessionData.IsVictory,
                Seed = _sessionData.Seed,
                Difficulty = (int)_sessionData.Difficulty,
                TotalDurationSeconds = _sessionData.TotalDuration.TotalSeconds,
                TotalKills = Framework.LogicLayer.Module.SpawnSystem.MonsterRegistry.Instance?.TotalKilledCount ?? 0,
                RoomsCleared = _sessionData.RoomsCleared
            };
        }

        /// <summary>
        /// 难度系数控制函数。
        /// 根据对局已用时间计算当前难度等级。
        /// 供 MonsterSpawnManager / 任务刷怪 等服务端系统读取，客户端不参与计算。
        /// </summary>
        /// <param name="intervalMinutes">每级时间间隔（分钟），默认 3 分钟。</param>
        /// <returns>当前难度等级（0 起）。</returns>
        public int GetDifficultyTier(float intervalMinutes = 3f)
        {
            if (_sessionData == null)
                return 0;

            double elapsedMinutes = _sessionData.TotalDuration.TotalMinutes;
            return Mathf.Max(0, Mathf.FloorToInt((float)(elapsedMinutes / intervalMinutes)));
        }

        private void Log(string msg)
        {
            if (verboseLog)
                Debug.Log($"[RunManager] {msg}", this);
        }
    }
}
