using UnityEngine;
using System;
using System.Collections;
using Framework.Singleton;

namespace Framework.Mono
{
    /// <summary>
    /// 基于MonoBehaviour的公共Mono模块
    /// 非继承自Mono的类可以通过公共Mono模块处理协程和生命周期事件
    /// </summary>
    public class MonoManager : MonoSingletonBase<MonoManager>
    {
        /// <summary>
        /// 初始化（单例创建时调用）
        /// </summary>
        protected override void Initialize()
        {
            base.Initialize();
            Debug.Log("[MonoManager] 初始化完成");
        }

        #region 协程管理
        /// <summary>
        /// 启动协程
        /// </summary>
        public new Coroutine StartCoroutine(IEnumerator routine)
        {
            return base.StartCoroutine(routine);
        }

        /// <summary>
        /// 停止指定协程
        /// </summary>
        public new void StopCoroutine(Coroutine routine)
        {
            base.StopCoroutine(routine);
        }

        /// <summary>
        /// 停止所有协程
        /// </summary>
        public new void StopAllCoroutines()
        {
            base.StopAllCoroutines();
        }
        #endregion

        #region 全局生命周期事件
        /// <summary>
        /// 每帧更新事件
        /// </summary>
        public event Action OnUpdate;

        /// <summary>
        /// 每帧延迟更新事件
        /// </summary>
        public event Action OnLateUpdate;

        /// <summary>
        /// 物理帧更新事件
        /// </summary>
        public event Action OnFixedUpdate;
        #endregion

        #region Unity生命周期（自动触发）
        private void Update()
        {
            OnUpdate?.Invoke();
        }

        private void LateUpdate()
        {
            OnLateUpdate?.Invoke();
        }

        private void FixedUpdate()
        {
            OnFixedUpdate?.Invoke();
        }
        #endregion

        /// <summary>
        /// 释放资源
        /// </summary>
        public override void Release()
        {
            // 清空事件订阅，避免内存泄漏
            OnUpdate = null;
            OnLateUpdate = null;
            OnFixedUpdate = null;
            
            StopAllCoroutines();
            base.Release();
            Debug.Log("[MonoManager] 已释放");
        }
    }
}
    