using Unity.Netcode;
using UnityEngine;

namespace Matrix.RunSystem
{
    /// <summary>
    /// 大厅管理器 P0 占位。正式版负责房间创建/加入/准备/难度投票。
    /// </summary>
    public sealed class LobbyManager : NetworkBehaviour
    {
        [Header("P0 Stub Settings")]
        [SerializeField] private bool autoReadyForTesting = true;
        [SerializeField] private float autoAdvanceDelay = 1f;

        private RunManager _runManager;
        private bool _advanced;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            _runManager = GetComponent<RunManager>();

            if (IsServer && autoReadyForTesting && !_advanced)
            {
                _advanced = true;
                Invoke(nameof(AdvanceFromLobby), autoAdvanceDelay);
            }
        }

        private void AdvanceFromLobby()
        {
            if (_runManager != null)
            {
                Debug.Log("[LobbyManager] P0: Auto-advancing from Lobby.");
                _runManager.TransitionTo(RunState.RunInit);
            }
        }
    }
}
