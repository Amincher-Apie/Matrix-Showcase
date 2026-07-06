using UnityEngine;
using System.Collections;

namespace Framework.Resource
{
    /// <summary>
    /// 资源加载接口（定义统一行为，适配不同加载方式）
    /// </summary>
    public interface IResourceLoader
    {
        /// <summary>
        /// 同步加载资源
        /// </summary>
        /// <typeparam name="T">资源类型（如Texture2D、GameObject）</typeparam>
        /// <param name="path">资源路径（Resources文件夹下的相对路径，无需后缀）</param>
        /// <returns>加载成功的资源，失败返回null</returns>
        T Load<T>(string path) where T : Object;

        /// <summary>
        /// 异步加载资源（通过回调返回结果）
        /// </summary>
        /// <typeparam name="T">资源类型</typeparam>
        /// <param name="path">资源路径</param>
        /// <param name="onLoaded">加载完成回调（参数1：是否成功，参数2：加载的资源）</param>
        /// <returns>协程对象</returns>
        Coroutine LoadAsync<T>(string path, System.Action<bool, T> onLoaded) where T : Object;

        /// <summary>
        /// 异步加载并实例化预制体
        /// </summary>
        /// <param name="path">预制体路径</param>
        /// <param name="parent">父节点（可选）</param>
        /// <param name="onInstantiated">实例化完成回调（参数1：是否成功，参数2：实例化的对象）</param>
        /// <returns>协程对象</returns>
        Coroutine InstantiateAsync(string path, Transform parent, System.Action<bool, GameObject> onInstantiated);

        /// <summary>
        /// 释放单个资源
        /// </summary>
        /// <param name="asset">要释放的资源对象</param>
        void Release(Object asset);

        /// <summary>
        /// 释放所有未使用的资源
        /// </summary>
        void ReleaseUnusedResources();
        
        T[] LoadAll<T>(string path) where T : Object;
        //Coroutine LoadAllAsync<T>(string path, System.Action<bool, T> onLoaded) where T : Object;
    }
}