/// <summary>
/// 可被敌人感知与攻击的对象类型。
/// 当前阶段先覆盖玩家，并为后续任务目标、建筑与护送目标预留枚举位。
/// </summary>
public enum AttackableObjectType
{
    /// <summary>
    /// 未知类型。
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// 玩家对象。
    /// </summary>
    Player = 1,

    /// <summary>
    /// 任务目标对象。
    /// </summary>
    MissionTarget = 2,

    /// <summary>
    /// 建筑对象。
    /// </summary>
    Building = 3,

    /// <summary>
    /// 护送目标对象。
    /// </summary>
    EscortTarget = 4
}
