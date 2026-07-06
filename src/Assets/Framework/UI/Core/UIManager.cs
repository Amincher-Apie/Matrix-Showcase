using System;
using System.Collections.Generic;
using Framework.Singleton;
using Framework.UI.Base;
using UnityEngine;
using UnityEngine.UIElements;

namespace Framework.UI.Core
{
    /// <summary>
    /// UI管理器 - 负责窗口的加载、显示、隐藏、销毁等生命周期管理
    /// 采用单例模式，确保全局唯一实例
    /// </summary>
    public class UIManager : SingletonBase<UIManager>
    {
        #region 核心属性与字段
        
        /// <summary>场景中专门负责渲染UI的摄像机</summary>
        public Camera UICamera { get; private set; }
    
        /// <summary>所有UI的根节点（场景中名为"UIRoot"的GameObject）</summary>
        private Transform _uiRoot;
    
        /// <summary>窗口配置表 - 存储窗口预制体路径等信息</summary>
        private WindowConfig _windowConfig;
    
        /// <summary>已创建窗口字典 - 键：窗口名称，值：窗口实例（包括隐藏状态的窗口）</summary>
        private readonly Dictionary<string, WindowBase> _windowDic = new Dictionary<string, WindowBase>();
    
        /// <summary>已创建窗口列表 - 存储所有已实例化的窗口，方便批量操作</summary>
        private readonly List<WindowBase> _windowList = new List<WindowBase>();
    
        /// <summary>可见窗口列表 - 仅存储当前处于显示状态的窗口</summary>
        private readonly List<WindowBase> _visibleWindowList = new List<WindowBase>();
        
        /// <summary>
        /// 模拟窗口的栈 解决复杂窗口层级管理的自动化问题
        /// </summary>
        private readonly List<WindowBase> _windowStack = new List<WindowBase>();
        
        /// <summary>
        /// 开始弹出堆栈的标记，可以用来处理多种情况，比如：正在出栈种有其他界面弹出，可以直接放到栈内进行弹出 等
        /// </summary>
        private bool _startPopStackWndStatus = false;
        
        #endregion

        #region 生命周期函数
        /// <summary>
        /// 初始化UI管理器（单例初始化时调用）
        /// 负责获取UI相机、根节点和窗口配置
        /// </summary>
        protected override void Initialize()
        {
            base.Initialize();
        
            // 查找场景中的UI相机和根节点
            UICamera = GameObject.Find("UICamera")?.GetComponent<Camera>();
            _uiRoot = GameObject.Find("UIRoot")?.transform;

            // 验证初始化环境
            if (UICamera == null)
                Debug.LogError("初始化失败：未找到名为UICamera的相机");
            if (_uiRoot == null)
                Debug.LogError("初始化失败：未找到名为UIRoot的根节点");

            // 加载窗口配置表
            _windowConfig = Resources.Load<WindowConfig>("WindowConfig");
            if (_windowConfig == null)
            {
                Debug.LogError("初始化失败：请在Resources文件夹下创建WindowConfig资源");
                return;
            }

            // 编辑器模式下自动生成窗口配置
#if UNITY_EDITOR
            _windowConfig.GeneratorWindowConfig();
#endif
        }

        /// <summary>
        /// 释放资源（程序退出或切换场景时调用）
        /// 销毁所有窗口并清空集合
        /// </summary>
        public override void Release()
        {
            base.Release();
            DestroyAllWindow();
            _windowDic.Clear();
            _windowList.Clear();
            _visibleWindowList.Clear();
            _windowStack.Clear();
        }
        #endregion

        #region 窗口预加载
        /// <summary>
        /// 预加载窗口（提前实例化窗口但不显示，优化后续显示速度）
        /// </summary>
        /// <typeparam name="T">窗口类型（继承自WindowBase）</typeparam>
        public void PreLoadWindow<T>() where T : WindowBase, new()
        {
            string windowName = typeof(T).Name;

            // 避免重复预加载
            if (_windowDic.ContainsKey(windowName))
            {
                Debug.LogWarning($"窗口{windowName}已预加载，无需重复操作");
                return;
            }

            // 创建窗口实例并加载资源
            T window = new T();
            GameObject windowObj = LoadWindow(windowName);
            if (windowObj == null)
            {
                Debug.LogError($"预加载窗口失败：{windowName}");
                return;
            }

            // 初始化窗口属性
            InitializeWindowProperties(window, windowObj);
            window.SetVisible(false); // 预加载状态下隐藏窗口
            window.OnAwake(); // 调用窗口初始化逻辑

            // 添加到管理集合
            _windowDic.Add(windowName, window);
            _windowList.Add(window);

            Debug.Log($"预加载窗口完成：{windowName}");
        }
        #endregion

        #region 窗口显示与初始化
        /// <summary>
        /// 显示窗口（若未实例化则先初始化，已实例化则直接显示）
        /// </summary>
        /// <typeparam name="T">窗口类型（继承自WindowBase）</typeparam>
        /// <returns>窗口实例</returns>
        public T PopUpWindow<T>() where T : WindowBase, new()
        {
            string windowName = typeof(T).Name;
            WindowBase existingWindow = GetWindow(windowName);

            // 窗口已存在则直接显示
            if (existingWindow != null)
            {
                ShowWindow(existingWindow);
                return existingWindow as T;
            }

            // 窗口不存在则初始化并显示
            T newWindow = new T();
            return InitializeAndShowWindow(newWindow, windowName) as T;
        }
        
        /// <summary>
        /// 显示指定窗口实例
        /// </summary>
        public WindowBase PopUpWindow(WindowBase window)
        {
            string wndName = window.GetType().Name;
            WindowBase wnd = GetWindow(wndName);
            if (wnd != null)
            {
                return ShowWindow(wndName);
            }
            return InitializeAndShowWindow(window, wndName);
        }

        /// <summary>
        /// 初始化并显示窗口（加载资源、绑定属性、调用生命周期）
        /// </summary>
        /// <param name="window">窗口实例</param>
        /// <param name="windowName">窗口名称</param>
        /// <returns>初始化完成的窗口实例</returns>
        private WindowBase InitializeAndShowWindow(WindowBase window, string windowName)
        {
            // 加载窗口预制体
            GameObject windowObj = LoadWindow(windowName);
            if (windowObj == null)
            {
                Debug.LogError($"初始化窗口失败：未找到{windowName}的预制体");
                return null;
            }

            // 初始化窗口属性
            InitializeWindowProperties(window, windowObj);
        
            // 调整层级（确保显示在最上层）
            window.Transform.SetAsLastSibling();
        
            // 调用窗口生命周期
            window.OnAwake();   // 初始化
            window.SetVisible(true); // 显示窗口
            window.OnShow();    // 显示时逻辑

            // 管理窗口集合（防止重复添加）
            RemoveExistingWindow(windowName);
            _windowDic.Add(windowName, window);
            _windowList.Add(window);
            _visibleWindowList.Add(window);

            // 更新遮罩显示状态
            SetWindowMaskVisible();

            Debug.Log($"初始化并显示窗口：{windowName}");
            return window;
        }

        /// <summary>
        /// 初始化窗口基础属性（绑定GameObject、Transform等）
        /// </summary>
        private void InitializeWindowProperties(WindowBase window, GameObject windowObj)
        {
            window.GameObject = windowObj;
            window.Transform = windowObj.transform;
            window.Canvas = windowObj.GetComponent<Canvas>();
            window.Name = windowObj.name;

            // 绑定UI相机（确保UI渲染正确）
            if (window.Canvas != null)
            {
                window.Canvas.worldCamera = UICamera;
                
                // 从配置中读取并设置 Canvas 的 sortingOrder
                var windowData = _windowConfig?.GetWindowData(window.Name);
                if (windowData != null)
                {
                    window.Canvas.sortingOrder = windowData.sortingOrder;
                }
            }

            // 适配根节点（全屏窗口自动铺满）
            var rectTrans = windowObj.GetComponent<RectTransform>();
            if (rectTrans != null)
            {
                rectTrans.anchorMax = Vector2.one;
                rectTrans.offsetMax = Vector2.zero;
                rectTrans.offsetMin = Vector2.zero;
            }
        }

        /// <summary>
        /// 显示已存在的窗口
        /// </summary>
        private void ShowWindow(WindowBase window)
        {
            if (window == null || window.Visible)
                return;

            // 更新显示状态
            window.Transform.SetAsLastSibling(); // 置于顶层
            window.SetVisible(true);
            window.OnShow();

            // 添加到可见列表
            if (!_visibleWindowList.Contains(window))
                _visibleWindowList.Add(window);

            // 更新遮罩
            SetWindowMaskVisible();

            Debug.Log($"显示窗口：{window.Name}");
        }
        
        /// <summary>
        /// 根据窗口名称显示窗口
        /// </summary>
        private WindowBase ShowWindow(string winName)
        {
            if (!_windowDic.ContainsKey(winName))
            {
                Debug.LogError($"{winName} 窗口不存在，请调用PopUpWindow进行弹出");
                return null;
            }
            
            WindowBase window = _windowDic[winName];
            if (window.GameObject != null && !window.Visible)
            {
                _visibleWindowList.Add(window);
                window.Transform.SetAsLastSibling();
                window.SetVisible(true);
                SetWindowMaskVisible();
                window.OnShow();
                return window;
            }
            
            // 窗口若已经弹出，调用OnShow生命周期接口刷新界面数据
            if (window.GameObject != null && window.Visible)
            {
                window.OnShow();
            }
            
            return window;
        }
        #endregion

        #region 窗口隐藏
        /// <summary>
        /// 根据窗口名称隐藏窗口
        /// </summary>
        public void HideWindow(string windowName)
        {
            WindowBase window = GetWindow(windowName);
            if (window != null)
            {
                HideWindowInternal(window);
            }
        }

        /// <summary>
        /// 根据窗口类型隐藏窗口
        /// </summary>
        public void HideWindow<T>() where T : WindowBase, new()
        {
            HideWindow(typeof(T).Name);
        }

        /// <summary>
        /// 隐藏指定窗口实例
        /// </summary>
        private void HideWindowInternal(WindowBase window)
        {
            if (window == null || !window.Visible)
                return;

            // 更新状态
            window.SetVisible(false);
            window.OnHide();

            // 从可见列表移除
            _visibleWindowList.Remove(window);

            // 更新遮罩
            SetWindowMaskVisible();
            
            // 处理堆栈窗口
            PopNextWindowFromStack(window);
        }
        #endregion

        #region 窗口销毁
        /// <summary>
        /// 根据窗口名称销毁窗口
        /// </summary>
        public void DestroyWindow(string windowName)
        {
            WindowBase window = GetWindow(windowName);
            if (window != null)
            {
                DestroyWindowInternal(window);
            }
        }

        /// <summary>
        /// 根据窗口类型销毁窗口
        /// </summary>
        public void DestroyWindow<T>() where T : WindowBase, new()
        {
            DestroyWindow(typeof(T).Name);
        }

        /// <summary>
        /// 销毁指定窗口实例
        /// </summary>
        private void DestroyWindowInternal(WindowBase window)
        {
            if (window == null)
                return;

            string windowName = window.Name;

            // 调用生命周期
            window.SetVisible(false);
            window.OnHide();
            window.OnDestroy();

            // 从管理集合移除
            _windowDic.Remove(windowName);
            _windowList.Remove(window);
            _visibleWindowList.Remove(window);

            // 销毁GameObject
            GameObjectDestroyWindow(window.GameObject);

            // 更新遮罩
            SetWindowMaskVisible();
            
            // 处理堆栈窗口
            PopNextWindowFromStack(window);

            Debug.Log($"销毁窗口：{windowName}");
        }

        /// <summary>
        /// 销毁所有窗口（可指定过滤列表不销毁某些窗口）
        /// </summary>
        /// <param name="filterList">不销毁的窗口名称列表</param>
        public void DestroyAllWindow(List<string> filterList = null)
        {
            // 从后往前遍历，避免删除元素时索引错乱
            for (int i = _windowList.Count - 1; i >= 0; i--)
            {
                WindowBase window = _windowList[i];
                if (window == null) continue;

                // 过滤不需要销毁的窗口
                if (filterList != null && filterList.Contains(window.Name))
                    continue;

                DestroyWindowInternal(window);
            }

            // 卸载未使用资源（优化内存）
            Resources.UnloadUnusedAssets();
        }

        /// <summary>
        /// 销毁窗口GameObject（实际删除场景中的物体）
        /// </summary>
        private void GameObjectDestroyWindow(GameObject windowObj)
        {
            if (windowObj != null)
            {
                GameObject.Destroy(windowObj);
            }
        }

        /// <summary>
        /// 移除已存在的窗口（用于重复初始化时的清理）
        /// </summary>
        private void RemoveExistingWindow(string windowName)
        {
            if (_windowDic.TryGetValue(windowName, out WindowBase existingWindow))
            {
                _windowDic.Remove(windowName);
                _windowList.Remove(existingWindow);
                _visibleWindowList.Remove(existingWindow);
                GameObjectDestroyWindow(existingWindow.GameObject);
            }
        }
        #endregion

        #region 窗口查找
        /// <summary>
        /// 根据窗口名称获取窗口实例
        /// </summary>
        public WindowBase GetWindow(string windowName)
        {
            _windowDic.TryGetValue(windowName, out WindowBase window);
            return window;
        }

        /// <summary>
        /// 根据窗口类型获取可见的窗口实例
        /// </summary>
        public T GetVisibleWindow<T>() where T : WindowBase
        {
            string windowName = typeof(T).Name;
            foreach (var window in _visibleWindowList)
            {
                if (window.Name == windowName)
                {
                    return (T)window;
                }
            }
            
            Debug.LogWarning($"未找到可见窗口：{windowName}");
            return null;
        }
        #endregion

        #region 遮罩管理
        /// <summary>
        /// 控制窗口遮罩显示（确保最上层窗口显示遮罩，避免多层遮罩叠加）
        /// </summary>
        private void SetWindowMaskVisible()
        {
            // 检查配置是否启用遮罩系统
            if (UISetting.Instance == null || !UISetting.Instance.SINGMASK_SYSTEM)
                return;

            // 找到最上层的窗口（渲染层级最高且显示在最后）
            WindowBase topWindow = FindTopVisibleWindow();

            // 所有窗口先隐藏遮罩，再让最上层窗口显示遮罩
            foreach (var window in _visibleWindowList)
            {
                window.SetMaskVisible(window == topWindow);
            }
        }

        /// <summary>
        /// 查找当前可见窗口中最上层的窗口
        /// 优先比较Canvas层级，层级相同则比较兄弟节点索引
        /// </summary>
        private WindowBase FindTopVisibleWindow()
        {
            if (_visibleWindowList.Count == 0)
                return null;

            WindowBase topWindow = _visibleWindowList[0];
            int maxSortingOrder = topWindow.Canvas.sortingOrder;
            int maxSiblingIndex = topWindow.Transform.GetSiblingIndex();

            foreach (var window in _visibleWindowList)
            {
                // 跳过无效窗口
                if (window == null || window.GameObject == null)
                    continue;

                int currentOrder = window.Canvas.sortingOrder;
                int currentIndex = window.Transform.GetSiblingIndex();

                // 比较层级：排序层级高的在前
                if (currentOrder > maxSortingOrder)
                {
                    topWindow = window;
                    maxSortingOrder = currentOrder;
                    maxSiblingIndex = currentIndex;
                }
                // 排序层级相同则比较兄弟节点索引（值大的显示在上面）
                else if (currentOrder == maxSortingOrder && currentIndex > maxSiblingIndex)
                {
                    topWindow = window;
                    maxSiblingIndex = currentIndex;
                }
            }

            return topWindow;
        }
        #endregion

        #region 窗口堆栈系统

        /// <summary>
        /// 弹出栈顶的窗口
        /// </summary>
        public void PopTopWindowFromStack()
        {
            /*
             * 当调用 StartPopFirstStackWindow() 启动出栈流程时，_startPopStackWndStatus 会被设为 true。
             * 若在出栈过程中（如窗口动画未完成时）再次调用出栈方法（如用户快速点击返回按钮），该标记会直接阻止重复执行，避免堆栈顺序错乱。
             */
            
            if (_startPopStackWndStatus)
                return;
            
            _startPopStackWndStatus = true;
            PopStackWindow();
        }

        /// <summary>
        /// 将窗口压入堆栈并管理其状态
        /// </summary>
        /// <param name="popCallBack">窗口弹出时的回调</param>
        /// <param name="isSingle">是否只允许存在一个实例 如果只允许存在一个实例 遇到重复入栈的window直接return</param>
        /// <param name="pushToStackTop">是否压入栈顶</param>
        /// <typeparam name="T">压入窗体的类型</typeparam>
        public void PushWindowToStack<T>(Action<WindowBase> popCallBack = null, bool isSingle = false, bool pushToStackTop = false) where T : WindowBase, new()
        {
            string windowName = typeof(T).Name;
            
            // 检查是否只允许单一实例
            if (isSingle)
            {
                foreach (var item in _windowStack)
                {
                    if(item.Name == windowName)
                        return;
                }
            }
            
            // 当已显示窗口列表中有该window时 不进行压栈操作 只进行刷新已显示窗口的操作
            // 避免同一窗口重复入栈
            WindowBase window = GetWindow(windowName);
            if (window != null)
            {
                window.OnShow();
                return;
            }
            
            T newWindow = new T { PopStackListener = popCallBack, Name = windowName };

            if (pushToStackTop)
            {
                _windowStack.Insert(0, newWindow);
            }
            else
            {
                _windowStack.Add(newWindow);
            }
        }

        /// <summary>
        /// 弹出堆栈窗口
        /// </summary>
        private bool PopStackWindow()
        {
            if (_windowStack.Count == 0)
            {
                _startPopStackWndStatus = false;
                return false;
            }

            WindowBase window = _windowStack[0]; // 获得栈顶的窗口
            _windowStack.RemoveAt(0); // 从栈顶弹出并移除
            
            WindowBase popUpWindow = PopUpWindow(window); // 弹出窗口并获得具体的窗口类型
            popUpWindow.PopStackListener = window.PopStackListener;
            
            // 标记该窗口是通过堆栈系统弹出的 用于后续链式出栈逻辑
            popUpWindow.IsPopStack = true;
            
            // 传递原窗口的回调函数 PopStackListener 并执行 完成弹出后的自定义逻辑（如数据传递）
            popUpWindow.PopStackListener?.Invoke(popUpWindow);
            popUpWindow.PopStackListener = null;
            
            return true;
        }
        
        /// <summary>
        /// 压入并立即弹出窗口
        /// </summary>
        public void PushAndPopWindowInStack<T>(Action<WindowBase> popCallBack = null, bool isSingle = false, bool pushToStackTop = false) where T : WindowBase, new()
        {
            PushWindowToStack<T>(popCallBack, isSingle, pushToStackTop);
            PopTopWindowFromStack();
        }

        /// <summary>
        /// 从堆栈中弹出下一个窗口
        /// </summary>
        private void PopNextWindowFromStack(WindowBase window)
        {
            if (window != null && _startPopStackWndStatus && window.IsPopStack)
            {
                window.IsPopStack = false;
                PopStackWindow();
            }
        }

        #endregion

        #region 资源加载
        /// <summary>
        /// 加载窗口预制体（从Resources加载）
        /// </summary>
        /// <param name="windowName">窗口名称</param>
        /// <returns>实例化后的窗口GameObject</returns>
        private GameObject LoadWindow(string windowName)
        {
            // 从配置表获取路径
            var windowData = _windowConfig?.GetWindowData(windowName);
            if (windowData == null)
            {
                Debug.LogError($"窗口配置不存在：{windowName}");
                return null;
            }

            // 加载预制体
            GameObject prefab = Resources.Load<GameObject>(windowData.path);
            if (prefab == null)
            {
                Debug.LogError($"预制体未找到：路径={windowData.path}，窗口名={windowName}");
                return null;
            }

            // 实例化到UI根节点
            GameObject windowObj = GameObject.Instantiate(prefab, _uiRoot);
            windowObj.name = windowName; // 确保名称正确（与类名一致）
            windowObj.transform.localScale = Vector3.one;
            windowObj.transform.localPosition = Vector3.zero;
            windowObj.transform.localRotation = Quaternion.identity;

            return windowObj;
        }
        #endregion
    }
}