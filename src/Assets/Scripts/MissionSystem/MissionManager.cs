using System;
using System.Collections.Generic;
using Matrix.PCG;
using Unity.Netcode;
using UnityEngine;

namespace Matrix.Missions
{
    public enum MissionGuideTargetKind
    {
        None = 0,
        Connector = 1,
        TriggerArea = 2,
        Objective = 3,
        RoomCenter = 4
    }

    public struct MissionGuideTarget
    {
        public MissionGuideTargetKind Kind;
        public Vector3 WorldPoint;
        public Vector3 FinalWorldPoint;
        public int CurrentRoomNodeId;
        public int TargetRoomNodeId;
        public int NextRoomNodeId;
        public int PathNodeCount;
        public MissionTriggerZone TriggerZone;

        public bool IsValid => Kind != MissionGuideTargetKind.None;

        public static MissionGuideTarget Create(
            MissionGuideTargetKind kind,
            Vector3 worldPoint,
            int currentRoomNodeId,
            int targetRoomNodeId,
            int nextRoomNodeId,
            int pathNodeCount,
            MissionTriggerZone triggerZone = null,
            Vector3 finalWorldPoint = default)
        {
            return new MissionGuideTarget
            {
                Kind = kind,
                WorldPoint = worldPoint,
                FinalWorldPoint = finalWorldPoint,
                CurrentRoomNodeId = currentRoomNodeId,
                TargetRoomNodeId = targetRoomNodeId,
                NextRoomNodeId = nextRoomNodeId,
                PathNodeCount = pathNodeCount,
                TriggerZone = triggerZone
            };
        }
    }

    public sealed class MissionManager : NetworkBehaviour
    {
        [Header("Mission Data")]
        [SerializeField]
        internal MissionLibrary missionLibrary;

        [SerializeField]
        internal PcgMapGenerator pcgMapGenerator;

        [SerializeField]
        internal EnemySpawnService enemySpawnService;

        [Header("Integrations")]
        [SerializeField]
        private MonoBehaviour missionGroupProviderBehaviour;

        [SerializeField]
        internal MonoBehaviour missionLobbyForwarderBehaviour;

        [SerializeField]
        private MissionPointerManager missionPointerManager;

        [SerializeField]
        internal Framework.LogicLayer.Module.SpawnSystem.MonsterSpawnManager monsterSpawnManager;

        [Header("Runtime")]
        [SerializeField]
        private bool autoRequestMissionGroupOnSpawn = true;

        private NetworkList<MissionNetState> _replicatedMissions;
        private readonly List<MissionBase> _runtimeMissions = new List<MissionBase>();
        private readonly Dictionary<int, MissionBase> _missionBySlot = new Dictionary<int, MissionBase>();
        private readonly Dictionary<int, MissionTriggerZone> _triggerZones = new Dictionary<int, MissionTriggerZone>();
        private readonly HashSet<int> _rewardedMissionSlots = new HashSet<int>();
        private readonly HashSet<int> _localEnteredMissionSlots = new HashSet<int>();

        private MissionContext _missionContext;
        private MissionGroupRuntimeData _selectedMissionGroup;
        private bool _runtimeBootstrapped;
        private bool _registeredDeathListener;

        public IReadOnlyList<MissionBase> RuntimeMissions => _runtimeMissions;
        public PcgMapGenerationResult CurrentMapResult => pcgMapGenerator != null ? pcgMapGenerator.LastResult : null;

        private IMissionGroupProvider MissionGroupProvider => missionGroupProviderBehaviour as IMissionGroupProvider;
        private IMissionLobbyForwarder MissionLobbyForwarder => missionLobbyForwarderBehaviour as IMissionLobbyForwarder;

        public bool TryCollectMissionHudEntries(List<MissionHudEntry> entries)
        {
            if (entries == null)
            {
                return false;
            }

            entries.Clear();

            Dictionary<int, MissionHudEntry> entriesBySlot = new Dictionary<int, MissionHudEntry>();

            if (_replicatedMissions != null)
            {
                for (int i = 0; i < _replicatedMissions.Count; i++)
                {
                    MissionNetState state = _replicatedMissions[i];
                    string missionId = state.MissionId.ToString();
                    MissionConfig config = missionLibrary != null ? missionLibrary.FindById(missionId) : null;
                    MissionType missionType = config != null ? config.MissionType : state.MissionType;
                    MissionCategory missionCategory = config != null ? config.MissionCategory : state.MissionCategory;
                    string displayName = config != null && !string.IsNullOrWhiteSpace(config.DisplayName)
                        ? config.DisplayName
                        : missionId;

                    entriesBySlot[state.SlotIndex] = new MissionHudEntry
                    {
                        SlotIndex = state.SlotIndex,
                        MissionId = missionId,
                        MissionType = missionType,
                        MissionCategory = missionCategory,
                        State = state.State,
                        CurrentProgress = state.CurrentProgress,
                        TargetProgress = state.TargetProgress,
                        DisplayName = displayName,
                        StatusText = MissionHudStatusFormatter.ResolveStatusText(
                            missionType,
                            state.State,
                            state.CurrentProgress,
                            state.TargetProgress)
                    };
                }
            }

            for (int i = 0; i < _runtimeMissions.Count; i++)
            {
                MissionBase mission = _runtimeMissions[i];
                if (mission == null)
                {
                    continue;
                }

                string missionId = mission.Config != null ? mission.Config.MissionId : string.Empty;
                MissionType missionType = mission.Config != null ? mission.Config.MissionType : MissionType.Eliminate;
                MissionCategory missionCategory = mission.Config != null ? mission.Config.MissionCategory : MissionCategory.Secondary;
                string displayName = mission.Config != null && !string.IsNullOrWhiteSpace(mission.Config.DisplayName)
                    ? mission.Config.DisplayName
                    : mission.GetPointerLabel();

                entriesBySlot[mission.SlotIndex] = new MissionHudEntry
                {
                    SlotIndex = mission.SlotIndex,
                    MissionId = missionId,
                    MissionType = missionType,
                    MissionCategory = missionCategory,
                    State = mission.State,
                    CurrentProgress = mission.CurrentProgress,
                    TargetProgress = mission.TargetProgress,
                    DisplayName = displayName,
                    StatusText = mission.ResolveHudStatusText()
                };
            }

            entries.AddRange(entriesBySlot.Values);
            return entries.Count > 0;
        }

        /// <summary>
        /// NGO 对象生成后，开始向服务端请求任务组并监听同步列表。
        /// </summary>
        private void Awake()
        {
            _replicatedMissions = new NetworkList<MissionNetState>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            _replicatedMissions.OnListChanged += OnReplicatedMissionListChanged;
            RegisterUnitDeathListener();

            if (IsServer)
            {
                EnsureMissionGroupPrepared(NetworkManager != null ? NetworkManager.LocalClientId : 0);
            }

            if (autoRequestMissionGroupOnSpawn && IsClient)
            {
                RequestMissionGroupServerRpc();
            }
        }

        /// <summary>
        /// NGO 对象退场时取消订阅运行时回调。
        /// </summary>
        public override void OnNetworkDespawn()
        {
            if (_replicatedMissions != null)
            {
                _replicatedMissions.OnListChanged -= OnReplicatedMissionListChanged;
            }
            UnregisterUnitDeathListener();
            ClearRuntimeTriggerZones();
            _replicatedMissions?.Dispose();
            base.OnNetworkDespawn();
        }

        public override void OnDestroy()
        {
            ClearRuntimeTriggerZones();
            _replicatedMissions?.Dispose();
            base.OnDestroy();
        }

        /// <summary>
        /// 每帧尝试绑定地图结果并在服务端推进任务。
        /// </summary>
        private void Update()
        {
            TryBootstrapRuntimeMissions();

            if (!IsServer)
            {
                return;
            }

            for (int i = 0; i < _runtimeMissions.Count; i++)
            {
                MissionBase mission = _runtimeMissions[i];
                if (mission == null)
                {
                    continue;
                }

                MissionState beforeState = mission.State;
                int beforeProgress = mission.CurrentProgress;
                mission.TickServer(Time.deltaTime);

                if (beforeState != mission.State || beforeProgress != mission.CurrentProgress)
                {
                    SyncMissionState(mission);
                }
            }
        }

        /// <summary>
        /// 允许客户端进入地图时显式向服务端请求本局任务组。
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void RequestMissionGroupServerRpc(ServerRpcParams rpcParams = default)
        {
            EnsureMissionGroupPrepared(rpcParams.Receive.SenderClientId);
        }

        /// <summary>
        /// 向外提供当前任务组对应的 PCG 输入，便于地图系统在开局时直接装配。
        /// </summary>
        public bool TryBuildCurrentPcgTaskInput(out MapTaskInput taskInput)
        {
            taskInput = null;

            if (_selectedMissionGroup != null)
            {
                taskInput = _selectedMissionGroup.CreatePcgTaskInput();
                return taskInput != null;
            }

            return TryBuildReplicatedPcgTaskInput(out taskInput);
        }

        /// <summary>
        /// 客户端没有 _selectedMissionGroup，只能从 Host 同步的 MissionNetState 快照还原 PCG 任务语义。
        /// 用于等待房主 seed 后进行确定性本地地图复刻。
        /// </summary>
        private bool TryBuildReplicatedPcgTaskInput(out MapTaskInput taskInput)
        {
            taskInput = null;

            if (_replicatedMissions == null || _replicatedMissions.Count == 0)
            {
                return false;
            }

            List<MissionNetState> orderedStates = new List<MissionNetState>();
            for (int i = 0; i < _replicatedMissions.Count; i++)
            {
                orderedStates.Add(_replicatedMissions[i]);
            }

            orderedStates.Sort((a, b) => a.SlotIndex.CompareTo(b.SlotIndex));

            MapTaskInput input = new MapTaskInput
            {
                TaskProvider = "MissionManager.Replicated"
            };

            bool hasPrimary = false;
            for (int i = 0; i < orderedStates.Count; i++)
            {
                MissionNetState state = orderedStates[i];
                string externalTaskId = state.ExternalTaskId.ToString();

                if (state.MissionCategory == MissionCategory.Primary)
                {
                    input.PrimaryTask = new PrimaryTaskInput
                    {
                        TaskType = PrimaryTaskType.BossBattle,
                        ExternalTaskId = externalTaskId
                    };
                    hasPrimary = true;
                    continue;
                }

                input.SideTasks.Add(new SideTaskInput
                {
                    TaskType = MissionGroupRuntimeData.MapToSideTaskType(state.MissionType),
                    ExternalTaskId = externalTaskId
                });
            }

            if (!hasPrimary || input.SideTasks.Count == 0)
            {
                return false;
            }

            taskInput = input;
            return true;
        }

        /// <summary>
        /// 获取当前客户端本地玩家的 Transform，用于任务指引器计算。
        /// </summary>
        public Transform GetLocalPlayerTransform()
        {
            if (_missionContext == null)
            {
                return null;
            }

            PlayerActor playerActor = _missionContext.FindLocalPlayerActor();
            return playerActor != null ? playerActor.transform : null;
        }

        public bool HasLocalEnteredMission(int slotIndex)
        {
            return _localEnteredMissionSlots.Contains(slotIndex);
        }

        public bool TryResolveMissionGuideTarget(
            Vector3 playerWorldPosition,
            MissionBase mission,
            bool localMissionEntered,
            out MissionGuideTarget guideTarget)
        {
            guideTarget = default;

            PcgMapGenerationResult mapResult = CurrentMapResult;
            if (mapResult == null || mapResult.Graph == null || mission == null || mission.RoomNodeId < 0)
            {
                return false;
            }

            int targetRoomNodeId = mission.RoomNodeId;
            int currentRoomNodeId = FindNearestRoomNode(playerWorldPosition);
            Vector3 finalWorldPoint = mission.ResolveObjectiveGuidePoint();

            if (localMissionEntered)
            {
                Vector3 objectivePoint = mission.ResolveObjectiveGuidePoint();
                guideTarget = MissionGuideTarget.Create(
                    MissionGuideTargetKind.Objective,
                    objectivePoint,
                    currentRoomNodeId,
                    targetRoomNodeId,
                    targetRoomNodeId,
                    0,
                    mission.TriggerZone,
                    finalWorldPoint);
                return true;
            }

            if (currentRoomNodeId >= 0 && currentRoomNodeId != targetRoomNodeId)
            {
                List<int> path = BuildShortestGuidePath(mapResult, currentRoomNodeId, targetRoomNodeId);
                if (TryResolveNearestPathConnectorWorldPoint(
                        path,
                        out Vector3 connectorPoint,
                        out _,
                        out int linkedRoomNodeId))
                {
                    guideTarget = MissionGuideTarget.Create(
                        MissionGuideTargetKind.Connector,
                        connectorPoint,
                        currentRoomNodeId,
                        targetRoomNodeId,
                        linkedRoomNodeId,
                        path.Count,
                        mission.TriggerZone,
                        finalWorldPoint);
                    return true;
                }

                List<int> graphPath = BuildShortestPath(mapResult.Graph, currentRoomNodeId, targetRoomNodeId);
                if (TryResolveNearestPathConnectorWorldPoint(
                        graphPath,
                        out connectorPoint,
                        out _,
                        out linkedRoomNodeId))
                {
                    guideTarget = MissionGuideTarget.Create(
                        MissionGuideTargetKind.Connector,
                        connectorPoint,
                        currentRoomNodeId,
                        targetRoomNodeId,
                        linkedRoomNodeId,
                        graphPath.Count,
                        mission.TriggerZone,
                        finalWorldPoint);
                    return true;
                }

                if (TryResolveMissionTriggerAreaPoint(mission, out Vector3 fallbackTriggerAreaPoint, out MissionTriggerZone fallbackTriggerZone))
                {
                    guideTarget = MissionGuideTarget.Create(
                        MissionGuideTargetKind.TriggerArea,
                        fallbackTriggerAreaPoint,
                        currentRoomNodeId,
                        targetRoomNodeId,
                        targetRoomNodeId,
                        graphPath.Count,
                        fallbackTriggerZone,
                        finalWorldPoint);
                    return true;
                }

                Vector3 distantFallbackObjectivePoint = mission.ResolveObjectiveGuidePoint();
                guideTarget = MissionGuideTarget.Create(
                    MissionGuideTargetKind.Objective,
                    distantFallbackObjectivePoint,
                    currentRoomNodeId,
                    targetRoomNodeId,
                    targetRoomNodeId,
                    graphPath.Count,
                    mission.TriggerZone,
                    finalWorldPoint);
                return true;
            }

            if (TryResolveMissionTriggerAreaPoint(mission, out Vector3 triggerAreaPoint, out MissionTriggerZone triggerZone))
            {
                guideTarget = MissionGuideTarget.Create(
                    MissionGuideTargetKind.TriggerArea,
                    triggerAreaPoint,
                    currentRoomNodeId,
                    targetRoomNodeId,
                    targetRoomNodeId,
                    currentRoomNodeId == targetRoomNodeId ? 1 : 0,
                    triggerZone,
                    finalWorldPoint);
                return true;
            }

            Vector3 fallbackObjectivePoint = mission.ResolveObjectiveGuidePoint();
            guideTarget = MissionGuideTarget.Create(
                MissionGuideTargetKind.Objective,
                fallbackObjectivePoint,
                currentRoomNodeId,
                targetRoomNodeId,
                targetRoomNodeId,
                0,
                mission.TriggerZone,
                finalWorldPoint);
            return true;
        }

        /// <summary>
        /// 处理玩家进入任务触发区事件。
        /// </summary>
        public void HandleMissionTriggerEntered(MissionTriggerZone triggerZone, PlayerActor playerActor, bool isLocalPlayer)
        {
            if (triggerZone == null)
            {
                return;
            }

            if (isLocalPlayer)
            {
                _localEnteredMissionSlots.Add(triggerZone.SlotIndex);
                if (missionPointerManager != null)
                {
                    missionPointerManager.MarkLocalMissionEntered(triggerZone.SlotIndex, true);
                }
            }

            if (!IsServer || !_missionBySlot.TryGetValue(triggerZone.SlotIndex, out MissionBase mission) || mission == null)
            {
                return;
            }

            MissionState beforeState = mission.State;
            mission.HandlePlayerEnteredTrigger(playerActor, isLocalPlayer);

            if (beforeState != mission.State)
            {
                Debug.Log($"[MissionManager] 任务状态变更 Slot={triggerZone.SlotIndex} Room={triggerZone.RoomNodeId} {beforeState} → {mission.State}");
                SyncMissionState(mission);
            }
        }

        /// <summary>
        /// 由运行时追踪目标在销毁时回调，用于推进防御与破坏等任务。
        /// </summary>
        public void ReportTrackedTargetDestroyed(int slotIndex, int targetKey, ulong networkObjectId)
        {
            if (!IsServer || !_missionBySlot.TryGetValue(slotIndex, out MissionBase mission) || mission == null)
            {
                return;
            }

            MissionState beforeState = mission.State;
            int beforeProgress = mission.CurrentProgress;
            mission.HandleTrackedTargetDestroyed(targetKey, networkObjectId);

            if (beforeState != mission.State || beforeProgress != mission.CurrentProgress)
            {
                SyncMissionState(mission);
            }
        }

        /// <summary>
        /// 供外部占点逻辑调用，用于累加捕获任务进度。
        /// </summary>
        public void ReportCaptureProgress(int slotIndex, float deltaProgress)
        {
            if (!IsServer || !_missionBySlot.TryGetValue(slotIndex, out MissionBase mission) || !(mission is CaptureMission))
            {
                return;
            }

            CaptureMission captureMission = mission as CaptureMission;
            MissionState beforeState = captureMission.State;
            int beforeProgress = captureMission.CurrentProgress;
            captureMission.AddCaptureProgress(deltaProgress);

            if (beforeState != captureMission.State || beforeProgress != captureMission.CurrentProgress)
            {
                SyncMissionState(captureMission);
            }
        }

        /// <summary>
        /// 查询指定任务的推荐入口房间节点 ID（来自 TaskTriggerConnection.ConnectedNodeId）。
        /// 入口房间是任务的”前厅”，通过连接房引导玩家进入任务房。
        /// </summary>
        public void ReportCapturePickupCollected(int slotIndex, ulong requesterClientId, string itemId)
        {
            if (!IsServer || !_missionBySlot.TryGetValue(slotIndex, out MissionBase mission) || !(mission is CaptureMission))
            {
                return;
            }

            CaptureMission captureMission = mission as CaptureMission;
            MissionState beforeState = captureMission.State;
            int beforeProgress = captureMission.CurrentProgress;
            bool handled = captureMission.HandleCapturePickupCollected(requesterClientId, itemId);

            if (handled && (beforeState != captureMission.State || beforeProgress != captureMission.CurrentProgress))
            {
                SyncMissionState(captureMission);
            }
        }

        public bool TryGetRecommendedEntranceNodeId(int slotIndex, MissionType missionType, int roomNodeId, out int entranceNodeId)
        {
            entranceNodeId = -1;
            PcgMapGenerationResult mapResult = CurrentMapResult;
            if (mapResult?.TaskTriggerConnections == null)
            {
                return false;
            }

            RoomRole expectedRole = ResolveRoomRole(missionType);

            for (int i = 0; i < mapResult.TaskTriggerConnections.Count; i++)
            {
                TaskTriggerConnection connection = mapResult.TaskTriggerConnections[i];
                if (connection.TaskRole == expectedRole &&
                    connection.TaskNodeId == roomNodeId &&
                    connection.ConnectedNodeId >= 0)
                {
                    entranceNodeId = connection.ConnectedNodeId;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 为本地指引器解析从玩家当前位置到目标任务房间的”下一扇门”。
        /// </summary>
        public bool TryResolveNextDoorGuidePoint(Vector3 playerWorldPosition, int targetRoomNodeId, out Vector3 worldPoint)
        {
            worldPoint = Vector3.zero;

            PcgMapGenerationResult mapResult = CurrentMapResult;
            if (mapResult == null || mapResult.Graph == null)
            {
                return false;
            }

            int currentRoomNodeId = FindNearestRoomNode(playerWorldPosition);
            if (currentRoomNodeId < 0)
            {
                return false;
            }

            if (currentRoomNodeId == targetRoomNodeId)
            {
                return false;
            }

            List<int> path = BuildShortestGuidePath(mapResult, currentRoomNodeId, targetRoomNodeId);
            if (TryResolveNearestPathConnectorWorldPoint(path, out worldPoint, out _, out _))
            {
                return true;
            }

            List<int> graphPath = BuildShortestPath(mapResult.Graph, currentRoomNodeId, targetRoomNodeId);
            return TryResolveNearestPathConnectorWorldPoint(graphPath, out worldPoint, out _, out _);
        }

        /// <summary>
        /// 根据网络列表事件刷新本地任务运行时状态。
        /// </summary>
        private void OnReplicatedMissionListChanged(NetworkListEvent<MissionNetState> changeEvent)
        {
            TryBootstrapRuntimeMissions();

            if (changeEvent.Index < 0 || changeEvent.Index >= _replicatedMissions.Count)
            {
                return;
            }

            MissionNetState state = _replicatedMissions[changeEvent.Index];
            if (_missionBySlot.TryGetValue(state.SlotIndex, out MissionBase mission) && mission != null)
            {
                MissionState previousState = mission.State;
                int previousProgress = mission.CurrentProgress;
                mission.ApplyReplicatedState(state.State, state.CurrentProgress, state.TargetProgress);

                if (!IsServer)
                {
                    PublishMissionEvents(
                        mission,
                        previousState,
                        previousProgress,
                        mission.State,
                        mission.CurrentProgress,
                        grantRewards: false);
                }
            }
        }

        /// <summary>
        /// 在服务端确保本局任务组已经被拉取或随机完成。
        /// </summary>
        private void EnsureMissionGroupPrepared(ulong requesterClientId)
        {
            if (!IsServer || missionLibrary == null)
            {
                return;
            }

            if (_replicatedMissions.Count > 0 && _selectedMissionGroup != null)
            {
                return;
            }

            MissionPullContext pullContext = new MissionPullContext
            {
                RequesterClientId = requesterClientId,
                IsServer = IsServer,
                IsHost = IsHost,
                HasExistingGroup = _selectedMissionGroup != null,
                ConnectedClientCount = NetworkManager != null && NetworkManager.ConnectedClientsList != null ? NetworkManager.ConnectedClientsList.Count : 1,
                SuggestedSeed = ResolveSuggestedSeed(requesterClientId)
            };

            if (!TryResolveMissionGroup(pullContext, out _selectedMissionGroup) || _selectedMissionGroup == null)
            {
                Debug.LogError("[Mission] 无法构建本局任务组，请检查 MissionLibrary 或外部任务源。");
                return;
            }

            RebuildReplicatedMissionStates(_selectedMissionGroup);
        }

        /// <summary>
        /// 统一执行大厅转发、服务器拉取和本地随机三种任务组选取策略。
        /// </summary>
        private bool TryResolveMissionGroup(MissionPullContext context, out MissionGroupRuntimeData missionGroup)
        {
            missionGroup = null;

            if (MissionLobbyForwarder != null &&
                MissionLobbyForwarder.TryGetForwardedMissionGroup(context, missionLibrary, out missionGroup) &&
                missionGroup != null &&
                missionGroup.IsValidGroup())
            {
                return true;
            }

            if (MissionGroupProvider != null &&
                MissionGroupProvider.TryPullMissionGroup(context, missionLibrary, out missionGroup) &&
                missionGroup != null &&
                missionGroup.IsValidGroup())
            {
                return true;
            }

            if (!missionLibrary.TryBuildRandomGroup(context.SuggestedSeed, out missionGroup))
            {
                return false;
            }

            if (MissionLobbyForwarder != null)
            {
                MissionLobbyForwarder.ForwardHostMissionGroup(missionGroup);
            }

            return true;
        }

        /// <summary>
        /// 将服务端选中的任务组写入 NGO 同步列表。
        /// </summary>
        private void RebuildReplicatedMissionStates(MissionGroupRuntimeData missionGroup)
        {
            _replicatedMissions.Clear();
            _rewardedMissionSlots.Clear();
            _localEnteredMissionSlots.Clear();
            if (missionGroup == null || missionGroup.Missions == null)
            {
                return;
            }

            for (int i = 0; i < missionGroup.Missions.Count; i++)
            {
                MissionSelectionData selection = missionGroup.Missions[i];
                if (selection == null)
                {
                    continue;
                }

                MissionConfig config = missionLibrary != null ? missionLibrary.FindById(selection.MissionId) : null;
                MissionNetState state = MissionNetState.CreateInitial(selection.SlotIndex, config, selection.ExternalTaskId);
                if (config == null)
                {
                    state.MissionId = new Unity.Collections.FixedString128Bytes(selection.MissionId ?? string.Empty);
                    state.MissionType = selection.MissionType;
                    state.MissionCategory = selection.MissionCategory;
                    Debug.LogWarning($"[MissionManager] MissionConfig 未命中，使用任务组选项兜底写入同步快照。Slot={selection.SlotIndex} MissionId={selection.MissionId} Type={selection.MissionType} Category={selection.MissionCategory}");
                }

                _replicatedMissions.Add(state);
            }
        }

        /// <summary>
        /// 当地图生成结果就绪后，构建任务运行时实例、触发区和指引器。
        /// </summary>
        private void TryBootstrapRuntimeMissions()
        {
            if (_runtimeBootstrapped || missionLibrary == null || pcgMapGenerator == null || CurrentMapResult == null || _replicatedMissions.Count == 0)
            {
                return;
            }

            bool resolvedAllRooms = ResolveMissionRoomsAgainstMap();
            if (!HasAnyResolvedMissionRoom())
            {
                return;
            }

            EnsureMissionPointerManager();
            _missionContext = new MissionContext(this, missionLibrary, pcgMapGenerator, CurrentMapResult, enemySpawnService, missionPointerManager);

            for (int i = 0; i < _replicatedMissions.Count; i++)
            {
                MissionNetState state = _replicatedMissions[i];
                MissionConfig config = missionLibrary.FindById(state.MissionId.ToString());
                MissionType missionType = config != null ? config.MissionType : state.MissionType;
                if (config == null || state.RoomNodeId < 0 || !IsMissionRoomBindable(missionType, state.RoomNodeId))
                {
                    if (state.RoomNodeId < 0)
                    {
                        Debug.LogWarning($"[MissionManager] 任务房间尚未绑定，跳过运行时注册。Slot={state.SlotIndex} Type={state.MissionType} MissionId={state.MissionId}");
                    }
                    else
                    {
                        Debug.LogWarning($"[MissionManager] 任务房间与实际 PCG 房间不匹配，跳过运行时注册。Slot={state.SlotIndex} Type={missionType} Room={state.RoomNodeId} MissionId={state.MissionId}");
                    }
                    continue;
                }

                MissionBase mission = CreateMissionInstance(config.MissionType);
                if (mission == null)
                {
                    continue;
                }

                mission.Initialize(_missionContext, config, state.SlotIndex, state.RoomNodeId);
                mission.ApplyReplicatedState(state.State, state.CurrentProgress, state.TargetProgress);

                if (config.TriggerOnRoomEnter && _missionContext.TryGetPlacedRoom(state.RoomNodeId, out PcgPlacedRoom placedRoom) && placedRoom.RoomInstance != null)
                {
                    if (MissionTriggerZone.TryAttachToRoomBounds(
                            placedRoom.RoomInstance,
                            this,
                            state.SlotIndex,
                            state.RoomNodeId,
                            out MissionTriggerZone zone))
                    {
                        mission.BindTriggerZone(zone);
                        _triggerZones[state.SlotIndex] = zone;
                    }
                    else
                    {
                        Debug.LogWarning($"[MissionManager] 任务房间缺少可用的 PcgRoomBounds/BoundCollider，无法绑定进房触发。Slot={state.SlotIndex} Room={state.RoomNodeId} Type={missionType}");
                    }
                }

                _runtimeMissions.Add(mission);
                _missionBySlot[state.SlotIndex] = mission;
            }

            if (IsServer)
            {
                for (int i = 0; i < _runtimeMissions.Count; i++)
                {
                    MissionBase mission = _runtimeMissions[i];
                    if (mission == null)
                    {
                        continue;
                    }

                    if (mission.State == MissionState.Inactive)
                    {
                        mission.Prepare();
                        SyncMissionState(mission);
                    }
                }
            }

            if (missionPointerManager != null)
            {
                missionPointerManager.Initialize(this);
            }

            if (!resolvedAllRooms)
            {
                Debug.LogWarning("[MissionManager] 存在任务未绑定到 PCG 房间，已先注册可用任务。请检查 MissionGroup 与 PCG TaskInput/RoomRole 分配是否一致。");
            }

            // MonsterSpawnManager 已由 RunManager.OnMapGenerationCompleted() 初始化，
            // 此处仅保留 PollMissionState()（在 FixedUpdate 中自动运行）来更新玩家人数和活跃任务数。

            _runtimeBootstrapped = true;
        }

        /// <summary>
        /// 将地图图结构中的任务房间分配结果回填到网络任务状态中。
        /// </summary>
        private bool ResolveMissionRoomsAgainstMap()
        {
            if (CurrentMapResult == null || CurrentMapResult.Graph == null)
            {
                return false;
            }

            if (!IsServer)
            {
                for (int i = 0; i < _replicatedMissions.Count; i++)
                {
                    MissionNetState state = _replicatedMissions[i];
                    if (state.RoomNodeId < 0 || !IsMissionRoomBindable(state.MissionType, state.RoomNodeId))
                    {
                        return false;
                    }
                }

                return true;
            }

            bool resolvedAll = true;
            HashSet<int> usedRoomNodeIds = new HashSet<int>();
            for (int i = 0; i < _replicatedMissions.Count; i++)
            {
                MissionNetState state = _replicatedMissions[i];
                int existingNodeId = state.RoomNodeId;
                if (existingNodeId >= 0)
                {
                    if (IsMissionRoomBindable(state.MissionType, existingNodeId))
                    {
                        usedRoomNodeIds.Add(existingNodeId);
                    }
                    else
                    {
                        state.RoomNodeId = -1;
                        _replicatedMissions[i] = state;
                        resolvedAll = false;
                    }
                }
            }

            for (int i = 0; i < _replicatedMissions.Count; i++)
            {
                MissionNetState state = _replicatedMissions[i];
                if (state.RoomNodeId >= 0)
                {
                    continue;
                }

                int roomNodeId = FindMissionRoomNode(state.MissionType, usedRoomNodeIds);
                if (roomNodeId < 0)
                {
                    resolvedAll = false;
                    continue;
                }

                state.RoomNodeId = roomNodeId;
                usedRoomNodeIds.Add(roomNodeId);
                _replicatedMissions[i] = state;
            }

            return resolvedAll;
        }

        private bool HasAnyResolvedMissionRoom()
        {
            if (_replicatedMissions == null)
            {
                return false;
            }

            for (int i = 0; i < _replicatedMissions.Count; i++)
            {
                MissionNetState state = _replicatedMissions[i];
                if (state.RoomNodeId >= 0 && IsMissionRoomBindable(state.MissionType, state.RoomNodeId))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 根据任务类型在当前地图图结构中查找对应的房间节点。
        /// </summary>
        private int FindMissionRoomNode(MissionType missionType, HashSet<int> usedRoomNodeIds)
        {
            if (CurrentMapResult == null || CurrentMapResult.Graph == null)
            {
                return -1;
            }

            int triggerNodeId = FindMissionRoomNodeFromTaskTriggerConnections(missionType, usedRoomNodeIds);
            if (triggerNodeId >= 0)
            {
                return triggerNodeId;
            }

            for (int i = 0; i < CurrentMapResult.Graph.NodeCount; i++)
            {
                RoomGraphNode node = CurrentMapResult.Graph.GetNode(i);
                if (node == null)
                {
                    continue;
                }

                if (usedRoomNodeIds != null && usedRoomNodeIds.Contains(node.Id))
                {
                    continue;
                }

                if (missionType == MissionType.Boss && IsMissionRoomBindable(missionType, node.Id))
                {
                    return node.Id;
                }

                if (missionType != MissionType.Boss && IsMissionRoomBindable(missionType, node.Id))
                {
                    return node.Id;
                }
            }

            return -1;
        }

        private int FindMissionRoomNodeFromTaskTriggerConnections(MissionType missionType, HashSet<int> usedRoomNodeIds)
        {
            if (CurrentMapResult?.TaskTriggerConnections == null || CurrentMapResult.TaskTriggerConnections.Count == 0)
            {
                return -1;
            }

            RoomRole expectedRole = ResolveRoomRole(missionType);
            int fallbackNodeId = -1;

            for (int i = 0; i < CurrentMapResult.TaskTriggerConnections.Count; i++)
            {
                TaskTriggerConnection connection = CurrentMapResult.TaskTriggerConnections[i];
                if (connection.TaskRole != expectedRole)
                {
                    continue;
                }

                if (usedRoomNodeIds != null && usedRoomNodeIds.Contains(connection.TaskNodeId))
                {
                    continue;
                }

                if (!IsMissionRoomBindable(missionType, connection.TaskNodeId))
                {
                    continue;
                }

                if (connection.IsPrimaryTrigger)
                {
                    return connection.TaskNodeId;
                }

                if (fallbackNodeId < 0)
                {
                    fallbackNodeId = connection.TaskNodeId;
                }
            }

            return fallbackNodeId;
        }

        private bool IsMissionRoomBindable(MissionType missionType, int roomNodeId)
        {
            if (CurrentMapResult == null || CurrentMapResult.Graph == null || roomNodeId < 0)
            {
                return false;
            }

            RoomGraphNode node = FindGraphNode(roomNodeId);
            if (node == null)
            {
                return false;
            }

            RoomRole expectedRole = ResolveRoomRole(missionType);
            if (node.AssignedRole != expectedRole)
            {
                return false;
            }

            if (missionType != MissionType.Boss)
            {
                SideTaskType expectedSideTask = MissionGroupRuntimeData.MapToSideTaskType(missionType);
                if (!node.HasAssignedSideTask || node.AssignedSideTask != expectedSideTask)
                {
                    return false;
                }
            }

            PcgPlacedRoom placedRoom = FindPlacedRoom(roomNodeId);
            if (placedRoom == null || placedRoom.RoomInstance == null)
            {
                return false;
            }

            if (placedRoom.Role != expectedRole)
            {
                return false;
            }

            return true;
        }

        private RoomGraphNode FindGraphNode(int nodeId)
        {
            if (CurrentMapResult == null || CurrentMapResult.Graph == null)
            {
                return null;
            }

            for (int i = 0; i < CurrentMapResult.Graph.NodeCount; i++)
            {
                RoomGraphNode node = CurrentMapResult.Graph.GetNode(i);
                if (node != null && node.Id == nodeId)
                {
                    return node;
                }
            }

            return null;
        }

        private PcgPlacedRoom FindPlacedRoom(int nodeId)
        {
            if (CurrentMapResult == null || CurrentMapResult.PlacedRooms == null)
            {
                return null;
            }

            for (int i = 0; i < CurrentMapResult.PlacedRooms.Count; i++)
            {
                PcgPlacedRoom placedRoom = CurrentMapResult.PlacedRooms[i];
                if (placedRoom != null && placedRoom.NodeId == nodeId)
                {
                    return placedRoom;
                }
            }

            return null;
        }

        private static RoomRole ResolveRoomRole(MissionType missionType)
        {
            switch (missionType)
            {
                case MissionType.Boss:
                    return RoomRole.Boss;
                case MissionType.Defense:
                    return RoomRole.SideDefense;
                case MissionType.Capture:
                    return RoomRole.SideCapture;
                case MissionType.Destroy:
                    return RoomRole.SideDestroy;
                default:
                    return RoomRole.SideElimination;
            }
        }

        /// <summary>
        /// 工厂化创建具体任务实例，保持 MissionManager 只依赖统一入口。
        /// </summary>
        private static MissionBase CreateMissionInstance(MissionType missionType)
        {
            switch (missionType)
            {
                case MissionType.Boss:
                    return new BossMission();
                case MissionType.Defense:
                    return new DefenseMission();
                case MissionType.Capture:
                    return new CaptureMission();
                case MissionType.Destroy:
                    return new DestroyMission();
                default:
                    return new EliminateMission();
            }
        }

        private void EnsureMissionPointerManager()
        {
            if (missionPointerManager != null)
            {
                return;
            }

            missionPointerManager = FindObjectOfType<MissionPointerManager>();
            if (missionPointerManager != null)
            {
                return;
            }

            GameObject pointerManagerObject = new GameObject("MissionPointerManager_Runtime");
            missionPointerManager = pointerManagerObject.AddComponent<MissionPointerManager>();
        }

        /// <summary>
        /// 将运行时任务状态回写到 NGO 同步列表。
        /// </summary>
        private void SyncMissionState(MissionBase mission)
        {
            if (!IsServer || mission == null)
            {
                return;
            }

            for (int i = 0; i < _replicatedMissions.Count; i++)
            {
                MissionNetState state = _replicatedMissions[i];
                if (state.SlotIndex != mission.SlotIndex)
                {
                    continue;
                }

                MissionState previousState = state.State;
                int previousProgress = state.CurrentProgress;
                state.State = mission.State;
                state.CurrentProgress = mission.CurrentProgress;
                state.TargetProgress = mission.TargetProgress;
                state.RoomNodeId = mission.RoomNodeId;
                _replicatedMissions[i] = state;
                PublishMissionEvents(
                    mission,
                    previousState: previousState,
                    previousProgress: previousProgress,
                    currentState: mission.State,
                    currentProgress: mission.CurrentProgress,
                    grantRewards: true);
                return;
            }
        }

        private void PublishMissionEvents(
            MissionBase mission,
            MissionState previousState,
            int previousProgress,
            MissionState currentState,
            int currentProgress,
            bool grantRewards)
        {
            if (mission == null)
            {
                return;
            }

            string missionId = mission.Config != null ? mission.Config.MissionId : string.Empty;
            int missionType = mission.Config != null ? (int)mission.Config.MissionType : -1;

            if (previousState != currentState)
            {
                EventCenter.Instance.Trigger(EventName.MissionStateChanged,
                    new MissionStateChangedEvt
                    {
                        SlotIndex = mission.SlotIndex,
                        MissionId = missionId,
                        MissionType = missionType,
                        OldState = (int)previousState,
                        NewState = (int)currentState,
                        RoomNodeId = mission.RoomNodeId
                    });
            }

            if (previousProgress != currentProgress)
            {
                EventCenter.Instance.Trigger(EventName.MissionProgressChanged,
                    new MissionProgressChangedEvt
                    {
                        SlotIndex = mission.SlotIndex,
                        MissionId = missionId,
                        MissionType = missionType,
                        State = (int)currentState,
                        CurrentProgress = mission.CurrentProgress,
                        TargetProgress = mission.TargetProgress,
                        RoomNodeId = mission.RoomNodeId
                    });
            }

            if (previousState != MissionState.Completed && currentState == MissionState.Completed)
            {
                if (grantRewards)
                {
                    GrantMissionRewards(mission);
                }

                EventCenter.Instance.Trigger(EventName.MissionCompleted,
                    new MissionCompletedEvt
                    {
                        SlotIndex = mission.SlotIndex,
                        MissionId = missionId,
                        MissionType = missionType,
                        RoomNodeId = mission.RoomNodeId
                    });
            }

            if (previousState != MissionState.Failed && currentState == MissionState.Failed)
            {
                EventCenter.Instance.Trigger(EventName.MissionFailed,
                    new MissionFailedEvt
                    {
                        SlotIndex = mission.SlotIndex,
                        MissionId = missionId,
                        MissionType = missionType,
                        RoomNodeId = mission.RoomNodeId
                    });
            }
        }

        private void GrantMissionRewards(MissionBase mission)
        {
            if (!IsServer || mission == null || mission.Config == null || _rewardedMissionSlots.Contains(mission.SlotIndex))
            {
                return;
            }

            _rewardedMissionSlots.Add(mission.SlotIndex);

            if (NetworkManager == null || NetworkManager.ConnectedClientsList == null)
            {
                return;
            }

            // 发放货币奖励
            int currencyAmount = mission.Config.CurrencyReward;
            if (currencyAmount > 0)
            {
                for (int clientIndex = 0; clientIndex < NetworkManager.ConnectedClientsList.Count; clientIndex++)
                {
                    NetworkObject playerObject = NetworkManager.ConnectedClientsList[clientIndex].PlayerObject;
                    NetworkInventory inventory = playerObject != null ? playerObject.GetComponent<NetworkInventory>() : null;
                    if (inventory == null)
                    {
                        continue;
                    }

                    inventory.InGameCurrency.Value += currencyAmount;
                }

                Debug.Log($"[MissionManager] 货币奖励发放: Slot={mission.SlotIndex} Type={mission.Config.MissionType} +{currencyAmount}");
            }

            // 发放物品奖励
            IReadOnlyList<MissionRewardEntry> rewards = mission.Config.Rewards;
            if (rewards != null && rewards.Count > 0)
            {
                for (int i = 0; i < rewards.Count; i++)
                {
                    MissionRewardEntry reward = rewards[i];
                    if (reward == null || string.IsNullOrWhiteSpace(reward.RewardId) || reward.Amount <= 0)
                    {
                        continue;
                    }

                    BaseInventoryItemSO itemSO = SOManager.Instance != null
                        ? SOManager.Instance.GetSOById<BaseInventoryItemSO>(reward.RewardId)
                        : null;

                    if (itemSO == null)
                    {
                        Debug.LogWarning($"[MissionManager] 任务奖励未找到道具 SO: {reward.RewardId}");
                        continue;
                    }

                    InventoryItem item = new InventoryItem(itemSO);
                    for (int clientIndex = 0; clientIndex < NetworkManager.ConnectedClientsList.Count; clientIndex++)
                    {
                        NetworkObject playerObject = NetworkManager.ConnectedClientsList[clientIndex].PlayerObject;
                        NetworkInventory inventory = playerObject != null ? playerObject.GetComponent<NetworkInventory>() : null;
                        if (inventory == null)
                        {
                            continue;
                        }

                        inventory.TryAddItemServer(item, reward.Amount);
                    }
                }
            }
        }

        /// <summary>
        /// 注册单位死亡事件，供 Boss/歼灭类任务进行击杀判定。
        /// </summary>
        private void RegisterUnitDeathListener()
        {
            if (_registeredDeathListener || !IsServer)
            {
                return;
            }

            EventCenter.Instance.AddListener<UnitDiedEvt>(EventName.UnitDied, HandleUnitDiedEvent);
            _registeredDeathListener = true;
        }

        /// <summary>
        /// 取消单位死亡事件监听。
        /// </summary>
        private void UnregisterUnitDeathListener()
        {
            if (!_registeredDeathListener || !IsServer)
            {
                return;
            }

            EventCenter.Instance.RemoveListener<UnitDiedEvt>(EventName.UnitDied, HandleUnitDiedEvent);
            _registeredDeathListener = false;
        }

        private void ClearRuntimeTriggerZones()
        {
            foreach (KeyValuePair<int, MissionTriggerZone> pair in _triggerZones)
            {
                MissionTriggerZone zone = pair.Value;
                if (zone == null)
                {
                    continue;
                }

                zone.ClearMissionManager(this);
                if (zone.AddedByMissionSystem)
                {
                    Destroy(zone);
                }
            }

            _triggerZones.Clear();
        }

        /// <summary>
        /// 把统一死亡事件分发给所有可处理击杀的任务实例。
        /// </summary>
        private void HandleUnitDiedEvent(UnitDiedEvt unitDiedEvent)
        {
            for (int i = 0; i < _runtimeMissions.Count; i++)
            {
                MissionBase mission = _runtimeMissions[i];
                if (mission == null)
                {
                    continue;
                }

                MissionState beforeState = mission.State;
                int beforeProgress = mission.CurrentProgress;
                bool handled = mission.HandleUnitDied(unitDiedEvent.unitId);

                if (handled && (beforeState != mission.State || beforeProgress != mission.CurrentProgress))
                {
                    SyncMissionState(mission);
                }
            }
        }

        /// <summary>
        /// 从当前地图结果中查找两个房间节点间的连接关系。
        /// </summary>
        private PcgRoomConnection FindConnection(int nodeA, int nodeB)
        {
            if (CurrentMapResult == null || CurrentMapResult.Connections == null)
            {
                return null;
            }

            for (int i = 0; i < CurrentMapResult.Connections.Count; i++)
            {
                PcgRoomConnection connection = CurrentMapResult.Connections[i];
                if (connection == null)
                {
                    continue;
                }

                bool samePair = (connection.NodeA == nodeA && connection.NodeB == nodeB) ||
                                (connection.NodeA == nodeB && connection.NodeB == nodeA);

                if (samePair)
                {
                    return connection;
                }
            }

            return null;
        }

        /// <summary>
        /// 选择当前房间朝向下一房间的开启门位置，作为指引器目标。
        /// </summary>
        private bool TryResolveConnectorWorldPoint(PcgRoomConnection connection, int currentRoomNodeId, out Vector3 worldPoint)
        {
            worldPoint = Vector3.zero;
            if (connection == null || !connection.IsResolved)
            {
                return false;
            }

            PcgConnectorMarker connector = null;
            bool usedAsOutgoing = false;

            if (connection.NodeA == currentRoomNodeId)
            {
                connector = connection.ConnectorFrom;
                usedAsOutgoing = connection.ConnectorFromOutgoing;
            }
            else if (connection.NodeB == currentRoomNodeId)
            {
                connector = connection.ConnectorTo;
                usedAsOutgoing = connection.ConnectorToOutgoing;
            }

            if (connector == null || !connector.gameObject.activeInHierarchy)
            {
                return false;
            }

            worldPoint = connector.GetSocketWorldPoint(usedAsOutgoing);
            return true;
        }

        private bool TryResolveNearestPathConnectorWorldPoint(
            List<int> path,
            out Vector3 worldPoint,
            out int connectorRoomNodeId,
            out int linkedRoomNodeId)
        {
            worldPoint = Vector3.zero;
            connectorRoomNodeId = -1;
            linkedRoomNodeId = -1;

            if (path == null || path.Count < 2)
            {
                return false;
            }

            for (int i = 0; i < path.Count - 1; i++)
            {
                int fromNodeId = path[i];
                int toNodeId = path[i + 1];
                PcgRoomConnection connection = FindConnection(fromNodeId, toNodeId);

                if (TryResolveConnectorWorldPoint(connection, fromNodeId, out worldPoint))
                {
                    connectorRoomNodeId = fromNodeId;
                    linkedRoomNodeId = toNodeId;
                    return true;
                }

                if (TryResolveConnectorWorldPoint(connection, toNodeId, out worldPoint))
                {
                    connectorRoomNodeId = toNodeId;
                    linkedRoomNodeId = fromNodeId;
                    return true;
                }
            }

            return false;
        }

        private bool TryResolveMissionTriggerAreaPoint(
            MissionBase mission,
            out Vector3 worldPoint,
            out MissionTriggerZone triggerZone)
        {
            worldPoint = Vector3.zero;
            triggerZone = mission != null ? mission.TriggerZone : null;
            if (triggerZone != null && triggerZone.TryGetWorldBounds(out Bounds triggerBounds))
            {
                worldPoint = triggerBounds.center;
                return true;
            }

            return mission != null &&
                   mission.Context != null &&
                   mission.Context.TryResolveRoomCenter(mission.RoomNodeId, out worldPoint);
        }

        /// <summary>
        /// 通过房间包围盒判定玩家当前或最近所在的房间节点。
        /// </summary>
        private int FindNearestRoomNode(Vector3 worldPosition)
        {
            if (_missionContext == null || CurrentMapResult == null || CurrentMapResult.PlacedRooms == null)
            {
                return -1;
            }

            int bestNodeId = -1;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < CurrentMapResult.PlacedRooms.Count; i++)
            {
                PcgPlacedRoom room = CurrentMapResult.PlacedRooms[i];
                if (room == null || room.RoomInstance == null)
                {
                    continue;
                }

                if (!room.RoomInstance.TryGetWorldBounds(out Bounds bounds, out _))
                {
                    continue;
                }

                if (bounds.Contains(worldPosition))
                {
                    return room.NodeId;
                }

                float distance = bounds.SqrDistance(worldPosition);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestNodeId = room.NodeId;
                }
            }

            return bestNodeId;
        }

        private static List<int> BuildShortestGuidePath(PcgMapGenerationResult mapResult, int startNodeId, int targetNodeId)
        {
            List<int> result = new List<int>();
            RoomGraph graph = mapResult != null ? mapResult.Graph : null;
            if (graph == null ||
                mapResult.Connections == null ||
                startNodeId < 0 ||
                targetNodeId < 0 ||
                startNodeId >= graph.NodeCount ||
                targetNodeId >= graph.NodeCount)
            {
                return result;
            }

            Queue<int> queue = new Queue<int>();
            int[] predecessor = new int[graph.NodeCount];
            bool[] visited = new bool[graph.NodeCount];

            for (int i = 0; i < predecessor.Length; i++)
            {
                predecessor[i] = -1;
            }

            visited[startNodeId] = true;
            queue.Enqueue(startNodeId);

            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                if (current == targetNodeId)
                {
                    break;
                }

                List<int> neighbors = GetGuideNeighborsSorted(mapResult, current);
                for (int i = 0; i < neighbors.Count; i++)
                {
                    int neighbor = neighbors[i];
                    if (neighbor < 0 || neighbor >= visited.Length || visited[neighbor])
                    {
                        continue;
                    }

                    visited[neighbor] = true;
                    predecessor[neighbor] = current;
                    queue.Enqueue(neighbor);
                }
            }

            if (!visited[targetNodeId])
            {
                return result;
            }

            int walk = targetNodeId;
            while (walk >= 0)
            {
                result.Insert(0, walk);
                walk = predecessor[walk];
            }

            return result;
        }

        private static List<int> GetGuideNeighborsSorted(PcgMapGenerationResult mapResult, int nodeId)
        {
            List<int> neighbors = new List<int>();
            if (mapResult?.Connections == null)
            {
                return neighbors;
            }

            for (int i = 0; i < mapResult.Connections.Count; i++)
            {
                PcgRoomConnection connection = mapResult.Connections[i];
                if (!IsGuideConnectionUsable(connection))
                {
                    continue;
                }

                if (connection.NodeA == nodeId)
                {
                    neighbors.Add(connection.NodeB);
                }
                else if (connection.NodeB == nodeId)
                {
                    neighbors.Add(connection.NodeA);
                }
            }

            neighbors.Sort();
            return neighbors;
        }

        private static bool IsGuideConnectionUsable(PcgRoomConnection connection)
        {
            if (connection == null || !connection.IsResolved)
            {
                return false;
            }

            bool fromUsable = connection.ConnectorFrom != null && connection.ConnectorFrom.gameObject.activeInHierarchy;
            bool toUsable = connection.ConnectorTo != null && connection.ConnectorTo.gameObject.activeInHierarchy;
            return fromUsable || toUsable;
        }

        /// <summary>
        /// 在图结构上构造两个房间节点之间的最短路径。
        /// </summary>
        private static List<int> BuildShortestPath(RoomGraph graph, int startNodeId, int targetNodeId)
        {
            List<int> result = new List<int>();
            if (graph == null || startNodeId < 0 || targetNodeId < 0 || startNodeId >= graph.NodeCount || targetNodeId >= graph.NodeCount)
            {
                return result;
            }

            Queue<int> queue = new Queue<int>();
            int[] predecessor = new int[graph.NodeCount];
            bool[] visited = new bool[graph.NodeCount];

            for (int i = 0; i < predecessor.Length; i++)
            {
                predecessor[i] = -1;
            }

            visited[startNodeId] = true;
            queue.Enqueue(startNodeId);

            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                if (current == targetNodeId)
                {
                    break;
                }

                List<int> neighbors = graph.GetNeighborsSorted(current);
                for (int i = 0; i < neighbors.Count; i++)
                {
                    int neighbor = neighbors[i];
                    if (visited[neighbor])
                    {
                        continue;
                    }

                    visited[neighbor] = true;
                    predecessor[neighbor] = current;
                    queue.Enqueue(neighbor);
                }
            }

            if (!visited[targetNodeId])
            {
                return result;
            }

            int walk = targetNodeId;
            while (walk >= 0)
            {
                result.Insert(0, walk);
                walk = predecessor[walk];
            }

            return result;
        }

        /// <summary>
        /// 根据请求者和时间生成一个建议用的随机种子。
        /// </summary>
        private static int ResolveSuggestedSeed(ulong requesterClientId)
        {
            unchecked
            {
                return (int)DateTime.UtcNow.Ticks ^ (int)requesterClientId ^ Environment.TickCount;
            }
        }
    }
}
