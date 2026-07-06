# UI Framework 模块

## 1. 模块职责

独立的 UI 框架层，提供：

- 窗口全生命周期管理（预加载/弹出/显示/隐藏/销毁）
- 窗口层级与遮罩管理（单遮罩模式 / 叠遮罩模式）
- 窗口堆栈系统（Push/Pop 栈管理，支持链式出栈）
- 控件事件绑定（Button/Toggle/InputField/Slider）
- 窗口动画系统（DOTween：淡入/淡出 + 子元素依次呈现）
- **编辑器代码生成工具链**（3 个工具：控件扫描 → 绑定脚本 → 窗口脚本，支持一键生成完整窗口代码）
- UI 与 3D 渲染纹理的集成（`RenderTextureFor3DUITester`）

## 2. 模块边界

| 边界 | 说明 |
|------|------|
| **架构** | 核心 `WindowBehavior` 是纯 C# 抽象类，不依赖 `MonoBehaviour`，窗口生命周期自主管理 |
| **资源加载** | 通过 `Resources.Load<GameObject>(path)` 从 `WindowConfig`（SO）指定的路径加载预制体 |
| **动画** | 依赖 DOTween（`DOFade`/`DOScale`），可通过 `_disableAnimation` 全局禁用 |
| **不负责** | 窗口业务逻辑（游戏层 `Scripts/UI/` 中继承 `WindowBase` 的子类）；UI 数据（由各窗口自行管理） |

**文件分布**：

```
Assets/Framework/UI/
├── Base/
│   ├── WindowBehavior.cs         # 抽象基类（纯C#，不依赖Mono）
│   ├── WindowBase.cs             # 窗口基类（控件/动画/遮罩）
│   ├── WindowConfig.cs           # 窗口配置表 (SO)
│   └── UISetting.cs              # 框架设置 (SO)
├── Core/
│   └── UIManager.cs              # UI管理器 (Singleton)
├── Editor/
│   ├── AnalysisControlTool.cs    # 控件分析工具
│   ├── ControlData.cs            # 控件数据结构
│   ├── GenerateWindowComponentTool.cs  # 窗口脚本生成
│   ├── GenerateBindComponentTool.cs    # 绑定组件脚本生成
│   ├── GenerateItemComponentTool.cs    # Item脚本生成
│   └── ScriptDisplayWindow.cs         # 脚本预览窗口
└── Test/
    ├── UITester.cs               # UI基础测试
    ├── CombatUITester.cs          # 战斗UI测试
    ├── NetworkSceneUITester.cs    # 网络场景UI测试
    └── RenderTextureFor3DUITester.cs  # 3D渲染纹理UI测试
```

## 3. 核心流程

### 3.1 窗口生命周期

```
UIManager.Initialize()
    → UICamera + UIRoot + WindowConfig(SO) 绑定
    │
窗口弹出:
    PopUpWindow<T>()
        ├─ 已创建? → ShowWindow() → OnShow() → ShowAnimation()
        └─ 未创建? → InitializeAndShowWindow()
            ├─ LoadWindow(name) → Resources.Load + Instantiate
            ├─ InitializeWindowProperties() → Canvas/Transform/sortingOrder
            ├─ OnAwake() → InitBaseComponents() (CanvasGroup/UIMask/UIContent)
            ├─ SetVisible(true) → CanvasGroup.alpha + interactable
            ├─ OnShow() → ShowAnimation()
            └─ 添加到 windowDic/windowList/visibleWindowList

窗口隐藏:
    HideWindow(name)
        └─ HideWindowInternal()
            ├─ SetVisible(false) + OnHide()
            └─ visibleWindowList 移除 → PopNextWindowFromStack()

窗口销毁:
    DestroyWindow(name)
        └─ DestroyWindowInternal()
            ├─ OnHide() + OnDestroy()
            └─ GameObject.Destroy → 各列表移除
```

### 3.2 ShowAnimation 动画流程

```
ShowAnimation()
    ├─ CanvasGroup.DOFade(0→1, 0.3s)              # 根节点淡入
    ├─ UIContent 子元素依次淡入:
    │   ├─ 给每个子元素加 CanvasGroup（如无）
    │   ├─ delay = 0.1 + index × 0.05              # 错开0.05s
    │   └─ childCanvasGroup.DOFade(0→target, 0.25s)
    └─ UIMask.DOFade(0→targetMask, 0.3s)          # 遮罩淡入
```

### 3.3 窗口堆栈流程

```
PushWindowToStack<T>(popCallback, isSingle, pushToStackTop)
    → 检查 isSingle 去重
    → 窗口已显示? → 仅 OnShow() 刷新
    → 否则创建 WindowBase 实例 → 压入 _windowStack

PopTopWindowFromStack()
    → _startPopStackWndStatus 防重入
    → PopStackWindow():
        → 取栈顶 window → 移出
        → PopUpWindow(window) → 设置 IsPopStack=true
        → 执行 PopStackListener 回调

HideWindow/DestroyWindow 后自动:
    → PopNextWindowFromStack():
        → window.IsPopStack && _startPopStackWndStatus
        → 自动出下一个栈顶窗口
```

### 3.4 遮罩单遮系统

```
SetWindowMaskVisible()
    → UISetting.SINGMASK_SYSTEM? 否则跳过
    → FindTopVisibleWindow() (sortingOrder ↓ siblingIndex)
    → 最上层窗口: SetMaskVisible(true) / 其他: false
    → 避免多层遮罩叠加
```

### 3.5 代码生成工具链

三个工具的工作流：

```
1. 选中 Prefab → "生成窗口绑定组件数据脚本"
   GenerateBindComponentTool → 递归扫描子物体
   → 收集 Button/Toggle/InputField/Slider/... 组件
   → 序列化为 ControlData JSON → PlayerPrefs 暂存

2. (自动或手动) → "生成Window脚本"
   GenerateWindowComponentTool → 读取 ControlData JSON
   → 生成含数据绑定代码的 .cs 窗口脚本
   → ScriptDisplayWindow 展示 + 写入磁盘

3. (对动态列表Item) → "生成Item脚本"
   GenerateItemComponentTool → 生成 Item 数据绑定组件
```

## 4. 核心类与文件

### UIManager (Singleton)

`UIManager.cs:14` — 全局唯一 UI 管理器。

**核心数据结构**：

| 字段 | 类型 | 说明 |
|------|------|------|
| `_windowDic` | `Dictionary<string, WindowBase>` | 已创建窗口（含隐藏） |
| `_windowList` | `List<WindowBase>` | 全部窗口 |
| `_visibleWindowList` | `List<WindowBase>` | 仅显示中的窗口 |
| `_windowStack` | `List<WindowBase>` | 窗口栈 |

**公开 API**：

| 方法 | 用途 |
|------|------|
| `PopUpWindow<T>()` | 泛型弹出窗口（自动初始化+显示） |
| `PreLoadWindow<T>()` | 预加载窗口（不显示） |
| `HideWindow(name/T)` | 隐藏窗口 |
| `DestroyWindow(name/T)` | 销毁窗口 |
| `DestroyAllWindow(filterList)` | 批量销毁（可过滤保护） |
| `GetWindow(name)` / `GetVisibleWindow<T>()` | 窗口查询 |
| `PushWindowToStack<T>(callback, isSingle, pushToTop)` | 压栈 |
| `PopTopWindowFromStack()` | 出栈 |
| `PushAndPopWindowInStack<T>()` | 压入并立即弹出 |

### WindowBehavior（抽象基类）

`WindowBehavior.cs:11` — 纯 C# 抽象类，不依赖 `MonoBehaviour`。

**设计意图**：窗口生命周期由 `UIManager` 控制，避免 `MonoBehaviour.Awake/Start` 顺序不确定导致空引用。

**公开属性**：`GameObject` / `Transform` / `Canvas` / `Name` / `Visible` / `IsPopStack` / `IsFullScreenWindow` / `PopStackListener`

**生命周期**：`OnAwake()` → `OnShow()` → `OnUpdate()` → `OnHide()` → `OnDestroy()`

### WindowBase（窗口基类）

`WindowBase.cs:11` — 继承 `WindowBehavior`，增加：

**控件管理**：
- `AddButtonClickListener(button, action)` — 自动 `RemoveAllListeners` + 绑定
- `AddToggleClickListener(toggle, action)` — 含开关状态回调
- `AddInputFieldListener(input, onChange, onEndEdit)`
- `AddSliderListener(slider, action)`
- `RemoveAllListeners()` — 批量清除

**动画系统**（DOTween）：
- `ShowAnimation()` — 黑屏淡入 + 子元素依次呈现
- `HideAnimation()` — 整体快速淡出 → `UIManager.HideWindow()`
- `SetDisableAnimation(bool)` — 全局禁用动画

**设计态 Alpha 缓存**：通过 `CacheDesignerAlphas()` 在 `OnAwake` 时缓存设计师在 Prefab 中设置的 CanvasGroup.alpha 值，动画使用目标 alpha 为设计态值而非恒为 1。

### WindowConfig (ScriptableObject)

`WindowConfig.cs:28` — 窗口配置表，`Resources.Load<WindowConfig>("WindowConfig")`。

**WindowData 字段**：

| 字段 | 说明 |
|------|------|
| `windowName` | 类名（与 Prefab 名一致） |
| `path` | `Resources` 下预制体路径（如 `Prefab/UI/LoginWindow`） |
| `isFullScreen` | 全屏窗口标记 |
| `sortingOrder` | Canvas 层级 |

**编辑器自动生成**：`GeneratorWindowConfig()` — 反射扫描所有 `WindowBase` 子类，自动补全新窗口的默认配置，已存在的保留手动修改。

### UISetting (ScriptableObject)

`UISetting.cs:29` — `Resources.Load<UISetting>("UISetting")`，懒加载静态实例。

| 字段 | 默认值 | 说明 |
|------|--------|------|
| `SINGMASK_SYSTEM` | `false` | 是否启用单遮罩模式 |
| `ParseType` | `Name` | 控件解析方式（名称/Tag） |
| `GeneratorType` | `Bind` | 代码生成方式（查找/绑定） |
| `WindowGeneratorPath` | — | 窗口脚本生成路径 |
| `BindComponentGeneratorPath` | — | 绑定组件生成路径 |
| `ItemScriptsGeneratorPath` | — | Item 脚本生成路径 |
| `WindowPrefabFolderPathArr` | — | 窗口预制体存放路径 |
| `UsingNameSpaceArr` | — | 自动生成脚本的 Using 命名空间 |

### Editor 工具 (3 个)

| 工具 | 菜单入口 | 功能 |
|------|---------|------|
| `GenerateBindComponentTool` | `GameObject > 生成窗口绑定组件数据脚本` | 递归扫描选中 Prefab 的 UI 控件 → JSON |
| `GenerateWindowComponentTool` | `GameObject > 生成Window脚本` | 读取 JSON → 生成窗口 .cs 脚本 |
| `GenerateItemComponentTool` | — | 为动态列表 Item 生成数据绑定组件 |

**依赖**：数据通过 `PlayerPrefs`（`ControlDataList` JSON）在工具间传递（编辑器阶段临时通道，无需改进）。

### Test 文件 (4 个)

| 类 | 用途 |
|----|------|
| `UITester` | 基础 UI 测试（窗口弹出/隐藏） |
| `CombatUITester` | 战斗 UI 测试 |
| `NetworkSceneUITester` | 网络场景 UI 测试 |
| `RenderTextureFor3DUITester` | 3D 渲染纹理 UI 测试（配合 `RenderTextureFor3DuiTester` + 3D UI Camera，暂不维护） |

## 5. 对外接口

### UIManager 主要 API

| 方法 | 用途 |
|------|------|
| `PopUpWindow<T>()` | 弹出窗口 |
| `HideWindow(name/T)` | 隐藏窗口 |
| `DestroyWindow(name/T)` | 销毁窗口 |
| `GetWindow(name)` | 查询窗口实例 |
| `PushWindowToStack<T>(callback, isSingle, pushToTop)` | 压栈 |
| `PopTopWindowFromStack()` | 出栈 |
| `PreLoadWindow<T>()` | 预加载 |

### WindowBehavior 生命周期

| 方法 | 时机 | 用途 |
|------|------|------|
| `OnAwake()` | 窗口实例化后 | 初始化素材/控件引用 |
| `OnShow()` | 每次显示 | 刷新数据/绑定事件 |
| `OnUpdate()` | 每帧 | 轮询逻辑（需 UIManager 驱动） |
| `OnHide()` | 每次隐藏 | 暂停轮询/注销事件 |
| `OnDestroy()` | 销毁前 | 释放资源/注销全局事件 |

### WindowBase 事件

| 事件 | 触发时机 |
|------|---------|
| `OnDestroyEvent` (Action) | 窗口销毁时 |

## 6. 依赖模块

| 依赖模块 | 用途 |
|------|------|
| `Framework.Singleton.SingletonBase<T>` | UIManager 单例 |
| `DOTween` | 窗口动画（DOFade/DOScale） |
| `Odin Inspector`（可选） | UISetting 的 Inspector 美化（`#if ODIN_INSPECTOR` 条件编译） |
| `UnityEngine.UI` | Canvas/CanvasGroup/Button/Toggle/InputField/Slider |
| `UnityEngine.Resources` | Prefab 加载 |
| `UnityEditor`（#if UNITY_EDITOR） | 代码生成工具 + WindowConfig 自动生成 |
| `Newtonsoft.Json`（Plastic SCM 版） | 代码生成工具的 JSON 序列化 |

## 7. 被哪些模块依赖

| 依赖方 | 用途 |
|--------|------|
| `Scripts/UI/` 所有窗口类 | 继承 `WindowBase`，通过 `UIManager.PopUpWindow()` 弹出 |
| `PlayerControl` / 输入系统 | 按 ESC 弹出菜单窗口 |
| `RunSystem` | 对局结算窗口 |
| `InventorySystem` | 背包 UI 窗口 |
| `MissionSystem` (MissionPointerManager) | 自动创建 Canvas + 指引器 UI |

## 8. 事件订阅与广播

UI Framework **不通过 EventCenter 通信**。窗口间交互通过：
- `UIManager` 直接 API 调用（`HideWindow`/`PopUpWindow`）
- `PopStackListener` 回调传递数据
- C# event `OnDestroyEvent`

## 9. Inspector 字段

### WindowBase

| 字段 | 类型 | 默认 | 说明 |
|------|------|------|------|
| `_useDesignerAlpha` | `bool` | `true` | 使用预制体中设计师设置的 alpha 作为动画终点 |
| `IsFullScreen` | `bool` | `false` | 全屏窗口标记（弹出时其他窗口伪隐藏） |

### UISetting (SO)

| 字段 | 类型 | 说明 |
|------|------|------|
| `SINGMASK_SYSTEM` | `bool` | 单遮罩模式开关 |
| `ParseType` | `ParseType` | 控件解析方式 |
| `GeneratorType` | `GeneratorType` | 代码生成方式 |
| `WindowGeneratorPath` | `string` | 窗口脚本生成路径 |
| `BindComponentGeneratorPath` | `string` | 绑定组件生成路径 |
| `ItemScriptsGeneratorPath` | `string` | Item 脚本生成路径 |
| `WindowPrefabFolderPathArr` | `string[]` | 窗口预制体存放路径 |
| `UsingNameSpaceArr` | `string[]` | 自动生成脚本的 Using 命名空间 |

### WindowConfig (SO)

| 字段 | 类型 | 说明 |
|------|------|------|
| `windowDatas` | `List<WindowData>` | 所有窗口的配置列表 |

### WindowData

| 字段 | 类型 | 说明 |
|------|------|------|
| `windowName` | `string` | 类名（需与 Prefab 名一致） |
| `path` | `string` | Resources 下预制体路径 |
| `isFullScreen` | `bool` | 全屏标记 |
| `sortingOrder` | `int` | Canvas 层级 |

## 10. Prefab / Scene / ScriptableObject 依赖

| 类型 | 路径/名称 | 用途 |
|------|----------|------|
| SO | `Resources/WindowConfig.asset` | 窗口配置表（必需） |
| SO | `Resources/UISetting.asset` | 框架设置（必需） |
| Prefab | `Resources/Prefab/UI/UIComponent/*` | UI 通用组件预制体 |
| Prefab | `Resources/Prefab/UI/UIItem/*` | UI Item 预制体 |
| Scene | `UITestScene.unity` | UI 测试场景 |
| Scene | `SampleScene.unity` | UICamera + UIRoot 所在主场景 |

**场景依赖**：
- 场景中必须有名为 `UICamera` 的 Camera
- 场景中必须有名为 `UIRoot` 的 GameObject（作为所有 UI 的根节点）

## 11. 代码生成工具使用约定

**操作流程**（新建一个窗口）：

```
1. 在 UISetting 中配置 _WindowPrefabFolderPathArr_（预制体存放路径）
2. 创建窗口 Prefab → 挂载 Canvas/CanvasGroup/UIContent/UIMask
3. 选中 Prefab → 菜单 "生成窗口绑定组件数据脚本" → 生成 JSON
4. 菜单 "生成Window脚本" → 生成 .cs 窗口类
5. 窗口自动注册到 WindowConfig.asset（通过 GeneratorWindowConfig 反射扫描）
```

**生成脚本结构**：

```
Window类 (继承WindowBase):
  → DataComponent (数据绑定)
  → 控件引用 (自动查找到的 Button/Text/Image 等)
  → 生命周期覆写 (OnAwake/OnShow/OnHide/OnDestroy)
```

## 12. 常见问题

**Q: 为什么 WindowBehavior 不继承 MonoBehaviour？**
A: 避免 Unity 的 `Awake/Start` 调用顺序不确定导致空引用。UIManager 在 `InitializeAndShowWindow` 中按固定顺序调用 `OnAwake()` → `SetVisible()` → `OnShow()`，保证组件引用已就绪。

**Q: 窗口的 Canvas 和 SortingOrder 如何管理？**
A: `WindowConfig` 中配置 `sortingOrder`。`FindTopVisibleWindow()` 按 `sortingOrder ↓ siblingIndex` 找最上层窗口。单遮罩模式下仅最上层窗口显示遮罩。

**Q: 代码生成工具的反射扫描范围是什么？**
A: `Assembly.GetAssembly(typeof(WindowBase))` 扫描 `WindowBase` 所在程序集中所有非抽象子类。所有游戏窗口均继承 `WindowBase`，同一程序集，无需额外配置。

**Q: 如何关闭一个窗口并自动打开上一个？**
A: 使用 `HideWindow` 或 `DestroyWindow` 后，若该窗口是通过堆栈弹出的（`IsPopStack=true`），UIManager 自动调用 `PopNextWindowFromStack()` 弹出栈中下一个窗口。

**Q: 代码生成工具的 JSON 数据在哪里传递？**
A: 通过 `PlayerPrefs`（key=`ControlDataList`）在编辑器工具间传递，非正式数据通道（仅编辑器阶段）。

## 13. 当前完成度

| 功能 | 状态 |
|------|------|
| UIManager 核心管理 | 完成 |
| WindowBehavior 生命周期 | 完成 |
| WindowBase 控件管理 | 完成 |
| 窗口动画系统 | 完成 |
| 单遮罩系统 | 完成 |
| 窗口堆栈系统 | 完成 |
| WindowConfig SO 配置 | 完成 |
| 代码生成工具（3个） | 完成 |
| WindowConfig 自动生成 | 完成 |
| `OnUpdate()` 驱动 | **设计如此** — UIManager 不遍历窗口调用 `OnUpdate()`。如需每帧逻辑，写在代码生成工具产出的 `XXComponent`（继承 MonoBehaviour 的组件脚本）中 |
| 预制体销毁时的资源卸载 | **未实现** — `DestroyAllWindow` 中调 `Resources.UnloadUnusedAssets()` 但单个 `DestroyWindow` 不触发 |
| UICamera 的 3D UI 渲染管线 | **暂不维护** — `RenderTextureFor3DUITester` 不再继续开发 |

## 14. 修改本模块时必须同步更新的内容

- **WindowBehavior 新增生命周期** → 在 UIManager 的对应流程中调用
- **新增代码生成工具** → 同步更新 `UISetting` 的生成路径配置
- **WindowConfig 的 `GeneratorWindowConfig()` 逻辑变更** → 注意会清空现有配置并反射重建
- **修改 Prefab 根结构**（UIContent/UIMask）→ 同步更新 `InitBaseComponents()` 查找路径

## 15. 文档维护信息

| 项目 | 内容 |
|------|------|
| 创建日期 | 2026-05-14 |
| 覆盖文件数 | 15 个 .cs |
| 关联模块文档 | Singleton（SingletonBase）, 游戏层 Scripts/UI/（窗口实现类） |
