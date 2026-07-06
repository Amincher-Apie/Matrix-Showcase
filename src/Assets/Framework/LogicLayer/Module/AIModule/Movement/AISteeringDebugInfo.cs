using UnityEngine;

/// <summary>
/// AI 群体移动修正调试信息。
/// 该结构体仅用于开发期观察 steering 是否生效，不参与任何权威决策与同步逻辑。
/// </summary>
public struct AISteeringDebugInfo
{
    /// <summary>
    /// 本次修正前的原始期望方向。
    /// </summary>
    public Vector3 desiredDirection;

    /// <summary>
    /// 本次修正后的最终输出方向。
    /// </summary>
    public Vector3 finalDirection;

    /// <summary>
    /// 本次邻居筛选后命中的敌人数。
    /// </summary>
    public int neighborCount;

    /// <summary>
    /// 本次是否启用了空间桶查询。
    /// </summary>
    public bool usedSpatialBuckets;

    /// <summary>
    /// 本次是否启用了同房间过滤。
    /// </summary>
    public bool usedSameRegionFilter;

    /// <summary>
    /// 本次实际施加的分离方向。
    /// </summary>
    public Vector3 separationDirection;

    /// <summary>
    /// 本次实际施加的避障方向。
    /// </summary>
    public Vector3 obstacleAvoidanceDirection;

    /// <summary>
    /// 本次前向检测是否命中了障碍。
    /// </summary>
    public bool obstacleDetected;

    /// <summary>
    /// 本次调试快照生成时间。
    /// </summary>
    public float lastUpdateTime;
}
