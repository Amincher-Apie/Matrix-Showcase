using UnityEngine;

/// <summary>
/// 统一 Debug 日志封装。
/// 所有 Debug.Log 应通过此类输出，以便由 DebugManager Inspector 面板集中控制开关。
/// </summary>
public static class DebugLog
{
    /// <summary>带通道的 Info 日志。</summary>
    public static void Info(string channelKey, string message, Object context = null)
    {
        if (!ShouldLog(channelKey)) return;
        Debug.Log($"[{channelKey}] {message}", context);
    }

    /// <summary>带通道的 Warning 日志。</summary>
    public static void Warning(string channelKey, string message, Object context = null)
    {
        if (!ShouldLog(channelKey)) return;
        Debug.LogWarning($"[{channelKey}] {message}", context);
    }

    /// <summary>Error 日志始终输出，不受 DebugManager 控制。</summary>
    public static void Error(string channelKey, string message, Object context = null)
    {
        Debug.LogError($"[{channelKey}] {message}", context);
    }

    private static bool ShouldLog(string channelKey)
    {
        if (DebugManager.Instance != null)
            return DebugManager.Instance.IsEnabled(channelKey);
        return true; // 未挂载 DebugManager 时默认全部输出
    }
}
