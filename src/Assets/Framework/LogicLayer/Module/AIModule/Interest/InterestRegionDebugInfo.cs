using UnityEngine;

/// <summary>
/// 兴趣热点调试信息。
/// 该结构用于对外暴露当前服务端热点的来源、半径、剩余时间和调试标签，方便联机验证热点是否按预期生效。
/// </summary>
public struct InterestRegionDebugInfo
{
    /// <summary>
    /// 热点唯一 ID。
    /// </summary>
    public int id;

    /// <summary>
    /// 热点来源类型。
    /// </summary>
    public InterestRegionSourceType sourceType;

    /// <summary>
    /// 热点来源对象 ID。
    /// </summary>
    public ulong sourceObjectId;

    /// <summary>
    /// 热点中心点。
    /// </summary>
    public Vector3 center;

    /// <summary>
    /// 热点半径。
    /// </summary>
    public float radius;

    /// <summary>
    /// 热点到期时间戳。
    /// </summary>
    public float expireAt;

    /// <summary>
    /// 热点剩余有效时长，单位为秒。
    /// </summary>
    public float remainingTime;

    /// <summary>
    /// 调试标签。
    /// 用于标记该热点在设计层面的用途，例如 Combat、TaskEscort、Wave01 等。
    /// </summary>
    public string debugTag;
}
