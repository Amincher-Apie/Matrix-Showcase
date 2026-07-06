using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 被池化的网络实例：
/// - 仅使用一个父节点 RootParent；
/// - 只在 OnNetworkSpawn 之后进行层级切换，避免 Spawn 前改父级触发异常；
/// - OnNetworkDespawn 回到 RootParent 并禁用。
/// </summary>
[DisallowMultipleComponent]
public class PooledNetworkObject : NetworkBehaviour
{
    [Tooltip("池的唯一父节点（必须带 NetworkObject 脚本）。")]
    public Transform RootParent;

    public override void OnNetworkSpawn()
    {
        // ✅ 不要在 Spawn 时改父级
        // NGO 对 Parent 有约束：父级若是 NetworkObject 还必须是 spawned，
        // 父级若非 NetworkObject 也会引入 parent 同步不一致风险。
        // 因此这里什么都不做。
    }

    public override void OnNetworkDespawn()
    {
        // ✅ 不要在这里改父子层级（这是 NGO 校验最敏感的时刻）
        gameObject.SetActive(false);
    }
}