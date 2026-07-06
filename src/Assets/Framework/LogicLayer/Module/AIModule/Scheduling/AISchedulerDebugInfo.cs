/// <summary>
/// 服务端 AI 调度调试信息。
/// 该结构用于向外部暴露某个敌人当前的仿真级别、判定依据与关键时间戳，方便联机调试与后续状态同步。
/// </summary>
public struct AISchedulerDebugInfo
{
    /// <summary>
    /// 当前敌人是否已经注册进调度器。
    /// </summary>
    public bool isRegistered;

    /// <summary>
    /// 当前实际生效的仿真级别。
    /// </summary>
    public AISimulationLevel currentLevel;

    /// <summary>
    /// 最近一次评估期望切换到的目标级别。
    /// </summary>
    public AISimulationLevel desiredLevel;

    /// <summary>
    /// 当前级别映射出的服务端 Tick 间隔，单位为秒。
    /// </summary>
    public float currentTickInterval;

    /// <summary>
    /// 最近一次评估时记录的最近可攻击目标距离。
    /// 若为正无穷则表示当前没有可用候选目标。
    /// </summary>
    public float nearestTargetDistance;

    /// <summary>
    /// 最近一次评估时是否命中了附近兴趣热点。
    /// </summary>
    public bool hasNearbyInterest;

    /// <summary>
    /// 最近一次评估时是否被视为处于战斗中。
    /// </summary>
    public bool isInCombat;

    /// <summary>
    /// 最近一次评估时是否被视为高优先级敌人。
    /// </summary>
    public bool isPriorityEnemy;

    /// <summary>
    /// 最近一次评估时是否命中“最近受击”窗口。
    /// </summary>
    public bool wasRecentlyDamaged;

    /// <summary>
    /// 最近一次级别切换发生的时间戳。
    /// </summary>
    public float lastLevelChangeTime;

    /// <summary>
    /// 当前战斗锁定截至时间。
    /// 在该时间之前，敌人会倾向保持较高仿真级别。
    /// </summary>
    public float combatLockUntil;

    /// <summary>
    /// 最近一次受击或造成伤害时记录的时间戳。
    /// </summary>
    public float lastDamagedTime;

    /// <summary>
    /// 最近一次调度评估命中的主要判定原因。
    /// 该字段用于快速解释当前仿真级别为何成立，避免调试时只能手动比对多个布尔条件。
    /// </summary>
    public string decisionReason;
}
