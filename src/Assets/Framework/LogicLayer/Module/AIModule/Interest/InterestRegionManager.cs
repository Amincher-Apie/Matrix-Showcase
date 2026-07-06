using System.Collections.Generic;
using Framework.Singleton;
using UnityEngine;

/// <summary>
/// 兴趣区域管理器。
/// 当前阶段先提供服务端热点注册与查询骨架，后续可继续接入房间系统、任务目标和 PCG 拓扑。
/// </summary>
public class InterestRegionManager : SingletonBase<InterestRegionManager>
{
    /// <summary>
    /// 单条兴趣热点记录。
    /// </summary>
    private struct InterestRegionRecord
    {
        /// <summary>
        /// 热点唯一 ID。
        /// </summary>
        public int id;

        /// <summary>
        /// 热点中心点。
        /// </summary>
        public Vector3 center;

        /// <summary>
        /// 热点半径。
        /// </summary>
        public float radius;

        /// <summary>
        /// 热点过期时间。
        /// </summary>
        public float expireAt;

        /// <summary>
        /// 该热点的来源对象 ID。
        /// </summary>
        public ulong sourceObjectId;

        /// <summary>
        /// 热点来源类型。
        /// </summary>
        public InterestRegionSourceType sourceType;

        /// <summary>
        /// 调试标签。
        /// 用于帮助区分任务热点、刷怪热点和临时脚本热点。
        /// </summary>
        public string debugTag;
    }

    /// <summary>
    /// 当前所有已登记的兴趣热点。
    /// </summary>
    private readonly List<InterestRegionRecord> _regions = new List<InterestRegionRecord>();

    /// <summary>
    /// 递增的热点 ID 计数器。
    /// </summary>
    private int _nextRegionId = 1;

    /// <summary>
    /// 注册一个临时兴趣热点。
    /// 该接口可用于未来的战斗热点、任务推进点或刷怪激活区域。
    /// </summary>
    /// <param name="center">热点中心点。</param>
    /// <param name="radius">热点半径。</param>
    /// <param name="duration">热点持续时间，单位为秒。</param>
    /// <param name="sourceObjectId">触发该热点的对象 ID。</param>
    /// <returns>返回该热点的唯一 ID。</returns>
    public int RegisterTemporaryRegion(Vector3 center, float radius, float duration, ulong sourceObjectId = 0)
    {
        return RegisterRegion(center, radius, duration, InterestRegionSourceType.Unknown, sourceObjectId, null);
    }

    /// <summary>
    /// 注册或刷新一个通用兴趣热点。
    /// 该接口是当前管理器的统一底层入口，外部系统应优先通过来源类型明确的包装方法接入。
    /// </summary>
    /// <param name="center">热点中心点。</param>
    /// <param name="radius">热点半径。</param>
    /// <param name="duration">热点持续时间，单位为秒。</param>
    /// <param name="sourceType">热点来源类型。</param>
    /// <param name="sourceObjectId">热点来源对象 ID。</param>
    /// <param name="debugTag">调试标签。</param>
    /// <returns>返回被创建的热点 ID。</returns>
    public int RegisterRegion(
        Vector3 center,
        float radius,
        float duration,
        InterestRegionSourceType sourceType,
        ulong sourceObjectId = 0,
        string debugTag = null)
    {
        CleanupExpiredRegions();

        var id = _nextRegionId++;
        _regions.Add(new InterestRegionRecord
        {
            id = id,
            center = center,
            radius = Mathf.Max(0f, radius),
            expireAt = Time.time + Mathf.Max(0f, duration),
            sourceObjectId = sourceObjectId,
            sourceType = sourceType,
            debugTag = debugTag
        });

        return id;
    }

    /// <summary>
    /// 按来源对象注册或刷新一个临时兴趣热点。
    /// 该接口用于后续任务系统、刷怪系统或战斗事件在没有具体目标实体类时，仍能稳定地驱动服务端兴趣区更新。
    /// </summary>
    /// <param name="center">热点中心点。</param>
    /// <param name="radius">热点半径。</param>
    /// <param name="duration">热点持续时间，单位为秒。</param>
    /// <param name="sourceObjectId">热点来源对象 ID；当该值为 0 时会退化为一次性注册。</param>
    /// <returns>返回被创建或刷新的热点 ID。</returns>
    public int RegisterOrRefreshRegionBySource(Vector3 center, float radius, float duration, ulong sourceObjectId)
    {
        return RegisterOrRefreshRegionBySource(center, radius, duration, InterestRegionSourceType.Unknown, sourceObjectId, null);
    }

    /// <summary>
    /// 按来源对象注册或刷新一个带来源类型的兴趣热点。
    /// 该接口可让任务系统、刷怪系统或战斗系统在不创建具体目标实体的前提下稳定维护同一个热点。
    /// </summary>
    /// <param name="center">热点中心点。</param>
    /// <param name="radius">热点半径。</param>
    /// <param name="duration">热点持续时间，单位为秒。</param>
    /// <param name="sourceType">热点来源类型。</param>
    /// <param name="sourceObjectId">热点来源对象 ID；当该值为 0 时会退化为一次性注册。</param>
    /// <param name="debugTag">调试标签。</param>
    /// <returns>返回被创建或刷新的热点 ID。</returns>
    public int RegisterOrRefreshRegionBySource(
        Vector3 center,
        float radius,
        float duration,
        InterestRegionSourceType sourceType,
        ulong sourceObjectId,
        string debugTag = null)
    {
        CleanupExpiredRegions();

        if (sourceObjectId == 0)
        {
            return RegisterRegion(center, radius, duration, sourceType, sourceObjectId, debugTag);
        }

        for (var i = 0; i < _regions.Count; i++)
        {
            var region = _regions[i];
            if (region.sourceObjectId != sourceObjectId)
                continue;

            region.center = center;
            region.radius = Mathf.Max(0f, radius);
            region.expireAt = Time.time + Mathf.Max(0f, duration);
            region.sourceType = sourceType;
            region.debugTag = debugTag;
            _regions[i] = region;
            return region.id;
        }

        return RegisterRegion(center, radius, duration, sourceType, sourceObjectId, debugTag);
    }

    /// <summary>
    /// 注册或刷新一个战斗热点。
    /// 该接口主要供服务端战斗事件调用，用于驱动附近敌人的仿真唤醒。
    /// </summary>
    /// <param name="center">热点中心点。</param>
    /// <param name="radius">热点半径。</param>
    /// <param name="duration">热点持续时间，单位为秒。</param>
    /// <param name="sourceObjectId">战斗来源对象 ID。</param>
    /// <param name="debugTag">调试标签。</param>
    /// <returns>返回被创建或刷新的热点 ID。</returns>
    public int RegisterCombatRegion(Vector3 center, float radius, float duration, ulong sourceObjectId = 0, string debugTag = null)
    {
        return RegisterOrRefreshRegionBySource(center, radius, duration, InterestRegionSourceType.Combat, sourceObjectId, debugTag);
    }

    /// <summary>
    /// 注册或刷新一个任务热点。
    /// 该接口供任务系统直接推送兴趣区域，而不要求当前仓库中已经存在任务目标实体类。
    /// </summary>
    /// <param name="center">热点中心点。</param>
    /// <param name="radius">热点半径。</param>
    /// <param name="duration">热点持续时间，单位为秒。</param>
    /// <param name="sourceObjectId">任务来源对象 ID。</param>
    /// <param name="debugTag">调试标签。</param>
    /// <returns>返回被创建或刷新的热点 ID。</returns>
    public int RegisterTaskRegion(Vector3 center, float radius, float duration, ulong sourceObjectId = 0, string debugTag = null)
    {
        return RegisterOrRefreshRegionBySource(center, radius, duration, InterestRegionSourceType.Task, sourceObjectId, debugTag);
    }

    /// <summary>
    /// 注册或刷新一个刷怪热点。
    /// 该接口为后续 SpawnDirector 或波次系统预留统一接入点。
    /// </summary>
    /// <param name="center">热点中心点。</param>
    /// <param name="radius">热点半径。</param>
    /// <param name="duration">热点持续时间，单位为秒。</param>
    /// <param name="sourceObjectId">刷怪来源对象 ID。</param>
    /// <param name="debugTag">调试标签。</param>
    /// <returns>返回被创建或刷新的热点 ID。</returns>
    public int RegisterSpawnRegion(Vector3 center, float radius, float duration, ulong sourceObjectId = 0, string debugTag = null)
    {
        return RegisterOrRefreshRegionBySource(center, radius, duration, InterestRegionSourceType.Spawn, sourceObjectId, debugTag);
    }

    /// <summary>
    /// 手动移除一个兴趣热点。
    /// </summary>
    /// <param name="regionId">要移除的热点 ID。</param>
    public void RemoveRegion(int regionId)
    {
        for (var i = _regions.Count - 1; i >= 0; i--)
        {
            if (_regions[i].id == regionId)
            {
                _regions.RemoveAt(i);
                return;
            }
        }
    }

    /// <summary>
    /// 按来源对象移除所有兴趣热点。
    /// </summary>
    /// <param name="sourceObjectId">热点来源对象 ID。</param>
    public void RemoveRegionsBySource(ulong sourceObjectId)
    {
        for (var i = _regions.Count - 1; i >= 0; i--)
        {
            if (_regions[i].sourceObjectId == sourceObjectId)
            {
                _regions.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// 判断指定位置附近是否存在有效兴趣热点。
    /// </summary>
    /// <param name="position">待检测的位置。</param>
    /// <param name="queryRadius">检测半径。</param>
    /// <returns>返回 true 表示附近存在有效兴趣热点。</returns>
    public bool HasInterestNear(Vector3 position, float queryRadius)
    {
        CleanupExpiredRegions();

        for (var i = 0; i < _regions.Count; i++)
        {
            var region = _regions[i];
            var combinedRadius = region.radius + Mathf.Max(0f, queryRadius);
            if ((region.center - position).sqrMagnitude <= combinedRadius * combinedRadius)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 获取当前所有有效兴趣热点的调试快照。
    /// 该接口不会改变热点状态，只会把当前有效记录复制到外部缓冲区中。
    /// </summary>
    /// <param name="resultsBuffer">用于写入调试结果的外部缓冲区。</param>
    public void GetDebugRegions(List<InterestRegionDebugInfo> resultsBuffer)
    {
        if (resultsBuffer == null)
            return;

        CleanupExpiredRegions();
        resultsBuffer.Clear();

        var now = Time.time;
        for (var i = 0; i < _regions.Count; i++)
        {
            var region = _regions[i];
            resultsBuffer.Add(new InterestRegionDebugInfo
            {
                id = region.id,
                sourceType = region.sourceType,
                sourceObjectId = region.sourceObjectId,
                center = region.center,
                radius = region.radius,
                expireAt = region.expireAt,
                remainingTime = Mathf.Max(0f, region.expireAt - now),
                debugTag = region.debugTag
            });
        }
    }

    /// <summary>
    /// 清理所有已过期的兴趣热点。
    /// </summary>
    private void CleanupExpiredRegions()
    {
        var now = Time.time;
        for (var i = _regions.Count - 1; i >= 0; i--)
        {
            if (_regions[i].expireAt <= now)
            {
                _regions.RemoveAt(i);
            }
        }
    }
}
