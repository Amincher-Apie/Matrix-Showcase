using System;
using System.Collections.Generic;

public interface IConditionEvaluator
{
    bool Evaluate(ConditionBlock condition, in QualityEffectRuntimeContext ctx, in QualityEventContext evtCtx);
}

public interface IActionExecutor
{
    void Execute(ActionBlock action, in QualityEffectRuntimeContext ctx, in QualityEventContext evtCtx);
}

/// <summary>
/// 触发事件携带的上下文，由战斗系统在触发时填充。
/// </summary>
public struct QualityEventContext
{
    public object owner;     // 拥有者（PlayerActor/Unit）
    public object target;    // 目标（可选）
    public float baseDamage; // 本次事件的基础伤害（命中场景）
}

public static class QualityEffectRegistry
{
    private static readonly Dictionary<string, IConditionEvaluator> Conditions = new();
    private static readonly Dictionary<string, IActionExecutor> Actions = new();

    public static void RegisterCondition(string id, IConditionEvaluator evaluator)
    {
        Conditions[id] = evaluator;
    }

    public static void RegisterAction(string id, IActionExecutor executor)
    {
        Actions[id] = executor;
    }

    public static bool TryGetCondition(string id, out IConditionEvaluator evaluator)
    {
        return Conditions.TryGetValue(id, out evaluator);
    }

    public static bool TryGetAction(string id, out IActionExecutor executor)
    {
        return Actions.TryGetValue(id, out executor);
    }
}


