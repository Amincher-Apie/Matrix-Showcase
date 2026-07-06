using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Framework.UI.Base;

namespace Framework.UI.Editor
{
    /// <summary>
    /// 反向生成 UI Prefab 工具。
    /// 从已有的 DataComponent 代码，在 Prefab 中创建对应的 UI 控件结构并自动绑定引用。
    ///
    /// 使用方式：
    /// 1. 菜单：GameObject/补全所选窗口的 Prefab 控件 → 选中挂有 DataComponent 的 Prefab → 自动补全缺失控件
    /// 2. 菜单：GameObject/反向生成所有窗口 Prefab → 批量生成尚不存在的窗口 Prefab（已有 Prefab 会跳过）
    /// </summary>
    public class ReverseGeneratePrefabTool : UnityEditor.Editor
    {
        /// <summary>
        /// 支持的 UI 控件类型及其在字段名中的后缀。
        /// 按后缀长度降序排列，优先匹配长后缀（如 RawImage 优先于 Image）。
        /// </summary>
        private static readonly (string suffix, string controlType, Type componentType, Type[] extraComponents)[] SupportedTypes =
        {
            ("RawImage",        "RawImage",     typeof(RawImage),       null),
            ("InputField",      "InputField",   typeof(InputField),     new[] { typeof(Image) }),
            ("ScrollRect",      "ScrollRect",   typeof(ScrollRect),     null),
            ("Button",          "Button",       typeof(Button),         new[] { typeof(Image) }),
            ("Slider",          "Slider",       typeof(Slider),         null),
            ("Toggle",          "Toggle",       typeof(Toggle),         null),
            ("Image",           "Image",        typeof(Image),          null),
            ("Text",            "Text",         typeof(Text),           null),
            ("GameObject",      "GameObject",   typeof(RectTransform),  null),
            ("Transform",       "Transform",    typeof(RectTransform),  null),
        };

        #region Menu: 补全所选窗口的 Prefab 控件（推荐的主入口）

        [MenuItem("GameObject/补全所选窗口的 Prefab 控件", false, 101)]
        public static void GenerateControlsForSelectedPrefab()
        {
            var selected = Selection.activeGameObject;
            if (selected == null)
            {
                EditorUtility.DisplayDialog("提示", "请先在 Hierarchy 中选择一个挂有 DataComponent 的窗口对象。", "确定");
                return;
            }

            // 1. 在选中对象上查找 DataComponent
            var allMono = selected.GetComponents<MonoBehaviour>();
            MonoBehaviour dataComponent = null;
            Type dataComponentType = null;
            foreach (var m in allMono)
            {
                if (m != null && m.GetType().Name.EndsWith("DataComponent"))
                {
                    dataComponent = m;
                    dataComponentType = m.GetType();
                    break;
                }
            }

            if (dataComponent == null)
            {
                EditorUtility.DisplayDialog("提示",
                    $"所选 GameObject '{selected.name}' 上没有挂载任何 DataComponent。\n\n请确保选中带有 XXXDataComponent 组件的窗口对象。",
                    "确定");
                return;
            }

            // 2. 确定 Prefab 根节点和路径
            GameObject prefabRoot = selected;
            string prefabPath = null;
            bool isPrefab = PrefabUtility.GetPrefabAssetType(selected) != PrefabAssetType.NotAPrefab
                            || PrefabUtility.GetPrefabInstanceStatus(selected) != PrefabInstanceStatus.NotAPrefab;

            if (isPrefab)
            {
                prefabRoot = PrefabUtility.GetNearestPrefabInstanceRoot(selected) ?? selected;
                prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(selected);
            }

            // 3. 确保根结构完整（幂等：已有不重复创建）
            EnsureRootComponents(prefabRoot);

            // 4. 获取 UIContent
            Transform uiContent = prefabRoot.transform.Find("UIContent");
            if (uiContent == null)
            {
                Debug.LogError("[ReverseGeneratePrefab] UIContent 不存在且创建失败，无法继续。");
                return;
            }

            // 5. 遍历 DataComponent 字段，补全缺失的控件
            var fields = dataComponentType.GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Where(f => !f.IsStatic)
                .ToList();

            int createdCount = 0;
            int skippedCount = 0;
            foreach (var field in fields)
            {
                Type fieldType = field.FieldType;
                string originalFieldName = field.Name;
                string fieldName = originalFieldName;

                // 数组类型：取元素类型
                if (fieldType.IsArray)
                    fieldType = fieldType.GetElementType();

                if (!TryParseFieldName(fieldName, fieldType, out string controlName, out string controlType,
                        out Type componentType, out Type[] extraComponents))
                {
                    Debug.LogWarning($"[ReverseGeneratePrefab] 跳过无法识别的字段: {originalFieldName} (类型: {fieldType?.Name})");
                    continue;
                }

                string expectedName = $"[{controlType}]{controlName}";

                // 检查 UIContent 下是否已有同名控件 → 跳过
                if (uiContent.Find(expectedName) != null)
                {
                    skippedCount++;
                    continue;
                }

                // 创建缺失的控件
                GameObject controlGo = CreateControlFromField(field, uiContent);
                if (controlGo != null)
                {
                    createdCount++;

                    // 数组类型：创建占位子节点
                    if (field.FieldType.IsArray && field.FieldType.GetElementType() == typeof(GameObject))
                    {
                        for (int i = 0; i < 3; i++)
                        {
                            var child = new GameObject($"#Item{i}", typeof(RectTransform));
                            child.transform.SetParent(controlGo.transform, false);
                        }
                    }
                }
            }

            // 6. 绑定字段引用
            BindFields(dataComponent, dataComponentType, prefabRoot, uiContent);

            // 7. 保存 Prefab 或标记脏
            if (isPrefab && !string.IsNullOrEmpty(prefabPath))
            {
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
            }
            else
            {
                EditorUtility.SetDirty(prefabRoot);
            }

            // 8. 注册到 WindowConfig
            RefreshWindowConfig();

            Debug.Log($"[ReverseGeneratePrefab] {prefabRoot.name} —— 新增 {createdCount} 个控件，跳过 {skippedCount} 个已有控件。");
        }

        #endregion

        #region Menu: 批量生成所有窗口 Prefab（安全保护：跳过已有）

        [MenuItem("GameObject/反向生成所有窗口 Prefab", false, 100)]
        public static void ReverseGenerateAll()
        {
            var dataComponentTypes = FindAllDataComponentTypes();
            if (dataComponentTypes.Count == 0)
            {
                Debug.LogWarning("[ReverseGeneratePrefab] 未找到任何 DataComponent 类型。");
                return;
            }

            var uis = UISetting.Instance;
            string prefabFolder = GetPrefabFolder(uis);
            if (string.IsNullOrEmpty(prefabFolder))
            {
                Debug.LogError("[ReverseGeneratePrefab] UISetting.WindowPrefabFolderPathArr 为空，无法保存 Prefab。");
                return;
            }

            int createdCount = 0;
            int skippedCount = 0;

            foreach (var dcType in dataComponentTypes)
            {
                string windowName = dcType.Name.Replace("DataComponent", "");
                string relativePath = Path.Combine("Assets", "Resources", prefabFolder, $"{windowName}.prefab");

                // 安全检查：已有 Prefab → 跳过，不覆盖
                if (AssetDatabase.LoadAssetAtPath<GameObject>(relativePath) != null)
                {
                    Debug.Log($"[ReverseGeneratePrefab] 跳过已存在的 Prefab: {relativePath}");
                    skippedCount++;
                    continue;
                }

                try
                {
                    GeneratePrefabForWindow(windowName, dcType);
                    createdCount++;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[ReverseGeneratePrefab] 生成 {windowName} Prefab 失败: {ex.Message}\n{ex.StackTrace}");
                }
            }

            AssetDatabase.Refresh();
            RefreshWindowConfig();

            Debug.Log($"[ReverseGeneratePrefab] 批量生成完成：新建 {createdCount} 个，跳过 {skippedCount} 个已有 Prefab。");
        }

        #endregion

        #region 核心：从零创建 Prefab（仅批量模式 & 目标不存在时调用）

        private const string WindowRootTemplatePath = "Prefab/UI/WindowRoot";

        private static void GeneratePrefabForWindow(string windowName, Type dataComponentType)
        {
            var uis = UISetting.Instance;
            string prefabFolder = GetPrefabFolder(uis);
            if (string.IsNullOrEmpty(prefabFolder))
            {
                Debug.LogError("[ReverseGeneratePrefab] UISetting.WindowPrefabFolderPathArr 为空，无法保存 Prefab。");
                return;
            }

            // 1. 加载并实例化 WindowRoot 模板（获得正确的 Canvas/Scaler/Raycaster/锚点/适配等配置）
            GameObject template = Resources.Load<GameObject>(WindowRootTemplatePath);
            if (template == null)
            {
                Debug.LogError($"[ReverseGeneratePrefab] 未找到模板 Prefab: Resources/{WindowRootTemplatePath}.prefab，请确保 WindowRoot.prefab 存在。");
                return;
            }
            GameObject root = (GameObject)PrefabUtility.InstantiatePrefab(template);
            root.name = windowName;

            // 2. 获取模板中的 UIContent（Nested Prefab，已有全屏拉伸锚点配置）
            Transform uiContent = root.transform.Find("UIContent");
            if (uiContent == null)
            {
                Debug.LogError("[ReverseGeneratePrefab] 模板 WindowRoot 中未找到 UIContent 子节点。");
                DestroyImmediate(root);
                return;
            }

            // 3. 解析 DataComponent 字段并生成 UI 控件
            var fields = dataComponentType.GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Where(f => !f.IsStatic)
                .ToList();

            foreach (var field in fields)
            {
                GameObject controlGo = CreateControlFromField(field, uiContent.transform);
                if (controlGo != null && field.FieldType.IsArray && field.FieldType.GetElementType() == typeof(GameObject))
                {
                    for (int i = 0; i < 3; i++)
                    {
                        var child = new GameObject($"#Item{i}", typeof(RectTransform));
                        child.transform.SetParent(controlGo.transform, false);
                    }
                }
            }

            // 4. 挂载 DataComponent 并绑定字段
            var dataComp = root.AddComponent(dataComponentType) as MonoBehaviour;
            if (dataComp != null)
            {
                BindFields(dataComp, dataComponentType, root);
            }

            // 5. 保存为 Prefab
            string absoluteFolder = Path.Combine(Application.dataPath, "Resources", prefabFolder.TrimStart('/'));
            if (!Directory.Exists(absoluteFolder))
                Directory.CreateDirectory(absoluteFolder);

            string relativePath = Path.Combine("Assets", "Resources", prefabFolder, $"{windowName}.prefab");
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, relativePath);
            Debug.Log($"[ReverseGeneratePrefab] 已生成 Prefab: {relativePath}", prefab);

            // 6. 清理临时 GameObject
            DestroyImmediate(root);

            // 7. 确保 WindowConfig 中有此窗口条目
            EnsureWindowConfigEntry(windowName, $"{prefabFolder}/{windowName}");
        }

        #endregion

        #region 控件创建（复用现有逻辑）

        private static GameObject CreateControlFromField(FieldInfo field, Transform parent)
        {
            string fieldName = field.Name;
            Type fieldType = field.FieldType;

            if (fieldType.IsArray)
            {
                fieldType = fieldType.GetElementType();
            }

            if (!TryParseFieldName(fieldName, fieldType, out string controlName, out string controlType,
                    out Type componentType, out Type[] extraComponents))
            {
                Debug.LogWarning($"[ReverseGeneratePrefab] 跳过无法识别的字段: {fieldName} (类型: {fieldType.Name})");
                return null;
            }

            GameObject go = new GameObject($"[{controlType}]{controlName}", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            if (componentType != typeof(RectTransform))
                go.AddComponent(componentType);

            if (extraComponents != null)
            {
                foreach (var ec in extraComponents)
                {
                    if (ec != typeof(RectTransform))
                        go.AddComponent(ec);
                }
            }

            // 每个控件挂 LayoutElement，方便设计师直接调尺寸
            go.AddComponent<LayoutElement>();

            // Button 需要 Text 子节点
            if (controlType == "Button")
            {
                var textChild = new GameObject("Text", typeof(RectTransform), typeof(Text));
                textChild.transform.SetParent(go.transform, false);
                var t = textChild.GetComponent<Text>();
                t.text = controlName;
                t.alignment = TextAnchor.MiddleCenter;
                t.color = Color.black;
                t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }

            // ScrollRect 需要 Viewport + Content 子结构
            if (controlType == "ScrollRect")
            {
                var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
                viewport.transform.SetParent(go.transform, false);
                var vpImg = viewport.GetComponent<Image>();
                vpImg.color = Color.white;
                vpImg.raycastTarget = true;

                var content = new GameObject("Content", typeof(RectTransform));
                content.transform.SetParent(viewport.transform, false);
                var sr = go.GetComponent<ScrollRect>();
                sr.viewport = viewport.GetComponent<RectTransform>();
                sr.content = content.GetComponent<RectTransform>();
                sr.horizontal = true;
                sr.vertical = true;
            }

            // Toggle 需要 Background + Checkmark 子结构
            if (controlType == "Toggle")
            {
                var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
                bg.transform.SetParent(go.transform, false);
                var checkmark = new GameObject("Checkmark", typeof(RectTransform), typeof(Image));
                checkmark.transform.SetParent(bg.transform, false);
                var toggle = go.GetComponent<Toggle>();
                toggle.targetGraphic = bg.GetComponent<Image>();
                toggle.graphic = checkmark.GetComponent<Image>();
            }

            // Slider 需要 Background + Fill + Handle 子结构
            if (controlType == "Slider")
            {
                var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
                bg.transform.SetParent(go.transform, false);
                var fillArea = new GameObject("Fill Area", typeof(RectTransform));
                fillArea.transform.SetParent(go.transform, false);
                var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
                fill.transform.SetParent(fillArea.transform, false);
                var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
                handleArea.transform.SetParent(go.transform, false);
                var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
                handle.transform.SetParent(handleArea.transform, false);
                var slider = go.GetComponent<Slider>();
                slider.targetGraphic = handle.GetComponent<Image>();
                slider.fillRect = fill.GetComponent<RectTransform>();
                slider.handleRect = handle.GetComponent<RectTransform>();
            }

            return go;
        }

        /// <summary>
        /// 从字段名中提取 controlName 和 controlType。
        /// "HealthSlider" (type=Slider) → controlName="Health", controlType="Slider"
        /// </summary>
        private static bool TryParseFieldName(string fieldName, Type fieldType, out string controlName, out string controlType, out Type componentType, out Type[] extraComponents)
        {
            controlName = fieldName;
            controlType = "GameObject";
            componentType = typeof(RectTransform);
            extraComponents = null;

            // 按后缀长度降序尝试匹配
            foreach (var (suffix, ct, compType, extras) in SupportedTypes)
            {
                if (fieldName.EndsWith(suffix, StringComparison.Ordinal) && fieldName.Length > suffix.Length)
                {
                    controlName = fieldName.Substring(0, fieldName.Length - suffix.Length);
                    controlType = ct;
                    componentType = compType;
                    extraComponents = extras;
                    return true;
                }
            }

            // 备选：直接用字段类型名作为 controlType
            if (typeof(Component).IsAssignableFrom(fieldType) || fieldType == typeof(GameObject))
            {
                controlType = fieldType.Name;
                componentType = typeof(RectTransform);
                return true;
            }

            return false;
        }

        #endregion

        #region 字段绑定

        /// <summary>
        /// 绑定 DataComponent 的字段引用到 Prefab 中的控件（从 root 搜索）。
        /// </summary>
        private static void BindFields(MonoBehaviour dataComp, Type dcType, GameObject root)
        {
            BindFields(dataComp, dcType, root, null);
        }

        /// <summary>
        /// 绑定 DataComponent 的字段引用到 Prefab 中的控件。
        /// 优先在 contentTransform 下搜索，再回退到 root 全树搜索。
        /// </summary>
        private static void BindFields(MonoBehaviour dataComp, Type dcType, GameObject root, Transform contentTransform)
        {
            var fields = dcType.GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Where(f => !f.IsStatic);

            foreach (var field in fields)
            {
                string fieldName = field.Name;
                Type fieldType = field.FieldType;

                // 优先搜索 content，再回退 root
                GameObject match = null;
                if (contentTransform != null)
                    match = FindChildByFieldName(contentTransform, fieldName, fieldType);
                if (match == null)
                    match = FindChildByFieldName(root.transform, fieldName, fieldType);

                if (match != null)
                {
                    if (fieldType == typeof(GameObject))
                    {
                        field.SetValue(dataComp, match);
                    }
                    else if (fieldType.IsArray && fieldType.GetElementType() == typeof(GameObject))
                    {
                        var children = new List<GameObject>();
                        for (int i = 0; i < match.transform.childCount; i++)
                        {
                            children.Add(match.transform.GetChild(i).gameObject);
                        }
                        field.SetValue(dataComp, children.ToArray());
                    }
                    else if (typeof(Component).IsAssignableFrom(fieldType))
                    {
                        var comp = match.GetComponent(fieldType);
                        if (comp != null)
                        {
                            field.SetValue(dataComp, comp);
                        }
                        else
                        {
                            Debug.LogWarning($"[ReverseGeneratePrefab] {match.name} 上未找到组件 {fieldType.Name}");
                        }
                    }
                }
            }
        }

        private static GameObject FindChildByFieldName(Transform root, string fieldName, Type fieldType)
        {
            // 尝试匹配后缀
            foreach (var (suffix, ct, _, _) in SupportedTypes)
            {
                if (fieldName.EndsWith(suffix) && fieldName.Length > suffix.Length)
                {
                    string controlName = fieldName.Substring(0, fieldName.Length - suffix.Length);
                    string expectedName = $"[{ct}]{controlName}";
                    var child = root.Find(expectedName);
                    if (child != null) return child.gameObject;

                    // 递归查找
                    for (int i = 0; i < root.childCount; i++)
                    {
                        var found = root.GetChild(i).Find(expectedName);
                        if (found != null) return found.gameObject;
                    }
                }
            }

            // 精确匹配
            var exact = root.Find(fieldName);
            if (exact != null) return exact.gameObject;

            return null;
        }

        #endregion

        #region 根结构保证（幂等）

        private const string UIContentTemplatePath = "Prefab/UI/UIComponent/UIContent";
        private const string UIMaskTemplatePath = "Prefab/UI/UIComponent/UIMask";

        /// <summary>
        /// 确保 root 上具备 WindowBase 所需的完整组件和子节点结构。
        /// 全部幂等：已有则不重复创建；缺失时从模板 Prefab 实例化以继承正确的 RectTransform 配置。
        /// </summary>
        private static void EnsureRootComponents(GameObject root)
        {
            // Canvas
            var canvas = root.GetComponent<Canvas>();
            if (canvas == null)
                canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            // CanvasScaler
            if (root.GetComponent<CanvasScaler>() == null)
                root.AddComponent<CanvasScaler>();

            // GraphicRaycaster
            if (root.GetComponent<GraphicRaycaster>() == null)
                root.AddComponent<GraphicRaycaster>();

            // CanvasGroup（根节点）
            if (root.GetComponent<CanvasGroup>() == null)
                root.AddComponent<CanvasGroup>();

            // UIContent（直接子节点）—— 缺失时从 UIContent.prefab 模板实例化
            Transform uiContent = root.transform.Find("UIContent");
            if (uiContent == null)
            {
                var uiContentTemplate = Resources.Load<GameObject>(UIContentTemplatePath);
                if (uiContentTemplate != null)
                {
                    var uiContentGo = (GameObject)PrefabUtility.InstantiatePrefab(uiContentTemplate);
                    uiContentGo.transform.SetParent(root.transform, false);
                }
                else
                {
                    // 回退：模板缺失时手动创建
                    var uiContentGo = new GameObject("UIContent", typeof(RectTransform));
                    uiContentGo.transform.SetParent(root.transform, false);
                    uiContentGo.AddComponent<CanvasGroup>().alpha = 1f;
                }
            }

            // UIMask（直接子节点）
            EnsureUIMask(root.transform);
        }

        private static Transform EnsureUIMask(Transform rootTransform)
        {
            Transform uiMask = rootTransform.Find("UIMask");
            if (uiMask == null)
            {
                var uiMaskTemplate = Resources.Load<GameObject>(UIMaskTemplatePath);
                if (uiMaskTemplate != null)
                {
                    var uiMaskGo = (GameObject)PrefabUtility.InstantiatePrefab(uiMaskTemplate);
                    uiMaskGo.transform.SetParent(rootTransform, false);
                    uiMask = uiMaskGo.transform;
                }
                else
                {
                    // 回退：模板缺失时手动创建
                    var uiMaskGo = new GameObject("UIMask", typeof(RectTransform), typeof(Image));
                    uiMaskGo.transform.SetParent(rootTransform, false);
                    var maskImg = uiMaskGo.GetComponent<Image>();
                    maskImg.color = new Color(0f, 0f, 0f, 0.5f);
                    var maskCg = uiMaskGo.AddComponent<CanvasGroup>();
                    maskCg.alpha = 0f;
                    maskCg.interactable = false;
                    maskCg.blocksRaycasts = false;
                    uiMask = uiMaskGo.transform;
                }
            }
            return uiMask;
        }

        #endregion

        #region 辅助

        private static List<Type> FindAllDataComponentTypes()
        {
            return Assembly.GetAssembly(typeof(WindowBase))
                .GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && typeof(MonoBehaviour).IsAssignableFrom(t) && t.Name.EndsWith("DataComponent"))
                .ToList();
        }

        private static string GetPrefabFolder(UISetting uis)
        {
            if (uis.WindowPrefabFolderPathArr != null && uis.WindowPrefabFolderPathArr.Length > 0)
                return uis.WindowPrefabFolderPathArr[0];
            return "Prefab/UI";
        }

        private static void EnsureWindowConfigEntry(string windowName, string path)
        {
            var wc = Resources.Load<WindowConfig>("WindowConfig");
            if (wc == null) return;

            var existing = wc.windowDatas.FirstOrDefault(d => d.windowName == windowName);
            if (existing != null)
            {
                if (string.IsNullOrEmpty(existing.path) || existing.path.Contains("//"))
                    existing.path = path;
            }
            else
            {
                wc.windowDatas.Add(new WindowData
                {
                    windowName = windowName,
                    path = path,
                    isFullScreen = false,
                    sortingOrder = 10,
                });
            }

            EditorUtility.SetDirty(wc);
        }

        private static void RefreshWindowConfig()
        {
            var wc = Resources.Load<WindowConfig>("WindowConfig");
            if (wc != null)
            {
                wc.GeneratorWindowConfig();
            }
        }

        #endregion
    }
}
