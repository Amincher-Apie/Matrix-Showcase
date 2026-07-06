using System.Collections.Generic;
using Matrix.PCG;
using UnityEngine;
using UnityEngine.UI;

namespace Matrix.Missions
{
    public sealed class MissionPointerManager : MonoBehaviour
    {
        private const string DefaultMissionIconPrefabPath = "Prefab/UI/UIItem/MissionBox/BasicMissionIcon-Instance";
        private const string DefaultMissionUIConfigPath = "Configs/Missions/UI/MissionUIConfig";
        private const float PointerLayoutPadding = 8f;

        [SerializeField]
        private Canvas pointerCanvas;

        [SerializeField]
        private GameObject missionIconPrefab;

        [SerializeField]
        private MissionUIConfigSO missionUIConfig;

        [SerializeField]
        private Vector2 screenOffset = new Vector2(0f, 40f);

        [SerializeField]
        private float screenEdgePadding = 48f;

        [SerializeField, Min(0f)]
        private float pointerOverlapThreshold = 112f;

        [SerializeField, Min(0f)]
        private float pointerOverlapSpacing = 112f;

        private readonly Dictionary<int, MissionPointer> _pointers = new Dictionary<int, MissionPointer>();
        private readonly HashSet<int> _localEnteredMissionSlots = new HashSet<int>();
        private readonly HashSet<int> _activeMissionSlots = new HashSet<int>();
        private readonly List<int> _slotsToRemove = new List<int>();
        private readonly List<PointerPlacement> _pointerPlacements = new List<PointerPlacement>();
        private readonly List<int> _placementGroupIndices = new List<int>();
        private readonly List<float> _placementGroupOffsets = new List<float>();
        private readonly HashSet<int> _resolvedPlacementIndices = new HashSet<int>();

        private MissionManager _missionManager;
        private global::GameHUDWindowDataComponent _hudDataComponent;
        private RectTransform _canvasRect;
        private RectTransform _pointerLayer;
        private Camera _worldCamera;
        private bool _hasExplicitPointerCanvas;

        private void Awake()
        {
            _hasExplicitPointerCanvas = pointerCanvas != null;
        }

        /// <summary>
        /// 初始化任务指引器管理器，并为当前所有任务创建 UI 指引器。
        /// </summary>
        public void Initialize(MissionManager missionManager)
        {
            _missionManager = missionManager;
            EnsureCanvas();

            foreach (KeyValuePair<int, MissionPointer> pair in _pointers)
            {
                if (pair.Value != null)
                {
                    Destroy(pair.Value.gameObject);
                }
            }

            _pointers.Clear();
            _localEnteredMissionSlots.Clear();

            SyncPointersWithRuntimeMissions();
        }

        private void SyncPointersWithRuntimeMissions()
        {
            IReadOnlyList<MissionBase> missions = _missionManager != null ? _missionManager.RuntimeMissions : null;
            if (missions == null)
            {
                return;
            }

            _activeMissionSlots.Clear();
            for (int i = 0; i < missions.Count; i++)
            {
                MissionBase mission = missions[i];
                if (mission == null)
                {
                    continue;
                }

                _activeMissionSlots.Add(mission.SlotIndex);
                if (_pointers.TryGetValue(mission.SlotIndex, out MissionPointer pointer) && pointer != null)
                {
                    ConfigurePointerVisual(pointer, mission);
                    continue;
                }

                pointer = CreateRuntimePointer(_canvasRect, mission);
                pointer.SetVisible(true);
                _pointers[mission.SlotIndex] = pointer;
            }

            _slotsToRemove.Clear();
            foreach (int slotIndex in _pointers.Keys)
            {
                if (!_activeMissionSlots.Contains(slotIndex))
                {
                    _slotsToRemove.Add(slotIndex);
                }
            }

            for (int i = 0; i < _slotsToRemove.Count; i++)
            {
                int slotIndex = _slotsToRemove[i];
                if (_pointers.TryGetValue(slotIndex, out MissionPointer pointer) && pointer != null)
                {
                    Destroy(pointer.gameObject);
                }

                _pointers.Remove(slotIndex);
                _localEnteredMissionSlots.Remove(slotIndex);
            }
        }

        /// <summary>
        /// 标记本地玩家是否已经进入指定任务房间。
        /// </summary>
        public void MarkLocalMissionEntered(int slotIndex, bool entered)
        {
            if (entered)
            {
                _localEnteredMissionSlots.Add(slotIndex);
            }
            else
            {
                _localEnteredMissionSlots.Remove(slotIndex);
            }
        }

        /// <summary>
        /// 每帧刷新所有任务指引器的目标位置与显隐。
        /// </summary>
        private void LateUpdate()
        {
            if (_missionManager == null)
            {
                return;
            }

            EnsureCanvas();
            SyncPointersWithRuntimeMissions();

            if (_canvasRect == null)
            {
                return;
            }

            Transform localPlayer = _missionManager.GetLocalPlayerTransform();
            if (localPlayer == null)
            {
                return;
            }

            IReadOnlyList<MissionBase> missions = _missionManager.RuntimeMissions;
            _pointerPlacements.Clear();
            for (int i = 0; i < missions.Count; i++)
            {
                MissionBase mission = missions[i];
                if (mission == null || !_pointers.TryGetValue(mission.SlotIndex, out MissionPointer pointer) || pointer == null)
                {
                    continue;
                }

                bool visible = mission.State != MissionState.Completed && mission.State != MissionState.Failed;
                pointer.SetVisible(visible);
                if (!visible)
                {
                    continue;
                }

                ConfigurePointerVisual(pointer, mission);

                if (!TryResolvePointerGuideTarget(localPlayer.position, mission, out MissionGuideTarget guideTarget))
                {
                    pointer.SetVisible(false);
                    continue;
                }

                Vector3 guideWorldPoint = guideTarget.WorldPoint;
                pointer.SetDistance(Vector3.Distance(localPlayer.position, guideTarget.FinalWorldPoint));

                if (TryResolvePointerAnchoredPosition(guideWorldPoint, out Vector2 anchoredPosition, out float angle))
                {
                    pointer.SetArrowRotation(angle);
                    _pointerPlacements.Add(new PointerPlacement(pointer, anchoredPosition, pointer.GetLayoutSize()));
                }
                else
                {
                    pointer.SetVisible(false);
                }
            }

            ApplyPointerPlacements();
        }

        private MissionPointer CreateRuntimePointer(RectTransform parent, MissionBase mission)
        {
            int slotIndex = mission != null ? mission.SlotIndex : -1;
            MissionPointer pointer = MissionPointer.CreateRuntimePointer(parent, slotIndex, ResolveMissionIconPrefab());
            ConfigurePointerVisual(pointer, mission);
            return pointer;
        }

        private void ConfigurePointerVisual(MissionPointer pointer, MissionBase mission)
        {
            if (pointer == null)
            {
                return;
            }

            ResolveMissionIconVisual(mission, out Sprite icon, out Color iconBaseColor);
            pointer.SetVisual(icon, iconBaseColor);
        }

        private void ResolveMissionIconVisual(MissionBase mission, out Sprite icon, out Color iconBaseColor)
        {
            MissionUIConfigSO config = ResolveMissionUIConfig();
            MissionType missionType = mission != null && mission.Config != null ? mission.Config.MissionType : MissionType.Eliminate;
            MissionCategory missionCategory = mission != null && mission.Config != null ? mission.Config.MissionCategory : MissionCategory.Secondary;

            icon = config != null ? config.ResolveIcon(missionType) : null;
            iconBaseColor = config != null ? config.ResolveBaseColor(missionCategory) : ResolveFallbackMissionIconBaseColor(missionCategory);
        }

        private GameObject ResolveMissionIconPrefab()
        {
            if (missionIconPrefab != null)
            {
                return missionIconPrefab;
            }

            missionIconPrefab = Resources.Load<GameObject>(DefaultMissionIconPrefabPath);
            return missionIconPrefab;
        }

        private MissionUIConfigSO ResolveMissionUIConfig()
        {
            if (missionUIConfig != null)
            {
                return missionUIConfig;
            }

            if (_hudDataComponent == null)
            {
                _hudDataComponent = FindObjectOfType<global::GameHUDWindowDataComponent>();
            }

            if (_hudDataComponent != null)
            {
                missionUIConfig = _hudDataComponent.ResolveMissionUIConfig();
            }

            if (missionUIConfig != null)
            {
                return missionUIConfig;
            }

            missionUIConfig = Resources.Load<MissionUIConfigSO>(DefaultMissionUIConfigPath);
            return missionUIConfig;
        }

        private static Color ResolveFallbackMissionIconBaseColor(MissionCategory missionCategory)
        {
            switch (missionCategory)
            {
                case MissionCategory.Primary:
                    return new Color32(120, 24, 38, 255);
                case MissionCategory.Secondary:
                    return new Color32(32, 92, 116, 255);
                default:
                    return Color.white;
            }
        }

        /// <summary>
        /// 解析某个任务当前应该指向的世界坐标点。
        /// 未进入任务房时优先使用推荐入口(ConnectedNodeId)作为导航中间目标；
        /// 已进入任务房后切换为任务目标点。
        /// </summary>
        private bool TryResolvePointerGuideTarget(Vector3 playerPosition, MissionBase mission, out MissionGuideTarget guideTarget)
        {
            guideTarget = default;
            if (_missionManager == null || _missionManager.CurrentMapResult == null || mission == null)
            {
                return false;
            }

            bool localMissionEntered =
                _localEnteredMissionSlots.Contains(mission.SlotIndex) ||
                _missionManager.HasLocalEnteredMission(mission.SlotIndex);

            if (_missionManager.TryResolveMissionGuideTarget(
                    playerPosition,
                    mission,
                    localMissionEntered,
                    out guideTarget))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// 将世界坐标转换为屏幕边缘或屏幕内的 UI 位置。
        /// </summary>
        private bool TryResolvePointerAnchoredPosition(Vector3 worldPoint, out Vector2 anchoredPosition, out float angle)
        {
            anchoredPosition = Vector2.zero;
            angle = 0f;

            if (_worldCamera == null || _canvasRect == null || pointerCanvas == null)
            {
                return false;
            }

            Vector3 screenPoint = _worldCamera.WorldToScreenPoint(worldPoint);
            Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            bool isBehindCamera = screenPoint.z < 0f;

            if (isBehindCamera)
            {
                screenPoint.x = Screen.width - screenPoint.x;
                screenPoint.y = Screen.height - screenPoint.y;
            }

            bool isInsideScreen =
                !isBehindCamera &&
                screenPoint.x >= screenEdgePadding &&
                screenPoint.x <= Screen.width - screenEdgePadding &&
                screenPoint.y >= screenEdgePadding &&
                screenPoint.y <= Screen.height - screenEdgePadding;

            Vector2 targetScreenPoint;
            if (isInsideScreen)
            {
                targetScreenPoint = new Vector2(screenPoint.x, screenPoint.y);
            }
            else
            {
                float clampedX = Mathf.Clamp(screenPoint.x, screenEdgePadding, Screen.width - screenEdgePadding);
                float clampedY = Mathf.Clamp(screenPoint.y, screenEdgePadding, Screen.height - screenEdgePadding);
                targetScreenPoint = new Vector2(clampedX, clampedY) + screenOffset;
                targetScreenPoint.x = Mathf.Clamp(targetScreenPoint.x, screenEdgePadding, Screen.width - screenEdgePadding);
                targetScreenPoint.y = Mathf.Clamp(targetScreenPoint.y, screenEdgePadding, Screen.height - screenEdgePadding);
            }

            Vector2 direction = new Vector2(screenPoint.x, screenPoint.y) - screenCenter;
            angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;

            return RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvasRect,
                targetScreenPoint,
                pointerCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : pointerCanvas.worldCamera,
                out anchoredPosition);
        }

        private void ApplyPointerPlacements()
        {
            if (_pointerPlacements.Count == 0)
            {
                return;
            }

            _resolvedPlacementIndices.Clear();
            for (int i = 0; i < _pointerPlacements.Count; i++)
            {
                if (_resolvedPlacementIndices.Contains(i))
                {
                    continue;
                }

                _placementGroupIndices.Clear();
                _placementGroupIndices.Add(i);
                _resolvedPlacementIndices.Add(i);

                for (int j = i + 1; j < _pointerPlacements.Count; j++)
                {
                    if (_resolvedPlacementIndices.Contains(j) || !OverlapsPlacementGroup(j))
                    {
                        continue;
                    }

                    _placementGroupIndices.Add(j);
                    _resolvedPlacementIndices.Add(j);
                }

                ApplyPointerPlacementGroup();
            }
        }

        private bool OverlapsPlacementGroup(int placementIndex)
        {
            PointerPlacement placement = _pointerPlacements[placementIndex];
            for (int i = 0; i < _placementGroupIndices.Count; i++)
            {
                PointerPlacement groupPlacement = _pointerPlacements[_placementGroupIndices[i]];
                if (PlacementsOverlap(placement, groupPlacement))
                {
                    return true;
                }
            }

            return false;
        }

        private bool PlacementsOverlap(PointerPlacement a, PointerPlacement b)
        {
            float deltaX = Mathf.Abs(a.BasePosition.x - b.BasePosition.x);
            float deltaY = Mathf.Abs(a.BasePosition.y - b.BasePosition.y);
            Vector2 combinedHalfSize = (a.LayoutSize + b.LayoutSize) * 0.5f;

            bool rectanglesOverlap =
                deltaX <= combinedHalfSize.x + PointerLayoutPadding &&
                deltaY <= combinedHalfSize.y + PointerLayoutPadding;

            return rectanglesOverlap || Vector2.Distance(a.BasePosition, b.BasePosition) <= pointerOverlapThreshold;
        }

        private void ApplyPointerPlacementGroup()
        {
            if (_placementGroupIndices.Count == 1)
            {
                PointerPlacement placement = _pointerPlacements[_placementGroupIndices[0]];
                placement.Pointer.SetAnchoredPosition(ClampPointerLocalPosition(placement.BasePosition, placement.LayoutSize));
                return;
            }

            Vector2 groupCenter = Vector2.zero;
            for (int i = 0; i < _placementGroupIndices.Count; i++)
            {
                groupCenter += _pointerPlacements[_placementGroupIndices[i]].BasePosition;
            }

            groupCenter /= _placementGroupIndices.Count;
            BuildPointerGroupOffsets(out float groupHalfWidth, out float groupHalfHeight);
            groupCenter = ClampPointerGroupCenter(groupCenter, groupHalfWidth, groupHalfHeight);

            for (int i = 0; i < _placementGroupIndices.Count; i++)
            {
                PointerPlacement placement = _pointerPlacements[_placementGroupIndices[i]];
                Vector2 offset = new Vector2(_placementGroupOffsets[i], 0f);
                placement.Pointer.SetAnchoredPosition(ClampPointerLocalPosition(groupCenter + offset, placement.LayoutSize));
            }
        }

        private void BuildPointerGroupOffsets(out float groupHalfWidth, out float groupHalfHeight)
        {
            _placementGroupOffsets.Clear();
            groupHalfWidth = 0f;
            groupHalfHeight = 0f;

            if (_placementGroupIndices.Count == 0)
            {
                return;
            }

            _placementGroupOffsets.Add(0f);
            float currentX = 0f;
            for (int i = 1; i < _placementGroupIndices.Count; i++)
            {
                PointerPlacement previous = _pointerPlacements[_placementGroupIndices[i - 1]];
                PointerPlacement current = _pointerPlacements[_placementGroupIndices[i]];
                currentX += ResolvePointerCenterSpacing(previous, current);
                _placementGroupOffsets.Add(currentX);
            }

            float centerShift = currentX * 0.5f;
            for (int i = 0; i < _placementGroupOffsets.Count; i++)
            {
                _placementGroupOffsets[i] -= centerShift;
                PointerPlacement placement = _pointerPlacements[_placementGroupIndices[i]];
                groupHalfWidth = Mathf.Max(groupHalfWidth, Mathf.Abs(_placementGroupOffsets[i]) + placement.LayoutSize.x * 0.5f);
                groupHalfHeight = Mathf.Max(groupHalfHeight, placement.LayoutSize.y * 0.5f);
            }
        }

        private float ResolvePointerCenterSpacing(PointerPlacement a, PointerPlacement b)
        {
            float layoutSpacing = (a.LayoutSize.x + b.LayoutSize.x) * 0.5f + PointerLayoutPadding;
            return Mathf.Max(pointerOverlapSpacing, layoutSpacing);
        }

        private Vector2 ClampPointerGroupCenter(Vector2 groupCenter, float groupHalfWidth, float groupHalfHeight)
        {
            if (_canvasRect == null)
            {
                return groupCenter;
            }

            Rect rect = _canvasRect.rect;
            float halfWidth = Mathf.Max(0f, rect.width * 0.5f - screenEdgePadding);
            float halfHeight = Mathf.Max(0f, rect.height * 0.5f - screenEdgePadding);
            float maxCenterX = Mathf.Max(0f, halfWidth - groupHalfWidth);
            float maxCenterY = Mathf.Max(0f, halfHeight - groupHalfHeight);

            groupCenter.x = Mathf.Clamp(groupCenter.x, -maxCenterX, maxCenterX);
            groupCenter.y = Mathf.Clamp(groupCenter.y, -maxCenterY, maxCenterY);
            return groupCenter;
        }

        private Vector2 ClampPointerLocalPosition(Vector2 localPosition, Vector2 layoutSize)
        {
            if (_canvasRect == null)
            {
                return localPosition;
            }

            Rect rect = _canvasRect.rect;
            float halfWidth = Mathf.Max(0f, rect.width * 0.5f - screenEdgePadding - layoutSize.x * 0.5f);
            float halfHeight = Mathf.Max(0f, rect.height * 0.5f - screenEdgePadding - layoutSize.y * 0.5f);

            localPosition.x = Mathf.Clamp(localPosition.x, -halfWidth, halfWidth);
            localPosition.y = Mathf.Clamp(localPosition.y, -halfHeight, halfHeight);
            return localPosition;
        }

        /// <summary>
        /// 确保指引器管理器拥有可用的 UI Canvas。
        /// </summary>
        private void EnsureCanvas()
        {
            if (!_hasExplicitPointerCanvas)
            {
                Canvas hudCanvas = ResolveHudCanvas();
                if (hudCanvas != null)
                {
                    pointerCanvas = hudCanvas;
                }
            }

            if (pointerCanvas == null)
            {
                pointerCanvas = FindObjectOfType<Canvas>();
            }

            if (pointerCanvas == null)
            {
                GameObject canvasObject = new GameObject("MissionPointerCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                pointerCanvas = canvasObject.GetComponent<Canvas>();
                pointerCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

                CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
            }

            RectTransform previousCanvasRect = _canvasRect;
            _canvasRect = ResolvePointerLayer(pointerCanvas);
            _worldCamera = Camera.main;

            if (previousCanvasRect != null && _canvasRect != null && previousCanvasRect != _canvasRect)
            {
                ReparentPointers(_canvasRect);
            }
        }

        private Canvas ResolveHudCanvas()
        {
            if (_hudDataComponent == null)
            {
                _hudDataComponent = FindObjectOfType<global::GameHUDWindowDataComponent>();
            }

            if (_hudDataComponent == null)
            {
                return null;
            }

            Canvas hudCanvas = _hudDataComponent.GetComponentInParent<Canvas>();
            return hudCanvas != null && hudCanvas.isActiveAndEnabled ? hudCanvas : null;
        }

        private RectTransform ResolvePointerLayer(Canvas canvas)
        {
            RectTransform parentRect = null;
            if (_hudDataComponent != null)
            {
                parentRect = _hudDataComponent.transform as RectTransform;
            }

            if (parentRect == null && canvas != null)
            {
                parentRect = canvas.transform as RectTransform;
            }

            if (parentRect == null)
            {
                return null;
            }

            if (_pointerLayer != null && _pointerLayer.parent == parentRect)
            {
                _pointerLayer.SetAsLastSibling();
                return _pointerLayer;
            }

            Transform existing = parentRect.Find("MissionPointerLayer_Runtime");
            if (existing != null)
            {
                _pointerLayer = existing as RectTransform;
            }

            if (_pointerLayer == null || _pointerLayer.parent != parentRect)
            {
                GameObject layerObject = new GameObject("MissionPointerLayer_Runtime", typeof(RectTransform));
                _pointerLayer = layerObject.transform as RectTransform;
                _pointerLayer.SetParent(parentRect, false);
            }

            _pointerLayer.anchorMin = Vector2.zero;
            _pointerLayer.anchorMax = Vector2.one;
            _pointerLayer.pivot = new Vector2(0.5f, 0.5f);
            _pointerLayer.offsetMin = Vector2.zero;
            _pointerLayer.offsetMax = Vector2.zero;
            _pointerLayer.localScale = Vector3.one;
            _pointerLayer.SetAsLastSibling();

            return _pointerLayer;
        }

        private void ReparentPointers(RectTransform newParent)
        {
            foreach (KeyValuePair<int, MissionPointer> pair in _pointers)
            {
                MissionPointer pointer = pair.Value;
                if (pointer != null)
                {
                    pointer.transform.SetParent(newParent, false);
                }
            }
        }

        private struct PointerPlacement
        {
            public PointerPlacement(MissionPointer pointer, Vector2 basePosition, Vector2 layoutSize)
            {
                Pointer = pointer;
                BasePosition = basePosition;
                LayoutSize = new Vector2(Mathf.Max(1f, layoutSize.x), Mathf.Max(1f, layoutSize.y));
            }

            public MissionPointer Pointer { get; }
            public Vector2 BasePosition { get; }
            public Vector2 LayoutSize { get; }
        }
    }
}
