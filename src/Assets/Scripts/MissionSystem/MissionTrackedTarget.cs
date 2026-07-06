using Unity.Netcode;
using UnityEngine;

namespace Matrix.Missions
{
    public sealed class MissionTrackedTarget : NetworkBehaviour
    {
        private MissionManager _missionManager;
        private int _slotIndex;
        private int _targetKey;
        private bool _notified;

        /// <summary>
        /// 初始化一个运行时任务目标追踪器。
        /// </summary>
        public void Initialize(MissionManager missionManager, int slotIndex, int targetKey)
        {
            _missionManager = missionManager;
            _slotIndex = slotIndex;
            _targetKey = targetKey;
            _notified = false;
        }

        public override void OnNetworkDespawn()
        {
            NotifyDestroyed();
            base.OnNetworkDespawn();
        }

        private void OnDestroy()
        {
            NotifyDestroyed();
        }

        /// <summary>
        /// 当目标对象被销毁或网络反生成时，回传给任务管理器推进任务逻辑。
        /// </summary>
        public void NotifyDestroyed()
        {
            if (_notified || _missionManager == null)
            {
                return;
            }

            _notified = true;

            ulong networkObjectId = 0;
            NetworkObject networkObject = GetComponent<NetworkObject>();
            if (networkObject != null)
            {
                networkObjectId = networkObject.NetworkObjectId;
            }

            var contributionTracker = GetComponent<DamageContributionTracker>();
            contributionTracker?.DistributeConfiguredRewardAndReset();

            _missionManager.ReportTrackedTargetDestroyed(_slotIndex, _targetKey, networkObjectId);
        }
    }
}
