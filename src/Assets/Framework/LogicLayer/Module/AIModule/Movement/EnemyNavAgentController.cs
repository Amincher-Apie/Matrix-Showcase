using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace Framework.LogicLayer.Module.AIModule.Movement
{
    /// <summary>
    /// 敌人 NavMeshAgent 控制器。
    ///
    /// 职责：
    /// 1. 包装 NavMeshAgent，只在服务端启用 Agent
    /// 2. 提供 SetDestinationThrottled()，避免每帧 SetDestination
    /// 3. 提供 Stop()、HasArrived()、TryWarpToNavMesh()
    /// 4. 根据 AIScheduler 的仿真级别动态调整 repath 频率
    /// 5. 将 Agent 速度同步给 Animator
    /// 6. 客户端禁用 NavMeshAgent，只做插值和动画
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class EnemyNavAgentController : NetworkBehaviour
    {
        [Header("Agent Settings")]
        [Tooltip("引用的 NavMeshAgent 组件。")]
        [SerializeField]
        private NavMeshAgent agent;

        [Tooltip("Agent 速度（默认使用 AI 配置的 defaultMoveSpeed）。")]
        [SerializeField]
        private float agentSpeed = 3f;

        [Tooltip("Agent 角速度（转身速度）。")]
        [SerializeField]
        private float agentAngularSpeed = 360f;

        [Header("Throttling")]
        [Tooltip("两次 SetDestination 之间的最小间隔（秒）。降低服务端压力。")]
        [SerializeField]
        private float repathInterval = 0.25f;

        [Tooltip("目标位置变化小于此距离时，不触发重新寻路。")]
        [SerializeField]
        private float destinationChangeThreshold = 0.5f;

        [Header("Arrival")]
        [Tooltip("Agent 判定为已到达的剩余距离阈值（米）。")]
        [SerializeField]
        private float arrivalDistance = 0.3f;

        [Header("Teleport Recovery")]
        [Tooltip("当 Agent 不在 NavMesh 上时，是否自动 Warp 到最近可走面。")]
        [SerializeField]
        private bool autoWarpOnStartup = true;

        [Tooltip("Warp 时使用的最大采样距离（米）。")]
        [SerializeField]
        private float warpSampleDistance = 3f;

        [Header("LOD - Interval Overrides")]
        [Tooltip("Full 仿真级别时的 repath 间隔（秒）。")]
        [SerializeField]
        private float fullRepathInterval = 0.15f;

        [Tooltip("Reduced 仿真级别时的 repath 间隔（秒）。")]
        [SerializeField]
        private float reducedRepathInterval = 0.75f;

        [Tooltip("Dormant 仿真级别时的 repath 间隔（秒）。为 0 表示暂停寻路。")]
        [SerializeField]
        private float dormantRepathInterval = 0f;

        [Header("Stuck Detection")]
        [Tooltip("Agent 在持续多长时间（秒）无有效位移后判定为卡死。")]
        [SerializeField]
        private float stuckDetectionTime = 2.0f;

        [Tooltip("卡死判定期间，Agent 的最小位移量（米）。低于此值视为停滞。")]
        [SerializeField]
        private float stuckMinMovement = 0.15f;

        [Tooltip("两次卡死检测的间隔（秒）。")]
        [SerializeField]
        private float stuckCheckInterval = 0.5f;

        [Tooltip("卡死恢复时对目标位置的随机偏移半径（米），打破多 Agent 对称死锁。")]
        [SerializeField]
        private float stuckRecoveryJitterRadius = 1.5f;

        [Header("Crowd Flow")]
        [SerializeField]
        private bool enableCrowdFlowControl = true;

        [SerializeField]
        private float queueProbeDistance = 1.35f;

        [SerializeField]
        private float queueResumeDistance = 1.75f;

        [SerializeField]
        [Range(1f, 90f)]
        private float queueProbeAngle = 40f;

        [SerializeField]
        private float narrowAreaEdgeDistance = 1.1f;

        [SerializeField]
        private float queueSameDestinationTolerance = 3f;

        [SerializeField]
        private float queueReleaseDelay = 0.15f;

        [SerializeField]
        [Range(0, 99)]
        private int avoidancePriorityMin = 20;

        [SerializeField]
        [Range(0, 99)]
        private int avoidancePriorityMax = 80;

        [Header("Animation Sync")]
        [Tooltip("是否将 Agent.velocity 同步给 Animator 的 Speed 参数。")]
        [SerializeField]
        private bool syncVelocityToAnimator = true;

        [Tooltip("Animator 的 Speed 参数路径（为空则不设置）。")]
        [SerializeField]
        private string animatorSpeedParameter = "Speed";

        [Header("NavMeshLink Trace")]
        [Tooltip("启用 NavMeshLink 穿越追踪日志（用于诊断跨房间寻路问题）。")]
        [SerializeField]
        private bool enableLinkTrace = true;

        [Tooltip("路径拐角追踪的最小间隔（秒），避免刷屏。")]
        [SerializeField]
        private float linkTraceInterval = 1.5f;

        [Header("Debug")]
        [Tooltip("是否输出详细调试日志。")]
        [SerializeField]
        private bool verboseLog;

        private float _nextRepathTime;
        private Vector3 _lastDestination = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        private float _currentRepathInterval;
        private AISimulationLevel _lastSimulationLevel = AISimulationLevel.Full;
        private Animator _animator;
        private Vector3 _smoothVelocity;
        private float _smoothSpeed;

        // 卡死检测状态
        private float _stuckTimer;
        private Vector3 _stuckCheckPosition;
        private float _lastStuckCheckTime;
        private bool _isInStuckRecovery;
        private float _stuckRecoveryEndTime;
        private bool _isQueuedByCrowd;
        private float _queueReleaseTime;
        private int _baseAvoidancePriority = 50;
        private bool _isDead;
        private float _activeStoppingDistance;
        private Collider[] _cachedColliders;
        private bool[] _cachedColliderEnabledStates;
        private Rigidbody[] _cachedRigidbodies;
        private bool[] _cachedRigidbodyCollisionStates;
        private bool[] _cachedRigidbodyGravityStates;
        private Rigidbody _rootRigidbody;
        private bool _deathCollisionSuppressed;

        // NavMeshLink 追踪状态
        private float _lastLinkTraceTime;
        private bool _wasOnOffMeshLink;
        private bool _tracePathNextFrame;
        private Vector3 _lastTracedDestination;

        private static readonly List<EnemyNavAgentController> ActiveServerControllers =
            new List<EnemyNavAgentController>(64);

        public bool IsOnNavMesh => agent != null && agent.isOnNavMesh;

        public Vector3 Velocity => agent != null ? agent.velocity : Vector3.zero;

        public float RemainingDistance =>
            agent != null && agent.enabled ? agent.remainingDistance : float.PositiveInfinity;

        public override void OnNetworkSpawn()
        {
            if (agent == null)
                agent = GetComponent<NavMeshAgent>();

            ResetRuntimeState();
            RestoreCollisionState();
            ConfigureRootRigidbodyForNavAgent(true);
            ResetAnimatorParameters();

            if (IsServer)
            {
                agent.enabled = true;
                InitializeAgent();
                RegisterActiveServerController();
                EventCenter.Instance.AddListener<UnitDiedEvt>(EventName.UnitDied, OnUnitDied);
            }
            else
            {
                agent.enabled = false;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer)
            {
                EventCenter.Instance.RemoveListener<UnitDiedEvt>(EventName.UnitDied, OnUnitDied);
            }
            UnregisterActiveServerController();
            ReleaseCrowdQueue();
            base.OnNetworkDespawn();
        }

        private void ResetRuntimeState()
        {
            _nextRepathTime = 0f;
            _lastDestination = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            _currentRepathInterval = repathInterval;
            _lastSimulationLevel = AISimulationLevel.Full;
            _smoothVelocity = Vector3.zero;
            _smoothSpeed = 0f;
            _stuckTimer = 0f;
            _stuckCheckPosition = transform.position;
            _lastStuckCheckTime = Time.time;
            _isInStuckRecovery = false;
            _stuckRecoveryEndTime = 0f;
            _isQueuedByCrowd = false;
            _queueReleaseTime = 0f;
            _isDead = false;
            _activeStoppingDistance = arrivalDistance;
            _deathCollisionSuppressed = false;
        }

        private void ResetAnimatorParameters()
        {
            var anim = GetComponent<Animator>();
            if (anim == null)
                return;

            anim.SetFloat("Speed", 0f);
            anim.SetBool("IsDead", false);
            anim.ResetTrigger("Attack");
            anim.ResetTrigger("Hit");
            anim.ResetTrigger("Die");
        }

        private void ConfigureRootRigidbodyForNavAgent(bool snapToTransform)
        {
            if (_rootRigidbody == null)
                _rootRigidbody = GetComponent<Rigidbody>();

            if (_rootRigidbody == null)
                return;

            if (!_rootRigidbody.isKinematic)
            {
                _rootRigidbody.velocity = Vector3.zero;
                _rootRigidbody.angularVelocity = Vector3.zero;
                _rootRigidbody.isKinematic = true;
            }

            _rootRigidbody.useGravity = false;

            if (snapToTransform)
            {
                _rootRigidbody.position = transform.position;
                _rootRigidbody.rotation = transform.rotation;
            }
        }

        private void InitializeAgent()
        {
            if (agent == null)
                return;

            agent.speed = agentSpeed;
            agent.angularSpeed = agentAngularSpeed;
            agent.acceleration = 8f;
            agent.stoppingDistance = _activeStoppingDistance;
            agent.isStopped = false;

            // 启用高质量避障，让 NavMeshAgent 在多 Agent 场景中互相绕开
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
            agent.autoRepath = true;
            agent.autoTraverseOffMeshLink = true;
            ApplyStableAvoidancePriority();

            if (agent.isOnNavMesh)
            {
                agent.ResetPath();
                agent.velocity = Vector3.zero;
            }

            if (verboseLog)
                AIDebug.LogChannel("AI.Navigation", $"[EnemyNavAgentController] InitializeAgent: speed={agentSpeed}, angularSpeed={agentAngularSpeed}, avoidance={agent.obstacleAvoidanceType}, isOnNavMesh={agent.isOnNavMesh}", this);

            if (autoWarpOnStartup)
            {
                TryWarpToNavMesh();
            }

            _animator = GetComponent<Animator>();
            _stuckCheckPosition = transform.position;
            _lastStuckCheckTime = Time.time;
        }

        /// <summary>
        /// 动态更新 Agent 速度。
        /// 用于与 AI 配置或属性模块的速度保持同步。
        /// </summary>
        /// <param name="speed">新的速度值。</param>
        public void SetSpeed(float speed)
        {
            if (agent == null)
                return;

            agentSpeed = Mathf.Max(0f, speed);
            agent.speed = agentSpeed;
        }

        public void SetStoppingDistance(float distance)
        {
            _activeStoppingDistance = Mathf.Max(arrivalDistance, distance);
            if (agent != null && agent.enabled)
            {
                agent.stoppingDistance = _activeStoppingDistance;
            }
        }

        /// <summary>
        /// 节流版 SetDestination。
        /// 只有当 repath 冷却已过且目标位置发生足够变化时才真正调用 agent.SetDestination()。
        /// </summary>
        /// <param name="target">目标世界坐标。</param>
        /// <returns>返回 true 表示本帧已触发实际寻路；返回 false 表示被节流跳过。</returns>
        public bool SetDestinationThrottled(Vector3 target)
        {
            AIDebug.LogChannel("AI.Navigation", $"[EnemyNavAgentController] SetDestinationThrottled 被调用: 目标={target} 当前={transform.position}", this);

            if (_isDead)
                return false;

            if (agent == null || !agent.enabled)
            {
                AIDebug.LogChannel("AI.Navigation", $"[EnemyNavAgentController] SetDestinationThrottled 失败: agent为空或未启用 agent={(agent != null)} enabled={agent?.enabled}", this);
                return false;
            }

            if (!agent.isOnNavMesh)
            {
                AIDebug.LogChannel("AI.Navigation", $"[EnemyNavAgentController] 不在 NavMesh 上，尝试 Warp。当前位置={transform.position}", this);
                TryWarpToNavMesh();
                return false;
            }

            if (_currentRepathInterval <= 0f)
            {
                AIDebug.LogChannel("AI.Navigation", $"[EnemyNavAgentController] SetDestinationThrottled: repath间隔=0(Dormant模式)，ResetPath并返回false", this);
                agent.ResetPath();
                return false;
            }

            if (Time.time < _nextRepathTime)
            {
                AIDebug.LogChannel("AI.Navigation", $"[EnemyNavAgentController] SetDestinationThrottled: 节流中 _nextRepathTime={_nextRepathTime:F3} Time.time={Time.time:F3}", this);

                if (Vector3.Distance(_lastDestination, target) < destinationChangeThreshold)
                {
                    AIDebug.LogChannel("AI.Navigation", $"[EnemyNavAgentController] SetDestinationThrottled: 节流中(位置未变) 返回false", this);
                    return false;
                }

                if (agent.hasPath && agent.remainingDistance <= arrivalDistance)
                {
                    AIDebug.LogChannel("AI.Navigation", $"[EnemyNavAgentController] SetDestinationThrottled: 节流中(已到达) remaining={agent.remainingDistance:F2} 返回false", this);
                    return false;
                }
            }

            _nextRepathTime = Time.time + _currentRepathInterval;
            _lastDestination = target;

            bool success = agent.SetDestination(target);
            AIDebug.LogChannel("AI.Navigation", $"[EnemyNavAgentController] SetDestination: 目标={target} 成功={success} repath间隔={_currentRepathInterval:F3}s isOnNavMesh={agent.isOnNavMesh}", this);

            if (success && enableLinkTrace)
            {
                _tracePathNextFrame = true;
                _lastTracedDestination = target;
            }

            return success;
        }

        /// <summary>
        /// 设置新的 repath 间隔，用于 LOD 级别切换。
        /// </summary>
        /// <param name="interval">新的间隔时间（秒）。</param>
        public void SetRepathInterval(float interval)
        {
            if (verboseLog)
            {
                AIDebug.LogChannel("AI.Navigation", $"[EnemyNavAgentController] SetRepathInterval: {interval:F3}s", this);
            }
            _currentRepathInterval = Mathf.Max(0f, interval);
        }

        /// <summary>
        /// 根据仿真级别自动调整 repath 频率。
        /// 由 AIScheduler 或 EnemyAIModule 在 ServerTick 时调用。
        /// </summary>
        /// <param name="level">当前目标仿真级别。</param>
        public void ApplySimulationLevel(AISimulationLevel level)
        {
            if (level == _lastSimulationLevel)
                return;

            var previousLevel = _lastSimulationLevel;
            _lastSimulationLevel = level;

            _currentRepathInterval = level switch
            {
                AISimulationLevel.Full => fullRepathInterval,
                AISimulationLevel.Reduced => reducedRepathInterval,
                AISimulationLevel.Dormant => dormantRepathInterval,
                _ => repathInterval
            };

            AIDebug.LogChannel("AI.Navigation", $"[EnemyNavAgentController] LOD 级别切换: {previousLevel} -> {level} repath间隔={_currentRepathInterval:F3}s", this);

            if (level == AISimulationLevel.Dormant && agent != null && agent.enabled && agent.isOnNavMesh)
            {
                ReleaseCrowdQueue();
                agent.ResetPath();
                agent.velocity = Vector3.zero;
            }
        }

        /// <summary>
        /// 停止 Agent 移动。
        /// </summary>
        public void Stop()
        {
            ReleaseCrowdQueue();

            if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            {
                if (verboseLog)
                {
                    AIDebug.LogChannel("AI.Navigation", $"[EnemyNavAgentController] Stop: agent未就绪或不在NavMesh上", this);
                }
                return;
            }

            agent.ResetPath();
            agent.velocity = Vector3.zero;
            if (verboseLog)
            {
                AIDebug.LogChannel("AI.Navigation", $"[EnemyNavAgentController] Stop: 停止移动", this);
            }
        }

        /// <summary>
        /// 判断 Agent 是否已到达目标位置。
        /// </summary>
        /// <param name="distance">自定义到达判定距离（覆盖默认值）。</param>
        /// <returns>返回 true 表示已到达或非常接近目标。</returns>
        public bool HasArrived(float distance = -1f)
        {
            if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            {
                AIDebug.LogChannel("AI.Navigation", $"[EnemyNavAgentController] HasArrived: agent未就绪 agent={agent != null} enabled={agent?.enabled} isOnNavMesh={agent?.isOnNavMesh}", this);
                return false;
            }

            if (agent.pathPending)
            {
                AIDebug.LogChannel("AI.Navigation", $"[EnemyNavAgentController] HasArrived: pathPending=true 返回false", this);
                return false;
            }

            // 如果没有路径，认为尚未到达任何目的地
            if (!agent.hasPath)
            {
                AIDebug.LogChannel("AI.Navigation", $"[EnemyNavAgentController] HasArrived: 无路径(hasPath=false) 返回false", this);
                return false;
            }

            var threshold = distance > 0f ? distance : arrivalDistance;
            var remaining = agent.remainingDistance;
            var arrived = remaining <= threshold;

            // 关键修复：如果 remainingDistance 是 0 或 infinity，说明路径无效（目标不可达或未设置有效路径）
            // 这种情况下不应该认为"已到达"，应该返回 false 并继续尝试寻路
            if (remaining <= 0f || float.IsInfinity(remaining))
            {
                AIDebug.LogChannel("AI.Navigation", $"[EnemyNavAgentController] HasArrived: 路径无效 remaining={remaining} pathStatus={agent.pathStatus} 返回false", this);
                return false;
            }

            AIDebug.LogChannel("AI.Navigation", $"[EnemyNavAgentController] HasArrived: threshold={threshold:F2}m remaining={remaining:F2}m arrived={arrived}", this);

            return arrived;
        }

        /// <summary>
        /// 判断 Agent 当前是否处于空闲（无路径且速度接近零）。
        /// </summary>
        public bool IsIdle()
        {
            if (agent == null || !agent.enabled)
                return true;

            return !agent.hasPath && agent.velocity.sqrMagnitude < 0.01f;
        }

        /// <summary>
        /// 检测 Agent 是否在多 Agent 拥堵中卡死。
        /// 判定条件：有路径且剩余距离较远，但实际位移在 stuckDetectionTime 内低于 stuckMinMovement。
        /// </summary>
        /// <returns>返回 true 表示 Agent 处于卡死状态。</returns>
        public bool IsStuck()
        {
            if (_isQueuedByCrowd)
                return false;

            if (agent == null || !agent.enabled || !agent.isOnNavMesh)
                return false;

            if (!agent.hasPath || agent.pathPending)
                return false;

            // 已接近目标的不算卡死
            if (agent.remainingDistance <= arrivalDistance + 1.0f)
                return false;

            // 按间隔检测位移
            if (Time.time - _lastStuckCheckTime < stuckCheckInterval)
                return _stuckTimer >= stuckDetectionTime;

            var moved = Vector3.Distance(transform.position, _stuckCheckPosition);
            _stuckCheckPosition = transform.position;
            _lastStuckCheckTime = Time.time;

            if (moved < stuckMinMovement)
            {
                _stuckTimer += stuckCheckInterval;
            }
            else
            {
                _stuckTimer = 0f;
            }

            return _stuckTimer >= stuckDetectionTime;
        }

        /// <summary>
        /// 尝试从卡死状态恢复。
        /// 策略：重置路径 → 对目标位置施加小随机偏移打破对称死锁 → 强制立即寻路。
        /// 如果处于恢复冷却中则跳过。
        /// </summary>
        /// <returns>返回 true 表示已触发恢复操作。</returns>
        public bool TryRecoverFromStuck()
        {
            if (!IsStuck())
                return false;

            // 恢复冷却：避免频繁触发
            if (_isInStuckRecovery && Time.time < _stuckRecoveryEndTime)
                return false;

            _isInStuckRecovery = true;
            _stuckRecoveryEndTime = Time.time + stuckDetectionTime;

            // 对目标位置施加随机水平偏移，打破多 Agent 的对称死锁
            var jitter = Random.insideUnitCircle * stuckRecoveryJitterRadius;
            var jitteredTarget = _lastDestination + new Vector3(jitter.x, 0f, jitter.y);

            // 确保偏移后的目标仍在 NavMesh 上
            if (NavMesh.SamplePosition(jitteredTarget, out var hit, stuckRecoveryJitterRadius * 2f, NavMesh.AllAreas))
            {
                jitteredTarget = hit.position;
            }

            ReleaseCrowdQueue();
            agent.ResetPath();
            var success = agent.SetDestination(jitteredTarget);
            _nextRepathTime = Time.time + _currentRepathInterval;
            _stuckTimer = 0f;

            if (verboseLog)
                AIDebug.LogChannel("AI.Navigation", $"[EnemyNavAgentController] 卡死恢复: 原始目标={_lastDestination} 偏移目标={jitteredTarget} 成功={success}", this);

            return success;
        }

        /// <summary>
        /// 尝试将 Agent 传送到最近的 NavMesh 可走面上。
        /// </summary>
        /// <returns>返回 true 表示传送成功。</returns>
        public bool TryWarpToNavMesh()
        {
            if (agent == null || !agent.enabled)
                return false;

            if (agent.isOnNavMesh)
                return true;

            if (NavMesh.SamplePosition(transform.position, out var hit, warpSampleDistance, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
                if (verboseLog)
                    AIDebug.LogChannel("AI.Navigation", $"[EnemyNavAgentController] Warp 到 NavMesh: {hit.position}", this);
                return true;
            }

            if (verboseLog || !Application.isPlaying)
                AIDebug.LogWarning($"[EnemyNavAgentController] 无法 Warp：附近 {warpSampleDistance}m 内找不到 NavMesh，当前位置={transform.position}", this);
            return false;
        }

        /// <summary>
        /// 紧急传送到指定世界坐标附近的 NavMesh 上。
        /// 用于服务器端强制同步位置（如出生点修正、强制位移后修复）。
        /// </summary>
        /// <param name="worldPosition">目标世界坐标。</param>
        /// <returns>返回 true 表示传送成功。</returns>
        public bool TryWarpToPosition(Vector3 worldPosition)
        {
            if (agent == null || !agent.enabled)
                return false;

            if (NavMesh.SamplePosition(worldPosition, out var hit, warpSampleDistance, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
                _lastDestination = hit.position;
                return true;
            }

            AIDebug.LogWarning($"[EnemyNavAgentController] Warp 到指定位置失败：附近找不到 NavMesh，位置={worldPosition}", this);
            return false;
        }

        /// <summary>
        /// 更新动画同步。
        /// 在 LateUpdate 或专用的动画同步 MonoBehaviour 中调用。
        /// </summary>
        public void RetireForDeath()
        {
            if (_isDead && _deathCollisionSuppressed && (agent == null || !agent.enabled))
                return;

            _isDead = true;
            UnregisterActiveServerController();
            ReleaseCrowdQueue();
            DisableAgentForDeath();
            SuppressCollisionForDeath();
        }

        private void DisableAgentForDeath()
        {
            if (agent == null)
                return;

            if (agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.ResetPath();
                agent.velocity = Vector3.zero;
            }

            agent.enabled = false;
        }

        private void CacheCollisionComponentsIfNeeded()
        {
            if (_cachedColliders != null)
                return;

            _cachedColliders = GetComponentsInChildren<Collider>(true);
            _cachedColliderEnabledStates = new bool[_cachedColliders.Length];
            for (int i = 0; i < _cachedColliders.Length; i++)
            {
                _cachedColliderEnabledStates[i] = _cachedColliders[i] != null && _cachedColliders[i].enabled;
            }

            _cachedRigidbodies = GetComponentsInChildren<Rigidbody>(true);
            _cachedRigidbodyCollisionStates = new bool[_cachedRigidbodies.Length];
            _cachedRigidbodyGravityStates = new bool[_cachedRigidbodies.Length];
            for (int i = 0; i < _cachedRigidbodies.Length; i++)
            {
                _cachedRigidbodyCollisionStates[i] = _cachedRigidbodies[i] == null || _cachedRigidbodies[i].detectCollisions;
                _cachedRigidbodyGravityStates[i] = _cachedRigidbodies[i] == null || _cachedRigidbodies[i].useGravity;
            }
        }

        private void SuppressCollisionForDeath()
        {
            CacheCollisionComponentsIfNeeded();

            if (_cachedColliders != null)
            {
                for (int i = 0; i < _cachedColliders.Length; i++)
                {
                    if (_cachedColliders[i] != null)
                    {
                        _cachedColliders[i].enabled = false;
                    }
                }
            }

            if (_cachedRigidbodies != null)
            {
                for (int i = 0; i < _cachedRigidbodies.Length; i++)
                {
                    var rb = _cachedRigidbodies[i];
                    if (rb == null)
                        continue;

                    if (!rb.isKinematic)
                    {
                        rb.velocity = Vector3.zero;
                        rb.angularVelocity = Vector3.zero;
                    }
                    rb.detectCollisions = false;
                    rb.useGravity = false;
                }
            }

            _deathCollisionSuppressed = true;
        }

        private void RestoreCollisionState()
        {
            CacheCollisionComponentsIfNeeded();

            if (_cachedColliders != null && _cachedColliderEnabledStates != null)
            {
                for (int i = 0; i < _cachedColliders.Length && i < _cachedColliderEnabledStates.Length; i++)
                {
                    if (_cachedColliders[i] != null)
                    {
                        _cachedColliders[i].enabled = _cachedColliderEnabledStates[i];
                    }
                }
            }

            if (_cachedRigidbodies != null && _cachedRigidbodyCollisionStates != null)
            {
                for (int i = 0; i < _cachedRigidbodies.Length && i < _cachedRigidbodyCollisionStates.Length; i++)
                {
                    var rb = _cachedRigidbodies[i];
                    if (rb == null)
                        continue;

                    rb.detectCollisions = _cachedRigidbodyCollisionStates[i];
                    if (_cachedRigidbodyGravityStates != null && i < _cachedRigidbodyGravityStates.Length)
                    {
                        rb.useGravity = _cachedRigidbodyGravityStates[i];
                    }
                    if (!rb.isKinematic)
                    {
                        rb.velocity = Vector3.zero;
                        rb.angularVelocity = Vector3.zero;
                    }
                }
            }

            _deathCollisionSuppressed = false;
        }

        private void RegisterActiveServerController()
        {
            if (!ActiveServerControllers.Contains(this))
            {
                ActiveServerControllers.Add(this);
            }
        }

        private void UnregisterActiveServerController()
        {
            ActiveServerControllers.Remove(this);
        }

        private void ApplyStableAvoidancePriority()
        {
            if (agent == null)
                return;

            var min = Mathf.Clamp(Mathf.Min(avoidancePriorityMin, avoidancePriorityMax), 0, 99);
            var max = Mathf.Clamp(Mathf.Max(avoidancePriorityMin, avoidancePriorityMax), 0, 99);
            var range = Mathf.Max(1, max - min + 1);
            var objectKey = GetStableObjectKey();

            unchecked
            {
                var hash = (int)((objectKey * 1103515245UL + 12345UL) % (ulong)range);
                _baseAvoidancePriority = min + hash;
            }

            agent.avoidancePriority = _baseAvoidancePriority;
        }

        private ulong GetStableObjectKey()
        {
            var networkObject = GetComponent<NetworkObject>();
            if (networkObject != null && networkObject.IsSpawned)
                return networkObject.NetworkObjectId;

            return (ulong)GetInstanceID();
        }

        private void UpdateCrowdFlowControl()
        {
            if (!enableCrowdFlowControl || !IsAgentReadyForCrowdFlow())
            {
                ReleaseCrowdQueue();
                return;
            }

            if (!agent.hasPath || agent.pathPending || GetRemainingDistance(agent) <= arrivalDistance + 0.5f)
            {
                ReleaseCrowdQueue();
                return;
            }

            var flowDirection = GetCurrentFlowDirection();
            if (flowDirection.sqrMagnitude <= 1e-4f)
            {
                ReleaseCrowdQueue();
                return;
            }

            if (ShouldYieldToAgentAhead(flowDirection))
            {
                HoldCrowdQueue();
                return;
            }

            if (_isQueuedByCrowd && Time.time >= _queueReleaseTime)
            {
                ReleaseCrowdQueue();
            }
        }

        private bool ShouldYieldToAgentAhead(Vector3 flowDirection)
        {
            if (!IsCrowdSensitiveArea())
                return false;

            var maxDistance = _isQueuedByCrowd
                ? Mathf.Max(queueResumeDistance, queueProbeDistance)
                : queueProbeDistance;
            maxDistance = Mathf.Max(maxDistance, agent.radius * 2.25f);
            var minDot = Mathf.Cos(queueProbeAngle * Mathf.Deg2Rad);
            var selfRemaining = GetRemainingDistance(agent);
            var selfPosition = transform.position;

            for (int i = ActiveServerControllers.Count - 1; i >= 0; i--)
            {
                var other = ActiveServerControllers[i];
                if (other == null)
                {
                    ActiveServerControllers.RemoveAt(i);
                    continue;
                }

                if (other == this || !other.IsAgentReadyForCrowdFlow() || !other.agent.hasPath || other.agent.pathPending)
                    continue;

                if (!IsSameTrafficStream(other, flowDirection))
                    continue;

                var offset = other.transform.position - selfPosition;
                offset.y = 0f;
                var distance = offset.magnitude;
                if (distance <= 1e-4f || distance > maxDistance)
                    continue;

                if (Vector3.Dot(flowDirection, offset / distance) < minDot)
                    continue;

                if (ShouldYieldByPathProgress(other, selfRemaining))
                    return true;
            }

            return false;
        }

        private bool IsSameTrafficStream(EnemyNavAgentController other, Vector3 flowDirection)
        {
            if (other == null)
                return false;

            if (IsUsableDestination(_lastDestination) && IsUsableDestination(other._lastDestination))
            {
                var destinationOffset = _lastDestination - other._lastDestination;
                destinationOffset.y = 0f;
                var tolerance = Mathf.Max(0.1f, queueSameDestinationTolerance);
                if (destinationOffset.sqrMagnitude <= tolerance * tolerance)
                    return true;
            }

            var otherDirection = other.GetCurrentFlowDirection();
            return otherDirection.sqrMagnitude <= 1e-4f || Vector3.Dot(flowDirection, otherDirection) > 0.35f;
        }

        private bool ShouldYieldByPathProgress(EnemyNavAgentController other, float selfRemaining)
        {
            var otherRemaining = GetRemainingDistance(other.agent);
            if (!float.IsInfinity(selfRemaining) && !float.IsInfinity(otherRemaining))
            {
                const float progressSlack = 0.25f;
                if (otherRemaining + progressSlack < selfRemaining)
                    return true;

                if (selfRemaining + progressSlack < otherRemaining)
                    return false;
            }

            return other._baseAvoidancePriority <= _baseAvoidancePriority;
        }

        private bool IsCrowdSensitiveArea()
        {
            if (narrowAreaEdgeDistance <= 0f)
                return true;

            var edgeThreshold = Mathf.Max(narrowAreaEdgeDistance, agent.radius * 2.2f);
            if (NavMesh.FindClosestEdge(transform.position, out var hit, NavMesh.AllAreas) && hit.distance <= edgeThreshold)
                return true;

            return IsLocallyBlocked();
        }

        private bool IsLocallyBlocked()
        {
            if (agent == null || !agent.hasPath || agent.pathPending)
                return false;

            if (GetRemainingDistance(agent) <= arrivalDistance + 1f)
                return false;

            var desiredSpeed = agent.desiredVelocity.magnitude;
            var actualSpeed = agent.velocity.magnitude;
            return desiredSpeed > Mathf.Max(0.1f, agent.speed * 0.3f)
                   && actualSpeed < Mathf.Max(0.05f, agent.speed * 0.2f);
        }

        private Vector3 GetCurrentFlowDirection()
        {
            if (agent == null || !agent.enabled || !agent.isOnNavMesh)
                return Vector3.zero;

            var direction = agent.desiredVelocity;
            direction.y = 0f;
            if (direction.sqrMagnitude > 1e-4f)
                return direction.normalized;

            if (agent.hasPath)
            {
                direction = agent.steeringTarget - transform.position;
                direction.y = 0f;
                if (direction.sqrMagnitude > 1e-4f)
                    return direction.normalized;
            }

            if (IsUsableDestination(_lastDestination))
            {
                direction = _lastDestination - transform.position;
                direction.y = 0f;
                if (direction.sqrMagnitude > 1e-4f)
                    return direction.normalized;
            }

            direction = transform.forward;
            direction.y = 0f;
            return direction.sqrMagnitude > 1e-4f ? direction.normalized : Vector3.zero;
        }

        private void HoldCrowdQueue()
        {
            if (!IsAgentReadyForCrowdFlow())
                return;

            _isQueuedByCrowd = true;
            _queueReleaseTime = Time.time + Mathf.Max(0f, queueReleaseDelay);
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }

        private void ReleaseCrowdQueue()
        {
            if (!_isQueuedByCrowd)
                return;

            _isQueuedByCrowd = false;
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = false;
            }
        }

        private bool IsAgentReadyForCrowdFlow()
        {
            return !_isDead && agent != null && agent.enabled && agent.isOnNavMesh;
        }

        private static float GetRemainingDistance(NavMeshAgent navAgent)
        {
            if (navAgent == null || !navAgent.enabled || !navAgent.isOnNavMesh || navAgent.pathPending)
                return float.PositiveInfinity;

            var remaining = navAgent.remainingDistance;
            return float.IsNaN(remaining) || float.IsInfinity(remaining)
                ? float.PositiveInfinity
                : remaining;
        }

        private static bool IsUsableDestination(Vector3 destination)
        {
            return !float.IsNaN(destination.x)
                   && !float.IsNaN(destination.y)
                   && !float.IsNaN(destination.z)
                   && !float.IsInfinity(destination.x)
                   && !float.IsInfinity(destination.y)
                   && !float.IsInfinity(destination.z)
                   && Mathf.Abs(destination.x) < 1000000f
                   && Mathf.Abs(destination.y) < 1000000f
                   && Mathf.Abs(destination.z) < 1000000f;
        }

        public void SyncAnimation(float deltaTime = -1f)
        {
            if (!syncVelocityToAnimator || _animator == null)
            {
                if (_animator == null)
                    _animator = GetComponent<Animator>();
                if (!syncVelocityToAnimator || _animator == null)
                    return;
            }

            if (deltaTime < 0f)
                deltaTime = Time.deltaTime;

            var rawSpeed = agent != null && agent.enabled ? agent.velocity.magnitude : 0f;
            _smoothSpeed = Mathf.Lerp(_smoothSpeed, rawSpeed, deltaTime * 10f);

            if (!string.IsNullOrEmpty(animatorSpeedParameter))
            {
                _animator.SetFloat(animatorSpeedParameter, _smoothSpeed);
            }
        }

        private void FixedUpdate()
        {
            if (IsSpawned)
            {
                ConfigureRootRigidbodyForNavAgent(false);
            }
        }

        private void Update()
        {
            if (IsServer && IsSpawned && !_isDead)
            {
                UpdateCrowdFlowControl();
                SyncAnimation();
            }

            if (enableLinkTrace && IsServer && IsSpawned)
            {
                TraceLinkNavigation();
            }
        }

        private void TraceLinkNavigation()
        {
            if (agent == null || !agent.enabled || !agent.isOnNavMesh)
                return;

            // 追踪 Link 穿越状态变化
            var nowOnLink = agent.isOnOffMeshLink;
            if (nowOnLink != _wasOnOffMeshLink)
            {
                _wasOnOffMeshLink = nowOnLink;
                if (nowOnLink)
                {
                    var link = agent.currentOffMeshLinkData;
                    AIDebug.LogChannel("AI.Navigation", $"[NavLinkTrace] Enemy[{gameObject.name}] 开始穿越 NavMeshLink！" +
                              $"activated={link.activated} linkType={link.linkType} " +
                              $"offMeshLink={(link.offMeshLink != null ? link.offMeshLink.name : "null")} " +
                              $"agentPos={transform.position}", this);
                }
                else
                {
                    AIDebug.LogChannel("AI.Navigation", $"[NavLinkTrace] Enemy[{gameObject.name}] 完成/离开 NavMeshLink。" +
                              $"agentPos={transform.position} hasPath={agent.hasPath} " +
                              $"remainingDist={agent.remainingDistance:F2}", this);
                }
            }

            // 追踪路径拐角详情
            if (_tracePathNextFrame && !agent.pathPending)
            {
                _tracePathNextFrame = false;

                var path = agent.path;
                if (path == null)
                {
                    AIDebug.LogWarning($"[NavLinkTrace] Enemy[{gameObject.name}] 路径为空！目标={_lastTracedDestination}", this);
                    return;
                }

                var pathStatus = path.status;
                var corners = path.corners;
                var cornerCount = corners?.Length ?? 0;

                AIDebug.LogChannel("AI.Navigation", $"[NavLinkTrace] Enemy[{gameObject.name}] 路径详情：" +
                          $"status={pathStatus} corners={cornerCount} " +
                          $"目标={_lastTracedDestination} " +
                          $"agentPos={transform.position} " +
                          $"autoTraverseOffMeshLink={agent.autoTraverseOffMeshLink} " +
                          $"剩余={agent.remainingDistance:F2}m", this);

                if (cornerCount > 0)
                {
                    for (int i = 0; i < cornerCount; i++)
                    {
                        AIDebug.LogChannel("AI.Navigation", $"[NavLinkTrace]   Corner[{i}]: {corners[i]}", this);
                    }

                    // 检查路径是否可能穿越 Link：任何两个相邻 corner 之间距离过大
                    for (int i = 0; i < cornerCount - 1; i++)
                    {
                        var segLen = Vector3.Distance(corners[i], corners[i + 1]);
                        if (segLen > 3f)
                        {
                            AIDebug.LogChannel("AI.Navigation", $"[NavLinkTrace]   ⚠ Corner[{i}]→Corner[{i + 1}] 距离={segLen:F2}m，可能是 Link 跳越点！", this);
                        }
                    }
                }
                else if (pathStatus != NavMeshPathStatus.PathComplete)
                {
                    AIDebug.LogWarning($"[NavLinkTrace] Enemy[{gameObject.name}] 路径不完整！" +
                                     $"status={pathStatus} 目标={_lastTracedDestination}", this);
                }
            }

            // 定期输出简要状态
            if (Time.time - _lastLinkTraceTime >= linkTraceInterval)
            {
                _lastLinkTraceTime = Time.time;
                var path = agent.path;
                var hasPath = agent.hasPath;
                var pathPending = agent.pathPending;
                var onLink = agent.isOnOffMeshLink;
                var remaining = agent.remainingDistance;
                var pathStatus = hasPath && !pathPending ? agent.pathStatus.ToString() : (pathPending ? "Pending" : "None");

                AIDebug.LogChannel("AI.Navigation", $"[NavLinkTrace] Enemy[{gameObject.name}] 定期状态: " +
                          $"hasPath={hasPath} pathStatus={pathStatus} " +
                          $"onLink={onLink} remaining={remaining:F2}m " +
                          $"velocity={agent.velocity.magnitude:F2} " +
                          $"dest={agent.destination} " +
                          $"pos={transform.position}", this);
            }
        }

        private void OnUnitDied(UnitDiedEvt evt)
        {
            var networkObject = GetComponent<NetworkObject>();
            ulong myId = networkObject != null
                ? networkObject.NetworkObjectId
                : (ulong)GetInstanceID();
            if (evt.unitId != myId)
                return;

            RetireForDeath();
        }

        private void LateUpdate()
        {
            // Speed 由服务端 SyncAnimation → NetworkAnimator 同步。
            // 客户端不再本地写入，避免与 NetworkAnimator 的同步冲突。
        }

        public override void OnDestroy()
        {
            if (IsServer)
            {
                EventCenter.Instance.RemoveListener<UnitDiedEvt>(EventName.UnitDied, OnUnitDied);
            }
            UnregisterActiveServerController();
            ReleaseCrowdQueue();

            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                agent.ResetPath();
            }

            base.OnDestroy();
        }
    }
}
