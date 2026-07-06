using UnityEngine;
using Unity.Netcode;

/// <summary>
/// 网络代理基类，处理所有网络相关逻辑
/// </summary>
public abstract class NetworkProxyBase : NetworkBehaviour, INetworkProxy
{
    /// <summary>
    /// 网络对象ID
    /// </summary>
    public ulong NetworkObjectId => NetworkObject?.NetworkObjectId ?? 0;
    
    /// <summary>
    /// 是否为服务器
    /// </summary>
    public bool IsServer => NetworkManager.Singleton?.IsServer ?? false;
    
    /// <summary>
    /// 是否为客户端
    /// </summary>
    public bool IsClient => NetworkManager.Singleton?.IsClient ?? false;
    
    /// <summary>
    /// 是否为拥有者
    /// </summary>
    public bool IsOwner => NetworkObject?.IsOwner ?? false;
    
    /// <summary>
    /// 关联的逻辑对象
    /// </summary>
    protected ILogicObject _logicObject;
    
    /// <summary>
    /// 设置关联的逻辑对象
    /// </summary>
    public void SetLogicObject(ILogicObject logicObject)
    {
        _logicObject = logicObject;
    }
    
    /// <summary>
    /// NGO网络生成回调
    /// </summary>
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        Debug.Log($"NetworkProxyBase: 网络对象 {NetworkObjectId} 生成");
        NetworkObjectManager.Instance.RegisterNetworkObject(NetworkObjectId);
    }
    
    /// <summary>
    /// NGO网络销毁回调
    /// </summary>
    public override void OnNetworkDespawn()
    {
        Debug.Log($"NetworkProxyBase: 网络对象 {NetworkObjectId} 销毁");
        if (NetworkObjectManager.Instance != null)
        {
            NetworkObjectManager.Instance.UnregisterNetworkObject(NetworkObjectId);
        }
        base.OnNetworkDespawn();
    }
    
    public abstract T GetServerAttributeModule<T>() where T : ServerAttributeModule;
    
    public abstract T GetServerWeaponRuntime<T>() where T : ServerWeaponRuntime;
    
    public abstract T GetServerCombatModule<T>() where T : ServerCombatModule;
    
}
