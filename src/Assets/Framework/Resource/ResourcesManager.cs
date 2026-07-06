using UnityEngine;
using Framework.Singleton;
using System;
using Framework.Mono;
using Object = UnityEngine.Object;

namespace Framework.Resource
{
    /// <summary>
    /// 资源管理器 统一管理资源加载 
    /// </summary>
    public class ResourcesManager : SingletonBase<ResourcesManager>
    {
        // 当前使用的资源加载器
        private IResourceLoader _currentLoader;

        /// <summary>
        /// 初始化（单例创建时调用）
        /// </summary>
        protected override void Initialize()
        {
            base.Initialize();
            
            // 初始使用Resources加载器，后续可切换为AddressablesLoader
            _currentLoader = new ResourcesLoader();
            
            Debug.Log("[ResourcesManager] 初始化完成，当前使用：Resources加载模式");
        }

        /// <summary>
        /// 切换资源加载器
        /// </summary>
        /// <param name="newLoader">新的资源加载器实例</param>
        public void SwitchLoader(IResourceLoader newLoader)
        {
            if (newLoader == null)
            {
                Debug.LogError("[ResourcesManager] 切换加载器失败：新加载器不能为空");
                return;
            }

            _currentLoader = newLoader;
            Debug.Log("[ResourcesManager] 资源加载器已切换");
        }

        #region 资源加载接口

        /// <summary>
        /// 同步加载资源
        /// </summary>
        public T Load<T>(string path) where T : Object
        {
            if (_currentLoader == null)
            {
                Debug.LogError("[ResourcesManager] 加载器未初始化，无法加载资源");
                return null;
            }

            return _currentLoader.Load<T>(path);
        }

        /// <summary>
        /// 异步加载资源
        /// </summary>
        public Coroutine LoadAsync<T>(string path, Action<bool, T> onLoaded) where T : Object
        {
            if (_currentLoader == null)
            {
                Debug.LogError("[ResourcesManager] 加载器未初始化，无法异步加载资源");
                onLoaded?.Invoke(false, null);
                return null;
            }

            return _currentLoader.LoadAsync(path, onLoaded);
        }

        /// <summary>
        /// 异步加载并实例化预制体
        /// </summary>
        public Coroutine InstantiateAsync(string path, Transform parent = null, Action<bool, GameObject> onInstantiated = null)
        {
            if (_currentLoader == null)
            {
                Debug.LogError("[ResourcesManager] 加载器未初始化，无法实例化预制体");
                onInstantiated?.Invoke(false, null);
                return null;
            }

            return _currentLoader.InstantiateAsync(path, parent, onInstantiated);
        }

        #endregion

        #region 资源释放接口

        /// <summary>
        /// 释放单个资源
        /// </summary>
        public void Release(Object asset)
        {
            _currentLoader?.Release(asset);
        }

        /// <summary>
        /// 释放所有未使用的资源
        /// </summary>
        public void ReleaseUnusedResources()
        {
            _currentLoader?.ReleaseUnusedResources();
        }

        public T[] LoadAll<T>(string path) where T : Object
        {
            return _currentLoader?.LoadAll<T>(path);
        }

        #endregion

        #region 生命周期

        /// <summary>
        /// 释放资源管理器
        /// </summary>
        public override void Release()
        {
            // 释放所有未使用资源
            ReleaseUnusedResources();
            
            _currentLoader = null;
            base.Release();
            Debug.Log("[ResourcesManager] 已释放");
        }

        #endregion
    }
}
    