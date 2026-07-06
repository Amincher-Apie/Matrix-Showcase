/// <summary>
/// AI 路径查询结果类型。
/// 该枚举用于描述路径服务本次返回的是已到达、直接移动、拓扑移动还是失败，
/// 便于上层状态机和调试面板快速理解当前结果来源。
/// </summary>
[System.Obsolete("AIPathResultType 已废弃。寻路改由 NavMeshAgent 负责。")]
public enum AIPathResultType
{
    /// <summary>
    /// 无结果。
    /// 表示本次路径查询没有得到可用的移动输出。
    /// </summary>
    None = 0,

    /// <summary>
    /// 已经到达目标。
    /// 上层通常应停止移动，转入攻击、待机或切换下一个路径点。
    /// </summary>
    Arrived = 1,

    /// <summary>
    /// 直接朝目标点移动。
    /// 这是当前最小实现的主要返回模式，用于兼容现有直线位移行为。
    /// </summary>
    DirectMove = 2,

    /// <summary>
    /// 使用拓扑路径得到的中间移动点。
    /// 该模式用于后续房间/区块级路径与局部移动分层接入。
    /// </summary>
    TopologyMove = 3,

    /// <summary>
    /// 路径查询失败。
    /// 表示既无法使用拓扑路径，也不允许或无法退化为直接移动。
    /// </summary>
    Failed = 4,

    /// <summary>
    /// 使用 NavMesh 路径得到的移动点。
    /// 由运行时烘焙的 NavMesh 查询返回，绕障质量最高。
    /// </summary>
    NavMeshMove = 5
}
