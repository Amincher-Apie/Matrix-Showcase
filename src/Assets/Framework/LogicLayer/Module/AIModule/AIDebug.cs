using UnityEngine;

/// <summary>
/// AI 系统的全局调试日志工具类。
/// 所有 AI 相关模块应使用此类进行 Debug.Log 输出，以便通过 DebugManager 统一控制。
/// </summary>
public static class AIDebug
{
    /// <summary>
    /// 全局 AI Debug 日志总开关。
    /// 设置时同步所有 AI 子通道；获取时返回 AI 顶级通道状态。
    /// </summary>
    public static bool Enabled
    {
        get
        {
            if (DebugManager.Instance != null)
                return DebugManager.Instance.IsEnabled("AI");
            return AIScheduler.GlobalDebugLogEnabled; // fallback
        }
        set
        {
            // 同步所有 AI 子通道
            if (DebugManager.Instance != null)
            {
                DebugManager.Instance.SetEnabled("AI", value);
                DebugManager.Instance.SetEnabled("AI.StateMachine", value);
                DebugManager.Instance.SetEnabled("AI.EnemyAIModule", value);
                DebugManager.Instance.SetEnabled("AI.Scheduler", value);
                DebugManager.Instance.SetEnabled("AI.AttackState", value);
                DebugManager.Instance.SetEnabled("AI.ChaseState", value);
                DebugManager.Instance.SetEnabled("AI.IdleState", value);
                DebugManager.Instance.SetEnabled("AI.PatrolState", value);
                DebugManager.Instance.SetEnabled("AI.Navigation", value);
                DebugManager.Instance.SetEnabled("AI.Steering", value);
            }
            AIScheduler.GlobalDebugLogEnabled = value; // 保持兼容
        }
    }

    /// <summary>
    /// 输出 AI Debug 日志（仅当全局开关开启时）。
    /// </summary>
    public static void Log(string message, Object context = null)
    {
        if (!ShouldLog("AI")) return;
        Debug.Log($"[AI] {message}", context);
    }

    /// <summary>
    /// 输出 AI Debug 日志（带敌人 ID）。
    /// </summary>
    public static void Log(ulong objectId, string message, Object context = null)
    {
        if (!ShouldLog("AI")) return;
        Debug.Log($"[AI] Enemy[{objectId}] {message}", context);
    }

    /// <summary>
    /// 输出 AI Debug 警告。
    /// </summary>
    public static void LogWarning(string message, Object context = null)
    {
        if (!ShouldLog("AI")) return;
        Debug.LogWarning($"[AI] {message}", context);
    }

    /// <summary>
    /// 输出 AI Debug 警告（带敌人 ID）。
    /// </summary>
    public static void LogWarning(ulong objectId, string message, Object context = null)
    {
        if (!ShouldLog("AI")) return;
        Debug.LogWarning($"[AI] Enemy[{objectId}] {message}", context);
    }

    /// <summary>
    /// 按通道 Key 输出 AI Debug 日志（细粒度控制）。
    /// </summary>
    public static void LogChannel(string channelKey, string message, Object context = null)
    {
        if (!ShouldLog(channelKey)) return;
        Debug.Log($"[AI] [{channelKey}] {message}", context);
    }

    /// <summary>
    /// 按通道 Key 输出 AI Debug 日志（带敌人 ID）。
    /// </summary>
    public static void LogChannel(string channelKey, ulong objectId, string message, Object context = null)
    {
        if (!ShouldLog(channelKey)) return;
        Debug.Log($"[AI] [{channelKey}] Enemy[{objectId}] {message}", context);
    }

    // ════════════════════════════════════════════════════
    // Internal
    // ════════════════════════════════════════════════════

    private static bool ShouldLog(string channelKey)
    {
        if (DebugManager.Instance != null)
            return DebugManager.Instance.IsEnabled(channelKey);
        return AIScheduler.GlobalDebugLogEnabled; // fallback：未挂载 DebugManager 时回退旧开关
    }
}
