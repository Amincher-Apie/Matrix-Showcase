# UI框架设计文档

## 目录

- [1. 框架概述](#1-框架概述)
- [2. 架构设计](#2-架构设计)
- [3. 核心组件](#3-核心组件)
- [4. 生命周期管理](#4-生命周期管理)
- [5. 动画系统](#5-动画系统)
- [6. 窗口堆栈系统](#6-窗口堆栈系统)
- [7. 编辑器工具](#7-编辑器工具)
- [8. 使用指南](#8-使用指南)
- [9. API参考](#9-api参考)
- [10. 设计模式与最佳实践](#10-设计模式与最佳实践)

---

## 1. 框架概述

### 1.1 设计目标

本UI框架旨在为Unity游戏开发提供一个**高效、易用、可扩展**的窗口管理系统，具有以下核心特性：

- **解耦设计**：窗口逻辑与Unity MonoBehaviour完全解耦，避免脚本执行顺序问题
- **生命周期管理**：完整的窗口生命周期管理，从创建到销毁全流程控制
- **动画系统**：内置流畅的窗口切换动画，支持自定义扩展
- **堆栈管理**：智能的窗口堆栈系统，自动处理复杂窗口层级关系
- **编辑器支持**：强大的编辑器工具，自动生成代码，提升开发效率
- **性能优化**：窗口预加载、资源缓存等优化机制

### 1.2 技术特点

- **单例模式**：UIManager采用单例模式，全局唯一访问点
- **配置驱动**：通过ScriptableObject配置窗口信息，灵活可维护
- **反射机制**：编辑器工具利用反射自动扫描和生成代码
- **DOTween集成**：基于DOTween实现流畅的动画效果
- **事件驱动**：支持窗口间通信和回调机制

### 1.3 目录结构

```
Framework/UI/
├── Base/              # 基础类
│   ├── WindowBehavior.cs      # 窗口行为基类（抽象）
│   ├── WindowBase.cs          # 窗口基类（具体实现）
│   ├── WindowConfig.cs        # 窗口配置（ScriptableObject）
│   └── UISetting.cs           # UI全局设置
├── Core/              # 核心管理
│   └── UIManager.cs           # UI管理器（单例）
└── Editor/            # 编辑器工具
    ├── GenerateWindowComponentTool.cs    # 窗口脚本生成工具
    ├── GenerateBindComponentTool.cs      # 组件绑定脚本生成工具
    ├── GenerateItemComponentTool.cs      # Item脚本生成工具
    └── AnalysisControlTool.cs            # 控件分析工具
```

---

## 2. 架构设计

### 2.1 整体架构

```
┌─────────────────────────────────────────────────┐
│              UIManager (单例)                   │
│  - 窗口生命周期管理                               │
│  - 窗口资源加载                                   │
│  - 窗口堆栈管理                                   │
│  - 遮罩管理                                       │
└─────────────────────────────────────────────────┘
                    │
                    │ 管理
                    ▼
┌─────────────────────────────────────────────────┐
│            WindowBase (窗口基类)                  │
│  - 生命周期函数                                   │
│  - 动画系统                                       │
│  - 控件事件管理                                   │
│  - 可见性控制                                     │
└─────────────────────────────────────────────────┘
                    │
                    │ 继承
                    ▼
┌─────────────────────────────────────────────────┐
│         WindowBehavior (抽象基类)                 │
│  - 基础属性定义                                   │
│  - 生命周期接口                                   │
└─────────────────────────────────────────────────┘
```

### 2.2 核心设计理念

#### 2.2.1 解耦设计

框架的核心创新在于**窗口逻辑与Unity MonoBehaviour的完全解耦**：

- **问题**：传统Unity UI开发中，多个MonoBehaviour脚本的Awake执行顺序不确定，容易导致空引用
- **解决方案**：窗口类不继承MonoBehaviour，生命周期由UIManager统一管理
- **优势**：执行顺序可控，避免空引用，便于测试和维护

#### 2.2.2 配置驱动

窗口信息通过ScriptableObject配置，而非硬编码：

- **WindowConfig**：存储窗口预制体路径、层级、全屏标识等
- **编辑器自动生成**：自动扫描WindowBase子类，生成配置项
- **灵活扩展**：新增窗口只需创建类，配置自动生成

#### 2.2.3 单例管理

UIManager采用单例模式：

- **全局访问**：`UIManager.Instance` 统一访问点
- **资源管理**：统一管理窗口资源加载和释放
- **状态维护**：维护窗口字典、可见列表、堆栈等状态

---

## 3. 核心组件

### 3.1 WindowBehavior（抽象基类）

**职责**：定义窗口的基础属性和生命周期接口

**关键属性**：
```csharp
public GameObject GameObject { get; set; }      // 窗口GameObject
public Transform Transform { get; set; }        // 窗口Transform
public Canvas Canvas { get; set; }              // 窗口Canvas
public string Name { get; set; }                // 窗口名称
public bool Visible { get; set; }               // 可见性
public bool IsPopStack { get; set; }            // 是否通过堆栈弹出
public bool IsFullScreenWindow { get; set; }     // 是否全屏窗口
```

**生命周期方法**：
```csharp
public virtual void OnAwake() {}    // 初始化（对应Mono Awake）
public virtual void OnShow() {}     // 显示时调用
public virtual void OnUpdate() {}   // 更新时调用
public virtual void OnHide() {}     // 隐藏时调用
public virtual void OnDestroy() {}  // 销毁时调用
```

### 3.2 WindowBase（窗口基类）

**职责**：提供窗口的完整功能实现

**核心功能模块**：

#### 3.2.1 控件事件管理
```csharp
// 按钮事件
public void AddButtonClickListener(Button button, UnityAction action)

// 开关事件
public void AddToggleClickListener(Toggle toggle, UnityAction<bool, Toggle> action)

// 输入框事件
public void AddInputFieldListener(InputField inputField, 
    UnityAction<string> onChangeAction, 
    UnityAction<string> onEndEditAction)

// 滑动条事件
public void AddSliderListener(Slider slider, UnityAction<float> action)

// 移除所有监听器
public void RemoveAllListeners()
```

#### 3.2.2 可见性控制
```csharp
// 设置窗口可见性
public override void SetVisible(bool isVisible)

// 设置遮罩可见性
public void SetMaskVisible(bool isVisible)
```

#### 3.2.3 动画系统
```csharp
// 显示动画（可重写）
public virtual void ShowAnimation()

// 隐藏动画（可重写）
public virtual void HideAnimation()

// 禁用/启用动画
public void SetDisableAnimation(bool disable)
```

#### 3.2.4 窗口交互
```csharp
// 隐藏当前窗口
public void HideWindow()

// 显示其他窗口
public T ShowWindow<T>() where T : WindowBase, new()

// 销毁当前窗口
public void DestroySelf()
```

### 3.3 UIManager（UI管理器）

**职责**：统一管理所有窗口的生命周期和资源

**核心数据结构**：
```csharp
// 已创建窗口字典（包括隐藏的窗口）
private Dictionary<string, WindowBase> _windowDic;

// 已创建窗口列表
private List<WindowBase> _windowList;

// 可见窗口列表
private List<WindowBase> _visibleWindowList;

// 窗口堆栈
private List<WindowBase> _windowStack;
```

**核心方法**：

#### 3.3.1 窗口显示
```csharp
// 弹出窗口（泛型方法，推荐使用）
public T PopUpWindow<T>() where T : WindowBase, new()

// 弹出窗口（实例方法）
public WindowBase PopUpWindow(WindowBase window)

// 显示已存在的窗口
private void ShowWindow(WindowBase window)
private WindowBase ShowWindow(string winName)
```

#### 3.3.2 窗口隐藏
```csharp
// 隐藏窗口（名称）
public void HideWindow(string windowName)

// 隐藏窗口（类型）
public void HideWindow<T>() where T : WindowBase
```

#### 3.3.3 窗口销毁
```csharp
// 销毁窗口（名称）
public void DestroyWindow(string windowName)

// 销毁窗口（类型）
public void DestroyWindow<T>() where T : WindowBase

// 销毁所有窗口
public void DestroyAllWindow(List<string> filterList = null)
```

#### 3.3.4 窗口预加载
```csharp
// 预加载窗口（提前实例化但不显示）
public void PreLoadWindow<T>() where T : WindowBase, new()
```

#### 3.3.5 窗口查找
```csharp
// 根据名称获取窗口
public WindowBase GetWindow(string windowName)

// 获取可见窗口
public T GetVisibleWindow<T>() where T : WindowBase
```

### 3.4 WindowConfig（窗口配置）

**职责**：存储窗口的配置信息

**配置项**：
```csharp
[Serializable]
public class WindowData
{
    public string windowName;      // 窗口类名
    public string path;            // 预制体路径（Resources下）
    public bool isFullScreen;      // 是否全屏
    public int sortingOrder;       // Canvas层级
}
```

**功能**：
- 编辑器自动生成配置
- 配置缓存优化查询性能
- 支持手动编辑配置

### 3.5 UISetting（UI设置）

**职责**：全局UI系统设置

**配置项**：
- **SINGMASK_SYSTEM**：单遮罩模式开关
- **ParseType**：组件解析方式（名称/Tag）
- **GeneratorType**：代码生成方式（查找/绑定）
- **路径配置**：脚本生成路径、预制体路径等

---

## 4. 生命周期管理

### 4.1 窗口生命周期流程

```
创建窗口
    │
    ▼
InitializeWindowProperties()  // 初始化属性（GameObject、Transform、Canvas等）
    │
    ▼
OnAwake()                     // 窗口初始化（组件获取、数据绑定）
    │
    ▼
SetVisible(true)              // 设置可见性
    │
    ▼
OnShow()                      // 显示时逻辑
    │
    ├─→ ShowAnimation()       // 显示动画
    │
    ▼
[窗口显示中]
    │
    ├─→ OnUpdate()            // 更新逻辑（可选）
    │
    ▼
HideWindow()                  // 隐藏窗口
    │
    ├─→ HideAnimation()      // 隐藏动画
    │
    ▼
SetVisible(false)             // 设置不可见
    │
    ▼
OnHide()                      // 隐藏时逻辑
    │
    ▼
DestroyWindow()               // 销毁窗口
    │
    ├─→ OnDestroy()          // 销毁时逻辑
    │
    ▼
[窗口销毁]
```

### 4.2 生命周期方法详解

#### OnAwake()
- **调用时机**：窗口首次创建时
- **用途**：初始化组件引用、绑定数据、设置初始状态
- **示例**：
```csharp
public override void OnAwake()
{
    base.OnAwake();
    // 获取数据组件
    dataComponent = GameObject.GetComponent<BeginWindowDataComponent>();
    dataComponent.InitComponent(this);
    // 设置初始状态
    SetDisableAnimation(false);
}
```

#### OnShow()
- **调用时机**：窗口显示时（包括首次显示和重新显示）
- **用途**：刷新数据、重置状态、播放动画
- **注意**：每次显示都会调用，可用于数据刷新

#### OnHide()
- **调用时机**：窗口隐藏时
- **用途**：保存数据、清理临时状态、停止动画

#### OnDestroy()
- **调用时机**：窗口销毁时
- **用途**：释放资源、移除监听器、清理引用

#### OnUpdate()
- **调用时机**：每帧更新（需要手动调用）
- **用途**：实时更新UI数据

---

## 5. 动画系统

### 5.1 默认动画效果

框架提供了参考《雨中冒险 2》风格的默认动画：

#### 显示动画（ShowAnimation）
- **效果**：从黑屏淡入，内容元素依次呈现
- **实现**：
  1. 整个窗口从alpha=0淡入到alpha=1（0.3秒）
  2. 子元素依次淡入，每个延迟0.05秒，形成层次感
  3. 遮罩同步淡入

#### 隐藏动画（HideAnimation）
- **效果**：整个窗口快速淡出到黑屏
- **实现**：
  1. 整个窗口从alpha=1淡出到alpha=0（0.25秒）
  2. 遮罩同步淡出
  3. 动画完成后自动隐藏窗口

### 5.2 自定义动画

子类可以重写动画方法实现自定义效果：

```csharp
public class CustomWindow : WindowBase
{
    // 方式1：完全自定义
    public override void ShowAnimation()
    {
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 0f;
            _canvasGroup.DOFade(1f, 0.5f).SetEase(Ease.OutBounce);
        }
    }

    // 方式2：扩展基类动画
    public override void ShowAnimation()
    {
        base.ShowAnimation();  // 先执行基类动画
        
        // 添加自定义效果
        if (_uiContent != null)
        {
            _uiContent.localScale = Vector3.zero;
            _uiContent.DOScale(Vector3.one, 0.5f).SetEase(Ease.OutBack);
        }
    }
}
```

### 5.3 禁用动画

```csharp
// 在OnAwake中禁用动画
public override void OnAwake()
{
    base.OnAwake();
    SetDisableAnimation(true);  // 禁用该窗口的动画
}

// 或运行时动态控制
SetDisableAnimation(true);   // 禁用
SetDisableAnimation(false);  // 启用
```

### 5.4 动画注意事项

1. **HideAnimation必须调用UIManager.Instance.HideWindow(Name)**
   - 在动画完成后必须调用，否则窗口不会真正隐藏

2. **检查_disableAnimation标志**
   - 如果禁用了动画，应直接返回或隐藏窗口

3. **空值检查**
   - 使用_canvasGroup、_uiContent等前必须判空

---

## 6. 窗口堆栈系统

### 6.1 设计目的

解决复杂窗口层级管理的自动化问题，特别是：
- 窗口A打开窗口B，关闭B后自动返回A
- 多个窗口的层级关系管理
- 窗口间的数据传递

### 6.2 核心方法

#### 压入堆栈
```csharp
// 将窗口压入堆栈
public void PushWindowToStack<T>(
    Action<WindowBase> popCallBack = null,  // 弹出时的回调
    bool isSingle = false,                   // 是否只允许一个实例
    bool pushToStackTop = false              // 是否压入栈顶
) where T : WindowBase, new()
```

#### 弹出堆栈
```csharp
// 弹出栈顶窗口
public void PopTopWindowFromStack()

// 压入并立即弹出
public void PushAndPopWindowInStack<T>(...)
```

### 6.3 使用示例

```csharp
// 场景：主菜单 -> 设置窗口 -> 音效设置窗口

// 1. 主菜单打开设置窗口（压入堆栈）
UIManager.Instance.PushWindowToStack<SettingWindow>(
    popCallBack: (window) => {
        Debug.Log("设置窗口已关闭，返回主菜单");
    }
);

// 2. 设置窗口打开音效设置（压入堆栈）
UIManager.Instance.PushWindowToStack<AudioSettingWindow>();

// 3. 关闭音效设置（自动弹出，返回设置窗口）
UIManager.Instance.PopTopWindowFromStack();

// 4. 关闭设置窗口（自动弹出，返回主菜单，触发回调）
UIManager.Instance.PopTopWindowFromStack();
```

### 6.4 堆栈特性

- **自动管理**：窗口关闭时自动处理堆栈关系
- **链式出栈**：支持连续关闭多个窗口
- **回调支持**：窗口弹出时可执行自定义逻辑
- **单例控制**：可设置窗口是否只允许一个实例

---

## 7. 编辑器工具

### 7.1 窗口脚本生成工具

**功能**：自动生成窗口交互脚本

**使用步骤**：
1. 在Unity编辑器中选中窗口预制体
2. 打开工具窗口（菜单：UIFramework/Generate Window Component）
3. 工具自动分析预制体中的UI控件
4. 根据控件名称生成对应的交互方法
5. 自动生成数据组件绑定代码

**生成内容**：
- 窗口类（继承WindowBase）
- 数据组件类（存储UI控件引用）
- 控件事件方法（OnXXXClick等）

### 7.2 组件绑定脚本生成工具

**功能**：自动生成UI组件绑定代码

**解析方式**：
- **名称解析**：根据控件名称（如Button_Start）生成方法
- **Tag解析**：根据控件Tag生成方法

**生成方式**：
- **组件查找**：使用GetComponent/Find查找组件
- **组件绑定**：使用数据组件存储引用（推荐）

### 7.3 Item脚本生成工具

**功能**：为列表项生成脚本模板

**用途**：快速创建ScrollView、ListView等列表项的脚本

### 7.4 控件分析工具

**功能**：分析预制体中的UI控件结构

**输出**：控件层级关系、组件类型、命名规范检查等

---

## 8. 使用指南

### 8.1 创建新窗口

#### 步骤1：创建窗口预制体
1. 在Unity中创建UI Canvas
2. 设计窗口UI布局
3. 确保根节点有CanvasGroup组件
4. 可选：添加UIMask节点（遮罩）
5. 可选：添加UIContent节点（内容容器）
6. 保存为预制体，放到Resources/Prefab/UI/目录

#### 步骤2：生成窗口脚本
1. 选中窗口预制体
2. 打开窗口生成工具
3. 点击生成按钮
4. 工具自动生成窗口类和数据组件类

#### 步骤3：配置窗口
1. 打开WindowConfig资源
2. 编辑器会自动生成配置项
3. 检查路径和层级设置

#### 步骤4：编写业务逻辑
```csharp
public class MyWindow : WindowBase
{
    public MyWindowDataComponent dataComponent;

    public override void OnAwake()
    {
        base.OnAwake();
        dataComponent = GameObject.GetComponent<MyWindowDataComponent>();
        dataComponent.InitComponent(this);
    }

    public override void OnShow()
    {
        base.OnShow();
        // 刷新数据
        RefreshData();
    }

    public void OnCloseButtonClick()
    {
        HideWindow();
    }

    private void RefreshData()
    {
        // 更新UI显示
    }
}
```

### 8.2 显示窗口

```csharp
// 方式1：泛型方法（推荐）
var window = UIManager.Instance.PopUpWindow<MyWindow>();

// 方式2：从窗口内打开其他窗口
public void OnOpenSettingButtonClick()
{
    ShowWindow<SettingWindow>();
}
```

### 8.3 隐藏窗口

```csharp
// 方式1：从窗口内隐藏自己
HideWindow();

// 方式2：从外部隐藏窗口
UIManager.Instance.HideWindow<MyWindow>();
UIManager.Instance.HideWindow("MyWindow");
```

### 8.4 销毁窗口

```csharp
// 方式1：从窗口内销毁自己
DestroySelf();

// 方式2：从外部销毁窗口
UIManager.Instance.DestroyWindow<MyWindow>();
UIManager.Instance.DestroyWindow("MyWindow");
```

### 8.5 预加载窗口

```csharp
// 提前加载窗口资源，优化后续显示速度
UIManager.Instance.PreLoadWindow<MyWindow>();
```

### 8.6 窗口间通信

```csharp
// 方式1：通过UIManager获取窗口
var window = UIManager.Instance.GetWindow<MyWindow>();
if (window != null)
{
    window.SomeMethod();
}

// 方式2：通过事件系统（推荐）
EventCenter.Instance.EventTrigger("WindowDataChanged", data);
```

### 8.7 自定义动画

```csharp
public class CustomWindow : WindowBase
{
    public override void ShowAnimation()
    {
        // 自定义显示动画
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 0f;
            _canvasGroup.DOFade(1f, 0.5f)
                .SetEase(Ease.OutBounce);
        }
    }

    public override void HideAnimation()
    {
        // 自定义隐藏动画
        if (_canvasGroup != null)
        {
            _canvasGroup.DOFade(0f, 0.3f)
                .SetEase(Ease.InQuad)
                .OnComplete(() => 
                {
                    UIManager.Instance.HideWindow(Name);
                });
        }
        else
        {
            UIManager.Instance.HideWindow(Name);
        }
    }
}
```

---

## 9. API参考

### 9.1 UIManager API

#### 窗口显示
```csharp
// 弹出窗口（泛型）
public T PopUpWindow<T>() where T : WindowBase, new()

// 弹出窗口（实例）
public WindowBase PopUpWindow(WindowBase window)

// 预加载窗口
public void PreLoadWindow<T>() where T : WindowBase, new()
```

#### 窗口隐藏
```csharp
// 隐藏窗口（名称）
public void HideWindow(string windowName)

// 隐藏窗口（类型）
public void HideWindow<T>() where T : WindowBase
```

#### 窗口销毁
```csharp
// 销毁窗口（名称）
public void DestroyWindow(string windowName)

// 销毁窗口（类型）
public void DestroyWindow<T>() where T : WindowBase

// 销毁所有窗口
public void DestroyAllWindow(List<string> filterList = null)
```

#### 窗口查找
```csharp
// 根据名称获取窗口
public WindowBase GetWindow(string windowName)

// 获取可见窗口
public T GetVisibleWindow<T>() where T : WindowBase
```

#### 窗口堆栈
```csharp
// 压入堆栈
public void PushWindowToStack<T>(
    Action<WindowBase> popCallBack = null,
    bool isSingle = false,
    bool pushToStackTop = false
) where T : WindowBase, new()

// 弹出栈顶
public void PopTopWindowFromStack()

// 压入并立即弹出
public void PushAndPopWindowInStack<T>(...)
```

### 9.2 WindowBase API

#### 窗口控制
```csharp
// 隐藏窗口
public void HideWindow()

// 显示其他窗口
public T ShowWindow<T>() where T : WindowBase, new()

// 销毁自己
public void DestroySelf()

// 设置动画开关
public void SetDisableAnimation(bool disable)
```

#### 可见性控制
```csharp
// 设置窗口可见性
public override void SetVisible(bool isVisible)

// 设置遮罩可见性
public void SetMaskVisible(bool isVisible)
```

#### 控件事件
```csharp
// 按钮事件
public void AddButtonClickListener(Button button, UnityAction action)

// 开关事件
public void AddToggleClickListener(Toggle toggle, UnityAction<bool, Toggle> action)

// 输入框事件
public void AddInputFieldListener(InputField inputField, 
    UnityAction<string> onChangeAction, 
    UnityAction<string> onEndEditAction)

// 滑动条事件
public void AddSliderListener(Slider slider, UnityAction<float> action)

// 移除所有监听器
public void RemoveAllListeners()
```

#### 动画方法（可重写）
```csharp
// 显示动画
public virtual void ShowAnimation()

// 隐藏动画
public virtual void HideAnimation()
```

### 9.3 生命周期方法

```csharp
// 初始化（窗口创建时调用）
public override void OnAwake()

// 显示时（每次显示都调用）
public override void OnShow()

// 更新时（需要手动调用）
public override void OnUpdate()

// 隐藏时
public override void OnHide()

// 销毁时
public override void OnDestroy()
```

---

## 10. 设计模式与最佳实践

### 10.1 使用的设计模式

#### 单例模式（Singleton）
- **UIManager**：全局唯一的UI管理器
- **优势**：统一管理、全局访问、资源控制

#### 模板方法模式（Template Method）
- **WindowBehavior**：定义生命周期模板
- **WindowBase**：实现具体功能
- **子类**：重写特定方法

#### 策略模式（Strategy）
- **动画系统**：可重写的ShowAnimation/HideAnimation
- **解析方式**：名称解析/Tag解析可切换

#### 观察者模式（Observer）
- **控件事件**：Button、Toggle等事件监听
- **窗口回调**：PopStackListener回调机制

### 10.2 最佳实践

#### 1. 窗口命名规范
- 窗口类名与预制体名称保持一致
- 窗口类名以"Window"结尾（如：LoginWindow）

#### 2. 预制体结构规范
```
WindowRoot
├── Canvas (Canvas组件，设置sortingOrder)
│   ├── CanvasGroup (控制整体透明度)
│   ├── UIMask (可选，遮罩节点)
│   │   └── CanvasGroup (遮罩透明度)
│   └── UIContent (内容容器)
│       ├── Title (标题)
│       ├── Content (内容区域)
│       └── Buttons (按钮区域)
```

#### 3. 代码组织规范
```csharp
public class MyWindow : WindowBase
{
    #region 数据组件
    public MyWindowDataComponent dataComponent;
    #endregion

    #region 生命周期
    public override void OnAwake() { }
    public override void OnShow() { }
    public override void OnHide() { }
    public override void OnDestroy() { }
    #endregion

    #region 业务逻辑
    private void RefreshData() { }
    #endregion

    #region UI事件
    public void OnButtonClick() { }
    #endregion
}
```

#### 4. 资源管理
- 窗口预制体放在Resources/Prefab/UI/目录
- 使用预加载优化首次显示速度
- 及时销毁不用的窗口释放内存

#### 5. 性能优化建议
- 使用数据组件缓存UI引用，避免频繁查找
- 合理使用预加载，平衡内存和加载速度
- 隐藏窗口时及时清理监听器
- 避免在OnUpdate中执行耗时操作

#### 6. 错误处理
- 检查组件引用是否为空
- 验证窗口配置是否正确
- 使用Debug.Log记录关键操作

### 10.3 常见问题与解决方案

#### Q1: 窗口显示时组件引用为空？
**A**: 确保在OnAwake中获取组件，而不是在构造函数中。

#### Q2: 动画不执行？
**A**: 检查_disableAnimation标志，确保Canvas和CanvasGroup存在。

#### Q3: 窗口关闭后无法再次打开？
**A**: 检查是否正确调用了UIManager.Instance.HideWindow()，而不是直接SetActive(false)。

#### Q4: 多个窗口叠加时遮罩问题？
**A**: 使用UISetting中的SINGMASK_SYSTEM，启用单遮罩模式。

#### Q5: 窗口切换卡顿？
**A**: 使用预加载机制，提前加载窗口资源。

---

## 11. 总结

本UI框架通过**解耦设计**、**配置驱动**、**生命周期管理**等核心特性，为Unity游戏开发提供了一个**高效、易用、可扩展**的窗口管理系统。

### 核心优势

1. **解耦设计**：窗口逻辑与Unity MonoBehaviour解耦，避免执行顺序问题
2. **完整生命周期**：从创建到销毁的全流程管理
3. **流畅动画**：内置动画系统，支持自定义扩展
4. **智能堆栈**：自动管理窗口层级关系
5. **编辑器支持**：自动生成代码，提升开发效率
6. **性能优化**：预加载、缓存等优化机制

### 适用场景

- 游戏主菜单系统
- 设置界面管理
- 背包/商店界面
- 对话框系统
- 任何需要窗口管理的场景

### 未来扩展方向

- 窗口过渡效果库
- UI数据绑定系统
- 多语言支持
- UI性能分析工具
- 可视化编辑器

---

**文档版本**：v1.0  
**最后更新**：2024年  
**维护者**：Framework Team

