using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Framework.NetworkLayer.NetworkObjectPool
{
    /// <summary>
    /// 单个预制体的网络对象池
    /// </summary>
    public class NetworkObjectPool
    {/*
        /// 关键修复点：
        /// 1) PoolRoot 绝不挂 NetworkObject（避免 ScenePlacedObjects/GlobalObjectIdHash 冲突）
        /// 2) Spawn 前后不强行 SetParent 到 PoolRoot（避免 NGO Parent 约束问题）
        /// 3) Despawn/回收时再把对象放回 Root
    */
        /// <summary>
        /// 对应的预制体
        /// </summary>
        public GameObject Prefab { get; }

        /// <summary>
        /// 预制体路径
        /// </summary>
        public string PrefabPath { get; }

        /// <summary>
        /// 闲置对象栈
        /// </summary>
        private readonly Stack<NetworkObject> _idleObjects = new Stack<NetworkObject>();

        /// <summary>
        /// 活跃对象集合
        /// </summary>
        private readonly HashSet<NetworkObject> _activeObjects = new HashSet<NetworkObject>();
        
        // ✅ 唯一自动父节点
        private readonly Transform _root;


        /// <summary>
        /// 构造对象池（移除根节点创建）
        /// </summary>
        public NetworkObjectPool(string prefabPath)
        {
            PrefabPath = prefabPath;

            Prefab = Resources.Load<GameObject>(prefabPath);
            if (!Prefab)
                throw new ArgumentException($"[NetworkObjectPool] 预制体加载失败：{prefabPath}");

            if (Prefab.GetComponent<NetworkObject>() == null)
                throw new ArgumentException($"[NetworkObjectPool] 预制体 {prefabPath} 缺少 NetworkObject 组件");

            _root = EnsureRoot($"[{Prefab.name}]PoolRoot");
        }
        
        private static readonly Dictionary<string, Transform> s_roots = new Dictionary<string, Transform>();

        /// <summary>
        /// 确保唯一父节点存在，放在原点，不持久化
        /// </summary>
        private static Transform EnsureRoot(string rootName)
        {
            if (s_roots.TryGetValue(rootName, out var cached) && cached)
                return cached;

            var go = new GameObject(rootName);
            go.transform.position = Vector3.zero;

            // ✅ PoolRoot 绝对不要挂 NetworkObject！
            // 否则它没 Spawn 的情况下，非 Spawn 对象 reparent 过来会抛 SpawnStateException

            s_roots[rootName] = go.transform;
            return go.transform;
        }

        /// <summary>
        /// 从池获取对象
        /// 注意：不要在 Spawn 前/Spawn 中把对象 SetParent 到 PoolRoot（会引发 NGO 的 Parent 限制）
        /// </summary>
        public NetworkObject GetNetworkObject(Vector3 position, Quaternion rotation)
        {
            NetworkObject networkObject;

            if (_idleObjects.Count > 0)
            {
                networkObject = _idleObjects.Pop();
                networkObject.transform.SetPositionAndRotation(position, rotation);
            }
            else
            {
                networkObject = Object.Instantiate(Prefab, position, rotation).GetComponent<NetworkObject>();
            }

            // ✅ 记录 RootParent，回收时用
            var pno = networkObject.GetComponent<PooledNetworkObject>();
            if (!pno) pno = networkObject.gameObject.AddComponent<PooledNetworkObject>();
            pno.RootParent = _root;

            // ✅ Spawn/同步期间让它保持“无父级”（最稳）
            networkObject.transform.SetParent(null, worldPositionStays: true);

            networkObject.gameObject.SetActive(true);
            _activeObjects.Add(networkObject);
            return networkObject;
        }
        
        
        /// <summary>
        /// 回收对象到池：挂回 RootParent 并禁用
        /// </summary>
        public void ReturnNetworkObject(NetworkObject networkObject)
        {
            if (!networkObject || !_activeObjects.Contains(networkObject)) return;

            _activeObjects.Remove(networkObject);
            _idleObjects.Push(networkObject);

            // var t = networkObject.transform;
            // if (t) t.SetParent(_root, worldPositionStays: false);

            networkObject.gameObject.SetActive(false);
            
        }
        

        /// <summary>
        /// 预加载（可选）
        /// </summary>
        public void Prewarm(int count)
        {
            for (int i = 0; i < count; i++)
            {
                var networkObject = Object.Instantiate(Prefab).GetComponent<NetworkObject>();

                var pno = networkObject.GetComponent<PooledNetworkObject>();
                if (!pno) pno = networkObject.gameObject.AddComponent<PooledNetworkObject>();
                pno.RootParent = _root;

                var t = networkObject.transform;
                if (t != null) t.SetParent(_root, worldPositionStays: false);
                networkObject.gameObject.SetActive(false);
                _idleObjects.Push(networkObject);
            }
        }

        /// <summary>
        /// 清空池
        /// </summary>
        public void Clear(bool destroyActive)
        {
            // 销毁闲置对象
            foreach (var obj in _idleObjects)
                Object.Destroy(obj.gameObject);
            _idleObjects.Clear();

            // 销毁活跃对象（如果需要）
            if (destroyActive)
            {
                foreach (var obj in _activeObjects)
                    Object.Destroy(obj.gameObject);
                _activeObjects.Clear();
            }
        }
    }
}
