#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace Matrix.PCG.Navigation.Editor
{
    /// <summary>
    /// Editor 工具：在 Scene View 中预览预烘焙的 NavMeshData + 房间模型。
    ///
    /// 两种用法：
    /// 1. 拖 NavMeshData .asset → 在原点显示 NavMesh 蓝色面片
    /// 2. 拖 RoomPrefab (GameObject) → 临时实例化房间模型 + 加载其 NavMeshData，
    ///    可以直观看到 NavMesh 是否覆盖到门口
    ///
    /// 确保 Window → AI → Navigation → Show NavMesh 已勾选。
    /// </summary>
    public class NavMeshDataPreviewTool : EditorWindow
    {
        private GameObject _roomPrefab;
        private GameObject _tempInstance;
        private NavMeshData _previewData;
        private NavMeshDataInstance? _previewInstance;
        private bool _isShowing;
        private Vector3 _previewPosition = Vector3.zero;

        [MenuItem("Tools/PCG NavMesh/NavMeshData Preview Tool")]
        public static void ShowWindow()
        {
            var window = GetWindow<NavMeshDataPreviewTool>("NavMesh Preview");
            window.minSize = new Vector2(380, 320);
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("NavMeshData 预览（可结合房间模型）", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            EditorGUILayout.HelpBox(
                "方式一：拖 NavMeshData .asset → 在指定位置显示 NavMesh\n" +
                "方式二：拖 RoomPrefab → 临时实例化房间 + 自动加载其 NavMeshData\n" +
                "确保 Window → AI → Navigation → Show NavMesh 已勾选。",
                MessageType.Info);

            EditorGUILayout.Space(8);

            // ── 方式一：NavMeshData ──
            EditorGUILayout.LabelField("方式一：直接预览 NavMeshData", EditorStyles.miniBoldLabel);
            var newData = (NavMeshData)EditorGUILayout.ObjectField(
                "NavMeshData",
                _previewData,
                typeof(NavMeshData),
                false);

            if (newData != _previewData)
            {
                ClearPreview();
                _previewData = newData;
                if (_previewData != null)
                    _roomPrefab = null;
            }

            EditorGUILayout.Space(8);

            // ── 方式二：RoomPrefab ──
            EditorGUILayout.LabelField("方式二：从 RoomPrefab 加载（推荐）", EditorStyles.miniBoldLabel);
            var newPrefab = (GameObject)EditorGUILayout.ObjectField(
                "Room Prefab",
                _roomPrefab,
                typeof(GameObject),
                false);

            if (newPrefab != _roomPrefab)
            {
                ClearPreview();
                _roomPrefab = newPrefab;
                if (_roomPrefab != null)
                {
                    _previewData = null;
                    ResolveNavMeshFromPrefab();
                }
            }

            if (_roomPrefab != null && _previewData == null)
            {
                EditorGUILayout.HelpBox(
                    "该 Prefab 没有 RoomPrebakedNavMeshAsset 或 NavMeshData 为空。",
                    MessageType.Warning);
            }

            EditorGUILayout.Space(8);

            // ── 预览位置 ──
            _previewPosition = EditorGUILayout.Vector3Field("预览位置", _previewPosition);

            EditorGUILayout.Space(10);

            // ── 按钮 ──
            var canShow = (_previewData != null) && !_isShowing;
            EditorGUI.BeginDisabledGroup(!canShow);
            if (GUILayout.Button("Show In Scene", GUILayout.Height(30)))
            {
                ShowPreview();
            }
            EditorGUI.EndDisabledGroup();

            EditorGUI.BeginDisabledGroup(!_isShowing);
            if (GUILayout.Button("Clear", GUILayout.Height(25)))
            {
                ClearPreview();
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space(5);

            var status = _isShowing
                ? $"预览中 (位置: {_previewPosition})" +
                  (_tempInstance != null ? $" | 房间: {_tempInstance.name}" : "")
                : "空闲";
            EditorGUILayout.LabelField($"状态: {status}");
        }

        private void ResolveNavMeshFromPrefab()
        {
            if (_roomPrefab == null)
                return;

            var navAsset = _roomPrefab.GetComponentInChildren<RoomPrebakedNavMeshAsset>(true);
            if (navAsset == null || !navAsset.HasValidData)
                return;

            _previewData = navAsset.NavMeshData;
            _previewPosition = navAsset.LocalPositionOffset;

            Debug.Log($"[NavMeshPreview] 从 Prefab '{_roomPrefab.name}' 解析到 NavMeshData: {_previewData.name}");
        }

        private void ShowPreview()
        {
            if (_previewData == null)
                return;

            ClearPreview();

            // 实例化房间模型
            if (_roomPrefab != null)
            {
                _tempInstance = (GameObject)PrefabUtility.InstantiatePrefab(_roomPrefab);
                _tempInstance.transform.position = _previewPosition;
                _tempInstance.transform.rotation = Quaternion.identity;
                _tempInstance.name = $"[Preview] {_roomPrefab.name}";
                Selection.activeGameObject = _tempInstance;
            }

            // 加载 NavMeshData
            _previewInstance = NavMesh.AddNavMeshData(_previewData, _previewPosition, Quaternion.identity);
            _isShowing = true;

            // 聚焦 Scene View
            SceneView.lastActiveSceneView?.FrameSelected();
            SceneView.RepaintAll();

            var bounds = _previewData.sourceBounds;
            Debug.Log($"[NavMeshPreview] 已加载 {_previewData.name}，" +
                      $"Bounds: center={bounds.center}, size={bounds.size}，" +
                      $"位置: {_previewPosition}");
        }

        private void ClearPreview()
        {
            if (_previewInstance.HasValue)
            {
                NavMesh.RemoveNavMeshData(_previewInstance.Value);
                _previewInstance = null;
            }

            if (_tempInstance != null)
            {
                DestroyImmediate(_tempInstance);
                _tempInstance = null;
            }

            _isShowing = false;
            SceneView.RepaintAll();
        }

        private void OnDestroy()
        {
            ClearPreview();
        }

        private void OnDisable()
        {
            ClearPreview();
        }
    }
}
#endif
