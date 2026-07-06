using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// AI 状态机 - 管理 AI 状态切换和更新
/// </summary>
public class AIStateMachine
{
    private readonly EnemyActor _owner;
    private readonly EnemyAIConfig _config;
    
    private AIStateBase _currentState;
    private AIStateBase _previousState;

    // 状态历史（用于调试）
    private Queue<string> _stateHistory;
    private const int MAX_HISTORY_SIZE = 10;

    // 状态切换冷却
    private float _lastStateChangeTime = float.NegativeInfinity;
    
    public AIStateMachine(EnemyActor owner, EnemyAIConfig config)
    {
        _owner = owner;
        _config = config;
        _stateHistory = new Queue<string>();
    }
    
    /// <summary>
    /// 获取 Owner（供状态使用）
    /// </summary>
    public EnemyActor GetOwner()
    {
        return _owner;
    }
    
    /// <summary>
    /// 获取配置（供状态使用）
    /// </summary>
    public EnemyAIConfig GetConfig()
    {
        return _config;
    }
    
    /// <summary>
    /// 更新当前状态
    /// </summary>
    public void OnUpdate()
    {
        _currentState?.OnUpdate();
    }
    
    /// <summary>
    /// 切换状态
    /// </summary>
    public bool ChangeState(AIStateBase newState)
    {
        if (newState == null)
        {
            AIDebug.LogWarning("尝试切换到空状态");
            return false;
        }

        // 检查状态切换冷却
        var cooldown = _config?.stateSwitchCooldown ?? 0f;
        if (Time.time - _lastStateChangeTime < cooldown)
        {
            AIDebug.Log($"状态切换被冷却阻止: {_currentState?.GetType().Name} -> {newState.GetType().Name}, 剩余冷却: {cooldown - (Time.time - _lastStateChangeTime):F3}s");
            return false;
        }

        // 退出当前状态
        if (_currentState != null)
        {
            _currentState.OnExit();
            _previousState = _currentState;
        }

        // 记录状态历史
        if (_currentState != null)
        {
            AddToHistory(_currentState.GetType().Name);
        }

        // 切换到新状态
        _currentState = newState;
        _currentState.OnEnter();
        _lastStateChangeTime = Time.time;

        AIDebug.Log(_owner.ObjectId, $"状态切换: {_previousState?.GetType().Name ?? "None"} -> {_currentState.GetType().Name}");
        return true;
    }

    /// <summary>
    /// 返回到上一个状态
    /// </summary>
    public void RevertToPreviousState()
    {
        if (_previousState != null)
        {
            AIDebug.Log(_owner.ObjectId, $"返回上一个状态: {_previousState.GetType().Name}");
            ChangeState(_previousState);
        }
    }
    
    /// <summary>
    /// 获取当前状态
    /// </summary>
    public AIStateBase GetCurrentState()
    {
        return _currentState;
    }
    
    /// <summary>
    /// 获取当前状态名称（用于调试）
    /// </summary>
    public string GetCurrentStateName()
    {
        return _currentState?.GetType().Name ?? "None";
    }
    
    /// <summary>
    /// 清理状态机
    /// </summary>
    public void Cleanup()
    {
        _currentState?.OnExit();
        _currentState = null;
        _previousState = null;
        _stateHistory?.Clear();
    }
    
    /// <summary>
    /// 添加状态到历史记录
    /// </summary>
    private void AddToHistory(string stateName)
    {
        _stateHistory.Enqueue(stateName);
        if (_stateHistory.Count > MAX_HISTORY_SIZE)
        {
            _stateHistory.Dequeue();
        }
    }
    
    /// <summary>
    /// 获取状态历史（用于调试）
    /// </summary>
    public IEnumerable<string> GetStateHistory()
    {
        return _stateHistory;
    }
}

