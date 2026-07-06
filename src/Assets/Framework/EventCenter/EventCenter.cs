using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Framework.Singleton;

public class EventCenter : SingletonBase<EventCenter>
{
    // 事件表：枚举作为键，值=带优先级的监听者列表
    private readonly Dictionary<EventName, List<EventListenerBase>> _eventTable = new Dictionary<EventName, List<EventListenerBase>>();
    // 锁对象，保证线程安全
    private readonly object _eventLock = new object();

    #region 单例基类生命周期函数
    /// <summary>
    /// 初始化方法（单例实例创建时自动调用）
    /// </summary>
    protected override void Initialize()
    {
        base.Initialize();
        Debug.Log($"EventCenter 初始化完成，with HashCode{GetHashCode()}");
    }

    /// <summary>
    /// 启动方法（需手动调用）
    /// </summary>
    public override void Start()
    {
        base.Start();
    }

    /// <summary>
    /// 帧更新方法（需手动在Update中调用）
    /// </summary>
    public override void OnUpdate()
    {
        base.OnUpdate();
    }

    /// <summary>
    /// 资源释放方法（需手动调用）
    /// </summary>
    public override void Release()
    {
        ClearAllEvents(); // 释放时清空所有事件
        base.Release(); // 调用父类方法，将单例实例置空
    }
    #endregion

    #region 事件中心公开方法
    /// <summary>
    /// 清空所有事件（场景切换时调用，避免跨场景内存泄漏）
    /// </summary>
    public void ClearAllEvents()
    {
        lock (_eventLock)
        {
            _eventTable.Clear();
            Debug.Log("事件中心：所有事件已清空");
        }
    }
    #endregion

    #region 无参数事件
    public void AddListener(EventName eventName, UnityAction action, int priority = 0)
    {
        if (action == null)
        {
            Debug.LogError($"事件{eventName}：添加的委托不能为空！");
            return;
        }

        lock (_eventLock)
        {
            if (!_eventTable.TryGetValue(eventName, out var listenerList))
            {
                listenerList = new List<EventListenerBase>();
                _eventTable.Add(eventName, listenerList);
            }

            // 检测是否已存在该委托，避免重复添加
            foreach (var listener in listenerList)
            {
                if (listener is EventListener existingListener && 
                    Delegate.Equals(existingListener.Action, action))
                {
                    Debug.LogWarning($"事件{eventName}：委托{action.Method.Name}已存在，无需重复添加");
                    return;
                }
            }

            // 按优先级降序插入
            var newListener = new EventListener(action, priority);
            int insertIndex = listenerList.FindIndex(l => l.Priority < priority);
            if (insertIndex == -1)
                listenerList.Add(newListener);
            else
                listenerList.Insert(insertIndex, newListener);
        }
    }

    public void RemoveListener(EventName eventName, UnityAction action)
    {
        if (action == null) return;

        lock (_eventLock)
        {
            if (_eventTable.TryGetValue(eventName, out var listenerList))
            {
                // 找到并移除对应委托
                var targetListener = listenerList.Find(l => 
                    l is EventListener el && Delegate.Equals(el.Action, action));
                
                if (targetListener != null)
                {
                    listenerList.Remove(targetListener);
                    
                    // 如果列表为空，移除事件键
                    if (listenerList.Count == 0)
                        _eventTable.Remove(eventName);
                }
            }
        }
    }

    public void Trigger(EventName eventName)
    {
        lock (_eventLock)
        {
            if (!_eventTable.TryGetValue(eventName, out var listenerList))
                return;

            // 临时复制列表，避免执行中修改列表导致异常
            List<EventListenerBase> tempList = new List<EventListenerBase>(listenerList);
            
            foreach (var listener in tempList)
            {
                if (listener is EventListener eventListener)
                    eventListener.Invoke();
                else
                    Debug.LogError($"事件{eventName}：监听者类型不匹配，需要无参数监听者");
            }
        }
    }
    #endregion

    #region 单参数事件
    public void AddListener<T>(EventName eventName, UnityAction<T> action, int priority = 0)
    {
        if (action == null)
        {
            Debug.LogError($"事件{eventName}：添加的委托不能为空！");
            return;
        }

        lock (_eventLock)
        {
            if (!_eventTable.TryGetValue(eventName, out var listenerList))
            {
                listenerList = new List<EventListenerBase>();
                _eventTable.Add(eventName, listenerList);
            }

            // 检测是否已存在该委托，避免重复添加
            foreach (var listener in listenerList)
            {
                if (listener is EventListener<T> existingListener && 
                    Delegate.Equals(existingListener.Action, action))
                {
                    Debug.LogWarning($"事件{eventName}：委托{action.Method.Name}已存在，无需重复添加");
                    return;
                }
            }

            // 按优先级降序插入
            var newListener = new EventListener<T>(action, priority);
            int insertIndex = listenerList.FindIndex(l => l.Priority < priority);
            if (insertIndex == -1)
                listenerList.Add(newListener);
            else
                listenerList.Insert(insertIndex, newListener);
        }
    }

    public void RemoveListener<T>(EventName eventName, UnityAction<T> action)
    {
        if (action == null) return;

        lock (_eventLock)
        {
            if (_eventTable.TryGetValue(eventName, out var listenerList))
            {
                // 找到并移除对应委托
                var targetListener = listenerList.Find(l => 
                    l is EventListener<T> el && Delegate.Equals(el.Action, action));
                
                if (targetListener != null)
                {
                    listenerList.Remove(targetListener);
                    
                    // 如果列表为空，移除事件键
                    if (listenerList.Count == 0)
                        _eventTable.Remove(eventName);
                }
            }
        }
    }

    public void Trigger<T>(EventName eventName, T arg)
    {
        lock (_eventLock)
        {
            if (!_eventTable.TryGetValue(eventName, out var listenerList))
                return;

            // 临时复制列表，避免执行中修改列表导致异常
            List<EventListenerBase> tempList = new List<EventListenerBase>(listenerList);
            
            foreach (var listener in tempList)
            {
                if (listener is EventListener<T> eventListener)
                {
                    eventListener.SetArg(arg);
                    eventListener.Invoke();
                }
                else
                {
                    Debug.LogError($"事件{eventName}：监听者类型不匹配，需要UnityAction<{typeof(T).Name}>");
                }
            }
        }
    }
    #endregion

    #region 双参数事件
    public void AddListener<T1, T2>(EventName eventName, UnityAction<T1, T2> action, int priority = 0)
    {
        if (action == null)
        {
            Debug.LogError($"事件{eventName}：添加的委托不能为空！");
            return;
        }

        lock (_eventLock)
        {
            if (!_eventTable.TryGetValue(eventName, out var listenerList))
            {
                listenerList = new List<EventListenerBase>();
                _eventTable.Add(eventName, listenerList);
            }

            // 检测是否已存在该委托，避免重复添加
            foreach (var listener in listenerList)
            {
                if (listener is EventListener<T1, T2> existingListener && 
                    Delegate.Equals(existingListener.Action, action))
                {
                    Debug.LogWarning($"事件{eventName}：委托{action.Method.Name}已存在，无需重复添加");
                    return;
                }
            }

            // 按优先级降序插入
            var newListener = new EventListener<T1, T2>(action, priority);
            int insertIndex = listenerList.FindIndex(l => l.Priority < priority);
            if (insertIndex == -1)
                listenerList.Add(newListener);
            else
                listenerList.Insert(insertIndex, newListener);
        }
    }

    public void RemoveListener<T1, T2>(EventName eventName, UnityAction<T1, T2> action)
    {
        if (action == null) return;

        lock (_eventLock)
        {
            if (_eventTable.TryGetValue(eventName, out var listenerList))
            {
                // 找到并移除对应委托
                var targetListener = listenerList.Find(l => 
                    l is EventListener<T1, T2> el && Delegate.Equals(el.Action, action));
                
                if (targetListener != null)
                {
                    listenerList.Remove(targetListener);
                    
                    // 如果列表为空，移除事件键
                    if (listenerList.Count == 0)
                        _eventTable.Remove(eventName);
                }
            }
        }
    }

    public void Trigger<T1, T2>(EventName eventName, T1 arg1, T2 arg2)
    {
        lock (_eventLock)
        {
            if (!_eventTable.TryGetValue(eventName, out var listenerList))
                return;

            // 临时复制列表，避免执行中修改列表导致异常
            List<EventListenerBase> tempList = new List<EventListenerBase>(listenerList);
            
            foreach (var listener in tempList)
            {
                if (listener is EventListener<T1, T2> eventListener)
                {
                    eventListener.SetArg(arg1, arg2);
                    eventListener.Invoke();
                }
                else
                {
                    Debug.LogError($"事件{eventName}：监听者类型不匹配，需要UnityAction<{typeof(T1).Name},{typeof(T2).Name}>");
                }
            }
        }
    }
    #endregion

    #region 三参数事件
    public void AddListener<T1, T2, T3>(EventName eventName, UnityAction<T1, T2, T3> action, int priority = 0)
    {
        if (action == null)
        {
            Debug.LogError($"事件{eventName}：添加的委托不能为空！");
            return;
        }

        lock (_eventLock)
        {
            if (!_eventTable.TryGetValue(eventName, out var listenerList))
            {
                listenerList = new List<EventListenerBase>();
                _eventTable.Add(eventName, listenerList);
            }

            // 检测是否已存在该委托，避免重复添加
            foreach (var listener in listenerList)
            {
                if (listener is EventListener<T1, T2, T3> existingListener && 
                    Delegate.Equals(existingListener.Action, action))
                {
                    Debug.LogWarning($"事件{eventName}：委托{action.Method.Name}已存在，无需重复添加");
                    return;
                }
            }

            // 按优先级降序插入
            var newListener = new EventListener<T1, T2, T3>(action, priority);
            int insertIndex = listenerList.FindIndex(l => l.Priority < priority);
            if (insertIndex == -1)
                listenerList.Add(newListener);
            else
                listenerList.Insert(insertIndex, newListener);
        }
    }

    public void RemoveListener<T1, T2, T3>(EventName eventName, UnityAction<T1, T2, T3> action)
    {
        if (action == null) return;

        lock (_eventLock)
        {
            if (_eventTable.TryGetValue(eventName, out var listenerList))
            {
                // 找到并移除对应委托
                var targetListener = listenerList.Find(l => 
                    l is EventListener<T1, T2, T3> el && Delegate.Equals(el.Action, action));
                
                if (targetListener != null)
                {
                    listenerList.Remove(targetListener);
                    
                    // 如果列表为空，移除事件键
                    if (listenerList.Count == 0)
                        _eventTable.Remove(eventName);
                }
            }
        }
    }

    public void Trigger<T1, T2, T3>(EventName eventName, T1 arg1, T2 arg2, T3 arg3)
    {
        lock (_eventLock)
        {
            if (!_eventTable.TryGetValue(eventName, out var listenerList))
                return;

            // 临时复制列表，避免执行中修改列表导致异常
            List<EventListenerBase> tempList = new List<EventListenerBase>(listenerList);
            
            foreach (var listener in tempList)
            {
                if (listener is EventListener<T1, T2, T3> eventListener)
                {
                    eventListener.SetArg(arg1, arg2, arg3);
                    eventListener.Invoke();
                }
                else
                {
                    Debug.LogError($"事件{eventName}：监听者类型不匹配，需要UnityAction<{typeof(T1).Name},{typeof(T2).Name},{typeof(T3).Name}>");
                }
            }
        }
    }
    #endregion

    #region 四参数事件
    public void AddListener<T1, T2, T3, T4>(EventName eventName, UnityAction<T1, T2, T3, T4> action, int priority = 0)
    {
        if (action == null)
        {
            Debug.LogError($"事件{eventName}：添加的委托不能为空！");
            return;
        }

        lock (_eventLock)
        {
            if (!_eventTable.TryGetValue(eventName, out var listenerList))
            {
                listenerList = new List<EventListenerBase>();
                _eventTable.Add(eventName, listenerList);
            }

            // 检测是否已存在该委托，避免重复添加
            foreach (var listener in listenerList)
            {
                if (listener is EventListener<T1, T2, T3, T4> existingListener && 
                    Delegate.Equals(existingListener.Action, action))
                {
                    Debug.LogWarning($"事件{eventName}：委托{action.Method.Name}已存在，无需重复添加");
                    return;
                }
            }

            // 按优先级降序插入
            var newListener = new EventListener<T1, T2, T3, T4>(action, priority);
            int insertIndex = listenerList.FindIndex(l => l.Priority < priority);
            if (insertIndex == -1)
                listenerList.Add(newListener);
            else
                listenerList.Insert(insertIndex, newListener);
        }
    }

    public void RemoveListener<T1, T2, T3, T4>(EventName eventName, UnityAction<T1, T2, T3, T4> action)
    {
        if (action == null) return;

        lock (_eventLock)
        {
            if (_eventTable.TryGetValue(eventName, out var listenerList))
            {
                // 找到并移除对应委托
                var targetListener = listenerList.Find(l => 
                    l is EventListener<T1, T2, T3, T4> el && Delegate.Equals(el.Action, action));
                
                if (targetListener != null)
                {
                    listenerList.Remove(targetListener);
                    
                    // 如果列表为空，移除事件键
                    if (listenerList.Count == 0)
                        _eventTable.Remove(eventName);
                }
            }
        }
    }

    public void Trigger<T1, T2, T3, T4>(EventName eventName, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
    {
        lock (_eventLock)
        {
            if (!_eventTable.TryGetValue(eventName, out var listenerList))
                return;

            // 临时复制列表，避免执行中修改列表导致异常
            List<EventListenerBase> tempList = new List<EventListenerBase>(listenerList);
            
            foreach (var listener in tempList)
            {
                if (listener is EventListener<T1, T2, T3, T4> eventListener)
                {
                    eventListener.SetArg(arg1, arg2, arg3, arg4);
                    eventListener.Invoke();
                }
                else
                {
                    Debug.LogError($"事件{eventName}：监听者类型不匹配，需要UnityAction<{typeof(T1).Name},{typeof(T2).Name},{typeof(T3).Name},{typeof(T4).Name}>");
                }
            }
        }
    }
    #endregion

    #region 监听者模型（内部类）
    // 以下为内部实现，外部调用无需关心
    private abstract class EventListenerBase
    {
        public int Priority { get; protected set; }
        public abstract void Invoke();
    }

    // 无参数监听者
    private class EventListener : EventListenerBase
    {
        internal readonly UnityAction Action;
        
        public EventListener(UnityAction action, int priority)
        {
            Action = action;
            Priority = priority;
        }
        
        public override void Invoke() => Action?.Invoke();
    }

    // 单参数监听者
    private class EventListener<T> : EventListenerBase
    {
        internal readonly UnityAction<T> Action;
        private T _arg;
        
        public EventListener(UnityAction<T> action, int priority)
        {
            Action = action;
            Priority = priority;
        }
        
        public void SetArg(T arg) => _arg = arg;
        public override void Invoke() => Action?.Invoke(_arg);
    }

    // 双参数监听者
    private class EventListener<T1, T2> : EventListenerBase
    {
        internal readonly UnityAction<T1, T2> Action;
        private T1 _arg1;
        private T2 _arg2;
        
        public EventListener(UnityAction<T1, T2> action, int priority)
        {
            Action = action;
            Priority = priority;
        }
        
        public void SetArg(T1 arg1, T2 arg2)
        {
            _arg1 = arg1;
            _arg2 = arg2;
        }
        
        public override void Invoke() => Action?.Invoke(_arg1, _arg2);
    }

    // 三参数监听者
    private class EventListener<T1, T2, T3> : EventListenerBase
    {
        internal readonly UnityAction<T1, T2, T3> Action;
        private T1 _arg1;
        private T2 _arg2;
        private T3 _arg3;
        
        public EventListener(UnityAction<T1, T2, T3> action, int priority)
        {
            Action = action;
            Priority = priority;
        }
        
        public void SetArg(T1 arg1, T2 arg2, T3 arg3)
        {
            _arg1 = arg1;
            _arg2 = arg2;
            _arg3 = arg3;
        }
        
        public override void Invoke() => Action?.Invoke(_arg1, _arg2, _arg3);
    }

    // 四参数监听者
    private class EventListener<T1, T2, T3, T4> : EventListenerBase
    {
        internal readonly UnityAction<T1, T2, T3, T4> Action;
        private T1 _arg1;
        private T2 _arg2;
        private T3 _arg3;
        private T4 _arg4;
        
        public EventListener(UnityAction<T1, T2, T3, T4> action, int priority)
        {
            Action = action;
            Priority = priority;
        }
        
        public void SetArg(T1 arg1, T2 arg2, T3 arg3, T4 arg4)
        {
            _arg1 = arg1;
            _arg2 = arg2;
            _arg3 = arg3;
            _arg4 = arg4;
        }
        
        public override void Invoke() => Action?.Invoke(_arg1, _arg2, _arg3, _arg4);
    }
    #endregion
}
    