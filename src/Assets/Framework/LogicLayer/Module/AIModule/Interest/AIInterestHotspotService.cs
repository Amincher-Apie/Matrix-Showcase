using UnityEngine;

/// <summary>
/// AI 兴趣热点服务入口。
/// 该静态服务用于给任务系统、刷怪系统或脚本逻辑提供稳定的调用入口，避免外部模块直接依赖 InterestRegionManager 的内部细节。
/// </summary>
public static class AIInterestHotspotService
{
    /// <summary>
    /// 注册或刷新一个任务热点。
    /// 当前阶段适合任务系统仅凭对象生成字段或逻辑来源 ID 推送热点，而无需先实现任务目标实体类。
    /// </summary>
    /// <param name="center">热点中心点。</param>
    /// <param name="radius">热点半径。</param>
    /// <param name="duration">热点持续时间，单位为秒。</param>
    /// <param name="sourceObjectId">任务相关来源对象 ID；若没有则可传 0。</param>
    /// <param name="debugTag">调试标签，例如任务名、阶段名或目标说明。</param>
    /// <returns>返回被创建或刷新的热点 ID。</returns>
    public static int RegisterTaskHotspot(Vector3 center, float radius, float duration, ulong sourceObjectId = 0, string debugTag = null)
    {
        return InterestRegionManager.Instance.RegisterTaskRegion(center, radius, duration, sourceObjectId, debugTag);
    }

    /// <summary>
    /// 注册或刷新一个刷怪热点。
    /// 当前阶段该接口主要给未来 SpawnDirector 或波次系统预留统一入口。
    /// </summary>
    /// <param name="center">热点中心点。</param>
    /// <param name="radius">热点半径。</param>
    /// <param name="duration">热点持续时间，单位为秒。</param>
    /// <param name="sourceObjectId">刷怪点、波次或敌群的来源对象 ID；若没有则可传 0。</param>
    /// <param name="debugTag">调试标签，例如 SpawnPoint_A、Wave_02。</param>
    /// <returns>返回被创建或刷新的热点 ID。</returns>
    public static int RegisterSpawnHotspot(Vector3 center, float radius, float duration, ulong sourceObjectId = 0, string debugTag = null)
    {
        return InterestRegionManager.Instance.RegisterSpawnRegion(center, radius, duration, sourceObjectId, debugTag);
    }

    /// <summary>
    /// 注册或刷新一个脚本热点。
    /// 该接口适合临时事件、关卡脚本或测试代码快速驱动服务端兴趣区。
    /// </summary>
    /// <param name="center">热点中心点。</param>
    /// <param name="radius">热点半径。</param>
    /// <param name="duration">热点持续时间，单位为秒。</param>
    /// <param name="sourceObjectId">来源对象 ID；若没有则可传 0。</param>
    /// <param name="debugTag">调试标签。</param>
    /// <returns>返回被创建或刷新的热点 ID。</returns>
    public static int RegisterScriptedHotspot(Vector3 center, float radius, float duration, ulong sourceObjectId = 0, string debugTag = null)
    {
        return InterestRegionManager.Instance.RegisterRegion(
            center,
            radius,
            duration,
            InterestRegionSourceType.Scripted,
            sourceObjectId,
            debugTag);
    }

    /// <summary>
    /// 按来源对象移除热点。
    /// 该接口适合任务结束、波次结束或脚本失效时统一清理对应来源的热点。
    /// </summary>
    /// <param name="sourceObjectId">要清理的来源对象 ID。</param>
    public static void RemoveHotspotsBySource(ulong sourceObjectId)
    {
        InterestRegionManager.Instance.RemoveRegionsBySource(sourceObjectId);
    }
}
