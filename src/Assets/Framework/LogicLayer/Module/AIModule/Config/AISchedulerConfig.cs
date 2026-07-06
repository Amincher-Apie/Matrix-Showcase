using UnityEngine;

/// <summary>
/// 服务端 AI 调度配置。
/// 该配置用于统一描述多人联网场景下敌人仿真 LOD 的 Tick 频率、距离阈值与滞后参数。
/// </summary>
[CreateAssetMenu(fileName = "AISchedulerConfig", menuName = "Matrix/AI/AI Scheduler Config", order = 2)]
public class AISchedulerConfig : ScriptableObject
{
    /// <summary>
    /// 默认的 Resources 加载路径。
    /// 当前服务端调度器会优先从该路径加载配置资源，若缺失则回退到代码默认值。
    /// </summary>
    public const string DefaultResourcePath = "Configs/AI/AIScheduler_Default";

    [Header("Tick 间隔")]
    [Tooltip("Full 级别下的服务端 AI Tick 间隔，单位为秒。")]
    [Min(0.02f)]
    public float fullTickInterval = 0.1f;

    [Tooltip("Reduced 级别下的服务端 AI Tick 间隔，单位为秒。")]
    [Min(0.02f)]
    public float reducedTickInterval = 0.25f;

    [Tooltip("Dormant 级别下的服务端 AI Tick 间隔，单位为秒。")]
    [Min(0.02f)]
    public float dormantTickInterval = 0.75f;

    [Header("级别切换")]
    [Tooltip("仿真级别切换前的最短保持时间，用于避免边界抖动。")]
    [Min(0f)]
    public float minLevelHoldDuration = 2f;

    [Tooltip("进入战斗后维持 Full 级别的锁定时间，单位为秒。")]
    [Min(0f)]
    public float combatLockDuration = 4f;

    [Tooltip("敌人在最近受击或造成伤害后，仍被视为活跃的时间窗口，单位为秒。")]
    [Min(0f)]
    public float recentDamageMemoryDuration = 4f;

    [Header("距离阈值")]
    [Tooltip("进入 Full 级别的距离阈值。")]
    [Min(0f)]
    public float fullEnterDistance = 18f;

    [Tooltip("退出 Full 级别的距离阈值，应大于进入距离以形成滞后区间。")]
    [Min(0f)]
    public float fullExitDistance = 24f;

    [Tooltip("进入 Reduced 级别的距离阈值。")]
    [Min(0f)]
    public float reducedEnterDistance = 36f;

    [Tooltip("退出 Reduced 级别的距离阈值，应大于进入距离以形成滞后区间。")]
    [Min(0f)]
    public float reducedExitDistance = 44f;

    [Header("兴趣区查询")]
    [Tooltip("敌人在查询兴趣热点时附加的检测半径。")]
    [Min(0f)]
    public float interestQueryRadius = 12f;

    [Header("战斗热点")]
    [Tooltip("发生受击事件后，在服务端兴趣区里生成或刷新的热点半径。")]
    [Min(0f)]
    public float combatHotspotRadius = 10f;

    [Tooltip("发生受击事件后，在服务端兴趣区里生成或刷新的热点持续时间，单位为秒。")]
    [Min(0f)]
    public float combatHotspotDuration = 5f;

    [Header("调试")]
    [Tooltip("是否在服务端输出 AI 仿真级别切换日志。")]
    public bool logLevelChanges = false;

    /// <summary>
    /// 加载调度配置；若资源缺失则创建默认实例。
    /// 这样做可以保证当前阶段无需额外场景注入，也不会因为资源未创建而导致服务端 AI 停摆。
    /// </summary>
    /// <returns>返回可用的服务端 AI 调度配置实例。</returns>
    public static AISchedulerConfig LoadOrCreate()
    {
        var config = Resources.Load<AISchedulerConfig>(DefaultResourcePath);
        if (config != null)
        {
            return config;
        }

        return CreateInstance<AISchedulerConfig>();
    }
}
