using System.Collections.Generic;
using Matrix.PCG;
using UnityEngine;

/// <summary>
/// PCG 地图拓扑 Provider 桥接组件。
/// 该组件负责把现有 PcgMapGenerator 生成出的 RoomGraph 与房间实例，
/// 适配为 AI 侧可消费的 IPCGMapTopologyProvider。
/// </summary>
[DisallowMultipleComponent]
public class PcgMapTopologyProviderBridge : MonoBehaviour, IPCGMapTopologyProvider
{
    /// <summary>
    /// 运行时房间缓存记录。
    /// 该结构体保存 AI 解析房间归属与锚点所需的最小信息。
    /// </summary>
    private struct RoomRecord
    {
        /// <summary>
        /// 房间节点 ID。
        /// 该值直接对应 RoomGraph 中的 NodeId。
        /// </summary>
        public int nodeId;

        /// <summary>
        /// 房间根对象引用。
        /// 主要用于调试和重新计算边界。
        /// </summary>
        public PcgRoomRoot roomRoot;

        /// <summary>
        /// 房间世界包围盒。
        /// 用于把世界坐标解析到房间节点。
        /// </summary>
        public Bounds worldBounds;

        /// <summary>
        /// 房间推荐锚点。
        /// 当前阶段默认取房间根位置或包围盒中心。
        /// </summary>
        public Vector3 anchorPosition;
    }

    /// <summary>
    /// 目标 PCG 地图生成器。
    /// 若未显式指定，则会优先在当前对象上查找，再退化为场景内查找。
    /// </summary>
    [SerializeField]
    private PcgMapGenerator mapGenerator;

    /// <summary>
    /// 是否在组件启用时自动向 PCGMapTopologyService 注册自己。
    /// </summary>
    [SerializeField]
    private bool autoRegisterOnEnable = true;

    /// <summary>
    /// 是否在运行中自动侦测 PcgMapGenerator.LastResult 的变化并刷新缓存。
    /// </summary>
    [SerializeField]
    private bool autoRefreshGeneratorResult = true;

    /// <summary>
    /// 自动刷新间隔。
    /// 该值用于避免每帧都重复扫描房间结果。
    /// </summary>
    [SerializeField]
    [Min(0.1f)]
    private float refreshInterval = 0.5f;

    /// <summary>
    /// 当目标点不在任何房间包围盒内时，允许按最近房间退化解析的最大距离。
    /// 该值用于处理门口、走廊拼缝或房间边界外少量偏移。
    /// </summary>
    [SerializeField]
    [Min(0f)]
    private float nearestRoomFallbackDistance = 6f;

    /// <summary>
    /// 是否输出桥接层调试日志。
    /// </summary>
    [SerializeField]
    private bool verboseLog;

    /// <summary>
    /// 当前缓存的生成结果引用。
    /// 若生成器重新生成地图并替换结果对象，则桥接层会检测到变化并重建缓存。
    /// </summary>
    private PcgMapGenerationResult _cachedResult;

    /// <summary>
    /// 房间缓存列表。
    /// 用于顺序扫描解析世界坐标所属房间。
    /// </summary>
    private readonly List<RoomRecord> _rooms = new List<RoomRecord>();

    /// <summary>
    /// 节点 ID 到房间记录索引的映射。
    /// 用于快速查询房间锚点。
    /// </summary>
    private readonly Dictionary<int, int> _roomIndexByNodeId = new Dictionary<int, int>();

    /// <summary>
    /// 区域间 Socket 连接缓存。
    /// Key: (min << 16) | max（两节点 ID 的有序对），保证 (A,B) 和 (B,A) 映射到同一 Key。
    /// Value: 对应的 PcgRoomConnection。
    /// 用于 L2 Socket 层快速查找跨房间的 Socket 对。
    /// </summary>
    private readonly Dictionary<long, PcgRoomConnection> _connectionByRegionPair = new Dictionary<long, PcgRoomConnection>();

    private readonly List<PcgRoomConnection> _connectionList = new List<PcgRoomConnection>();

    /// <summary>
    /// 下一次允许自动刷新的时间。
    /// </summary>
    private float _nextRefreshTime;

    /// <summary>
    /// Unity 启用回调。
    /// 在运行期初始化桥接关系并尝试构建首帧缓存。
    /// </summary>
    private void OnEnable()
    {
        ResolveMapGenerator();
        RefreshCache(force: true);

        if (autoRegisterOnEnable)
        {
            PCGMapTopologyService.Instance.RegisterProvider(this);
        }
    }

    /// <summary>
    /// Unity 禁用回调。
    /// 组件失活时主动从拓扑服务中注销，避免残留失效 Provider。
    /// </summary>
    private void OnDisable()
    {
        if (autoRegisterOnEnable)
        {
            PCGMapTopologyService.Instance.UnregisterProvider(this);
        }
    }

    /// <summary>
    /// Unity 每帧更新回调。
    /// 用于在运行时低频检测生成结果是否发生变化。
    /// </summary>
    private void Update()
    {
        if (!autoRefreshGeneratorResult)
            return;

        if (Time.unscaledTime < _nextRefreshTime)
            return;

        _nextRefreshTime = Time.unscaledTime + Mathf.Max(0.1f, refreshInterval);
        RefreshCache(force: false);
    }

    /// <summary>
    /// 手动刷新 PCG 拓扑缓存。
    /// 该入口便于你在编辑器里直接触发一次重建。
    /// </summary>
    [ContextMenu("Refresh PCG Topology Cache")]
    public void RefreshTopologyCache()
    {
        RefreshCache(force: true);
    }

    /// <summary>
    /// 尝试根据世界坐标解析所属房间节点 ID。
    /// 优先使用房间包围盒命中；若未命中，则按最近房间做有限距离退化。
    /// </summary>
    /// <param name="worldPosition">需要解析的世界坐标。</param>
    /// <param name="regionId">输出解析到的房间节点 ID。</param>
    /// <returns>返回 true 表示成功解析到有效房间。</returns>
    public bool TryGetRegionId(Vector3 worldPosition, out int regionId)
    {
        RefreshCache(force: false);

        regionId = -1;
        if (_rooms.Count == 0)
            return false;

        var flatWorldPosition = Flatten(worldPosition);
        var bestDistanceSqr = float.PositiveInfinity;
        var bestNodeId = -1;

        for (var i = 0; i < _rooms.Count; i++)
        {
            var room = _rooms[i];
            if (ContainsXZ(room.worldBounds, flatWorldPosition))
            {
                regionId = room.nodeId;
                return true;
            }

            var distanceSqr = ComputePlanarBoundsDistanceSqr(room.worldBounds, flatWorldPosition);
            if (distanceSqr < bestDistanceSqr)
            {
                bestDistanceSqr = distanceSqr;
                bestNodeId = room.nodeId;
            }
        }

        if (bestNodeId >= 0 && bestDistanceSqr <= nearestRoomFallbackDistance * nearestRoomFallbackDistance)
        {
            regionId = bestNodeId;
            return true;
        }

        return false;
    }

    /// <summary>
    /// 尝试查询起点房间到终点房间的区域级路径。
    /// 当前直接基于 RoomGraph 的邻接关系做 BFS 最短路径。
    /// </summary>
    /// <param name="startRegionId">起点房间节点 ID。</param>
    /// <param name="targetRegionId">终点房间节点 ID。</param>
    /// <param name="regionPathBuffer">用于写入路径结果的外部缓冲区。</param>
    /// <returns>返回 true 表示成功得到房间级路径。</returns>
    public bool TryFindRegionPath(int startRegionId, int targetRegionId, List<int> regionPathBuffer)
    {
        RefreshCache(force: false);

        if (regionPathBuffer == null)
            return false;

        regionPathBuffer.Clear();

        var graph = _cachedResult != null ? _cachedResult.Graph : null;
        if (graph == null)
            return false;

        if (startRegionId < 0 || startRegionId >= graph.NodeCount)
            return false;

        if (targetRegionId < 0 || targetRegionId >= graph.NodeCount)
            return false;

        if (startRegionId == targetRegionId)
        {
            regionPathBuffer.Add(startRegionId);
            return true;
        }

        var visited = new bool[graph.NodeCount];
        var previous = new int[graph.NodeCount];
        for (var i = 0; i < previous.Length; i++)
        {
            previous[i] = -1;
        }

        var queue = new Queue<int>();
        queue.Enqueue(startRegionId);
        visited[startRegionId] = true;

        var found = false;
        while (queue.Count > 0)
        {
            var currentNodeId = queue.Dequeue();
            var neighbors = graph.GetNeighborsSorted(currentNodeId);
            for (var i = 0; i < neighbors.Count; i++)
            {
                var nextNodeId = neighbors[i];
                if (nextNodeId < 0 || nextNodeId >= graph.NodeCount || visited[nextNodeId])
                    continue;

                visited[nextNodeId] = true;
                previous[nextNodeId] = currentNodeId;

                if (nextNodeId == targetRegionId)
                {
                    found = true;
                    queue.Clear();
                    break;
                }

                queue.Enqueue(nextNodeId);
            }
        }

        if (!found)
            return false;

        BuildPathFromPrevious(targetRegionId, previous, regionPathBuffer);
        return regionPathBuffer.Count > 0;
    }

    /// <summary>
    /// 尝试获取指定房间节点的推荐锚点。
    /// 当前阶段优先返回缓存的房间中心点。
    /// </summary>
    /// <param name="regionId">房间节点 ID。</param>
    /// <param name="anchorPosition">输出推荐锚点位置。</param>
    /// <returns>返回 true 表示成功得到锚点。</returns>
    public bool TryGetRegionAnchor(int regionId, out Vector3 anchorPosition)
    {
        RefreshCache(force: false);

        if (_roomIndexByNodeId.TryGetValue(regionId, out var roomIndex) &&
            roomIndex >= 0 &&
            roomIndex < _rooms.Count)
        {
            anchorPosition = _rooms[roomIndex].anchorPosition;
            return true;
        }

        anchorPosition = default;
        return false;
    }

    /// <summary>
    /// 刷新桥接缓存。
    /// 若生成结果未发生变化，则只做轻量检查，不重复重建。
    /// </summary>
    /// <param name="force">是否强制刷新。</param>
    private void RefreshCache(bool force)
    {
        ResolveMapGenerator();

        var latestResult = mapGenerator != null ? mapGenerator.LastResult : null;
        if (!force && ReferenceEquals(latestResult, _cachedResult))
            return;

        _cachedResult = latestResult;
        RebuildRoomCache();
        RebuildConnectionCache();

        if (verboseLog)
        {
            var roomCount = _rooms.Count;
            var nodeCount = _cachedResult != null && _cachedResult.Graph != null ? _cachedResult.Graph.NodeCount : 0;
            AIDebug.LogChannel("AI.Navigation", $"[PcgMapTopologyProviderBridge] Cache refreshed. Rooms={roomCount}, GraphNodes={nodeCount}", this);
        }
    }

    /// <summary>
    /// 解析并缓存目标 PcgMapGenerator。
    /// 若序列化字段未赋值，则优先尝试当前对象，再退化为场景查找。
    /// </summary>
    private void ResolveMapGenerator()
    {
        if (mapGenerator != null)
            return;

        mapGenerator = GetComponent<PcgMapGenerator>();
        if (mapGenerator != null)
            return;

        mapGenerator = FindObjectOfType<PcgMapGenerator>();
    }

    /// <summary>
    /// 根据当前生成结果重建房间缓存。
    /// </summary>
    private void RebuildRoomCache()
    {
        _rooms.Clear();
        _roomIndexByNodeId.Clear();

        if (_cachedResult == null || _cachedResult.PlacedRooms == null)
            return;

        for (var i = 0; i < _cachedResult.PlacedRooms.Count; i++)
        {
            var placedRoom = _cachedResult.PlacedRooms[i];
            if (placedRoom == null || placedRoom.RoomInstance == null)
                continue;

            var roomRoot = placedRoom.RoomInstance;
            if (!TryResolveRoomBounds(roomRoot, out var worldBounds))
                continue;

            var anchorPosition = ResolveRoomAnchor(roomRoot, worldBounds);
            var roomRecord = new RoomRecord
            {
                nodeId = placedRoom.NodeId,
                roomRoot = roomRoot,
                worldBounds = worldBounds,
                anchorPosition = anchorPosition
            };

            _roomIndexByNodeId[roomRecord.nodeId] = _rooms.Count;
            _rooms.Add(roomRecord);
        }
    }

    /// <summary>
    /// 尝试解析房间世界包围盒。
    /// </summary>
    /// <param name="roomRoot">目标房间根对象。</param>
    /// <param name="worldBounds">输出房间世界包围盒。</param>
    /// <returns>返回 true 表示成功得到房间边界。</returns>
    private static bool TryResolveRoomBounds(PcgRoomRoot roomRoot, out Bounds worldBounds)
    {
        if (roomRoot != null && roomRoot.TryGetWorldBounds(out worldBounds, out _))
        {
            return true;
        }

        worldBounds = default;
        return false;
    }

    /// <summary>
    /// 解析房间推荐锚点。
    /// 当前优先使用房间根位置，其次退化为包围盒中心。
    /// </summary>
    /// <param name="roomRoot">目标房间根对象。</param>
    /// <param name="worldBounds">房间世界包围盒。</param>
    /// <returns>返回房间推荐锚点。</returns>
    private static Vector3 ResolveRoomAnchor(PcgRoomRoot roomRoot, Bounds worldBounds)
    {
        if (roomRoot != null)
        {
            var anchor = roomRoot.transform.position;
            anchor.y = worldBounds.center.y;
            return anchor;
        }

        return worldBounds.center;
    }

    /// <summary>
    /// 重建区域间连接缓存。
    /// 构建 (NodeA, NodeB) 有序对 → PcgRoomConnection 的映射，
    /// 用于 L2 Socket 层快速查询两房间之间的 Socket 对。
    /// </summary>
    private void RebuildConnectionCache()
    {
        _connectionByRegionPair.Clear();
        _connectionList.Clear();

        if (_cachedResult == null || _cachedResult.Connections == null)
            return;

        foreach (var conn in _cachedResult.Connections)
        {
            if (conn == null || !conn.IsResolved)
                continue;

            var key = MakePairKey(conn.NodeA, conn.NodeB);
            _connectionByRegionPair[key] = conn;
            _connectionList.Add(conn);
        }
    }

    /// <summary>
    /// 将两个节点 ID 编码为有序 64 位键，保证 (A,B) 和 (B,A) 映射到同一值。
    /// </summary>
    private static long MakePairKey(int a, int b)
    {
        if (a > b)
        {
            var tmp = a;
            a = b;
            b = tmp;
        }
        return ((long)a << 32) | ((uint)b);
    }

    /// <summary>
    /// L2 Socket 层核心实现：
    /// 查询从 currentRegion 出发前往 targetRegion 时，应该走哪一对 Socket。
    ///
    /// 策略：
    /// 1. 在连接缓存中查找 (currentRegion, targetRegion) 对应的 PcgRoomConnection。
    /// 2. 从中提取 ConnectorFrom / ConnectorTo。
    /// 3. 根据语义方向（ConnectorFromOutgoing / ConnectorToOutgoing）确定：
    ///    - 出口 Socket：当前区域作为起点，走 outgoing 方向的那个 Connector
    ///    - 入口 Socket：目标区域作为入口，走 incoming 方向的那个 Connector
    /// 4. 用 fromPosition 选择最近的有效 Socket（同区域可能有多个出口）。
    ///
    /// 返回值：
    /// - exitSocketWorldPos：敌人当前所在房间的出发门世界坐标
    /// - exitSocketNormal：出发门的几何法线（指向门外侧）
    /// - entrySocketWorldPos：目标房间内的到达门世界坐标
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

        if (currentRegionId == targetRegionId)
            return false;

        var key = MakePairKey(currentRegionId, targetRegionId);
        if (!_connectionByRegionPair.TryGetValue(key, out var conn))
            return false;

        PcgConnectorMarker exitMarker;
        PcgConnectorMarker entryMarker;

        if (conn.ConnectorFromOutgoing)
        {
            exitMarker = conn.ConnectorFrom;
            entryMarker = conn.ConnectorTo;
        }
        else
        {
            exitMarker = conn.ConnectorTo;
            entryMarker = conn.ConnectorFrom;
        }

        if (exitMarker == null || entryMarker == null)
            return false;

        if (!exitMarker.SupportsOutgoing)
            return false;

        exitSocketWorldPos = exitMarker.GetSocketWorldPoint(true);
        exitSocketNormal = exitMarker.GetSocketNormal(true);

        if (entryMarker.SupportsIncoming)
            entrySocketWorldPos = entryMarker.GetSocketWorldPoint(false);
        else
            entrySocketWorldPos = entryMarker.GetSocketBaseWorldPoint();

        return true;
    }

    /// <summary>
    /// L1→L2 翻译层：基于 regionPath 返回拓扑层面的下一跳锚点。
    /// - path.Count <= 1（同房间）：返回 finalTarget。
    /// - path.Count >= 2：返回第二个房间的锚点。
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
        if (TryGetRegionAnchor(nextRegionId, out var anchor))
        {
            topologyNextAnchor = anchor;
            return true;
        }

        return false;
    }

    /// <summary>
    /// 根据 BFS 的前驱数组重建完整路径。
    /// </summary>
    /// <param name="targetRegionId">终点房间节点 ID。</param>
    /// <param name="previous">前驱数组。</param>
    /// <param name="regionPathBuffer">输出路径缓冲区。</param>
    private static void BuildPathFromPrevious(int targetRegionId, int[] previous, List<int> regionPathBuffer)
    {
        var reversePath = new List<int>(16);
        var currentNodeId = targetRegionId;

        while (currentNodeId >= 0)
        {
            reversePath.Add(currentNodeId);
            currentNodeId = previous[currentNodeId];
        }

        for (var i = reversePath.Count - 1; i >= 0; i--)
        {
            regionPathBuffer.Add(reversePath[i]);
        }
    }

    /// <summary>
    /// 判断一个世界坐标是否在房间包围盒的水平投影内。
    /// 该方法只检查 XZ 平面，不要求 Y 轴完全落在房间高度范围中。
    /// </summary>
    /// <param name="bounds">目标包围盒。</param>
    /// <param name="worldPosition">待检测的世界坐标。</param>
    /// <returns>返回 true 表示水平位置命中房间范围。</returns>
    private static bool ContainsXZ(Bounds bounds, Vector3 worldPosition)
    {
        return worldPosition.x >= bounds.min.x &&
               worldPosition.x <= bounds.max.x &&
               worldPosition.z >= bounds.min.z &&
               worldPosition.z <= bounds.max.z;
    }

    /// <summary>
    /// 计算一个世界坐标到房间包围盒的水平距离平方。
    /// </summary>
    /// <param name="bounds">目标包围盒。</param>
    /// <param name="worldPosition">待计算的世界坐标。</param>
    /// <returns>返回在 XZ 平面上的距离平方。</returns>
    private static float ComputePlanarBoundsDistanceSqr(Bounds bounds, Vector3 worldPosition)
    {
        var closestX = Mathf.Clamp(worldPosition.x, bounds.min.x, bounds.max.x);
        var closestZ = Mathf.Clamp(worldPosition.z, bounds.min.z, bounds.max.z);
        var dx = worldPosition.x - closestX;
        var dz = worldPosition.z - closestZ;
        return dx * dx + dz * dz;
    }

    /// <summary>
    /// 将世界坐标压平到水平面。
    /// 该工具方法用于统一房间归属解析时的平面距离计算。
    /// </summary>
    /// <param name="worldPosition">原始世界坐标。</param>
    /// <returns>返回压平后的坐标。</returns>
    private static Vector3 Flatten(Vector3 worldPosition)
    {
        worldPosition.y = 0f;
        return worldPosition;
    }
}
