using Matrix.PCG;
using UnityEngine;

namespace Matrix.RunSystem
{
    /// <summary>
    /// 房间进入触发器。仿 MissionTriggerZone 模式，挂载在每个房间入口门位置。
    /// OnTriggerEnter → 通知 RunManager 玩家已进入房间。
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class RunRoomTrigger : MonoBehaviour
    {
        [SerializeField] private int roomNodeId;
        [SerializeField] private float reTriggerCooldown = 1f;

        private RunManager _runManager;
        private float _lastTriggerTime;
        private int _lastTriggeredByPlayerId = -1;

        public int RoomNodeId => roomNodeId;

        /// <summary>
        /// 工厂方法：运行时创建触发器包围盒。
        /// </summary>
        public static RunRoomTrigger CreateRuntimeZone(
            Transform parent,
            RunManager runManager,
            int nodeId,
            Bounds worldBounds,
            float height = 4f)
        {
            GameObject go = new GameObject($"RunRoomTrigger_{nodeId}");
            go.transform.SetParent(parent, false);
            go.transform.position = worldBounds.center;
            go.layer = LayerMask.NameToLayer("Ignore Raycast");

            BoxCollider box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(worldBounds.size.x, height, worldBounds.size.z);
            box.isTrigger = true;

            RunRoomTrigger trigger = go.AddComponent<RunRoomTrigger>();
            trigger.roomNodeId = nodeId;
            trigger._runManager = runManager;

            return trigger;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_runManager == null) return;

            PlayerActor player = other.GetComponentInParent<PlayerActor>();
            if (player == null) return;

            int playerId = player.GetInstanceID();
            float timeSinceLastTrigger = Time.time - _lastTriggerTime;

            if (playerId == _lastTriggeredByPlayerId && timeSinceLastTrigger < reTriggerCooldown)
                return;

            _lastTriggerTime = Time.time;
            _lastTriggeredByPlayerId = playerId;

            Debug.Log($"[RunRoomTrigger] Player entered Room {roomNodeId}");

            // 客户端通知服务端
            if (_runManager.IsClient && !_runManager.IsServer)
            {
                _runManager.ReportRoomEnteredServerRpc(roomNodeId);
            }
            else if (_runManager.IsServer)
            {
                if (_runManager.CurrentRoomNodeId != roomNodeId)
                    _runManager.ReportRoomEnteredServerRpc(roomNodeId);
            }
        }
    }
}
