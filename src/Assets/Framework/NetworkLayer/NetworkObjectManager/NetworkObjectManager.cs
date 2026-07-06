using System.Collections;
using System.Collections.Generic;
using Framework.Singleton;
using Unity.Netcode;
using UnityEngine;

public class NetworkObjectManager : MonoSingletonBase<NetworkObjectManager>
{
    #region 数据结构
    /// <summary>
    /// 网络对象数据容器
    /// </summary>
    private class NetworkObjectData
    {
        public NetworkObject NetworkObject;
        public INetworkProxy NetworkProxy;
        public ILogicObject LogicObject;
        public IRenderObject RenderObject;

        public NetworkObjectData(NetworkObject networkObject, INetworkProxy proxy, ILogicObject logicObject, IRenderObject renderObject)
        {
            NetworkObject = networkObject;
            NetworkProxy = proxy;
            LogicObject = logicObject;
            RenderObject = renderObject;
        }
    }
    #endregion

    #region 私有字段
    
    /// <summary>
    /// 网络对象ID与对象数据的映射表
    /// </summary>
    private Dictionary<ulong, NetworkObjectData> _networkObjectMap = new Dictionary<ulong, NetworkObjectData>();
    
    #endregion

    #region 公开方法

    /// <summary>
    /// 通过网络对象ID尝试获取网络代理对象
    /// </summary>
    /// <param name="networkObjectId">网络对象ID</param>
    /// <param name="proxy">输出的网络代理对象</param>
    /// <returns>是否获取成功</returns>
    public bool TryGetNetworkProxy<T>(ulong networkObjectId, out T proxy) where T : class, INetworkProxy
    {
        proxy = null;
        if (_networkObjectMap.TryGetValue(networkObjectId, out var data))
        {
            proxy = data.NetworkProxy as T;
            return proxy != null;
        }
        return false;
    }

    /// <summary>
    /// 通过网络对象ID尝试获取逻辑对象
    /// </summary>
    /// <param name="networkObjectId">网络对象ID</param>
    /// <param name="logicObject">输出的逻辑对象</param>
    /// <returns>是否获取成功</returns>
    public bool TryGetLogicObject<T>(ulong networkObjectId, out T logicObject) where T : class, ILogicObject
    {
        logicObject = null;
        if (_networkObjectMap.TryGetValue(networkObjectId, out var data))
        {
            logicObject = data.LogicObject as T;
            return logicObject != null;
        }
        return false;
    }

    /// <summary>
    /// 通过网络对象ID尝试获取渲染对象
    /// </summary>
    /// <param name="networkObjectId">网络对象ID</param>
    /// <param name="renderObject">输出的渲染对象</param>
    /// <returns>是否获取成功</returns>
    public bool TryGetRenderObject<T>(ulong networkObjectId, out T renderObject) where T : class, IRenderObject
    {
        renderObject = null;
        if (_networkObjectMap.TryGetValue(networkObjectId, out var data))
        {
            renderObject = data.RenderObject as T;
            return renderObject != null;
        }
        return false;
    }

    public void RegisterNetworkObject(ulong networkObjectId)
    {
        //通过网络id拿到对应的NetworkObject
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out var networkObject))
        {
            //若成功拿到，则注册进字典中进行缓存
            var networkObjectData = new NetworkObjectData(
                networkObject,
                networkObject.GetComponent<INetworkProxy>(),
                networkObject.GetComponent<ILogicObject>(),
                networkObject.GetComponent<IRenderObject>()
                );

            _networkObjectMap[networkObjectId] = networkObjectData;
        }
        else
        {
            Debug.LogError("网络Id为" + networkObjectId + "的网络对象在尝试获取NetworkObject失败");
        }
    }

    public void UnregisterNetworkObject(ulong networkObjectId)
    {
        _networkObjectMap.Remove(networkObjectId);
    }

    #endregion

    #region 私有方法

    

    #endregion
    
    
}
