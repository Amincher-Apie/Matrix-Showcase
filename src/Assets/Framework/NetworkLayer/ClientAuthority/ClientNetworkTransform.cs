using Unity.Netcode.Components;
using UnityEngine;

/// <summary>
/// 客户端权威的 NetworkTransform：拥有者可提交 Transform，同步给服务器与其他客户端。
/// </summary>
[DisallowMultipleComponent]
public class ClientNetworkTransform : NetworkTransform
{
    /// <summary>
    /// Used to determine who can write to this transform. Owner client only.
    /// This imposes state to the server. This is putting trust on your clients. Make sure no security-sensitive features use this transform.
    /// </summary>
    protected override bool OnIsServerAuthoritative()
    {
        return false;
    }
}
