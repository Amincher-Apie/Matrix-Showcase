using Unity.Netcode;
using UnityEngine;
using Matrix.Missions;

/// <summary>
/// 将 Raycast 命中的 Collider 解析到真正可结算的 Network 目标：
/// Collider -> (Parent chain) NetworkProxyBase -> same GO NetworkObject -> NetworkObjectId
///
/// 这样可以避免命中到对象池 PoolRoot（即使 PoolRoot 有 NetworkObject），
/// 因为 PoolRoot 通常不会有 NetworkProxyBase。
/// </summary>
public static class HitScanTargetResolver
{
    public static bool TryResolveTargetId(Collider col, out ulong targetId)
    {
        targetId = 0;
        if (col == null) return false;

        // 1) 先找最近的 NetworkProxyBase（战斗结算真正依赖它）
        var proxy = col.GetComponentInParent<NetworkProxyBase>();
        if (proxy == null)
        {
            return TryResolveMissionObjectiveId(col, out targetId);
        }

        // 2) 再取同节点上的 NetworkObject
        var no = proxy.GetComponent<NetworkObject>();
        if (no == null) return false;

        // 3) 必须是已 Spawn 的网络对象（避免拿到没 Spawn 的管理节点）
        if (!no.IsSpawned) return false;

        // 4) 必须是有效 id
        targetId = no.NetworkObjectId;
        return targetId != 0;
    }

    private static bool TryResolveMissionObjectiveId(Collider col, out ulong targetId)
    {
        targetId = 0;

        var objective = col.GetComponentInParent<DefenseObjective>();
        if (objective != null)
        {
            return TryResolveNetworkObjectId(objective.GetComponent<NetworkObject>(), out targetId);
        }

        var damageableTarget = col.GetComponentInParent<MissionDamageableTarget>();
        if (damageableTarget == null)
        {
            return false;
        }

        return TryResolveNetworkObjectId(damageableTarget.GetComponent<NetworkObject>(), out targetId);
    }

    private static bool TryResolveNetworkObjectId(NetworkObject no, out ulong targetId)
    {
        targetId = 0;

        if (no == null || !no.IsSpawned)
        {
            return false;
        }

        targetId = no.NetworkObjectId;
        return targetId != 0;
    }
}
