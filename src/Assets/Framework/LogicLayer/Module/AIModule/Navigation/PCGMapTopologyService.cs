using System.Collections.Generic;
using Framework.Singleton;
using UnityEngine;

/// <summary>
/// PCG 地图拓扑服务。
/// 当前阶段只提供“AI 侧统一访问入口 + Provider 注入骨架”，
/// 不假定仓库里已经存在完整的房间图实现。
/// </summary>
public class PCGMapTopologyService : SingletonBase<PCGMapTopologyService>
{
    /// <summary>
    /// 当前注册的拓扑数据提供者。
    /// 正式的 PCG 地图系统接入后，应在服务端把真实实现注册到这里。
    /// </summary>
    private IPCGMapTopologyProvider _provider;

    /// <summary>
    /// 记录上一次 Provider 序列化 ID，用于检测 Provider 是否发生替换。
    /// </summary>
    private int _lastRegisteredProviderId;

    /// <summary>
    /// 注册拓扑数据提供者。
    /// </summary>
    /// <param name="provider">提供房间/区块拓扑数据的实现。</param>
    public void RegisterProvider(IPCGMapTopologyProvider provider)
    {
        if (provider == null)
            return;

        _provider = provider;
        _lastRegisteredProviderId = provider.GetHashCode();

        AIDebug.LogChannel("AI.Navigation", $"[PCGMapTopologyService] Provider 已注册: {provider.GetType().Name}，触发 OnProviderRegistered 通知");
        OnProviderRegistered?.Invoke(provider);
    }

    /// <summary>
    /// 当 Provider 注册（或重新注册）时触发。
    /// 用于通知各 Consumer（如 PathService）刷新缓存。
    /// </summary>
    public event System.Action<IPCGMapTopologyProvider> OnProviderRegistered;

    /// <summary>
    /// 注销拓扑数据提供者。
    /// </summary>
    /// <param name="provider">准备移除的拓扑数据提供者实现。</param>
    public void UnregisterProvider(IPCGMapTopologyProvider provider)
    {
        if (_provider == provider)
        {
            AIDebug.LogChannel("AI.Navigation", $"[PCGMapTopologyService] Provider 注销: {provider?.GetType().Name}");
            _provider = null;
            _lastRegisteredProviderId = 0;
            OnProviderRegistered = null;
        }
    }

    /// <summary>
    /// 判断当前是否存在可用的拓扑数据提供者。
    /// </summary>
    /// <returns>返回 true 表示已经有系统向 AI 层提供 PCG 拓扑数据。</returns>
    public bool HasProvider()
    {
        return _provider != null;
    }

    /// <summary>
    /// 根据世界坐标解析所属区域。
    /// </summary>
    /// <param name="worldPosition">需要解析的世界坐标。</param>
    /// <param name="regionId">输出区域 ID。</param>
    /// <returns>返回 true 表示成功解析到区域。</returns>
    public bool TryResolveRegion(Vector3 worldPosition, out int regionId)
    {
        if (_provider == null)
        {
            regionId = -1;
            return false;
        }

        return _provider.TryGetRegionId(worldPosition, out regionId);
    }

    /// <summary>
    /// 查询起点与终点之间的区域级路径。
    /// </summary>
    /// <param name="startPosition">路径起点世界坐标。</param>
    /// <param name="targetPosition">路径终点世界坐标。</param>
    /// <param name="regionPathBuffer">用于写入区域路径的外部缓冲区。</param>
    /// <returns>返回 true 表示成功得到区域路径。</returns>
    public bool TryBuildRegionPath(Vector3 startPosition, Vector3 targetPosition, List<int> regionPathBuffer)
    {
        if (regionPathBuffer == null)
            return false;

        regionPathBuffer.Clear();

        if (_provider == null)
            return false;

        if (!_provider.TryGetRegionId(startPosition, out var startRegionId))
            return false;

        if (!_provider.TryGetRegionId(targetPosition, out var targetRegionId))
            return false;

        if (startRegionId == targetRegionId)
        {
            regionPathBuffer.Add(startRegionId);
            return true;
        }

        return _provider.TryFindRegionPath(startRegionId, targetRegionId, regionPathBuffer)
               && regionPathBuffer.Count > 0;
    }

    /// <summary>
    /// 获取指定区域的推荐锚点。
    /// </summary>
    /// <param name="regionId">需要获取锚点的区域 ID。</param>
    /// <param name="anchorPosition">输出锚点坐标。</param>
    /// <returns>返回 true 表示成功获取锚点。</returns>
    public bool TryGetRegionAnchor(int regionId, out Vector3 anchorPosition)
    {
        if (_provider == null)
        {
            anchorPosition = default;
            return false;
        }

        return _provider.TryGetRegionAnchor(regionId, out anchorPosition);
    }

    /// <summary>
    /// 查询从当前区域出发，前往相邻目标区域时，应该经过哪一对 Socket。
    /// L2 Socket 层的核心方法：负责"房间→房间"这一跳的穿门决策。
    /// </summary>
    public bool TryGetSocketPath(
        int currentRegionId,
        int targetRegionId,
        Vector3 fromPosition,
        out Vector3 exitSocketWorldPos,
        out Vector3 exitSocketNormal,
        out Vector3 entrySocketWorldPos)
    {
        exitSocketWorldPos = default;
        exitSocketNormal = default;
        entrySocketWorldPos = default;

        if (_provider == null)
            return false;

        return _provider.TryGetSocketPath(
            currentRegionId, targetRegionId, fromPosition,
            out exitSocketWorldPos, out exitSocketNormal, out entrySocketWorldPos);
    }

    /// <summary>
    /// 基于拓扑路径获取下一跳锚点。
    /// 当路径只有一个房间时返回最终目标；
    /// 否则返回第二个房间的锚点。
    /// </summary>
    public bool TryGetTopologyNextAnchor(
        List<int> regionPath,
        Vector3 currentPosition,
        Vector3 finalTarget,
        out Vector3 topologyNextAnchor)
    {
        topologyNextAnchor = default;

        if (regionPath == null || regionPath.Count == 0)
            return false;

        if (regionPath.Count <= 1)
        {
            topologyNextAnchor = finalTarget;
            return true;
        }

        var nextRegionId = regionPath[1];
        if (!TryGetRegionAnchor(nextRegionId, out var anchor))
            return false;

        topologyNextAnchor = anchor;
        return true;
    }
}
