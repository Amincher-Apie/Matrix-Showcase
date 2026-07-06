using UnityEngine;

namespace Matrix.Interaction
{
    /// <summary>
    /// 世界空间 Billboard UI 组件。
    /// 挂在任意 Billboard Prefab 上，负责世界空间定位和面朝相机。
    /// 保持纯净——只管理 Transform 和文本显示，不关心谁创建/销毁它。
    /// </summary>
    public sealed class WorldBillboardUI : MonoBehaviour
    {
        [SerializeField] private Vector3 offset = new Vector3(0f, 1.8f, 0f);
        [SerializeField] private UnityEngine.UI.Text promptText;

        private Transform _anchor;
        private Camera _camera;

        /// <summary>
        /// 显示提示文本。
        /// </summary>
        /// <param name="anchor">世界空间锚点位置。</param>
        /// <param name="text">提示文本。</param>
        public void Show(Transform anchor, string text)
        {
            _anchor = anchor;
            if (promptText != null)
            {
                promptText.text = text ?? string.Empty;
            }

            gameObject.SetActive(_anchor != null);
        }

        /// <summary>
        /// 隐藏提示。
        /// </summary>
        public void Hide()
        {
            _anchor = null;
            gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (_anchor == null)
            {
                Hide();
                return;
            }

            if (_camera == null)
            {
                _camera = Camera.main;
            }

            transform.position = _anchor.position + offset;

            if (_camera != null)
            {
                transform.rotation = Quaternion.LookRotation(transform.position - _camera.transform.position);
            }
        }
    }
}
