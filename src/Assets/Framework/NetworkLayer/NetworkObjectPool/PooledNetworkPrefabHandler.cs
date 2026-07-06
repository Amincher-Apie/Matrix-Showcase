using Unity.Netcode;
using UnityEngine;

namespace Framework.NetworkLayer.NetworkObjectPool
{
    /// <summary>
    /// 桥接NGO与对象池的处理器
    /// </summary>
    public class PooledNetworkPrefabHandler : INetworkPrefabInstanceHandler
    {
        private readonly NetworkObjectPool _pool;

        public PooledNetworkPrefabHandler(NetworkObjectPool pool)
        {
            _pool = pool;
        }

        /// <summary>
        /// 客户端生成对象时从池获取
        /// </summary>
        public NetworkObject Instantiate(ulong ownerClientId, Vector3 position, Quaternion rotation)
        {
            return _pool.GetNetworkObject(position, rotation);
        }

        /// <summary>
        /// 销毁逻辑
        /// </summary>
        public void Destroy(NetworkObject networkObject)
        {
            _pool.ReturnNetworkObject(networkObject);
        }
    }
}