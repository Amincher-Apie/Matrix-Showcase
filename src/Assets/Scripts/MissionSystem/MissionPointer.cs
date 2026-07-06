using Matrix.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Matrix.Missions
{
    public sealed class MissionPointer : MonoBehaviour
    {
        private RectTransform _rectTransform;
        private CanvasGroup _canvasGroup;
        private BasicMissionIcon _missionIcon;
        [SerializeField]
        private TMP_Text distanceText;

        private Text _legacyDistanceText;

        public int SlotIndex { get; private set; }

        /// <summary>
        /// 创建一个运行时任务指引器，并挂入指定 UI 父节点。
        /// </summary>
        public static MissionPointer CreateRuntimePointer(Transform parent, int slotIndex, GameObject iconPrefab)
        {
            GameObject root = iconPrefab != null
                ? Instantiate(iconPrefab, parent, false)
                : CreateFallbackIconObject(parent);

            root.name = $"MissionPointer_{slotIndex}";
            MissionPointer pointer = root.GetComponent<MissionPointer>();
            if (pointer == null)
            {
                pointer = root.AddComponent<MissionPointer>();
            }

            pointer.Initialize(slotIndex);
            return pointer;
        }

        public static MissionPointer CreateRuntimePointer(Transform parent, int slotIndex)
        {
            return CreateRuntimePointer(parent, slotIndex, null);
        }

        /// <summary>
        /// 初始化指引器的槽位编号与缓存组件。
        /// </summary>
        public void Initialize(int slotIndex)
        {
            SlotIndex = slotIndex;
            _rectTransform = transform as RectTransform;
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
            {
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            _missionIcon = GetComponent<BasicMissionIcon>();
            if (_missionIcon == null)
            {
                _missionIcon = gameObject.AddComponent<BasicMissionIcon>();
            }

            if (_rectTransform != null)
            {
                _rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                _rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                _rectTransform.pivot = new Vector2(0.5f, 0.5f);
            }

            EnsureDistanceText();
            SetVisible(false);
        }

        public void SetVisual(Sprite icon, Color iconBaseColor)
        {
            if (_missionIcon == null)
            {
                _missionIcon = GetComponent<BasicMissionIcon>() ?? gameObject.AddComponent<BasicMissionIcon>();
            }

            _missionIcon.SetVisual(icon, iconBaseColor);
        }

        /// <summary>
        /// 旧接口保留为空实现，任务指引器不再显示文字。
        /// </summary>
        public void SetLabel(string label)
        {
        }

        /// <summary>
        /// 切换指引器显隐状态。
        /// </summary>
        public void SetVisible(bool visible)
        {
            if (_canvasGroup == null)
            {
                return;
            }

            _canvasGroup.alpha = visible ? 1f : 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
        }

        /// <summary>
        /// 设置指引器在 Canvas 上的锚点位置。
        /// </summary>
        public void SetAnchoredPosition(Vector2 anchoredPosition)
        {
            if (_rectTransform != null)
            {
                _rectTransform.anchoredPosition = anchoredPosition;
            }
        }

        public Vector2 GetLayoutSize()
        {
            if (_rectTransform == null)
            {
                _rectTransform = transform as RectTransform;
            }

            if (_rectTransform == null)
            {
                return new Vector2(96f, 74f);
            }

            Vector2 size = _rectTransform.rect.size;
            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(_rectTransform, _rectTransform);
            if (bounds.size.sqrMagnitude > 0f)
            {
                size.x = Mathf.Max(size.x, bounds.size.x);
                size.y = Mathf.Max(size.y, bounds.size.y);
            }

            size.x = Mathf.Max(size.x, 96f);
            size.y = Mathf.Max(size.y, 74f);
            return size;
        }

        public void SetDistance(float distanceMeters)
        {
            EnsureDistanceText();
            int roundedMeters = Mathf.Max(0, Mathf.RoundToInt(distanceMeters));
            string text = $"{roundedMeters}m";

            if (distanceText != null)
            {
                distanceText.text = text;
                distanceText.ForceMeshUpdate();
            }

            if (_legacyDistanceText != null)
            {
                _legacyDistanceText.text = text;
            }
        }

        /// <summary>
        /// 旧接口保留为空实现，图标本体不随屏幕方向旋转。
        /// </summary>
        public void SetArrowRotation(float zRotation)
        {
        }

        private void EnsureDistanceText()
        {
            if (distanceText != null || _legacyDistanceText != null)
            {
                return;
            }

            distanceText = GetComponentInChildren<TMP_Text>(true);
            if (distanceText != null)
            {
                distanceText.raycastTarget = false;
                return;
            }

            _legacyDistanceText = GetComponentInChildren<Text>(true);
            if (_legacyDistanceText != null)
            {
                _legacyDistanceText.raycastTarget = false;
                if (_legacyDistanceText.font == null)
                {
                    _legacyDistanceText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                }
                return;
            }

            GameObject textObject = new GameObject("DistanceText", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(transform, false);

            RectTransform textRect = textObject.transform as RectTransform;
            if (textRect != null)
            {
                textRect.anchorMin = new Vector2(0.5f, 0f);
                textRect.anchorMax = new Vector2(0.5f, 0f);
                textRect.pivot = new Vector2(0.5f, 1f);
                textRect.anchoredPosition = new Vector2(0f, -4f);
                textRect.sizeDelta = new Vector2(96f, 24f);
            }

            distanceText = textObject.GetComponent<TextMeshProUGUI>();
            distanceText.fontSize = 18f;
            distanceText.alignment = TextAlignmentOptions.Top;
            distanceText.color = Color.white;
            distanceText.raycastTarget = false;
        }

        private static GameObject CreateFallbackIconObject(Transform parent)
        {
            GameObject root = new GameObject("MissionPointer", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            root.transform.SetParent(parent, false);

            RectTransform rootRect = root.transform as RectTransform;
            rootRect.sizeDelta = new Vector2(50f, 50f);

            Image baseImage = root.GetComponent<Image>();
            baseImage.color = new Color32(120, 24, 38, 255);
            baseImage.raycastTarget = false;

            GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconObject.transform.SetParent(root.transform, false);

            RectTransform iconRect = iconObject.transform as RectTransform;
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = Vector2.zero;
            iconRect.sizeDelta = new Vector2(20f, 20f);

            Image iconImage = iconObject.GetComponent<Image>();
            iconImage.color = Color.white;
            iconImage.raycastTarget = false;

            return root;
        }
    }
}
