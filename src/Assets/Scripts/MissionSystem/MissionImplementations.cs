using System.Collections.Generic;
using Matrix.Interaction;
using Matrix.PCG;
using Matrix.PCG.Instances;
using Unity.Netcode;
using UnityEngine;

namespace Matrix.Missions
{
    public sealed class BossMission : MissionBase
    {
        private ulong _bossUnitId;

        /// <summary>
        /// Boss 战激活时在房间内生成 Boss，并开始监听击杀。
        /// </summary>
        protected override void OnActivated()
        {
            if (Context == null || Context.Manager == null || !Context.Manager.IsServer)
            {
                return;
            }

            if (!Context.TryGetPlacedRoom(RoomNodeId, out PcgPlacedRoom room) || room.RoomInstance == null)
            {
                return;
            }

            IReadOnlyList<MissionSpawnEntry> spawnEntries = Config != null ? Config.SpawnEntries : null;
            MissionSpawnEntry spawnEntry = spawnEntries != null && spawnEntries.Count > 0 ? spawnEntries[0] : null;
            if (spawnEntry == null || string.IsNullOrWhiteSpace(spawnEntry.EnemyPrefabAddress))
            {
                Debug.LogWarning("[Mission] BossMission 缺少 Boss 刷新配置。");
                return;
            }

            Transform spawnPoint = room.RoomInstance.BossSpawnPoints != null && room.RoomInstance.BossSpawnPoints.Count > 0
                ? room.RoomInstance.BossSpawnPoints[0].transform
                : room.RoomInstance.transform;

            EnemySpawnService spawnService = Context.EnemySpawnService != null ? Context.EnemySpawnService : EnemySpawnService.Instance;
            if (spawnService == null)
            {
                return;
            }

            NetworkObject bossObject = spawnService.SpawnEnemy(
                spawnEntry.EnemyPrefabAddress,
                spawnPoint.position,
                spawnPoint.rotation,
                spawnEntry.AiConfigPath);

            if (bossObject == null)
            {
                return;
            }

            _bossUnitId = bossObject.NetworkObjectId;
            RegisterTrackedUnit(bossObject);
        }

        /// <summary>
        /// 当 Boss 死亡时完成任务。
        /// </summary>
        public override bool HandleUnitDied(ulong unitId)
        {
            if (State != MissionState.Active || unitId != _bossUnitId)
            {
                return false;
            }

            Complete();
            return true;
        }

        public override void Complete()
        {
            base.Complete();
            EventCenter.Instance.Trigger<BossDefeatedEvt>(EventName.BossDefeated,
                new BossDefeatedEvt
                {
                    RoomNodeId = RoomNodeId,
                    BossId = "Boss_" + RoomNodeId
                });
        }

        /// <summary>
        /// Boss 任务优先指向 Boss 刷新点。
        /// </summary>
        public override Vector3 ResolveObjectiveGuidePoint()
        {
            if (Context != null &&
                Context.TryGetPlacedRoom(RoomNodeId, out PcgPlacedRoom room) &&
                room.RoomInstance != null &&
                room.RoomInstance.BossSpawnPoints != null &&
                room.RoomInstance.BossSpawnPoints.Count > 0)
            {
                return room.RoomInstance.BossSpawnPoints[0].transform.position;
            }

            return base.ResolveObjectiveGuidePoint();
        }
    }

    public sealed class EliminateMission : MissionBase
    {
        /// <summary>
        /// 歼灭任务激活后只统计全图敌人死亡数量，敌人生成由房间/对局刷怪系统负责。
        /// </summary>
        protected override void OnActivated()
        {
            if (Context == null || Context.Manager == null || !Context.Manager.IsServer)
            {
                return;
            }

            int targetProgress = Config != null ? Config.KillTargetCount : 1;
            SetProgress(0, targetProgress);
        }

        /// <summary>
        /// 当任意敌人死亡时推进歼灭计数。
        /// </summary>
        public override bool HandleUnitDied(ulong unitId)
        {
            if (State != MissionState.Active || !IsEnemyUnit(unitId))
            {
                return false;
            }

            AddProgress(1);
            if (CurrentProgress >= TargetProgress)
            {
                Complete();
            }

            return true;
        }

        private static bool IsEnemyUnit(ulong unitId)
        {
            NetworkObjectManager objectManager = NetworkObjectManager.Instance;
            if (objectManager != null)
            {
                if (objectManager.TryGetNetworkProxy<EnemyNetworkProxy>(unitId, out var enemyProxy) && enemyProxy != null)
                {
                    return true;
                }

                if (objectManager.TryGetNetworkProxy<BossNetworkProxy>(unitId, out var bossProxy) && bossProxy != null)
                {
                    return true;
                }
            }

            if (NetworkManager.Singleton == null ||
                NetworkManager.Singleton.SpawnManager == null ||
                !NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(unitId, out NetworkObject networkObject) ||
                networkObject == null)
            {
                return false;
            }

            return networkObject.GetComponent<EnemyNetworkProxy>() != null ||
                   networkObject.GetComponent<BossNetworkProxy>() != null ||
                   networkObject.GetComponent<ServerEnemyAttributeModule>() != null;
        }
    }

    public sealed class DefenseMission : MissionBase
    {
        private GameObject _objectiveInstance;
        private DefenseObjective _defenseObjective;
        private float _remainingSeconds;
        private float _totalSeconds;

        /// <summary>
        /// 防御任务激活时生成防守目标并启动倒计时。
        /// </summary>
        protected override void OnActivated()
        {
            _remainingSeconds = Config != null ? Config.DefenseDurationSeconds : 30f;
            _totalSeconds = Mathf.Max(1f, _remainingSeconds);
            int totalSeconds = Mathf.CeilToInt(_totalSeconds);
            SetProgress(totalSeconds, totalSeconds);

            if (Context == null || Context.Manager == null || !Context.Manager.IsServer || Config == null)
            {
                return;
            }

            if (!Context.TryGetPlacedRoom(RoomNodeId, out PcgPlacedRoom room) || room.RoomInstance == null)
            {
                return;
            }

            Transform anchor = room.RoomInstance.transform;
            if (room.RoomInstance.DefenseObjectivePoints != null && room.RoomInstance.DefenseObjectivePoints.Count > 0)
            {
                PcgDefenseObjectivePointMarker marker = room.RoomInstance.DefenseObjectivePoints[0];
                if (marker != null)
                {
                    anchor = marker.transform;
                }
            }

            _objectiveInstance = Config.ObjectivePrefab != null
                ? Object.Instantiate(Config.ObjectivePrefab, anchor.position, anchor.rotation)
                : CreateRuntimeDefenseObjective(anchor.position, anchor.rotation);

            _defenseObjective = _objectiveInstance.GetComponent<DefenseObjective>();
            if (_defenseObjective == null)
            {
                _defenseObjective = _objectiveInstance.AddComponent<DefenseObjective>();
            }

            _defenseObjective.Configure(
                Config.DefenseObjectiveMaxHealth,
                Config.DefenseObjectiveShield,
                Config.DefenseObjectiveThreatPriority);

            NetworkObject networkObject = _objectiveInstance.GetComponent<NetworkObject>();
            if (networkObject == null)
            {
                networkObject = _objectiveInstance.AddComponent<NetworkObject>();
                Debug.LogWarning("[Mission] Defense 目标 Prefab 缺少 NetworkObject，已运行时补齐。需要人工确认该 Prefab 已注册到 NetworkPrefabs。");
            }

            AttachTrackedTarget(_objectiveInstance, 1);

            if (networkObject != null && !networkObject.IsSpawned)
            {
                networkObject.Spawn(true);
            }
        }

        /// <summary>
        /// 服务端每帧推进防御剩余时间，并在守住后完成任务。
        /// </summary>
        public override void TickServer(float deltaTime)
        {
            if (State != MissionState.Active)
            {
                return;
            }

            if (_objectiveInstance == null)
            {
                Fail();
                return;
            }

            if (_defenseObjective == null)
            {
                _defenseObjective = _objectiveInstance.GetComponent<DefenseObjective>();
            }

            if (_defenseObjective == null ||
                (_defenseObjective.CurrentHealth.Value <= 0f && _defenseObjective.CurrentShield.Value <= 0f))
            {
                Fail();
                return;
            }

            _remainingSeconds = Mathf.Max(0f, _remainingSeconds - deltaTime);
            int remainingSeconds = Mathf.CeilToInt(_remainingSeconds);
            int totalSeconds = Mathf.CeilToInt(Mathf.Max(1f, _totalSeconds));
            SetProgress(remainingSeconds, totalSeconds);

            if (_remainingSeconds <= 0f)
            {
                Complete();
            }
        }

        public override void Complete()
        {
            if (State == MissionState.Completed)
            {
                return;
            }

            SetProgress(0, TargetProgress);
            SetState(MissionState.Completed);
        }

        /// <summary>
        /// 防御目标被销毁后立即判定任务失败。
        /// </summary>
        public override void HandleTrackedTargetDestroyed(int targetKey, ulong networkObjectId)
        {
            if (targetKey != 1 || State == MissionState.Completed)
            {
                return;
            }

            Fail();
        }

        /// <summary>
        /// 防御任务优先指向当前防御目标。
        /// </summary>
        public override Vector3 ResolveObjectiveGuidePoint()
        {
            if (_objectiveInstance != null)
            {
                return _objectiveInstance.transform.position;
            }

            return base.ResolveObjectiveGuidePoint();
        }

        private static GameObject CreateRuntimeDefenseObjective(Vector3 position, Quaternion rotation)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "DefenseObjective_Runtime";
            go.transform.SetPositionAndRotation(position, rotation);
            go.transform.localScale = new Vector3(2f, 2f, 2f);
            go.AddComponent<NetworkObject>();
            go.AddComponent<DefenseObjective>();
            return go;
        }
    }

    public sealed class CaptureMission : MissionBase
    {
        private GameObject _captureTargetInstance;
        private GameObject _pickupInstance;
        private ulong _captureTargetId;

        /// <summary>
        /// 捕获任务激活时生成精英标靶，等待击杀后掉落拾取物。
        /// </summary>
        protected override void OnActivated()
        {
            SetProgress(0, 1);

            if (Context == null || Context.Manager == null || !Context.Manager.IsServer || Config == null)
            {
                return;
            }

            NetworkObject targetObject = SpawnCaptureTarget();
            if (targetObject == null)
            {
                Debug.LogWarning("[Mission] CaptureMission 缺少可生成标靶配置。请配置 SpawnEntries 或 ObjectivePrefab。");
                return;
            }

            _captureTargetId = targetObject.NetworkObjectId;
            _captureTargetInstance = targetObject.gameObject;
            RegisterTrackedUnit(targetObject);
        }

        public override bool HandleUnitDied(ulong unitId)
        {
            if (State != MissionState.Active || unitId != _captureTargetId || _pickupInstance != null)
            {
                return false;
            }

            Vector3 pickupPosition = ResolveTargetPosition(unitId);
            SpawnCapturePickup(pickupPosition);
            return true;
        }

        /// <summary>
        /// 由外部占点逻辑调用，用于累加捕获进度。
        /// </summary>
        public void AddCaptureProgress(float deltaProgress)
        {
            if (State != MissionState.Active)
            {
                return;
            }

            int current = Mathf.CeilToInt(CurrentProgress + deltaProgress);
            SetProgress(Mathf.Min(current, TargetProgress), TargetProgress);

            if (CurrentProgress >= TargetProgress)
            {
                Complete();
            }
        }

        public bool HandleCapturePickupCollected(ulong requesterClientId, string itemId)
        {
            if (State != MissionState.Active || Config == null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(Config.CaptureItemId) && itemId != Config.CaptureItemId)
            {
                return false;
            }

            SetProgress(1, 1);
            Complete();
            return true;
        }

        /// <summary>
        /// 捕获任务优先指向占点实体。
        /// </summary>
        public override Vector3 ResolveObjectiveGuidePoint()
        {
            if (_pickupInstance != null)
            {
                return _pickupInstance.transform.position;
            }

            if (_captureTargetInstance != null)
            {
                return _captureTargetInstance.transform.position;
            }

            return base.ResolveObjectiveGuidePoint();
        }

        private NetworkObject SpawnCaptureTarget()
        {
            if (!Context.TryGetPlacedRoom(RoomNodeId, out PcgPlacedRoom room) || room.RoomInstance == null)
            {
                return null;
            }

            Transform spawnPoint = ResolveCaptureSpawnPoint(room);
            EnemySpawnService spawnService = Context.EnemySpawnService != null ? Context.EnemySpawnService : EnemySpawnService.Instance;

            if (Config.SpawnEntries != null && Config.SpawnEntries.Count > 0 && spawnService != null)
            {
                for (int i = 0; i < Config.SpawnEntries.Count; i++)
                {
                    MissionSpawnEntry entry = Config.SpawnEntries[i];
                    if (entry == null || string.IsNullOrWhiteSpace(entry.EnemyPrefabAddress))
                    {
                        continue;
                    }

                    return spawnService.SpawnEnemy(
                        entry.EnemyPrefabAddress,
                        spawnPoint.position,
                        spawnPoint.rotation,
                        entry.AiConfigPath);
                }
            }

            if (Config.ObjectivePrefab == null)
            {
                return null;
            }

            GameObject targetObject = Object.Instantiate(Config.ObjectivePrefab, spawnPoint.position, spawnPoint.rotation);
            NetworkObject networkObject = targetObject.GetComponent<NetworkObject>();
            if (networkObject == null)
            {
                networkObject = targetObject.AddComponent<NetworkObject>();
                Debug.LogWarning("[Mission] Capture 标靶 Prefab 缺少 NetworkObject，已运行时补齐。需要人工确认该 Prefab 已注册到 NetworkPrefabs。");
            }

            if (!networkObject.IsSpawned)
            {
                networkObject.Spawn(true);
            }

            return networkObject;
        }

        private Transform ResolveCaptureSpawnPoint(PcgPlacedRoom room)
        {
            Context.TryCollectSpawnPoints(RoomNodeId, SpawnPointCategory.NormalEnemy, CachedPoints);
            if (CachedPoints.Count > 0)
            {
                return CachedPoints[0];
            }

            return room.RoomInstance.transform;
        }

        private Vector3 ResolveTargetPosition(ulong targetId)
        {
            if (NetworkManager.Singleton != null &&
                NetworkManager.Singleton.SpawnManager != null &&
                NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(targetId, out var networkObject) &&
                networkObject != null)
            {
                return networkObject.transform.position;
            }

            return _captureTargetInstance != null ? _captureTargetInstance.transform.position : ResolveObjectiveGuidePoint();
        }

        private void SpawnCapturePickup(Vector3 position)
        {
            if (string.IsNullOrWhiteSpace(Config.CaptureItemId))
            {
                Debug.LogWarning("[Mission] CaptureMission 未配置 CaptureItemId，无法生成拾取物。");
                return;
            }

            GameObject pickupObject = Config.CapturePickupPrefab != null
                ? Object.Instantiate(Config.CapturePickupPrefab, position + Vector3.up * 0.25f, Quaternion.identity)
                : CreateRuntimePickup(position + Vector3.up * 0.25f);

            PickupItem pickupItem = pickupObject.GetComponent<PickupItem>();
            if (pickupItem == null)
            {
                pickupItem = pickupObject.AddComponent<PickupItem>();
            }

            NetworkObject networkObject = pickupObject.GetComponent<NetworkObject>();
            if (networkObject == null)
            {
                networkObject = pickupObject.AddComponent<NetworkObject>();
                Debug.LogWarning("[Mission] Capture 拾取物 Prefab 缺少 NetworkObject，已运行时补齐。需要人工确认该 Prefab 已注册到 NetworkPrefabs。");
            }

            if (!networkObject.IsSpawned)
            {
                networkObject.Spawn(true);
            }

            pickupItem.ServerInit(
                Context.Manager,
                SlotIndex,
                Config.CaptureItemId,
                Config.CaptureItemAmount,
                Config.CapturePickupPrompt);

            _pickupInstance = pickupObject;
        }

        private static GameObject CreateRuntimePickup(Vector3 position)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "CapturePickup_Runtime";
            go.transform.position = position;
            go.transform.localScale = Vector3.one * 0.5f;
            go.AddComponent<NetworkObject>();
            go.AddComponent<PickupItem>();
            return go;
        }
    }

    public sealed class DestroyMission : MissionBase
    {
        private int _currentRoundIndex;
        private int _currentRoundDestroyed;
        private int _nextTargetKey = 1;

        /// <summary>
        /// 破坏任务激活时生成第一轮破坏目标。
        /// </summary>
        protected override void OnActivated()
        {
            _currentRoundIndex = 0;
            _currentRoundDestroyed = 0;
            SetProgress(0, Config != null ? Config.ResolveDestroyTargetCount() : 1);
            SpawnCurrentRoundTargets();
        }

        /// <summary>
        /// 当某个破坏目标被摧毁时，推进轮次与总进度。
        /// </summary>
        public override void HandleTrackedTargetDestroyed(int targetKey, ulong networkObjectId)
        {
            if (State != MissionState.Active || Config == null || Config.DestroyRounds.Count == 0)
            {
                return;
            }

            _currentRoundDestroyed++;
            AddProgress(1);

            MissionDestroyRoundConfig currentRound = Config.DestroyRounds[Mathf.Clamp(_currentRoundIndex, 0, Config.DestroyRounds.Count - 1)];
            int requiredCount = currentRound != null ? Mathf.Max(1, currentRound.TargetCount) : 1;

            if (_currentRoundDestroyed < requiredCount)
            {
                return;
            }

            _currentRoundIndex++;
            _currentRoundDestroyed = 0;

            if (_currentRoundIndex >= Config.DestroyRounds.Count)
            {
                Complete();
                return;
            }

            SpawnCurrentRoundTargets();
        }

        /// <summary>
        /// 在当前房间生成本轮的破坏目标。
        /// </summary>
        private void SpawnCurrentRoundTargets()
        {
            if (Context == null || Context.Manager == null || !Context.Manager.IsServer || Config == null)
            {
                return;
            }

            if (_currentRoundIndex < 0 || _currentRoundIndex >= Config.DestroyRounds.Count)
            {
                return;
            }

            MissionDestroyRoundConfig round = Config.DestroyRounds[_currentRoundIndex];
            if (round == null)
            {
                return;
            }

            if (!Context.TryGetPlacedRoom(RoomNodeId, out PcgPlacedRoom room) || room.RoomInstance == null)
            {
                return;
            }

            Context.TryCollectSpawnPoints(RoomNodeId, SpawnPointCategory.NormalEnemy, CachedPoints);
            if (CachedPoints.Count == 0)
            {
                CachedPoints.Add(room.RoomInstance.transform);
            }

            for (int i = 0; i < Mathf.Max(1, round.TargetCount); i++)
            {
                Transform anchor = CachedPoints[i % CachedPoints.Count];
                bool usesRuntimeTarget = round.TargetPrefab == null;
                GameObject targetObject = usesRuntimeTarget
                    ? CreateRuntimeDestroyTarget(anchor.position, anchor.rotation)
                    : Object.Instantiate(round.TargetPrefab, anchor.position, anchor.rotation);

                NetworkObject networkObject = targetObject.GetComponent<NetworkObject>();
                if (networkObject == null)
                {
                    networkObject = targetObject.AddComponent<NetworkObject>();
                    Debug.LogWarning("[Mission] Destroy 目标缺少 NetworkObject，已运行时补齐。需要人工确认该 Prefab 已注册到 NetworkPrefabs。");
                }

                bool hasProxy = targetObject.GetComponent<NetworkProxyBase>() != null;
                MissionDamageableTarget damageableTarget = targetObject.GetComponent<MissionDamageableTarget>();
                if (!hasProxy && damageableTarget == null)
                {
                    damageableTarget = targetObject.AddComponent<MissionDamageableTarget>();
                }

                if (usesRuntimeTarget && damageableTarget != null)
                {
                    damageableTarget.Configure(Mathf.Max(100f, round.GoldReward * 3f), 0f, 80);
                }

                DamageContributionTracker tracker = targetObject.GetComponent<DamageContributionTracker>();
                if (tracker == null)
                {
                    tracker = targetObject.AddComponent<DamageContributionTracker>();
                }

                tracker.ConfigureGoldReward(round.GoldReward);

                AttachTrackedTarget(targetObject, _nextTargetKey++);

                if (networkObject != null && !networkObject.IsSpawned)
                {
                    networkObject.Spawn(true);
                }
            }
        }

        private static GameObject CreateRuntimeDestroyTarget(Vector3 position, Quaternion rotation)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "DestroyTarget_Runtime";
            go.transform.SetPositionAndRotation(position, rotation);
            go.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);
            go.AddComponent<NetworkObject>();
            go.AddComponent<MissionDamageableTarget>();
            Debug.LogWarning("[Mission] Destroy 轮次未配置 TargetPrefab，已生成运行时目标。需要人工确认正式网络 Prefab 绑定。");
            return go;
        }
    }
}
