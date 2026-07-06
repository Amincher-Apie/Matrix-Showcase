#if UNITY_EDITOR
using System.Collections.Generic;
using Matrix.PCG;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace Matrix.PCG.Navigation.Editor
{
    /// <summary>
    /// Editor 工具：用于批量烘焙 RoomPrefab 的 NavMeshData 并自动回填到 RoomPrebakedNavMeshAsset。
    ///
    /// 功能：
    /// 1. 选中一个或多个 RoomPrefab
    /// 2. 临时实例化到烘焙场景
    /// 3. 使用 NavMeshSurface.BuildNavMesh() 烘焙
    /// 4. 保存 NavMeshData 为 .asset
    /// 5. 自动回填 RoomPrebakedNavMeshAsset.navMeshData
    ///
    /// 使用方法：
    /// MenuItem -> Tools -> PCG NavMesh -> Bake Room Prefabs
    /// </summary>
    public static class RoomNavMeshBakeEditor
    {
        private const string MenuRoot = "Tools/PCG NavMesh/";
        private const string BakeAllPrefabs = MenuRoot + "Bake All PCG Room Prefabs";
        private const string BakeSelectedPrefab = MenuRoot + "Bake Selected Prefab";
        private const string ValidateAllPrefabs = MenuRoot + "Validate All PCG Room Prefabs";
        private const string ClearBakedData = MenuRoot + "Clear Baked Data";

        private static readonly string OutputBasePath = "Assets/Resources/Data/PrebakedNavMeshAssets/";

        /// <summary>
        /// 参与 NavMesh 烘焙的层级名称。门/连接器应放置在 Door 层，不会被烘焙。
        /// </summary>
        private const string NavMeshLayerName = "NavMeshGeometry";

        [MenuItem(BakeSelectedPrefab)]
        public static void BakeSelectedPrefabMenu()
        {
            if (LayerMask.NameToLayer(NavMeshLayerName) == -1)
            {
                Debug.LogError($"[RoomNavMeshBakeEditor] Layer \"{NavMeshLayerName}\" 不存在！");
                return;
            }

            var selected = Selection.activeObject as GameObject;
            if (selected == null)
            {
                Debug.LogWarning("[RoomNavMeshBakeEditor] 请在 Project 窗口中选中一个 RoomPrefab。");
                return;
            }

            string prefabPath = AssetDatabase.GetAssetPath(selected);
            if (string.IsNullOrEmpty(prefabPath) || !prefabPath.EndsWith(".prefab"))
            {
                Debug.LogWarning($"[RoomNavMeshBakeEditor] {selected.name} 不是 Prefab 资源。");
                return;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (BakeSinglePrefab(prefab, prefabPath))
            {
                Debug.Log($"[RoomNavMeshBakeEditor] 单个烘焙完成: {prefab.name}");
            }
            else
            {
                Debug.LogError($"[RoomNavMeshBakeEditor] 单个烘焙失败: {prefab.name}");
            }
        }

        [MenuItem(BakeSelectedPrefab, true)]
        public static bool BakeSelectedPrefabMenuValidate()
        {
            return !EditorApplication.isPlaying
                   && Selection.activeObject is GameObject
                   && PrefabUtility.GetPrefabAssetType(Selection.activeObject) != PrefabAssetType.NotAPrefab;
        }

        [MenuItem(BakeAllPrefabs)]
        public static void BakeAllPrefabsMenu()
        {
            if (LayerMask.NameToLayer(NavMeshLayerName) == -1)
            {
                Debug.LogError($"[RoomNavMeshBakeEditor] Layer \"{NavMeshLayerName}\" 不存在！请在 Edit → Project Settings → Tags and Layers 中创建该层级。");
                return;
            }

            string path = EditorUtility.OpenFolderPanel("选择 RoomPrefab 所在目录", "Assets", "Prefabs");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { path.Replace(Application.dataPath, "Assets") });
            if (guids.Length == 0)
            {
                Debug.LogWarning("[RoomNavMeshBakeEditor] 指定目录下没有找到 Prefab 文件。");
                return;
            }

            int bakedCount = 0;
            int failedCount = 0;

            foreach (var guid in guids)
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null)
                {
                    continue;
                }

                if (BakeSinglePrefab(prefab, prefabPath))
                {
                    bakedCount++;
                }
                else
                {
                    failedCount++;
                }
            }

            Debug.Log($"[RoomNavMeshBakeEditor] 烘焙完成！成功={bakedCount}，失败={failedCount}");
            AssetDatabase.SaveAssets();
        }

        [MenuItem(BakeAllPrefabs, true)]
        public static bool BakeAllPrefabsMenuValidate()
        {
            return !EditorApplication.isPlaying;
        }

        [MenuItem(ValidateAllPrefabs)]
        public static void ValidateAllPrefabsMenu()
        {
            if (LayerMask.NameToLayer(NavMeshLayerName) == -1)
            {
                Debug.LogError($"[RoomNavMeshBakeEditor] Layer \"{NavMeshLayerName}\" 不存在！请在 Edit → Project Settings → Tags and Layers 中创建该层级。");
                return;
            }

            string path = EditorUtility.OpenFolderPanel("选择 RoomPrefab 所在目录", "Assets", "Prefabs");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { path.Replace(Application.dataPath, "Assets") });
            if (guids.Length == 0)
            {
                Debug.LogWarning("[RoomNavMeshBakeEditor] 指定目录下没有找到 Prefab 文件。");
                return;
            }

            int validCount = 0;
            int invalidCount = 0;
            var errors = new List<string>();

            foreach (var guid in guids)
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null)
                {
                    continue;
                }

                var result = ValidateSinglePrefab(prefab, prefabPath);
                if (result.isValid)
                {
                    validCount++;
                    Debug.Log($"[Validate] {prefab.name}: 校验通过");
                }
                else
                {
                    invalidCount++;
                    errors.Add($"[{prefab.name}] {result.error}");
                }
            }

            if (errors.Count > 0)
            {
                Debug.LogWarning($"[Validate] 校验完成！有效={validCount}，无效={invalidCount}\n" + string.Join("\n", errors));
            }
            else
            {
                Debug.Log($"[Validate] 校验完成！有效={validCount}，无效={invalidCount}");
            }
        }

        [MenuItem(ValidateAllPrefabs, true)]
        public static bool ValidateAllPrefabsMenuValidate()
        {
            return !EditorApplication.isPlaying;
        }

        [MenuItem(ClearBakedData)]
        public static void ClearBakedDataMenu()
        {
            if (!EditorUtility.DisplayDialog("确认清空", "确定要清空所有选中 Prefab 的预烘焙 NavMesh 数据吗？", "确定", "取消"))
            {
                return;
            }

            var selected = Selection.gameObjects;
            int clearedCount = 0;

            foreach (var go in selected)
            {
                if (go == null)
                {
                    continue;
                }

                var navMeshAsset = go.GetComponentInChildren<RoomPrebakedNavMeshAsset>(true);
                if (navMeshAsset != null)
                {
                    navMeshAsset.NavMeshData = null;
                    EditorUtility.SetDirty(navMeshAsset);
                    clearedCount++;
                }
            }

            Debug.Log($"[RoomNavMeshBakeEditor] 已清空 {clearedCount} 个 Prefab 的烘焙数据。");
            AssetDatabase.SaveAssets();
        }

        [MenuItem(ClearBakedData, true)]
        public static bool ClearBakedDataMenuValidate()
        {
            return !EditorApplication.isPlaying && Selection.gameObjects != null && Selection.gameObjects.Length > 0;
        }

        /// <summary>
        /// 烘焙单个 RoomPrefab。
        /// </summary>
        /// <param name="prefab">RoomPrefab 对象。</param>
        /// <param name="prefabPath">Prefab 资源路径。</param>
        /// <returns>是否烘焙成功。</returns>
        public static bool BakeSinglePrefab(GameObject prefab, string prefabPath)
        {
            if (prefab == null)
            {
                Debug.LogError("[RoomNavMeshBakeEditor] Prefab 为空！");
                return false;
            }

            var roomRoot = prefab.GetComponent<PcgRoomRoot>();
            if (roomRoot == null)
            {
                Debug.LogWarning($"[RoomNavMeshBakeEditor] {prefab.name} 缺少 PcgRoomRoot 组件，跳过。");
                return false;
            }

            var navMeshAsset = prefab.GetComponentInChildren<RoomPrebakedNavMeshAsset>(true);
            if (navMeshAsset == null)
            {
                Debug.LogWarning($"[RoomNavMeshBakeEditor] {prefab.name} 缺少 RoomPrebakedNavMeshAsset 组件，跳过。");
                return false;
            }

            var validation = ValidateSinglePrefab(prefab, prefabPath);
            if (!validation.isValid)
            {
                Debug.LogError($"[RoomNavMeshBakeEditor] {prefab.name} 校验失败：{validation.error}");
                return false;
            }

            string scenePath = EditorSceneManager.GetSceneAt(0).path;
            EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();

            var tempScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Additive);

            try
            {
                var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                if (instance == null)
                {
                    Debug.LogError($"[RoomNavMeshBakeEditor] 无法实例化 Prefab: {prefab.name}");
                    return false;
                }

                int navMeshLayer = LayerMask.NameToLayer(NavMeshLayerName);

                var surface = instance.AddComponent<NavMeshSurface>();
                surface.agentTypeID = navMeshAsset.AgentTypeId;
                surface.defaultArea = 0;
                surface.collectObjects = CollectObjects.Children;
                surface.layerMask = 1 << navMeshLayer;

                EditorSceneManager.MarkSceneDirty(tempScene);
                surface.BuildNavMesh();

                NavMeshData navMeshData = GetNavMeshDataFromSurface(surface);
                if (navMeshData == null)
                {
                    Debug.LogError($"[RoomNavMeshBakeEditor] {prefab.name} 烘焙结果为空！");
                    Object.DestroyImmediate(instance);
                    return false;
                }

                string assetPath = GenerateOutputPath(prefab, prefabPath);
                if (string.IsNullOrEmpty(assetPath))
                {
                    Object.DestroyImmediate(instance);
                    return false;
                }

                AssetDatabase.CreateAsset(navMeshData, assetPath);
                AssetDatabase.SaveAssets();

                navMeshAsset.NavMeshData = AssetDatabase.LoadAssetAtPath<NavMeshData>(assetPath);
                navMeshAsset.LocalPositionOffset = Vector3.zero;
                navMeshAsset.LocalEulerOffset = Vector3.zero;
                EditorUtility.SetDirty(navMeshAsset);

                Debug.Log($"[RoomNavMeshBakeEditor] 已烘焙 {prefab.name}，保存到: {assetPath}");

                Object.DestroyImmediate(instance);
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex);
                return false;
            }
            finally
            {
                EditorSceneManager.OpenScene(scenePath);
            }
        }

        /// <summary>
        /// 校验单个 RoomPrefab 是否满足烘焙条件。
        /// </summary>
        public static (bool isValid, string error) ValidateSinglePrefab(GameObject prefab, string prefabPath)
        {
            if (prefab == null)
            {
                return (false, "Prefab 对象为空");
            }

            var roomRoot = prefab.GetComponent<PcgRoomRoot>();
            if (roomRoot == null)
            {
                return (false, "缺少 PcgRoomRoot 组件");
            }

            if (roomRoot.RoomBounds == null)
            {
                return (false, "缺少 PcgRoomBounds 组件");
            }

            if (roomRoot.Connectors == null || roomRoot.Connectors.Count == 0)
            {
                return (false, "缺少 Connector");
            }

            var navMeshAsset = prefab.GetComponentInChildren<RoomPrebakedNavMeshAsset>(true);
            if (navMeshAsset == null)
            {
                return (false, "缺少 RoomPrebakedNavMeshAsset 组件");
            }

            if (!Mathf.Approximately(prefab.transform.lossyScale.x, 1f) ||
                !Mathf.Approximately(prefab.transform.lossyScale.y, 1f) ||
                !Mathf.Approximately(prefab.transform.lossyScale.z, 1f))
            {
                return (false, $"Prefab 缩放不为 (1,1,1)，当前为 {prefab.transform.lossyScale}");
            }

            Vector3 euler = prefab.transform.eulerAngles;
            if (Mathf.Abs(euler.x) > 0.01f || Mathf.Abs(euler.z) > 0.01f)
            {
                return (false, $"Prefab 有 X/Z 轴旋转，禁止非 Y 轴旋转。当前: {euler}");
            }

            if (navMeshAsset.NavMeshData != null)
            {
                return (true, $"已有烘焙数据: {navMeshAsset.NavMeshData.name}");
            }

            return (true, "校验通过，可以烘焙");
        }

        private static NavMeshData GetNavMeshDataFromSurface(NavMeshSurface surface)
        {
            var so = new SerializedObject(surface);
            var dataProperty = so.FindProperty("m_NavMeshData");
            if (dataProperty != null && dataProperty.objectReferenceValue is NavMeshData navMeshData)
            {
                return navMeshData;
            }
            return null;
        }

        /// <summary>
        /// 根据 Prefab 路径生成输出 Asset 的完整路径，保持与 Prefab 相同的文件夹结构。
        /// 例如: Assets/Resources/Prefab/Rooms/Connector/Connector - Corner.prefab
        ///       → Assets/Resources/Data/PrebakedNavMeshAssets/Rooms/Connector/Connector - Corner.asset
        /// </summary>
        private static string GenerateOutputPath(GameObject prefab, string prefabPath)
        {
            // 从 Prefab 路径中提取相对路径
            // 例如: Assets/Resources/Prefab/Rooms/Connector/Connector - Corner.prefab → Rooms/Connector/Connector - Corner
            const string prefabPrefix = "Assets/Resources/Prefab/";
            string relativePath = prefabPath;

            if (relativePath.StartsWith(prefabPrefix))
            {
                relativePath = relativePath.Substring(prefabPrefix.Length);
            }

            // 移除 .prefab 后缀
            if (relativePath.EndsWith(".prefab"))
            {
                relativePath = relativePath.Substring(0, relativePath.Length - 7);
            }

            // 获取目录部分
            string directory = "";
            string fileName = relativePath;

            int lastSlash = relativePath.LastIndexOf('/');
            if (lastSlash >= 0)
            {
                directory = relativePath.Substring(0, lastSlash);
                fileName = relativePath.Substring(lastSlash + 1);
            }

            // 构建完整输出路径
            string outputDirectory = OutputBasePath + directory;
            string assetPath = outputDirectory + "/" + fileName + ".asset";

            // 确保目录存在
            if (!EnsureDirectoryExists(outputDirectory))
            {
                return null;
            }

            // 生成唯一路径（如果已存在）
            return AssetDatabase.GenerateUniqueAssetPath(assetPath);
        }

        /// <summary>
        /// 确保指定目录存在，不存在则递归创建。
        /// </summary>
        private static bool EnsureDirectoryExists(string directoryPath)
        {
            if (AssetDatabase.IsValidFolder(directoryPath))
            {
                return true;
            }

            string currentPath = "";
            string[] pathParts = directoryPath.Split('/');

            foreach (var part in pathParts)
            {
                if (string.IsNullOrEmpty(part))
                {
                    continue;
                }

                string newPath = string.IsNullOrEmpty(currentPath) ? part : currentPath + "/" + part;

                if (!AssetDatabase.IsValidFolder(newPath))
                {
                    if (!string.IsNullOrEmpty(currentPath))
                    {
                        AssetDatabase.CreateFolder(currentPath, part);
                    }
                    else
                    {
                        // 第一个部分应该是 Assets
                        AssetDatabase.CreateFolder("Assets", part);
                    }
                }

                currentPath = newPath;
            }

            return AssetDatabase.IsValidFolder(directoryPath);
        }
    }
}
#endif