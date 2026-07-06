// 文件位置: LogicLayer/Services/ClientHitScanService.cs

using System.Collections.Generic;
using UnityEngine;

public class ClientHitScanService : IHitScanService
{
    public List<HitInfo> Raycast(Vector3 origin, Vector3 dir, float maxRange, int pelletCount)
    {
        var hits = new List<HitInfo>();

        // 客户端使用 Physics.RaycastAll 进行预测
        var raycastHits = Physics.RaycastAll(origin, dir, maxRange);

#if UNITY_EDITOR
        Debug.Log($"[ClientHitScanService] RaycastAll count = {raycastHits.Length}, maxRange = {maxRange}");
#endif

        foreach (var hit in raycastHits)
        {
            // ✅ 关键：通过 Proxy 解析目标，避免 PoolRoot / 非网络对象污染
            if (!HitScanTargetResolver.TryResolveTargetId(hit.collider, out ulong targetId))
            {
#if UNITY_EDITOR
                Debug.Log($"[ClientHitScanService] hit {hit.collider.name}, targetId=INVALID (no proxy/no spawned netobj)");
#endif
                continue;
            }

#if UNITY_EDITOR
            Debug.Log($"[ClientHitScanService] hit {hit.collider.name}, targetId={targetId}");
#endif

            hits.Add(new HitInfo
            {
                targetId = targetId,
                point = hit.point,
                normal = hit.normal,
                distance = hit.distance
            });
        }

        return hits;
    }
}