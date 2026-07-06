/// <summary>
/// AI 服务端仿真级别。
/// 不同级别会映射到不同的状态更新频率，用于多人联网场景下的服务端仿真 LOD。
/// </summary>
public enum AISimulationLevel
{
    /// <summary>
    /// 全速仿真。
    /// 适用于战斗中、热点附近或高关注度敌人。
    /// </summary>
    Full = 0,

    /// <summary>
    /// 降频仿真。
    /// 适用于离战斗较远但仍可能很快被激活的敌人。
    /// </summary>
    Reduced = 1,

    /// <summary>
    /// 低频仿真。
    /// 适用于当前关注度最低的敌人。
    /// </summary>
    Dormant = 2
}
