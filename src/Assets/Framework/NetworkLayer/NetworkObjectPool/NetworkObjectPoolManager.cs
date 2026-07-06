using System;
using System.Collections.Generic;
using Framework.Singleton;
using Unity.Netcode;
using UnityEngine;

namespace Framework.NetworkLayer.NetworkObjectPool
{
    /// <summary>
    /// 网络对象池管理器
    /// </summary>
    public class NetworkObjectPoolManager : SingletonBase<NetworkObjectPoolManager>
    {
        /// <summary>
        /// 路径→对象池的映射表
        /// </summary>
        private readonly Dictionary<string, NetworkObjectPool> _pools = new Dictionary<string, NetworkObjectPool>();
        private readonly HashSet<string> _registeredHandlers = new(); // ✅ 防止重复 AddHandler

        // ✅ 新增：本节点注册池与 Handler（Server/Client 都可）
        public void RegisterPoolOnThisNode(string prefabPath)
        {
            if (string.IsNullOrEmpty(prefabPath))
            {
                Debug.LogError("[NetworkObjectPoolManager] 预制体路径不能为空");
                return;
            }
            if (NetworkManager.Singleton == null)
            {
                Debug.LogError("[NetworkObjectPoolManager] NetworkManager.Singleton is null, cannot register handlers yet.");
                return;
            }
            GetOrCreatePoolLocal(prefabPath);
        }
        
        /// <summary>
        /// 保持服务器生成（有权限）
        /// </summary>
        /// <param name="prefabPath"></param>
        /// <param name="position"></param>
        /// <param name="rotation"></param>
        /// <param name="ownerClientId"></param>
        /// <returns></returns>
        public NetworkObject GetAndSpawn(string prefabPath,
            Vector3 position = default,
            Quaternion rotation = default,
            ulong ownerClientId = NetworkManager.ServerClientId,
            Action<NetworkObject> beforeSpawn = null)
        {
            if (!EnsureServer()) return null;
            if (string.IsNullOrEmpty(prefabPath))
            {
                Debug.LogError("[NetworkObjectPoolManager] 预制体路径不能为空");
                return null;
            }

            var pool = GetOrCreatePoolLocal(prefabPath); // 本地确保存在
            var networkObject = pool.GetNetworkObject(position, rotation);
            try
            {
                beforeSpawn?.Invoke(networkObject);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                pool.ReturnNetworkObject(networkObject);
                return null;
            }

            networkObject.SpawnWithOwnership(ownerClientId, true);
            return networkObject;
        }

        /// <summary>
        /// 服务器回收网络对象
        /// </summary>
        public void DespawnAndRecycle(NetworkObject networkObject, string prefabPath)
        {
            // if (networkObject == null || string.IsNullOrEmpty(prefabPath))
            // {
            //     Debug.LogError("[NetworkObjectPoolManager] 回收对象或路径不能为空");
            //     return;
            // }
            // if (!EnsureServer()) return;
            //
            // // 先进行网络层的 Despawn（不销毁 GameObject）
            // var objectId = networkObject.NetworkObjectId;
            // networkObject.Despawn(false);
            //
            // // 再回本地池（此处是服务器本地；客户端会通过 PrefabHandler.Destroy 回收）
            // if (_pools.TryGetValue(prefabPath, out var pool))
            // {
            //     pool.ReturnNetworkObject(networkObject);
            // }
            // else
            // {
            //     Debug.LogWarning($"[NetworkObjectPoolManager] 未找到路径 {prefabPath} 的对象池");
            // }
            if (networkObject == null || string.IsNullOrEmpty(prefabPath))
            {
                Debug.LogError("[NetworkObjectPoolManager] 回收对象或路径不能为空");
                return;
            }
            if (!EnsureServer()) return;

            // Despawn first; server returns locally, clients return through PrefabHandler.Destroy.
            networkObject.Despawn(false);

            if (_pools.TryGetValue(prefabPath, out var pool))
            {
                pool.ReturnNetworkObject(networkObject);
            }
            else
            {
                Debug.LogWarning($"[NetworkObjectPoolManager] Pool not found for prefabPath={prefabPath}");
            }
        }

        // /// <summary>
        // /// 预加载对象到池 → 该功能已移交至NetworkObjectBootstrap实现
        // /// </summary>
        // public void Preload(string prefabPath, int count = 30)
        // {
        //     if (count <= 0) return;
        //     var pool = GetOrCreatePool(prefabPath);
        //     pool.Prewarm(count);
        // }

        /// <summary>
        /// 清空指定池
        /// </summary>
        public void ClearPool(string prefabPath, bool destroyActive = false)
        {
            if (_pools.TryGetValue(prefabPath, out var pool))
            {
                pool.Clear(destroyActive);
                _pools.Remove(prefabPath);
                _registeredHandlers.Remove(prefabPath);

            }
        }

        /// <summary>
        /// 清空所有池
        /// </summary>
        public void ClearAllPools(bool destroyActive = false)
        {
            foreach (var pool in _pools.Values)
                pool.Clear(destroyActive);
            
            _pools.Clear();
            _registeredHandlers.Clear();

        }

        #region 私有方法
        /// <summary>
        /// 确保在服务器执行
        /// </summary>
        private bool EnsureServer()
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            {
                Debug.LogError("[NetworkObjectPoolManager] 仅服务器可执行此操作");
                return false;
            }
            return true;
        }

        // ❗本方法不再限制仅服务器调用（客户端也需要注册 Handler）
        private NetworkObjectPool GetOrCreatePoolLocal(string prefabPath)
        {
            if (!_pools.TryGetValue(prefabPath, out var pool))
            {
                pool = new NetworkObjectPool(prefabPath);
                _pools.Add(prefabPath, pool);
                
            }
            
            // ✅ Handler 注册：同一路径只注册一次
            if (!_registeredHandlers.Contains(prefabPath))
            {
                NetworkManager.Singleton.PrefabHandler.AddHandler(pool.Prefab, new PooledNetworkPrefabHandler(pool));
                _registeredHandlers.Add(prefabPath);
            }
            
            return pool;
        }
        
        
        #endregion
    }
}
