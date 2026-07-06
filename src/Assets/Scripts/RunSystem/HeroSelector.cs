using Unity.Netcode;
using UnityEngine;

namespace Matrix.RunSystem
{
    /// <summary>
    /// 英雄选择器 P0 占位。正式版负责英雄/Loadout 选择 UI + 同步。
    /// </summary>
    public sealed class HeroSelector : NetworkBehaviour
    {
        [Header("P0 Stub Settings")]
        [SerializeField] private string defaultHeroId = "DefaultHero";

        private RunManager _runManager;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            _runManager = GetComponent<RunManager>();
        }

        /// <summary>
        /// 自动选择默认英雄并推进到 RunInit。
        /// </summary>
        public void AutoSelectDefault()
        {
            if (_runManager == null) return;

            _runManager.SetSelectedHero(defaultHeroId, null);
            Debug.Log($"[HeroSelector] P0: Auto-selected hero '{defaultHeroId}'.");

            if (IsServer)
                _runManager.TransitionTo(RunState.RunInit);
        }
    }
}
