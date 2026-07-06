using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Matrix.Missions
{
    public abstract class MissionBase
    {
        private readonly HashSet<ulong> _trackedUnitIds = new HashSet<ulong>();

        public MissionContext Context { get; private set; }
        public MissionConfig Config { get; private set; }
        protected readonly List<Transform> CachedPoints = new List<Transform>();
        public int SlotIndex { get; private set; }
        public int RoomNodeId { get; private set; }
        public MissionState State { get; private set; }
        public int CurrentProgress { get; private set; }
        public int TargetProgress { get; private set; }
        public MissionTriggerZone TriggerZone { get; private set; }

        public event Action<MissionBase> StateChanged;
        public event Action<MissionBase> ProgressChanged;

        /// <summary>
        /// 初始化任务实例，并注入运行时上下文。
        /// </summary>
        public virtual void Initialize(MissionContext context, MissionConfig config, int slotIndex, int roomNodeId)
        {
            Context = context;
            Config = config;
            SlotIndex = slotIndex;
            RoomNodeId = roomNodeId;
            State = MissionState.Inactive;
            CurrentProgress = 0;
            TargetProgress = config != null ? config.ResolveTargetProgress() : 0;
        }

        /// <summary>
        /// 绑定房间触发区，用于进入房间时启动任务。
        /// </summary>
        public virtual void BindTriggerZone(MissionTriggerZone triggerZone)
        {
            TriggerZone = triggerZone;
        }

        /// <summary>
        /// 将任务切换到可触发状态。
        /// </summary>
        public virtual void Prepare()
        {
            if (State != MissionState.Inactive)
            {
                return;
            }

            SetState(MissionState.Ready);
        }

        /// <summary>
        /// 激活任务，并触发子类的具体生成逻辑。
        /// </summary>
        public virtual void Activate()
        {
            if (State == MissionState.Completed || State == MissionState.Failed)
            {
                return;
            }

            if (State == MissionState.Inactive)
            {
                Prepare();
            }

            if (State == MissionState.Active)
            {
                return;
            }

            SetState(MissionState.Active);
            Debug.Log($"[Mission] 任务已激活 | Slot={SlotIndex} Type={Config?.MissionType} Room={RoomNodeId} Name='{Config?.DisplayName}'");
            OnActivated();
        }

        /// <summary>
        /// 完成任务并更新状态。
        /// </summary>
        public virtual void Complete()
        {
            if (State == MissionState.Completed)
            {
                return;
            }

            SetProgress(TargetProgress, TargetProgress);
            SetState(MissionState.Completed);
        }

        /// <summary>
        /// 标记任务失败。
        /// </summary>
        public virtual void Fail()
        {
            if (State == MissionState.Failed)
            {
                return;
            }

            SetState(MissionState.Failed);
        }

        /// <summary>
        /// 同步来自网络层的任务状态快照。
        /// </summary>
        public virtual void ApplyReplicatedState(MissionState state, int currentProgress, int targetProgress)
        {
            State = state;
            CurrentProgress = currentProgress;
            TargetProgress = targetProgress;
        }

        /// <summary>
        /// 处理玩家进入触发区后的公共逻辑。
        /// </summary>
        public virtual void HandlePlayerEnteredTrigger(PlayerActor playerActor, bool isLocalPlayer)
        {
            if (State == MissionState.Ready || State == MissionState.Inactive)
            {
                Debug.Log($"[Mission] 玩家进入触发区 → 激活任务 Slot={SlotIndex} Type={Config?.MissionType} Room={RoomNodeId}");
                Activate();
            }
        }

        /// <summary>
        /// 由服务端逐帧驱动需要倒计时的任务。
        /// </summary>
        public virtual void TickServer(float deltaTime)
        {
        }

        /// <summary>
        /// 处理被追踪单位死亡事件。
        /// </summary>
        public virtual bool HandleUnitDied(ulong unitId)
        {
            return false;
        }

        /// <summary>
        /// 处理被追踪的任务目标被摧毁事件。
        /// </summary>
        public virtual void HandleTrackedTargetDestroyed(int targetKey, ulong networkObjectId)
        {
        }

        /// <summary>
        /// 解析任务进入房间后应指向的核心目标点。
        /// </summary>
        public virtual Vector3 ResolveObjectiveGuidePoint()
        {
            if (Context != null && Context.TryResolveRoomCenter(RoomNodeId, out Vector3 roomCenter))
            {
                return roomCenter;
            }

            return Vector3.zero;
        }

        /// <summary>
        /// 生成指引器的显示名称。
        /// </summary>
        public virtual string GetPointerLabel()
        {
            return Config != null ? Config.ResolvePointerLabel() : $"Mission_{SlotIndex}";
        }

        /// <summary>
        /// 生成 HUD 任务框的状态提示文本。
        /// </summary>
        public virtual string ResolveHudStatusText()
        {
            MissionType missionType = Config != null ? Config.MissionType : MissionType.Eliminate;
            return MissionHudStatusFormatter.ResolveStatusText(missionType, State, CurrentProgress, TargetProgress);
        }

        /// <summary>
        /// 将某个 NetworkObject 记录为任务击杀或销毁追踪目标。
        /// </summary>
        protected void RegisterTrackedUnit(NetworkObject networkObject)
        {
            if (networkObject == null)
            {
                return;
            }

            _trackedUnitIds.Add(networkObject.NetworkObjectId);
        }

        /// <summary>
        /// 判断某个死亡事件是否属于当前任务追踪的单位。
        /// </summary>
        protected bool ContainsTrackedUnit(ulong unitId)
        {
            return _trackedUnitIds.Contains(unitId);
        }

        /// <summary>
        /// 推进任务进度，并在进度变化时派发事件。
        /// </summary>
        protected void SetProgress(int currentProgress, int targetProgress)
        {
            CurrentProgress = Mathf.Max(0, currentProgress);
            TargetProgress = Mathf.Max(0, targetProgress);
            ProgressChanged?.Invoke(this);
        }

        /// <summary>
        /// 追加任务进度，并自动限制在目标范围内。
        /// </summary>
        protected void AddProgress(int amount)
        {
            int target = Mathf.Max(0, TargetProgress);
            int next = Mathf.Clamp(CurrentProgress + amount, 0, target > 0 ? target : int.MaxValue);
            SetProgress(next, target);
        }

        /// <summary>
        /// 给运行时实例挂接目标销毁回调组件。
        /// </summary>
        protected MissionTrackedTarget AttachTrackedTarget(GameObject targetObject, int targetKey)
        {
            if (targetObject == null)
            {
                return null;
            }

            MissionTrackedTarget trackedTarget = targetObject.GetComponent<MissionTrackedTarget>();
            if (trackedTarget == null)
            {
                trackedTarget = targetObject.AddComponent<MissionTrackedTarget>();
            }

            trackedTarget.Initialize(Context.Manager, SlotIndex, targetKey);
            return trackedTarget;
        }

        /// <summary>
        /// 任务正式激活后由子类覆写实现具体玩法逻辑。
        /// </summary>
        protected virtual void OnActivated()
        {
        }

        /// <summary>
        /// 更新任务生命周期状态并通知外部监听。
        /// </summary>
        protected void SetState(MissionState state)
        {
            if (State == state)
            {
                return;
            }

            State = state;
            StateChanged?.Invoke(this);
        }
    }
}
