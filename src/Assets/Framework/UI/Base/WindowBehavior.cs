using System;
using UnityEngine;

namespace Framework.UI.Base
{
    /// <summary>
    /// WindowBehavior是抽象类
    /// 作用是定义所有 UI 窗口的基础属性和生命周期函数 实现了解耦窗口逻辑与MonoBehaviour
    /// 使得所有Window的生命周期都由自己控制 不依赖于Mono 避免不同脚本Awake顺序不同造成的空引用问题
    /// </summary>
    public abstract class WindowBehavior
    {
        #region 公开属性
    
        /// <summary>关联窗口的实体GameObject</summary>
        public GameObject GameObject { get; set; }

        /// <summary>关联窗口的Transform</summary>
        public Transform Transform { get; set; }

        /// <summary>关联窗口独立的Canvas组件</summary>
        public Canvas Canvas { get; set; }

        /// <summary>窗口的名字 与窗口类名、预制体名保持一致</summary>
        public string Name {get; set; }

        /// <summary>窗口是否可见</summary>
        public bool Visible { get; set; }

        /// <summary>窗口是否通过堆栈系统弹出</summary>
        public bool IsPopStack {get; set; }
    
        /// <summary>窗口是否是全屏窗口 在OnAwake的时候设置 当全屏窗口弹出时 别的窗口会伪隐藏</summary>
        public bool IsFullScreenWindow {get; set; }
    
        /// <summary>堆栈回调事件 用于窗口弹出 关闭时通知堆栈系统更新状态</summary>
        public Action<WindowBase> PopStackListener { get; set; }

        #endregion
    
        #region 生命周期函数

        public virtual void OnAwake() {}
        public virtual void OnShow() {}
        public virtual void OnUpdate() {}
        public virtual void OnHide() {}
        public virtual void OnDestroy() {}

        #endregion

        #region 公开方法

        public virtual void SetVisible(bool isVisible) {}
    
        #endregion

    }
}
