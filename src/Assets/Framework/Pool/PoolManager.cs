using System;
using System.Collections;
using System.Collections.Generic;
using Framework.Mono;
using Framework.Resource;
using Framework.Singleton;
using UnityEngine;

/// <summary>
/// 对象池接口，实现此接口可自定义对象回收和取出时的行为
/// </summary>
public interface IPoolable
{
    /// <summary>
    /// 当对象从池取出时调用
    /// </summary>
    void OnSpawn();
    
    /// <summary>
    /// 当对象回收至池时调用
    /// </summary>
    void OnRecycle();
}

public class PoolManager : SingletonBase<PoolManager>
{
    #region 内部数据结构

    /// <summary>
    /// 单个对象池的数据类
    /// </summary>
    private class PoolItem
    {
        /// <summary>
        /// 该对象池用于存储对象的栈
        /// </summary>
        public readonly Stack<GameObject> ObjectPool = new Stack<GameObject>();
        
        /// <summary>
        /// 该对象池活跃对象的集合（用于跟踪和清理）
        /// </summary>
        public readonly HashSet<GameObject> ActiveObjects = new HashSet<GameObject>();
        
        /// <summary>
        /// 对象池根节点 用于层级管理
        /// </summary>
        public Transform Root;
        
        /// <summary>
        /// 该对象池对应对象的预制体缓存 
        /// </summary>
        public GameObject Prefab;
        
        /// <summary>
        /// 预制体加载状态
        /// </summary>
        public bool IsLoading = false;
        
        /// <summary>
        /// 预制体路径
        /// </summary>
        public string Path;
    }
    
    /// <summary>
    /// 以预制体路径名为键 存储对应对象池的映射关系
    /// </summary>
    private Dictionary<string, PoolItem> _pools = new Dictionary<string, PoolItem>();
    
    /// <summary>
    /// 全局对象池根节点
    /// </summary>
    private GameObject _rootObj;

    #endregion

    #region 公开方法

    /// <summary>
    /// 从对象池中同步获取对象
    /// </summary>
    /// <param name="path">对象对应预制体相对于Resources文件夹下的路径</param>
    /// <returns></returns>
    public GameObject Get(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogError("预制体的路径不能为空");
            return null;
        }
        
        // 解析路径并生成唯一键
        string key = GetPoolKey(path);

        //若之前对象没有池 则为这个对象新创建一个池子
        if (!_pools.TryGetValue(key, out var poolItem))
            poolItem = CreatePool(key, path);

        // 如果预制体正在加载中，等待加载完成
        if (poolItem.IsLoading)
        {
            Debug.LogWarning($"预制体 {path} 正在加载中，请使用异步方法获取");
            return null;
        }

        GameObject obj;

        //栈中有闲置的对象 直接返回闲置对象
        if (poolItem.ObjectPool.Count > 0) 
        {
            obj = poolItem.ObjectPool.Pop();
            poolItem.ActiveObjects.Add(obj);
        }
        else
        {
            //若栈中没有闲置的对象 则加载预制体 创建新的对象并返回
            GameObject prefab = null;

            //若池中已经缓存预制体
            if (poolItem.Prefab != null)
                prefab = poolItem.Prefab;
            else
            {
                //同步加载预制体
                prefab = ResourcesManager.Instance.Load<GameObject>(path);
                if (prefab == null)
                {
                    Debug.LogError($"同步加载预制体失败：{path}");
                    return null;
                }
                poolItem.Prefab = prefab;
            }
            
            obj = UnityEngine.Object.Instantiate(prefab);
            obj.name = $"{GetPrefabNameFromPath(path)}_{poolItem.ActiveObjects.Count}";
            poolItem.ActiveObjects.Add(obj);
        }
        
        obj.transform.SetParent(null, false);
        obj.SetActive(true);

        if (obj.TryGetComponent<IPoolable>(out var poolable))
        {
            poolable?.OnSpawn();
        }
        
        return obj;
    }

    /// <summary>
    /// 从对象池中异步获取对象 获得到的对象随onComplete回调回传
    /// </summary>
    /// <param name="path">预制体的路径</param>
    /// <param name="onComplete">获取对象的回调函数</param>
    public void GetAsync(string path, Action<GameObject> onComplete)
    {
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogError("预制体的路径不能为空");
            onComplete?.Invoke(null);
            return;
        }
        
        // 解析路径并生成唯一键
        string key = GetPoolKey(path);

        //若之前对象没有池 则为这个对象新创建一个池子
        if (!_pools.TryGetValue(key, out var poolItem))
            poolItem = CreatePool(key, path);

        //如果有闲置的对象
        if (poolItem.ObjectPool.Count > 0)
        {
            var obj = poolItem.ObjectPool.Pop();
            poolItem.ActiveObjects.Add(obj);
            obj.transform.SetParent(null, false);
            obj.SetActive(true);
            
            // 调用对象的Spawn方法
            var poolable = obj.GetComponent<IPoolable>();
            poolable?.OnSpawn();
            
            onComplete?.Invoke(obj);
            return;
        }
        
        // 如果正在加载，等待加载完成后再处理
        if (poolItem.IsLoading)
        {
            MonoManager.Instance.StartCoroutine(WaitForLoadComplete(poolItem, path, onComplete));
            return;
        }
        
        // 异步加载预制体
        poolItem.IsLoading = true;
        ResourcesManager.Instance.LoadAsync<GameObject>(
            path,
            (success, prefab) =>
            {
                poolItem.IsLoading = false;
                
                if (!success || prefab == null)
                {
                    Debug.LogError($"异步加载预制体失败：{path}");
                    onComplete?.Invoke(null);
                    return;
                }

                poolItem.Prefab = prefab;
                var obj = UnityEngine.Object.Instantiate(prefab);
                obj.name = $"{GetPrefabNameFromPath(path)}_{poolItem.ActiveObjects.Count}";
                poolItem.ActiveObjects.Add(obj);
                obj.SetActive(true);
                
                if (obj.TryGetComponent<IPoolable>(out var poolable))
                {
                    poolable?.OnSpawn();
                }
                
                onComplete?.Invoke(obj);
            });
    }

    /// <summary>
    /// 将对象回收至对象池，若不存在对应对象池则自动创建
    /// </summary>
    /// <param name="path">预制体路径（需与获取时一致）</param>
    /// <param name="obj">要回收的对象</param>
    public void Recycle(string path, GameObject obj)
    {
        if (obj == null)
        {
            Debug.LogWarning("尝试回收空对象");
            return;
        }
    
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogError("回收对象时路径不能为空");
            UnityEngine.Object.Destroy(obj);
            return;
        }

        string key = GetPoolKey(path);
        PoolItem poolItem;

        // 若不存在对应对象池，则自动创建
        if (!_pools.TryGetValue(key, out poolItem))
        {
            Debug.LogWarning($"未找到路径为{path}的对象池，自动创建新对象池");
            poolItem = CreatePool(key, path);
        }

        // 检查是否已经在池里，防止重复回收
        if (!poolItem.ActiveObjects.Contains(obj))
        {
            Debug.LogWarning($"尝试回收未从池获取的对象：{obj.name}，路径：{path}");
            return;
        }

        if (obj.TryGetComponent<IPoolable>(out var poolable))
        {
            poolable?.OnRecycle();
        }

        // 重置对象状态
        obj.SetActive(false);
        obj.transform.SetParent(poolItem.Root, false);
        obj.transform.localPosition = Vector3.zero;
        obj.transform.localRotation = Quaternion.identity;
        obj.transform.localScale = Vector3.one;

        // 从活跃集合中移除，添加到对象池
        poolItem.ActiveObjects.Remove(obj);
        poolItem.ObjectPool.Push(obj);
    }


    /// <summary>
    /// 预加载对象到对象池（同步）
    /// </summary>
    /// <param name="path">预制体路径</param>
    /// <param name="count">预加载数量</param>
    public void Preload(string path, int count = 1)
    {
        if (count <= 0)
        {
            Debug.LogWarning("预加载数量必须大于0");
            return;
        }
        
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogError("预加载路径不能为空");
            return;
        }

        string key = GetPoolKey(path);
        if (!_pools.TryGetValue(key, out var poolItem))
            poolItem = CreatePool(key, path);

        // 确保预制体已加载
        if (poolItem.Prefab == null)
        {
            poolItem.Prefab = ResourcesManager.Instance.Load<GameObject>(path);
            if (poolItem.Prefab == null)
            {
                Debug.LogError($"预加载失败，无法加载预制体：{path}");
                return;
            }
        }

        // 预实例化对象并加入对象池
        for (int i = 0; i < count; i++)
        {
            var obj = UnityEngine.Object.Instantiate(poolItem.Prefab);
            obj.name = $"{GetPrefabNameFromPath(path)}_preload_{i}";
            // 直接添加到对象池，不经过活跃状态
            poolItem.ObjectPool.Push(obj);
            obj.SetActive(false);
            obj.transform.SetParent(poolItem.Root, false);
            obj.transform.localPosition = Vector3.zero;
            obj.transform.localRotation = Quaternion.identity;
            obj.transform.localScale = Vector3.one;
        }
    }

    /// <summary>
    /// 预加载对象到对象池（异步）
    /// </summary>
    /// <param name="path">预制体路径</param>
    /// <param name="count">预加载数量</param>
    /// <param name="onComplete">完成回调</param>
    /// <param name="timeout">超时时间（秒），默认5秒</param>
    public void PreloadAsync(string path, int count = 1, Action<bool> onComplete = null, float timeout = 5f)
    {
        if (count <= 0)
        {
            Debug.LogWarning("预加载数量必须大于0");
            onComplete?.Invoke(false);
            return;
        }
        
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogError("预加载路径不能为空");
            onComplete?.Invoke(false);
            return;
        }

        // 启动协程处理异步预加载
        MonoManager.Instance.StartCoroutine(CoPreload(path, count, onComplete, timeout));
    }

    /// <summary>
    /// 清理指定路径的对象池
    /// </summary>
    /// <param name="path">预制体路径</param>
    /// <param name="destroyActive">是否销毁活跃对象</param>
    public void ClearPool(string path, bool destroyActive = false)
    {
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogError("清理对象池时路径不能为空");
            return;
        }

        string key = GetPoolKey(path);
        if (_pools.TryGetValue(key, out var poolItem))
        {
            // 销毁闲置对象
            foreach (var obj in poolItem.ObjectPool)
                UnityEngine.Object.Destroy(obj);
            poolItem.ObjectPool.Clear();

            // 处理活跃对象
            if (destroyActive)
            {
                foreach (var obj in poolItem.ActiveObjects)
                    UnityEngine.Object.Destroy(obj);
            }
            poolItem.ActiveObjects.Clear();

            // 销毁根节点
            if (poolItem.Root != null && poolItem.Root.gameObject != null)
                UnityEngine.Object.Destroy(poolItem.Root.gameObject);

            // 从字典移除
            _pools.Remove(key);
        }
    }

    /// <summary>
    /// 清理所有对象池
    /// </summary>
    /// <param name="destroyActive">是否销毁活跃对象</param>
    public void ClearAllPools(bool destroyActive = false)
    {
        foreach (var poolItem in _pools.Values)
        {
            // 销毁闲置对象
            foreach (var obj in poolItem.ObjectPool)
                UnityEngine.Object.Destroy(obj);
            poolItem.ObjectPool.Clear();

            // 处理活跃对象
            if (destroyActive)
            {
                foreach (var obj in poolItem.ActiveObjects)
                    UnityEngine.Object.Destroy(obj);
            }
            poolItem.ActiveObjects.Clear();

            // 销毁根节点
            if (poolItem.Root != null && poolItem.Root.gameObject != null)
                UnityEngine.Object.Destroy(poolItem.Root.gameObject);
        }

        _pools.Clear();

        // 销毁全局根节点
        if (_rootObj != null)
        {
            UnityEngine.Object.Destroy(_rootObj);
            _rootObj = null;
        }
    }

    /// <summary>
    /// 获取对象池信息
    /// </summary>
    /// <param name="path">预制体路径</param>
    /// <param name="idleCount">闲置对象数量</param>
    /// <param name="activeCount">活跃对象数量</param>
    /// <returns>是否存在该对象池</returns>
    public bool GetPoolInfo(string path, out int idleCount, out int activeCount)
    {
        idleCount = 0;
        activeCount = 0;
        
        if (string.IsNullOrEmpty(path))
            return false;

        string key = GetPoolKey(path);
        if (_pools.TryGetValue(key, out var poolItem))
        {
            idleCount = poolItem.ObjectPool.Count;
            activeCount = poolItem.ActiveObjects.Count;
            return true;
        }
        
        return false;
    }

    #endregion

    #region 私有工具方法

    /// <summary>
    /// 等待预制体加载完成
    /// </summary>
    private IEnumerator WaitForLoadComplete(PoolItem poolItem, string path, Action<GameObject> onComplete)
    {
        float waitTime = 0;
        float timeout = 10f; // 10秒超时
        
        while (poolItem.IsLoading)
        {
            yield return null;
            waitTime += Time.deltaTime;
            
            if (waitTime >= timeout)
            {
                Debug.LogError($"等待预制体 {path} 加载超时");
                onComplete?.Invoke(null);
                yield break;
            }
        }
        
        // 重新尝试获取对象
        GetAsync(path, onComplete);
    }

    /// <summary>
    /// 基于完整路径生成对象池唯一键
    /// </summary>
    private string GetPoolKey(string fullPath)
    {
        // 替换路径分隔符并统一转为小写，确保键的唯一性
        return fullPath.Replace("\\", "/").ToLowerInvariant();
    }
    
    /// <summary>
    /// 从完整路径中提取预制体名称
    /// </summary>
    private string GetPrefabNameFromPath(string fullPath)
    {
        fullPath = fullPath.Replace("\\", "/");
        int lastSlashIndex = fullPath.LastIndexOf('/');
        return lastSlashIndex == -1 ? fullPath : fullPath.Substring(lastSlashIndex + 1);
    }

    private PoolItem CreatePool(string key, string path)
    {
        EnsureRoot();
        string prefabName = GetPrefabNameFromPath(path);
        if (string.IsNullOrEmpty(prefabName))
            prefabName = "Unknown";
        
        // 新建池子并设置父节点
        var pool = new PoolItem
        {
            Root = new GameObject($"{prefabName}Pool").transform,
            Path = path
        };
        pool.Root.SetParent(_rootObj.transform, false);
        
        // 将池子加入字典中
        _pools.Add(key, pool);
        
        return pool;
    }
    
    /// <summary>
    /// 确保全局根节点存在
    /// </summary>
    private void EnsureRoot()
    {
        if (_rootObj == null)
        {
            _rootObj = new GameObject("PoolRoot");
            UnityEngine.Object.DontDestroyOnLoad(_rootObj);
        }
    }

    /// <summary>
    /// 异步预加载协程
    /// </summary>
    private IEnumerator CoPreload(string path, int count, Action<bool> onComplete, float timeout)
    {
        string key = GetPoolKey(path);
        PoolItem poolItem = null;

        // 等待对象池创建完成
        if (!_pools.TryGetValue(key, out poolItem))
        {
            poolItem = CreatePool(key, path);
        }

        // 如果预制体未加载且未在加载中，则开始加载
        if (poolItem.Prefab == null && !poolItem.IsLoading)
        {
            poolItem.IsLoading = true;
            bool loadSuccess = false;
            
            ResourcesManager.Instance.LoadAsync<GameObject>(path, (success, prefab) =>
            {
                poolItem.IsLoading = false;
                if (success && prefab != null)
                {
                    poolItem.Prefab = prefab;
                    loadSuccess = true;
                }
                else
                {
                    Debug.LogError($"预加载异步加载预制体失败：{path}");
                }
            });
            
            // 等待加载完成或超时
            float waitTime = 0;
            while (poolItem.IsLoading)
            {
                yield return null;
                waitTime += Time.deltaTime;
                
                if (waitTime >= timeout)
                {
                    Debug.LogError($"预加载预制体 {path} 超时");
                    poolItem.IsLoading = false;
                    onComplete?.Invoke(false);
                    yield break;
                }
            }
            
            if (!loadSuccess)
            {
                onComplete?.Invoke(false);
                yield break;
            }
        }
        // 如果预制体正在加载中，等待加载完成
        else if (poolItem.IsLoading)
        {
            float waitTime = 0;
            while (poolItem.IsLoading)
            {
                yield return null;
                waitTime += Time.deltaTime;
                
                if (waitTime >= timeout)
                {
                    Debug.LogError($"等待预制体 {path} 加载超时");
                    onComplete?.Invoke(false);
                    yield break;
                }
            }
        }

        // 检查预制体是否加载成功
        if (poolItem.Prefab == null)
        {
            Debug.LogError($"预制体 {path} 未加载成功，无法预加载");
            onComplete?.Invoke(false);
            yield break;
        }

        // 预加载指定数量的对象
        for (int i = 0; i < count; i++)
        {
            var obj = UnityEngine.Object.Instantiate(poolItem.Prefab);
            obj.name = $"{GetPrefabNameFromPath(path)}_preload_{i}";
            // 直接添加到对象池，不经过活跃状态
            poolItem.ObjectPool.Push(obj);
            obj.SetActive(false);
            obj.transform.SetParent(poolItem.Root, false);
            obj.transform.localPosition = Vector3.zero;
            obj.transform.localRotation = Quaternion.identity;
            obj.transform.localScale = Vector3.one;
            
            yield return null; 
        }

        onComplete?.Invoke(true);
    }

    #endregion
}
