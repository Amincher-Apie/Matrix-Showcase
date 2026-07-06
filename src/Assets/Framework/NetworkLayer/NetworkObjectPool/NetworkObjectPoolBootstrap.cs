// 文件：NetworkObjectPool/NetworkObjectPoolBootstrap.cs
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Framework.NetworkLayer.NetworkObjectPool
{
    /// <summary>
    /// 在本节点（服务器或客户端）注册所有需要池化的预制体的 PrefabHandler。
    /// 请把该脚本挂到常驻场景的一个对象上，并在 Inspector 配置 prefabPaths。
    /// </summary>
    public class NetworkObjectPoolBootstrap : MonoBehaviour
    {
        /*    /// 修复点：只在 NetworkManager 存在后注册，并避免重复回调注册。*/
        [Tooltip("Resources 下的预制体路径列表，例如: Framework/Bullets/XBullet")]
        public List<string> prefabPaths = new();
        
        private bool _hooked;
        
        private void Start()
        {
            TryRegisterAll();
            if (NetworkManager.Singleton != null && !_hooked)
            {
                _hooked = true;
                NetworkManager.Singleton.OnServerStarted += TryRegisterAll;
                NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            }
        }

        private void TryRegisterAll()
        {
            if (NetworkManager.Singleton == null) return;

            foreach (var path in prefabPaths)
            {
                NetworkObjectPoolManager.Instance.RegisterPoolOnThisNode(path);
            }
        }
        

        private void OnClientConnected(ulong _)
        {
            TryRegisterAll();
        }

        private void OnDestroy()
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnServerStarted -= TryRegisterAll;
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            }
        }
    }
    
}