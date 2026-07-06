using Matrix.Missions;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Matrix.Interaction
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed class PickupItem : NetworkBehaviour, IInteractable
    {
        private const string BillboardPrefabPath = "Prefab/UI/Interactor/PickupBillboard";

        public readonly NetworkVariable<FixedString128Bytes> ItemId = new NetworkVariable<FixedString128Bytes>();
        public readonly NetworkVariable<int> Amount = new NetworkVariable<int>(1);

        [SerializeField] private string interactionPrompt = "按 F 拾取";
        [SerializeField] private float interactionRadius = 2f;
        [SerializeField] private Transform interactionAnchor;

        private MissionManager _missionManager;
        private int _missionSlotIndex = -1;
        private bool _picked;

        private GameObject _billboardInstance;
        private WorldBillboardUI _billboard;

        public Transform InteractionAnchor => interactionAnchor != null ? interactionAnchor : transform;
        public string InteractionPrompt => interactionPrompt;
        public float InteractionRadius => interactionRadius;

        private void Awake()
        {
            EnsureTriggerCollider();
        }

        public void ServerInit(
            MissionManager missionManager,
            int missionSlotIndex,
            string itemId,
            int amount,
            string prompt = null)
        {
            if (!IsServer)
            {
                return;
            }

            _missionManager = missionManager;
            _missionSlotIndex = missionSlotIndex;
            ItemId.Value = new FixedString128Bytes(itemId ?? string.Empty);
            Amount.Value = Mathf.Max(1, amount);
            _picked = false;

            if (!string.IsNullOrWhiteSpace(prompt))
            {
                interactionPrompt = prompt;
            }
        }

        public bool CanInteract(ulong requesterId)
        {
            if (_picked || ItemId.Value.IsEmpty)
            {
                return false;
            }

            if (!IsServer)
            {
                return true;
            }

            return TryGetRequesterInventory(requesterId, out _);
        }

        public void OnInteractClient()
        {
            if (!IsClient)
            {
                return;
            }

            RequestPickupServerRpc();
        }

        public void OnInteractServer(ulong requesterId)
        {
            if (!IsServer || !CanInteract(requesterId))
            {
                return;
            }

            if (!TryGetRequesterInventory(requesterId, out var inventory))
            {
                return;
            }

            var itemSoId = ItemId.Value.ToString();
            var itemSO = SOManager.Instance != null
                ? SOManager.Instance.GetSOById<BaseInventoryItemSO>(itemSoId)
                : null;

            if (itemSO == null)
            {
                Debug.LogWarning($"[PickupItem] 未找到道具 SO: {itemSoId}");
                return;
            }

            if (!inventory.TryAddItemServer(new InventoryItem(itemSO), Amount.Value))
            {
                return;
            }

            _picked = true;
            _missionManager?.ReportCapturePickupCollected(_missionSlotIndex, requesterId, itemSoId);
            DespawnSelf();
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestPickupServerRpc(ServerRpcParams rpcParams = default)
        {
            OnInteractServer(rpcParams.Receive.SenderClientId);
        }

        private bool TryGetRequesterInventory(ulong requesterId, out NetworkInventory inventory)
        {
            inventory = null;

            var networkManager = NetworkManager.Singleton;
            if (networkManager == null ||
                networkManager.ConnectedClients == null ||
                !networkManager.ConnectedClients.TryGetValue(requesterId, out var client))
            {
                return false;
            }

            inventory = client.PlayerObject != null
                ? client.PlayerObject.GetComponent<NetworkInventory>()
                : null;

            return inventory != null;
        }

        private void EnsureTriggerCollider()
        {
            var sphere = GetComponent<SphereCollider>();
            if (sphere == null)
            {
                sphere = gameObject.AddComponent<SphereCollider>();
            }

            sphere.isTrigger = true;
            sphere.radius = Mathf.Max(0.1f, interactionRadius);
        }

        private void DespawnSelf()
        {
            CleanupBillboard();

            var networkObject = GetComponent<NetworkObject>();
            if (networkObject != null && networkObject.IsSpawned)
            {
                networkObject.Despawn(true);
                return;
            }

            Destroy(gameObject);
        }

        private void CleanupBillboard()
        {
            _billboard?.Hide();
            if (_billboardInstance != null)
            {
                Destroy(_billboardInstance);
                _billboardInstance = null;
            }
            _billboard = null;
        }

        #region IInteractable — Hover

        public void OnHoverEnter()
        {
            var prefab = Resources.Load<GameObject>(BillboardPrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[PickupItem] 未找到 Billboard Prefab: Resources/{BillboardPrefabPath}");
                return;
            }

            _billboardInstance = Instantiate(prefab);
            _billboard = _billboardInstance.GetComponent<WorldBillboardUI>();
            _billboard?.Show(InteractionAnchor, InteractionPrompt);
        }

        public void OnHoverExit()
        {
            CleanupBillboard();
        }

        #endregion
    }
}
