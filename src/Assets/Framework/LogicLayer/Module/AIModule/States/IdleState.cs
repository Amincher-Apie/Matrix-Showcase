using UnityEngine;

/// <summary>
/// 待机状态 - 怪物在待机状态下的行为
/// </summary>
public class IdleState : AIStateBase
{
    private float _idleDuration;
    private float _idleTimer;
    
    public IdleState(AIStateMachine stateMachine, EnemyAIConfig config) 
        : base(stateMachine, config)
    {
    }
    
    public override void OnEnter()
    {
        base.OnEnter();
        
        // 随机待机时间
        _idleDuration = Random.Range(
            _config.idleMinDuration, 
            _config.idleMaxDuration
        );
        _idleTimer = 0f;
        
        AIDebug.Log(OwnerId, $"进入待机状态，持续时间: {_idleDuration:F2}秒 位置={_owner?.WorldPosition}");
    }
    
    public override void OnUpdate()
    {
        if (_owner == null || _aiModule == null) return;
        
        // 检查是否有目标进入范围
        var target = _aiModule.GetCurrentTarget();
        if (target != null)
        {
            AIDebug.Log(OwnerId, $"检测到目标 {target.ObjectId}，切换到追击状态");
            // 切换到追击状态
            _stateMachine.ChangeState(new ChaseState(_stateMachine, _config));
            return;
        }
        
        // 更新待机计时器
        _idleTimer += Time.deltaTime;
        
        // 如果待机时间到了，切换到巡逻状态
        if (_idleTimer >= _idleDuration && _config.enablePatrol)
        {
            AIDebug.Log(OwnerId, "待机超时，切换到巡逻状态");
            _stateMachine.ChangeState(new PatrolState(_stateMachine, _config));
        }
    }
    
    public override void OnExit()
    {
        base.OnExit();
        AIDebug.Log(OwnerId, $"退出待机状态，实际持续时间: {_idleTimer:F2}秒");
    }
}

