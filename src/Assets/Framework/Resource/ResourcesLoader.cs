using System;
using UnityEngine;
using System.Collections;
using Framework.Mono;
using Object = UnityEngine.Object;

namespace Framework.Resource
{
    /// <summary>
    /// 基于Unity原生Resources的资源加载器
    /// </summary>
    public class ResourcesLoader : IResourceLoader
    {
        /// <summary>
        /// 同步加载资源
        /// </summary>
        public T Load<T>(string path) where T : Object
        {
            try
            {
                T asset = Resources.Load<T>(path);
                if (asset == null)
                {
                    Debug.LogError($"[ResourcesLoader] 同步加载失败，资源不存在：{path}");
                    return null;
                }
                return asset;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ResourcesLoader] 同步加载异常：{path}，错误：{e.Message}");
                return null;
            }
        }

        /// <summary>
        /// 异步加载资源
        /// </summary>
        public Coroutine LoadAsync<T>(string path, System.Action<bool, T> onLoaded) where T : Object
        {
            if (string.IsNullOrEmpty(path))
            {
                onLoaded?.Invoke(false, null);
                return null;
            }

            // 使用MonoManager的协程处理异步加载
            return MonoManager.Instance.StartCoroutine(LoadAsyncCoroutine(path, onLoaded));
        }

        /// <summary>
        /// 异步加载协程
        /// </summary>
        private IEnumerator LoadAsyncCoroutine<T>(string path, System.Action<bool, T> onLoaded) where T : Object
        {
            ResourceRequest request = Resources.LoadAsync<T>(path);
            yield return request;

            if (request.asset is T asset)
            {
                onLoaded?.Invoke(true, asset);
            }
            else
            {
                Debug.LogError($"[ResourcesLoader] 异步加载失败，资源不存在或类型错误：{path}");
                onLoaded?.Invoke(false, null);
            }
        }

        /// <summary>
        /// 异步加载并实例化预制体
        /// </summary>
        public Coroutine InstantiateAsync(string path, Transform parent, System.Action<bool, GameObject> onInstantiated)
        {
            return LoadAsync<GameObject>(path, (success, prefab) =>
            {
                if (success && prefab != null)
                {
                    GameObject instance = Object.Instantiate(prefab, parent);
                    onInstantiated?.Invoke(true, instance);
                }
                else
                {
                    onInstantiated?.Invoke(false, null);
                }
            });
        }

        /// <summary>
        /// 释放单个资源
        /// </summary>
        public void Release(Object asset)
        {
            if (asset == null) return;

            // 对于实例化的对象，先销毁实例
            if (asset is GameObject go && go.scene.IsValid())
            {
                Object.Destroy(go);
            }
            // 卸载资源（仅卸载未被引用的资源）
            Resources.UnloadAsset(asset);
        }

        /// <summary>
        /// 释放所有未使用的资源
        /// </summary>
        public void ReleaseUnusedResources()
        {
            Resources.UnloadUnusedAssets();
            System.GC.Collect(); // 触发垃圾回收
        }
        
        public T[] LoadAll<T>(string path) where T : Object
        {
            // Resources.LoadAll会加载path路径下所有T类型资源
            Object[] objects = Resources.LoadAll(path, typeof(T));
            // 转换为T[]
            T[] results = new T[objects.Length];
            for (int i = 0; i < objects.Length; i++)
            {
                results[i] = objects[i] as T;
            }
            return results;
        }
    }
}
    