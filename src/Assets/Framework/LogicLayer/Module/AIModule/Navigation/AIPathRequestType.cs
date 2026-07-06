/// <summary>
/// AI 路径请求类型。
/// 该枚举用于标记本次路径请求属于巡逻、追击、回归还是调查，
/// 便于后续在路径服务中按不同策略做拓扑路径、代价修正或调试统计。
/// </summary>
public enum AIPathRequestType
{
    /// <summary>
    /// 未知请求类型。
    /// 当外部未显式声明路径用途时使用，主要用于兼容和调试。
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// 巡逻路径请求。
    /// 适用于 PatrolState 这类围绕出生点或巡逻点移动的行为。
    /// </summary>
    Patrol = 1,

    /// <summary>
    /// 追击路径请求。
    /// 适用于 ChaseState 朝当前目标或最后已知位置推进的行为。
    /// </summary>
    Chase = 2,

    /// <summary>
    /// 回归路径请求。
    /// 适用于 ReturnState 返回出生点、警戒点或刷新点的行为。
    /// </summary>
    Return = 3,

    /// <summary>
    /// 调查路径请求。
    /// 预留给后续“听见声音”“调查热点”“搜索最后目击点”等行为。
    /// </summary>
    Investigate = 4
}
