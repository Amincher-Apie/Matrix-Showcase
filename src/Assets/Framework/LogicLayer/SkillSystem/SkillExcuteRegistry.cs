using System;
using System.Collections.Generic;

public static class SkillExecuteRegistry
{
    private static readonly Dictionary<string, ISkillExecute> _handlers = new();

    private static bool _initialized;

    public static void EnsureInitialized()
    {
        if (_initialized) return;
        _initialized = true;

        // 在这里注册所有技能执行器
        Register(new BombardAreaSkillExecutor());
        Register(new PiercingShotSkillExecutor());
        Register(new KineticBoostSkillExecutor());
    }

    public static void Register(ISkillExecute handler)
    {
        if (handler == null || string.IsNullOrEmpty(handler.Id))
        {
            UnityEngine.Debug.LogError("[SkillExecuteRegistry] 尝试注册空 handler 或 Id 为空");
            return;
        }

        if (_handlers.ContainsKey(handler.Id))
        {
            UnityEngine.Debug.LogWarning($"[SkillExecuteRegistry] 重复注册技能执行器: {handler.Id}");
            _handlers[handler.Id] = handler;
        }
        else
        {
            _handlers.Add(handler.Id, handler);
        }
    }

    public static ISkillExecute Get(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        _handlers.TryGetValue(id, out var handler);
        return handler;
    }

    public static ISkillExecute Get(SkillExecuteHandlerId id)
    {
        return Get(id.ToHandlerIdString());
    }
}
