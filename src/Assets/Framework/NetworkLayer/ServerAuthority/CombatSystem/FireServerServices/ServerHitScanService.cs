using System.Collections.Generic;
using UnityEngine;

public class ServerHitScanService : IHitScanService
{
    public List<HitInfo> Raycast(Vector3 origin, Vector3 dir, float maxRange, int pelletCount)
    {
        var hits = new List<HitInfo>();

        // 服务器使用 Physics.RaycastAll 进行权威检测
        var raycastHits = Physics.RaycastAll(origin, dir, maxRange);

        foreach (var hit in raycastHits)
        {
            // ✅ 关键：通过 Proxy 解析目标，避免 PoolRoot / 非网络对象污染
            if (!HitScanTargetResolver.TryResolveTargetId(hit.collider, out ulong targetId))
                continue;

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