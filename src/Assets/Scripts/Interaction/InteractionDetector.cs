using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Matrix.Interaction
{
    /// <summary>
    /// 本地玩家交互检测器。
    /// 挂在本地玩家 PlayerActor 上，每帧球形扫描最近可交互对象，
    /// 管理 hover 状态切换，并响应 Interact 按键。
    /// </summary>
    public sealed class InteractionDetector : MonoBehaviour
    {
        [SerializeField] private float detectionRadius = 2.5f;
        [SerializeField] private LayerMask interactionLayers = ~0;

        private readonly Collider[] _hits = new Collider[16];
        private PlayerNetworkProxy _playerProxy;
        private IInteractable _current;
        private InputAction _interactAction;

        private void Awake()
        {
            _playerProxy = GetComponent<PlayerNetworkProxy>();
        }

        private void OnEnable()
        {
            var inputSystem = PlayerInputSystem.Instance;
            if (inputSystem != null)
            {
                _interactAction = inputSystem.FindAction("Interact");
            }
        }

        private void OnDisable()
        {
            // 离场当前 hover 对象
            if (_current != null)
            {
                _current.OnHoverExit();
                _current = null;
            }

            _interactAction = null;
        }

        private void Update()
        {
            if (!CanRunLocalDetection())
            {
                ClearHover();
                return;
            }

            var nearest = FindNearestInteractable();

            // 切换 hover 对象
            if (nearest != _current)
            {
                if (_current != null)
                    _current.OnHoverExit();

                _current = nearest;

                if (_current != null)
                    _current.OnHoverEnter();
            }

            if (_current == null)
                return;

            // 交互按键
            if (_interactAction != null && _interactAction.WasPressedThisFrame())
            {
                _current.OnInteractClient();
            }
        }

        private void ClearHover()
        {
            if (_current != null)
            {
                _current.OnHoverExit();
                _current = null;
            }
        }

        private bool CanRunLocalDetection()
        {
            return _playerProxy != null &&
                   _playerProxy.IsOwner &&
                   NetworkManager.Singleton != null &&
                   NetworkManager.Singleton.IsClient;
        }

        private IInteractable FindNearestInteractable()
        {
            var count = Physics.OverlapSphereNonAlloc(
                transform.position,
                detectionRadius,
                _hits,
                interactionLayers,
                QueryTriggerInteraction.Collide);

            IInteractable best = null;
            var bestDistance = float.PositiveInfinity;
            var requesterId = NetworkManager.Singleton != null
                ? NetworkManager.Singleton.LocalClientId
                : 0;

            for (var i = 0; i < count; i++)
            {
                var hit = _hits[i];
                if (hit == null) continue;

                var interactable = FindInteractableInParents(hit);
                if (interactable == null || !interactable.CanInteract(requesterId)) continue;

                var anchor = interactable.InteractionAnchor;
                if (anchor == null) continue;

                var radius = Mathf.Max(0.1f, interactable.InteractionRadius);
                var distance = Vector3.Distance(transform.position, anchor.position);
                if (distance > radius || distance >= bestDistance) continue;

                best = interactable;
                bestDistance = distance;
            }

            return best;
        }

        private static IInteractable FindInteractableInParents(Collider hit)
        {
            var behaviours = hit.GetComponentsInParent<MonoBehaviour>();
            for (var i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IInteractable interactable)
                    return interactable;
            }

            return null;
        }
    }
}
