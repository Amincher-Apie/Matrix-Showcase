using Unity.Netcode;

/// <summary>
/// 网络代理接口，定义网络层与逻辑层的通信契约
/// </summary>
public interface INetworkProxy
{
    /// <summary>
    /// 网络对象ID
    /// </summary>
    ulong NetworkObjectId { get; }
    
    /// <summary>
    /// 是否为服务器
    /// </summary>
    bool IsServer { get; }
    
    /// <summary>
    /// 是否为客户端
    /// </summary>
    bool IsClient { get; }
    
    /// <summary>
    /// 是否为拥有者
    /// </summary>
    bool IsOwner { get; }
}