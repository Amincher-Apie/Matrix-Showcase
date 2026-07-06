using UnityEngine;

/// <summary>
/// AI 状态基类 - 所有 AI 状态的基类
/// </summary>
public abstract class AIStateBase
{
    protected AIStateMachine _stateMachine;
    protected EnemyActor _owner;
    protected EnemyAIConfig _config;
    protected EnemyAIModule _aiModule;
    
    protected float _stateStartTime;
    
    public AIStateBase(AIStateMachine stateMachine, EnemyAIConfig config)
    {
        _stateMachine = stateMachine;
        _owner = stateMachine.GetOwner();
        _config = config;
        
        // 从状态机获取 owner
        if (_owner != null)
        {
            _aiModule = _owner.GetModule<EnemyAIModule>();
        }
    }
    
    /// <summary>
    /// 状态进入时调用
    /// </summary>
    public virtual void OnEnter()
    {
        _stateStartTime = Time.time;
        
        // 获取 AI 模块引用
        if (_owner != null)
        {
            _aiModule = _owner.GetModule<EnemyAIModule>();
        }
    }
    
    /// <summary>
    /// 状态更新时调用（每帧）
    /// </summary>
    public abstract void OnUpdate();
    
    /// <summary>
    /// 状态退出时调用
    /// </summary>
    public virtual void OnExit()
    {
        // 子类可以重写以清理资源
    }
    
    /// <summary>
    /// 获取状态持续时间
    /// </summary>
    protected float GetStateDuration()
    {
        return Time.time - _stateStartTime;
    }

    /// <summary>
    /// 获取当前状态所属敌人的 ID。
    /// </summary>
    protected ulong OwnerId => _owner?.ObjectId ?? 0;
}

