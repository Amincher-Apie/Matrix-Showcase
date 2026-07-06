using Matrix.PCG;
using UnityEngine;

namespace Matrix.Missions
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class MissionTriggerZone : MonoBehaviour
    {
        private MissionManager _missionManager;
        private BoxCollider _boxCollider;

        public int SlotIndex { get; private set; }
        public int RoomNodeId { get; private set; }
        public bool AddedByMissionSystem { get; private set; }

        public bool TryGetWorldBounds(out Bounds bounds)
        {
            if (_boxCollider == null)
            {
                _boxCollider = GetComponent<BoxCollider>();
            }

            if (_boxCollider == null)
            {
                bounds = default;
                return false;
            }

            bounds = _boxCollider.bounds;
            return true;
        }

        public static bool TryAttachToRoomBounds(
            PcgRoomRoot roomRoot,
            MissionManager missionManager,
            int slotIndex,
            int roomNodeId,
            out MissionTriggerZone zone)
        {
            zone = null;
            if (roomRoot == null || roomRoot.RoomBounds == null)
            {
                return false;
            }

            if (!roomRoot.RoomBounds.TryGetBoundsCollider(out BoxCollider boundsCollider) || boundsCollider == null)
            {
                return false;
            }

            MissionTriggerZone[] existingZones = boundsCollider.GetComponents<MissionTriggerZone>();
            for (int i = 0; i < existingZones.Length; i++)
            {
                MissionTriggerZone existingZone = existingZones[i];
                if (existingZone != null && existingZone.SlotIndex == slotIndex)
                {
                    zone = existingZone;
                    break;
                }
            }

            if (zone == null)
            {
                zone = boundsCollider.gameObject.AddComponent<MissionTriggerZone>();
                zone.AddedByMissionSystem = true;
            }

            zone.Setup(missionManager, slotIndex, roomNodeId, boundsCollider);
            return true;
        }

        /// <summary>
        /// 绑定任务管理器引用，并将房间 BoundCollider 作为任务触发区使用。
        /// </summary>
        public void Setup(MissionManager missionManager, int slotIndex, int roomNodeId, BoxCollider boundsCollider)
        {
            _missionManager = missionManager;
            SlotIndex = slotIndex;
            RoomNodeId = roomNodeId;

            _boxCollider = boundsCollider != null ? boundsCollider : GetComponent<BoxCollider>();
            if (_boxCollider == null)
            {
                Debug.LogError($"[MissionTrigger] 任务触发区缺少 BoxCollider。Room={RoomNodeId} Slot={SlotIndex}", this);
                return;
            }

            _boxCollider.enabled = true;
            _boxCollider.isTrigger = true;

            if (!_boxCollider.gameObject.activeInHierarchy)
            {
                Debug.LogWarning($"[MissionTrigger] 房间 BoundCollider 所在对象未激活，任务触发不会生效。Room={RoomNodeId} Slot={SlotIndex}", this);
            }
        }

        public void ClearMissionManager(MissionManager missionManager)
        {
            if (_missionManager == missionManager)
            {
                _missionManager = null;
            }
        }

        /// <summary>
        /// 玩家进入房间触发区后，将事件转交给任务管理器。
        /// </summary>
        private void OnTriggerEnter(Collider other)
        {
            if (_missionManager == null || other == null)
            {
                return;
            }

            PlayerActor playerActor = other.GetComponentInParent<PlayerActor>();
            if (playerActor == null)
            {
                return;
            }

            PlayerNetworkProxy proxy = playerActor.GetComponent<PlayerNetworkProxy>();
            bool isLocalPlayer = proxy != null && proxy.IsOwner;

            Debug.Log($"[MissionTrigger] 玩家进入任务区域 Room={RoomNodeId} Slot={SlotIndex} IsLocal={isLocalPlayer}");
            _missionManager.HandleMissionTriggerEntered(this, playerActor, isLocalPlayer);
        }
    }
}
