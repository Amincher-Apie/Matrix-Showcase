/// <summary>
/// 兴趣热点来源类型。
/// 该枚举用于标记热点是由战斗、任务、刷怪还是脚本逻辑产生，方便后续做调试展示与差异化处理。
/// </summary>
public enum InterestRegionSourceType
{
    /// <summary>
    /// 未知来源。
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// 战斗事件产生的热点。
    /// </summary>
    Combat = 1,

    /// <summary>
    /// 任务系统产生的热点。
    /// </summary>
    Task = 2,

    /// <summary>
    /// 刷怪系统产生的热点。
    /// </summary>
    Spawn = 3,

    /// <summary>
    /// 其他脚本逻辑主动推送的热点。
    /// </summary>
    Scripted = 4
}
