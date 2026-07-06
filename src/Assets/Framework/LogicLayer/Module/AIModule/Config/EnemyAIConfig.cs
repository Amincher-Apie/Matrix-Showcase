using Framework.LogicLayer.Module.AIModule.Movement.Boids;
using UnityEngine;

/// <summary>
/// 敌人 AI 配置。
/// 该配置描述单个敌人在待机、巡逻、感知、追击、路径和攻击阶段使用的核心参数。
/// </summary>
[CreateAssetMenu(fileName = "EnemyAIConfig", menuName = "Matrix/AI/Enemy AI Config", order = 1)]
public class EnemyAIConfig : ScriptableObject
{
    [Header("基础设置")]
    [Tooltip("是否启用巡逻。")]
    public bool enablePatrol = true;

    [Tooltip("是否在出生后直接进入巡逻，否则先进入待机。")]
    public bool startWithPatrol = false;

    [Tooltip("默认移动速度。若属性模块没有提供速度，则回退到该值。")]
    public float defaultMoveSpeed = 3f;

    [Header("待机设置")]
    [Tooltip("最小待机时间，单位为秒。")]
    public float idleMinDuration = 2f;

    [Tooltip("最大待机时间，单位为秒。")]
    public float idleMaxDuration = 5f;

    [Header("巡逻设置")]
    [Tooltip("巡逻半径。")]
    public float patrolRadius = 10f;

    [Tooltip("到达巡逻点的判定距离。")]
    public float patrolPointReachDistance = 1f;

    [Header("感知设置")]
    [Tooltip("敌人的基础感知半径。")]
    public float detectionRange = 5f;

    [Tooltip("是否使用视野角筛选目标。")]
    public bool useFieldOfView = true;

    [Tooltip("视野角度，单位为度。")]
    [Range(0f, 360f)]
    public float fieldOfViewAngle = 120f;

    [Tooltip("是否检查视线遮挡。")]
    public bool checkLineOfSight = true;

    [Tooltip("用于视线检测的障碍物层遮罩。")]
    public LayerMask obstacleLayerMask = -1;

    [Tooltip("感知更新间隔，单位为帧数。")]
    [Min(1)]
    public int perceptionUpdateInterval = 5;

    [Header("感知评分设置")]
    [Tooltip("目标威胁优先级在感知评分中的权重。")]
    public float perceptionThreatScoreWeight = 1000f;

    [Tooltip("目标距离在感知评分中的权重。数值越大，距离越近的目标越容易被选中。")]
    public float perceptionDistanceScoreWeight = 1f;

    [Header("目标记忆设置")]
    [Tooltip("目标丢失后仍保留记忆的时间，单位为秒。")]
    public float targetMemoryDuration = 2.5f;

    [Tooltip("选定目标后的锁定时间，单位为秒。冷却内不会因重新感知而频繁切换目标。")]
    public float targetSwitchCooldown = 5f;

    [Tooltip("敌人接近最后目标点后，判定为已到达该位置的距离。")]
    public float lastKnownTargetReachDistance = 1f;

    [Header("路径设置")]
    [Tooltip("当房间或区块拓扑服务可用时，是否优先使用拓扑路径。")]
    public bool preferTopologyPath = true;

    [Tooltip("当拓扑路径暂时不可用时，是否允许退化为直接朝目标点移动。")]
    public bool allowDirectPathFallback = true;

    [Tooltip("同区域内的直接移动距离阈值。当敌人在同一区域内且与目标距离小于等于此值时，直接直线移动；大于此值时走拓扑路径。")]
    public float sameRegionDirectThreshold = 5f;

    [Header("群体移动修正")]
    [Tooltip("是否启用服务端群体移动修正。启用后会在 AI 输出移动意图时叠加简单的分离与避障修正。")]
    public bool enableSteeringCorrection = true;

    [Tooltip("用于搜索周围其他敌人的邻居半径。")]
    public float steeringNeighborRadius = 2.5f;

    [Tooltip("当其他敌人进入该半径时，会开始施加分离修正。")]
    public float steeringSeparationRadius = 1.2f;

    [Tooltip("分离修正的权重。值越大，敌人越倾向于主动拉开距离。")]
    public float steeringSeparationWeight = 1.5f;

    [Tooltip("群体移动修正使用的空间桶尺寸。值越大，桶更粗；值越小，桶更多但邻居筛选更精细。")]
    public float steeringSpatialBucketSize = 3f;

    [Tooltip("当 PCG 房间拓扑服务可用时，是否只对同房间敌人施加群体修正。")]
    public bool steeringLimitToSameRegionWhenPossible = true;

    [Tooltip("是否启用简单的前向避障修正。")]
    public bool enableObstacleAvoidance = true;

    [Tooltip("前向避障检测距离。根据敌人碰撞体大小调整，小型敌人可减小，大型敌人应增大。")]
    public float steeringObstacleCheckDistance = 1.5f;

    [Tooltip("左右试探避障方向时使用的偏转角度。")]
    [Range(5f, 85f)]
    public float steeringAvoidanceProbeAngle = 45f;

    [Tooltip("避障修正的权重。值越大，越优先绕开前方障碍。")]
    public float steeringObstacleAvoidanceWeight = 1.5f;

    [Header("追击设置")]
    [Tooltip("最大追击距离。超过该距离后敌人会脱战并返回出生点。")]
    public float maxChaseDistance = 10f;

    [Header("状态切换设置")]
    [Tooltip("状态切换冷却时间。敌人切换状态后，在该时间内不允许再次切换，防止感知抖动导致的状态震荡。")]
    public float stateSwitchCooldown = 0.3f;

    [Tooltip("ReturnState 中是否在目标重新出现时立即切换回追击状态。关闭后，敌人会先回到出生点再重新感知。")]
    public bool returnStateReactToTarget = true;

    [Header("攻击设置")]
    [Tooltip("攻击范围。")]
    public float attackRange = 2f;

    [Tooltip("攻击冷却时间，单位为秒。")]
    public float attackCooldown = 1.5f;

    [Header("调试设置")]
    [Tooltip("是否输出调试信息。")]
    public bool showDebugInfo = false;

    [Tooltip("是否绘制 Gizmos。")]
    public bool drawGizmos = true;

    [Header("Boids 群体行为")]
    [Tooltip("Boids 群体行为配置。若为空，则使用 BoidsCentralController 的全局默认配置。")]
    public BoidsConfig boidsConfig;

    [Tooltip("是否在本敌人上启用 Boids 行为。当全局 BoidsCentralController 未启用或 boidsConfig 为空时，此选项不生效。")]
    public bool enableBoidsBehavior = true;

    [Tooltip("Boids 计算结果与原有 steering 逻辑的混合权重。0 表示完全使用原有 steering，1 表示完全使用 Boids 结果。")]
    [Range(0f, 1f)]
    public float boidsBlendWeight = 0.4f;
}
