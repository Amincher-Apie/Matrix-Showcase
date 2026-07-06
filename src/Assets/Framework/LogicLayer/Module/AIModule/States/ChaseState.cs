using UnityEngine;

/// <summary>
/// 追击状态。
/// 负责持续追击当前目标，并在目标短暂丢失时朝最后已知位置搜索。
/// </summary>
public class ChaseState : AIStateBase
{
    private float _targetLostTime = float.NegativeInfinity;
    private bool _isSearchingLastKnownPosition;

    public ChaseState(AIStateMachine stateMachine, EnemyAIConfig config)
        : base(stateMachine, config)
    {
    }

    public override void OnEnter()
    {
        base.OnEnter();
        _targetLostTime = float.NegativeInfinity;
        _isSearchingLastKnownPosition = false;

        AIDebug.Log(OwnerId, $"进入追击状态 位置={_owner?.WorldPosition}");
    }

    public override void OnUpdate()
    {
        AIDebug.Log(OwnerId, "OnUpdate 被调用");
        if (_owner == null || _aiModule == null)
            return;

        var target = _aiModule.GetCurrentTarget();

        if (_isSearchingLastKnownPosition)
        {
            if (target != null)
            {
                _isSearchingLastKnownPosition = false;
                _targetLostTime = float.NegativeInfinity;
                AIDebug.Log(OwnerId, $"重新发现目标 {target.ObjectId}");
                _aiModule.MoveToPosition(target.GetTargetPoint(), _config.attackRange * 0.8f);
                return;
            }

            var searchElapsed = Time.time - _targetLostTime;
            if (searchElapsed >= _config.targetMemoryDuration || !_aiModule.HasRecentTargetMemory())
            {
                AIDebug.Log(OwnerId, "目标搜索超时，切换到待机状态");
                _isSearchingLastKnownPosition = false;
                HandleSearchComplete();
                return;
            }

            if (!_aiModule.MoveToPosition(_aiModule.GetLastKnownTargetPosition(), _config.lastKnownTargetReachDistance))
            {
                AIDebug.Log(OwnerId, "无法移动到最后已知位置，切换到待机状态");
                _isSearchingLastKnownPosition = false;
                HandleSearchComplete();
            }

            return;
        }

        if (target == null)
        {
            _targetLostTime = Time.time;
            _isSearchingLastKnownPosition = true;
            AIDebug.Log(OwnerId, $"目标丢失，开始搜索最后已知位置 {_aiModule.GetLastKnownTargetPosition()}");
            return;
        }

        var targetPos = target.GetTargetPoint();
        var distanceToTarget = Vector3.Distance(_owner.WorldPosition, targetPos);
        if (distanceToTarget <= _config.attackRange)
        {
            AIDebug.Log(OwnerId, $"到达攻击距离 {distanceToTarget:F2}m，切换到攻击状态");
            _stateMachine.ChangeState(new AttackState(_stateMachine, _config));
            return;
        }

        _aiModule.MoveToPosition(targetPos, _config.attackRange * 0.8f);
    }

    private void HandleSearchComplete()
    {
        AIDebug.Log(OwnerId, "搜索完成，切换到待机状态");
        _stateMachine.ChangeState(new IdleState(_stateMachine, _config));
    }

    public override void OnExit()
    {
        base.OnExit();
        _aiModule?.StopMoving();
        _isSearchingLastKnownPosition = false;
        _targetLostTime = float.NegativeInfinity;
        AIDebug.Log(OwnerId, "退出追击状态");
    }
}
