using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// PCG 地图拓扑数据提供者接口。
/// 该接口由未来真正持有房间/区块图数据的系统实现，
/// PathService 与 AI 模块只依赖该接口，不直接依赖具体 PCG 生成实现。
/// </summary>
public interface IPCGMapTopologyProvider
{
    /// <summary>
    /// 根据世界坐标解析其所属的房间或区块 ID。
    /// </summary>
    /// <param name="worldPosition">需要解析的世界坐标。</param>
    /// <param name="regionId">输出解析得到的房间或区块 ID。</param>
    /// <returns>返回 true 表示成功解析到有效区域。</returns>
    bool TryGetRegionId(Vector3 worldPosition, out int regionId);

    /// <summary>
    /// 根据起点区域和终点区域查询区域级路径。
    /// </summary>
    /// <param name="startRegionId">起点房间或区块 ID。</param>
    /// <param name="targetRegionId">终点房间或区块 ID。</param>
    /// <param name="regionPathBuffer">用于写入区域路径结果的外部缓冲区。</param>
    /// <returns>返回 true 表示成功得到区域级路径。</returns>
    bool TryFindRegionPath(int startRegionId, int targetRegionId, List<int> regionPathBuffer);

    /// <summary>
    /// 根据区域 ID 获取推荐进入点或锚点。
    /// </summary>
    /// <param name="regionId">目标区域 ID。</param>
    /// <param name="anchorPosition">输出该区域建议前往的锚点坐标。</param>
    /// <returns>返回 true 表示成功得到可用于移动的锚点。</returns>
    bool TryGetRegionAnchor(int regionId, out Vector3 anchorPosition);

    /// <summary>
    /// 查询从当前区域出发，前往相邻目标区域时，应该经过哪一对 Socket。
    /// L2 Socket 层的核心方法：负责"房间→房间"这一跳的穿门决策。
    ///
    /// 语义：
    /// - 从 currentRegion 出发，选一个 outgoing Socket 向 targetRegion 走
    /// - 返回：该 Socket 的出口世界坐标（敌人从房间内出发要去的门点）
    /// - 返回：该 Socket 的几何法线（指向门外，用于引导敌人朝向门的方向）
    /// - 返回：targetRegion 对应的入口 Socket 的世界坐标（敌人跨过门后应该到达的目标房间内的点）
    ///
    /// 当 currentRegion == targetRegion 时返回 false（同一房间内不涉及 Socket 决策）。
    /// </summary>
    /// <param name="currentRegionId">敌人当前所在的区域 ID。</param>
    /// <param name="targetRegionId">敌人想要进入的相邻区域 ID。</param>
    /// <param name="fromPosition">敌人当前世界坐标（用于选择最近的 Socket）。</param>
    /// <param name="exitSocketWorldPos">输出当前区域出口 Socket 的世界坐标。</param>
    /// <param name="exitSocketNormal">输出当前区域出口 Socket 的几何法线（指向门外侧）。</param>
    /// <param name="entrySocketWorldPos">输出目标区域入口 Socket 的世界坐标（跨门后到达点）。</param>
    /// <returns>返回 true 表示找到有效的 Socket 对；返回 false 表示两区域之间无有效连接。</returns>
    bool TryGetSocketPath(
        int currentRegionId,
        int targetRegionId,
        Vector3 fromPosition,
        out Vector3 exitSocketWorldPos,
        out Vector3 exitSocketNormal,
        out Vector3 entrySocketWorldPos);

    /// <summary>
    /// 查询敌人从当前位置跨房间时，应该去往的"拓扑下一跳锚点"。
    /// 与 TryGetSocketPath 不同的是：它基于 regionPath（拓扑层输出）来选择目标房间锚点。
    ///
    /// 当 regionPath.Count <= 1 时直接返回 targetPosition；
    /// 当 regionPath.Count >= 2 时返回第二个区域的锚点（入口锚点）。
    ///
    /// 这是 PathService L1 → L2 之间的"翻译层"。
    /// </summary>
    /// <param name="regionPath">从拓扑服务获得的房间节点 ID 序列。</param>
    /// <param name="currentPosition">敌人当前世界坐标。</param>
    /// <param name="finalTarget">全局目标位置。</param>
    /// <param name="topologyNextAnchor">输出拓扑层面的下一跳锚点（可能是房间锚点或 Socket 坐标）。</param>
    /// <returns>返回 true 表示成功。</returns>
    bool TryGetTopologyNextAnchor(
        List<int> regionPath,
        Vector3 currentPosition,
        Vector3 finalTarget,
        out Vector3 topologyNextAnchor);
}
