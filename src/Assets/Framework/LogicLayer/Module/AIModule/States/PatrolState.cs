using UnityEngine;

/// <summary>
/// 巡逻状态。
/// 该状态负责在巡逻点之间循环移动，并在发现目标后立即切回追击状态。
/// </summary>
public class PatrolState : AIStateBase
{
    /// <summary>
    /// 当前巡逻点列表。
    /// 当前阶段仍使用最小实现生成临时巡逻点，后续可替换为刷怪点或房间锚点配置。
    /// </summary>
    private Vector3[] _patrolPoints;

    /// <summary>
    /// 当前巡逻点索引。
    /// </summary>
    private int _currentPatrolIndex;

    /// <summary>
    /// 当前正在前往的巡逻点。
    /// </summary>
    private Vector3 _currentPatrolTarget;

    /// <summary>
    /// 构造巡逻状态。
    /// </summary>
    /// <param name="stateMachine">所属状态机。</param>
    /// <param name="config">敌人 AI 配置。</param>
    public PatrolState(AIStateMachine stateMachine, EnemyAIConfig config)
        : base(stateMachine, config)
    {
    }

    /// <summary>
    /// 进入巡逻状态时初始化巡逻点。
    /// </summary>
    public override void OnEnter()
    {
        base.OnEnter();

        InitializePatrolPoints();

        if (_patrolPoints != null && _patrolPoints.Length > 0)
        {
            _currentPatrolIndex = 0;
            _currentPatrolTarget = _patrolPoints[_currentPatrolIndex];
            AIDebug.Log(OwnerId, $"进入巡逻状态，巡逻点数={_patrolPoints.Length} 初始目标={_currentPatrolTarget} 位置={_owner?.WorldPosition}");
        }
        else
        {
            AIDebug.LogWarning(OwnerId, "巡逻点为空，切换到待机状态");
            _stateMachine.ChangeState(new IdleState(_stateMachine, _config));
        }
    }

    /// <summary>
    /// 每帧更新巡逻逻辑。
    /// 若发现目标则切换到追击；若到达当前巡逻点则切换到下一个点；否则继续移动。
    /// </summary>
    public override void OnUpdate()
    {
        if (_owner == null || _aiModule == null)
            return;

        var target = _aiModule.GetCurrentTarget();
        if (target != null)
        {
            AIDebug.Log(OwnerId, $"检测到目标 {target.ObjectId}，切换到追击状态");
            _stateMachine.ChangeState(new ChaseState(_stateMachine, _config));
            return;
        }

        if (_patrolPoints == null || _patrolPoints.Length == 0)
        {
            AIDebug.LogWarning(OwnerId, "巡逻点列表为空，切换到待机状态");
            _stateMachine.ChangeState(new IdleState(_stateMachine, _config));
            return;
        }

        var distanceToPatrolPoint = Vector3.Distance(_owner.WorldPosition, _currentPatrolTarget);
        if (distanceToPatrolPoint <= _config.patrolPointReachDistance)
        {
            AIDebug.Log(OwnerId, $"到达巡逻点[{_currentPatrolIndex}] {_currentPatrolTarget}，移动到下一个点");
            MoveToNextPatrolPoint();
        }
        else
        {
            MoveTowardsPatrolPoint();
        }
    }

    /// <summary>
    /// 初始化巡逻点。
    /// 当前先使用出生点为中心生成最小可用的四点巡逻路径，
    /// 后续可替换为地图房间锚点、巡逻路径配置或刷怪系统提供的点位。
    /// </summary>
    private void InitializePatrolPoints()
    {
        if (_owner == null || _aiModule == null)
            return;

        var center = _aiModule.GetInitialPosition();
        var patrolRadius = _config.patrolRadius;

        _patrolPoints = new[]
        {
            center + new Vector3(patrolRadius, 0f, 0f),
            center + new Vector3(0f, 0f, patrolRadius),
            center + new Vector3(-patrolRadius, 0f, 0f),
            center + new Vector3(0f, 0f, -patrolRadius)
        };
    }

    /// <summary>
    /// 切换到下一个巡逻点。
    /// </summary>
    private void MoveToNextPatrolPoint()
    {
        _currentPatrolIndex = (_currentPatrolIndex + 1) % _patrolPoints.Length;
        _currentPatrolTarget = _patrolPoints[_currentPatrolIndex];
    }

    /// <summary>
    /// 朝当前巡逻点移动。
    /// </summary>
    private void MoveTowardsPatrolPoint()
    {
        _aiModule.MoveToPosition(_currentPatrolTarget, _config.patrolPointReachDistance);
    }

    /// <summary>
    /// 离开巡逻状态时停止 NavMeshAgent。
    /// </summary>
    public override void OnExit()
    {
        base.OnExit();
        _aiModule?.StopMoving();
        AIDebug.Log(OwnerId, "退出巡逻状态");
    }
}
