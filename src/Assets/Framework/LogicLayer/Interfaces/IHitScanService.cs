using System.Collections.Generic;

public interface IHitScanService
{
    // 返回沿线命中（可带穿透/分箱过滤）
    List<HitInfo> Raycast(UnityEngine.Vector3 origin, UnityEngine.Vector3 dir, float maxRange, int pelletCount);
}

public struct HitInfo
{
    public ulong targetId;
    public UnityEngine.Vector3 point;
    public UnityEngine.Vector3 normal;
    public float distance;
}