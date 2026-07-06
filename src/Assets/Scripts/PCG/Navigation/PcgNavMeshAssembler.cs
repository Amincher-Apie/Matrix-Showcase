using System;
using System.Collections;
using System.Collections.Generic;
using Matrix.PCG;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

namespace Matrix.PCG.Navigation
{
    /// <summary>
    /// PCG 地图生成完成后，负责装配预烘焙 NavMesh 的核心组件。
    ///
    /// 工作流程：
    /// 1. 监听 PcgMapGenerator.OnGenerationCompleted
    /// 2. 清理上一次运行时的 NavMeshDataInstance 和 NavMeshLink
    /// 3. 遍历 result.PlacedRooms，加载每个房间的预烘焙 NavMeshData
    /// 4. 根据 result.Connections 创建 NavMeshLink
    /// 5. 校验 SpawnPoint 是否在 NavMesh 上
    /// 6. 触发 OnNavMeshAssembled 事件
    ///
    /// 挂载位置：PcgMapGenerator 同一个物体，或 PCGRoot。
    /// </summary>
    public class PcgNavMeshAssembler : MonoBehaviour
    {
        [Header("PCG Reference")]
        [Tooltip("PCG 地图生成器引用。留空则自动查找场景中的 PcgMapGenerator。")]
        [SerializeField]
        private PcgMapGenerator generator;

        [Header("NavMesh Config")]
        [Tooltip("期望的 Agent Type ID。必须与所有 RoomPrebakedNavMeshAsset.agentTypeId 一致。")]
        [SerializeField]
        private int expectedAgentTypeId;

        [Tooltip("采样到 NavMesh 时使用的最大距离（米）。用于连接点和刷怪点吸附。")]
        [SerializeField]
        private float linkSampleDistance = 2f;

        [Tooltip("Link 端点从门缝向各自房间内部缩进的距离。避免门两侧采样到同一个 NavMeshData。")]
        [SerializeField]
        [Min(0.05f)]
        private float linkEndpointInset = 0.75f;

        [Tooltip("吸附后 Link 两端允许的最小距离。过短通常表示两端采样到了同一侧 NavMesh。")]
        [SerializeField]
        [Min(0.01f)]
        private float minimumLinkLength = 0.25f;

        [Tooltip("NavMeshLink 默认宽度（门宽）。")]
        [SerializeField]
        private float defaultLinkWidth = 2f;

        [Tooltip("NavMeshLink 是否双向通行。")]
        [SerializeField]
        private bool bidirectionalLinks = true;

        [Header("Runtime Links Root")]
        [Tooltip("运行时创建的 NavMeshLink 对象的父级 Transform。留空则自动创建。")]
        [SerializeField]
        private Transform runtimeLinkRoot;

        [Header("SpawnPoint Validation")]
        [Tooltip("刷怪点采样失败时是否输出警告日志。")]
        [SerializeField]
        private bool warnOnSpawnPointFailure = true;

        [Tooltip("刷怪点采样最大距离。")]
        [SerializeField]
        private float spawnPointSampleDistance = 2f;

        [Header("Debug")]
        [Tooltip("是否输出详细日志。")]
        [SerializeField]
        private bool verboseLog;

        private readonly List<NavMeshDataInstance> _navMeshInstances = new List<NavMeshDataInstance>();
        private readonly List<NavMeshLink> _runtimeLinks = new List<NavMeshLink>();
        private readonly List<PcgSpawnPointResult> _invalidSpawnPoints = new List<PcgSpawnPointResult>();

        private Transform _internalLinkRoot;
        private bool _isAssembled;
        private PcgMapGenerationResult _currentResult;

        public bool IsAssembled => _isAssembled;

        /// <summary>
        /// 当 NavMesh 装配完成时触发。
        /// 此时所有房间的 NavMeshData 已加载，NavMeshLink 已创建，刷怪点已校验。
        /// </summary>
        public event Action<PcgMapGenerationResult> OnNavMeshAssembled;

        /// <summary>
        /// 获取所有无效的刷怪点（采样失败）。
        /// </summary>
        public IReadOnlyList<PcgSpawnPointResult> InvalidSpawnPoints => _invalidSpawnPoints;

        /// <summary>
        /// 获取当前已加载的 NavMeshDataInstance 数量。
        /// </summary>
        public int LoadedRoomCount => _navMeshInstances.Count;

        /// <summary>
        /// 获取当前已创建的 NavMeshLink 数量。
        /// </summary>
        public int CreatedLinkCount => _runtimeLinks.Count;

        private void Awake()
        {
            ResolveLinkRoot();
        }

        private void OnEnable()
        {
            ResolveGenerator();
            if (generator != null)
            {
                generator.OnGenerationCompleted += HandleGenerationCompleted;
            }
        }

        private void OnDisable()
        {
            if (generator != null)
            {
                generator.OnGenerationCompleted -= HandleGenerationCompleted;
            }

            ClearRuntimeNavMesh();
        }

        private void OnDestroy()
        {
            ClearRuntimeNavMesh();
        }

        /// <summary>
        /// 手动触发一次完整的 NavMesh 装配流程。
        /// 调用方负责确保 PCG 地图已经生成完成。
        /// </summary>
        public void Rebuild(PcgMapGenerationResult result)
        {
            if (result == null)
            {
                Debug.LogError("[PcgNavMeshAssembler] PcgMapGenerationResult 为空，无法装配 NavMesh！", this);
                return;
            }

            HandleGenerationCompleted(result);
        }

        /// <summary>
        /// 尝试将一个世界坐标吸附到 NavMesh 上。
        /// </summary>
        public bool TrySnapToNavMesh(Vector3 worldPosition, out Vector3 snappedPosition)
        {
            return TrySnapToNavMesh(worldPosition, out snappedPosition, linkSampleDistance);
        }

        /// <summary>
        /// 尝试将刷怪点吸附到 NavMesh 上。
        /// </summary>
        public bool TryResolveSpawnPoint(PcgSpawnPointResult spawnPoint, out Vector3 navMeshPosition)
        {
            if (spawnPoint?.PointTransform == null)
            {
                navMeshPosition = default;
                return false;
            }

            Vector3 raw = spawnPoint.PointTransform.position;
            if (NavMesh.SamplePosition(raw, out var hit, spawnPointSampleDistance, CreateQueryFilter()))
            {
                navMeshPosition = hit.position;
                return true;
            }

            navMeshPosition = default;
            return false;
        }

        /// <summary>
        /// 获取或创建一个运行时 Link 的父级对象。
        /// </summary>
        private Transform ResolveLinkRoot()
        {
            if (runtimeLinkRoot != null)
            {
                return runtimeLinkRoot;
            }

            if (_internalLinkRoot != null)
            {
                return _internalLinkRoot;
            }

            var go = new GameObject("[RuntimeNavMeshLinks]");
            go.transform.SetParent(transform, false);
            _internalLinkRoot = go.transform;
            return _internalLinkRoot;
        }

        private void ResolveGenerator()
        {
            if (generator != null)
            {
                return;
            }

            generator = GetComponent<PcgMapGenerator>();
            if (generator != null)
            {
                return;
            }

            generator = FindObjectOfType<PcgMapGenerator>();
        }

        private void HandleGenerationCompleted(PcgMapGenerationResult result)
        {
            _currentResult = result;

            if (verboseLog)
            {
                DebugLog.Info("PCG.NavMeshAssembler", $"[PcgNavMeshAssembler] 收到地图生成完成事件，Rooms={result.PlacedRooms.Count}，Connections={result.Connections.Count}", this);
            }

            _isAssembled = false;
            ClearRuntimeNavMesh();

            AddRoomNavMeshes(result);
            BuildRuntimeLinks(result);
            ValidateSpawnPoints(result);

            // 诊断：检查每个房间每个连接器位置的 NavMesh 覆盖情况
            RunPerConnectorDiagnostics(result);

            _isAssembled = true;

            // 始终输出装配概述（关键信息不依赖 verboseLog）
            DebugLog.Info("PCG.NavMeshAssembler", $"[PcgNavMeshAssembler] NavMesh 装配完成！" +
                      $"房间={_navMeshInstances.Count}，Links={_runtimeLinks.Count}，" +
                      $"无效刷怪点={_invalidSpawnPoints.Count}", this);

            if (verboseLog)
            {
                foreach (var link in _runtimeLinks)
                {
                    if (link != null)
                    {
                        var linkStart = link.transform.TransformPoint(link.startPoint);
                        var linkEnd = link.transform.TransformPoint(link.endPoint);
                        DebugLog.Info("PCG.NavMeshAssembler", $"[PcgNavMeshAssembler]   Link [{link.name}]: " +
                                  $"start={linkStart} end={linkEnd} " +
                                  $"width={link.width} bidirectional={link.bidirectional} " +
                                  $"agentTypeID={link.agentTypeID}", this);
                    }
                }
            }

            // 延迟一帧运行连通性验证：NavMesh 系统需要时间处理新添加的 NavMeshDataInstance 和 NavMeshLink
            StartCoroutine(DelayedConnectivityValidation());

            OnNavMeshAssembled?.Invoke(result);
        }

        /// <summary>
        /// 诊断：对每个已放置房间的每个连接器位置进行 NavMesh 采样，
        /// 用于定位哪些房间/连接器缺少 NavMesh 覆盖。
        /// </summary>
        private void RunPerConnectorDiagnostics(PcgMapGenerationResult result)
        {
            var roomOkCount = 0;
            var roomFailCount = 0;

            foreach (var placed in result.PlacedRooms)
            {
                if (placed?.RoomInstance == null)
                    continue;

                var roomRoot = placed.RoomInstance.GetComponent<PcgRoomRoot>();
                if (roomRoot == null)
                    continue;

                var connectors = roomRoot.Connectors;
                if (connectors == null || connectors.Count == 0)
                    continue;

                var failConnectors = new List<string>();

                foreach (var conn in connectors)
                {
                    if (conn == null)
                        continue;

                    var inward = -conn.GetSocketNormal(true);
                    var worldPos = conn.GetSocketBaseWorldPoint() + inward * linkEndpointInset;

                    if (!NavMesh.SamplePosition(worldPos, out var hit, linkSampleDistance, CreateQueryFilter()))
                    {
                        failConnectors.Add($"{conn.ConnectorId}({worldPos})");
                    }
                }

                if (failConnectors.Count > 0)
                {
                    roomFailCount++;
                    DebugLog.Warning("PCG.NavMeshAssembler", $"[PcgNavMeshAssembler] ✗ 房间 Node{placed.NodeId} [{placed.Role}] ({roomRoot.name}) " +
                                     $"有 {failConnectors.Count}/{connectors.Count} 个连接器不在 NavMesh 上: " +
                                     $"{string.Join(", ", failConnectors)}", roomRoot);
                }
                else
                {
                    roomOkCount++;
                }
            }

            DebugLog.Info("PCG.NavMeshAssembler", $"[PcgNavMeshAssembler] 连接器 NavMesh 覆盖诊断: {roomOkCount} 房间全部覆盖, " +
                      $"{roomFailCount} 房间有缺失 (sampleDist={linkSampleDistance}m)");
        }

        /// <summary>
        /// 延迟一帧后运行连通性验证，给 NavMesh 系统时间处理新的 DataInstance 和 Link。
        /// </summary>
        private System.Collections.IEnumerator DelayedConnectivityValidation()
        {
            yield return null; // 等待一帧
            RunConnectivityValidation();
        }

        /// <summary>
        /// 运行时验证：抽样检查 Link 两端是否在有效的 NavMesh 上，以及是否能计算跨房间路径。
        /// </summary>
        private void RunConnectivityValidation()
        {
            // 构建 NodeId → 房间信息 的快速查找表
            var roomLookup = new Dictionary<int, (string name, RoomRole role)>();
            if (_currentResult != null)
            {
                foreach (var placed in _currentResult.PlacedRooms)
                {
                    if (placed?.RoomInstance != null)
                        roomLookup[placed.NodeId] = (placed.RoomInstance.name, placed.Role);
                }
            }

            var activeLinks = FindObjectsOfType<NavMeshLink>();
            var activeLinkCount = 0;
            var validLinkCount = 0;

            foreach (var link in activeLinks)
            {
                if (link == null || !link.isActiveAndEnabled)
                    continue;

                activeLinkCount++;
                var startWorld = link.transform.TransformPoint(link.startPoint);
                var endWorld = link.transform.TransformPoint(link.endPoint);
                var queryFilter = CreateQueryFilter();

                var startOnMesh = NavMesh.SamplePosition(startWorld, out var startHit, 0.25f, queryFilter);
                var endOnMesh = NavMesh.SamplePosition(endWorld, out var endHit, 0.25f, queryFilter);
                var hasExpectedAgentType = link.agentTypeID == expectedAgentTypeId;
                var hasUsableLength = Vector3.Distance(startWorld, endWorld) >= minimumLinkLength;

                if (startOnMesh && endOnMesh && hasExpectedAgentType && hasUsableLength)
                {
                    validLinkCount++;
                }
                else
                {
                    DebugLog.Warning("PCG.NavMeshAssembler", $"[PcgNavMeshAssembler] ⚠ Link [{link.name}] 端点不在 NavMesh 上！" +
                                     $"startOnMesh={startOnMesh} endOnMesh={endOnMesh} " +
                                     $"agentTypeOk={hasExpectedAgentType} lengthOk={hasUsableLength} " +
                                     $"startWorld={startWorld} endWorld={endWorld}", this);
                }

                // 解析 Link 连接的两个房间信息
                var roomInfo = ResolveLinkRoomInfo(link.name, roomLookup);

                // 尝试计算跨 Link 的路径
                var path = new NavMeshPath();
                if (NavMesh.CalculatePath(startWorld, endWorld, queryFilter, path))
                {
                    if (path.status == NavMeshPathStatus.PathComplete)
                    {
                        DebugLog.Info("PCG.NavMeshAssembler", $"[PcgNavMeshAssembler]   ✓ Link [{link.name}] {roomInfo} 路径可达，corners={path.corners.Length}", this);
                    }
                    else
                    {
                        DebugLog.Warning("PCG.NavMeshAssembler", $"[PcgNavMeshAssembler]   ✗ Link [{link.name}] {roomInfo} 路径不可达！status={path.status}", this);
                    }
                }
            }

            DebugLog.Info("PCG.NavMeshAssembler", $"[PcgNavMeshAssembler] NavMesh 连通性验证: {validLinkCount}/{activeLinkCount} 个 Link 有效（场景中共 {activeLinks.Length} 个 NavMeshLink 组件）", this);

            // 如果没有检测到任何 Link，这是关键问题
            if (activeLinkCount == 0)
            {
                Debug.LogError($"[PcgNavMeshAssembler] ⚠ 场景中没有任何活动的 NavMeshLink！跨房间寻路将不可能工作！", this);
            }
        }

        /// <summary>
        /// 从 Link 名称 "NavLink_X_Y" 解析出 NodeId，查找对应房间信息。
        /// </summary>
        private static string ResolveLinkRoomInfo(string linkName, Dictionary<int, (string name, RoomRole role)> lookup)
        {
            // 格式: "NavLink_0_1" 或 "NavLink_12_345"
            var parts = linkName.Split('_');
            if (parts.Length < 3)
                return "";

            if (int.TryParse(parts[1], out var nodeA) && int.TryParse(parts[2], out var nodeB))
            {
                lookup.TryGetValue(nodeA, out var roomA);
                lookup.TryGetValue(nodeB, out var roomB);
                return $"[Node{nodeA}:{roomA.name}({roomA.role}) ↔ Node{nodeB}:{roomB.name}({roomB.role})]";
            }

            return "";
        }

        private void AddRoomNavMeshes(PcgMapGenerationResult result)
        {
            foreach (var placed in result.PlacedRooms)
            {
                if (placed?.RoomInstance == null)
                {
                    continue;
                }

                var navMeshAsset = placed.RoomInstance.GetComponentInChildren<RoomPrebakedNavMeshAsset>(true);
                if (navMeshAsset == null)
                {
                    Debug.LogError($"[PcgNavMeshAssembler] 房间 {placed.NodeId} ({placed.RoomInstance.name}) 缺少 RoomPrebakedNavMeshAsset 组件！", placed.RoomInstance);
                    continue;
                }

                if (!navMeshAsset.HasValidData)
                {
                    Debug.LogError($"[PcgNavMeshAssembler] 房间 {placed.NodeId} 的 RoomPrebakedNavMeshAsset 没有有效的 NavMeshData！", placed.RoomInstance);
                    continue;
                }

                if (navMeshAsset.AgentTypeId != expectedAgentTypeId)
                {
                    Debug.LogError($"[PcgNavMeshAssembler] 房间 {placed.NodeId} AgentType 不匹配！期望={expectedAgentTypeId}，实际={navMeshAsset.AgentTypeId}", placed.RoomInstance);
                    continue;
                }

                var instance = navMeshAsset.AddToWorld(placed.RoomInstance.transform);
                if (instance.valid)
                {
                    _navMeshInstances.Add(instance);

                    if (verboseLog)
                    {
                        DebugLog.Info("PCG.NavMeshAssembler", $"[PcgNavMeshAssembler] 已加载房间 {placed.NodeId} 的 NavMeshData: {navMeshAsset.NavMeshData.name}", this);
                    }
                }
                else
                {
                    Debug.LogError($"[PcgNavMeshAssembler] 房间 {placed.NodeId} 的 NavMeshData 添加失败！", placed.RoomInstance);
                }
            }
        }

        private void BuildRuntimeLinks(PcgMapGenerationResult result)
        {
            foreach (var conn in result.Connections)
            {
                if (conn == null || !conn.IsResolved)
                {
                    continue;
                }

                if (conn.ConnectorFrom == null || conn.ConnectorTo == null)
                {
                    DebugLog.Warning("PCG.NavMeshAssembler", $"[PcgNavMeshAssembler] 连接 Node{conn.NodeA}-Node{conn.NodeB} 的 Connector 为空，跳过！", this);
                    continue;
                }

                // 检查连接器是否允许创建 NavMeshLink（如封门则跳过）
                if (!conn.ConnectorFrom.AllowNavLink || !conn.ConnectorTo.AllowNavLink)
                {
                    if (verboseLog)
                    {
                        DebugLog.Info("PCG.NavMeshAssembler", $"[PcgNavMeshAssembler] 连接 Node{conn.NodeA}-Node{conn.NodeB} 不允许 NavLink（AllowNavLink=false），跳过", this);
                    }
                    continue;
                }

                var startSocket = conn.ConnectorFrom.GetSocketWorldPoint(conn.ConnectorFromOutgoing);
                var endSocket = conn.ConnectorTo.GetSocketWorldPoint(conn.ConnectorToOutgoing);
                var startInward = -conn.ConnectorFrom.GetSocketNormal(conn.ConnectorFromOutgoing);
                var endInward = -conn.ConnectorTo.GetSocketNormal(conn.ConnectorToOutgoing);
                var rawStart = startSocket + startInward * linkEndpointInset;
                var rawEnd = endSocket + endInward * linkEndpointInset;

                // 使用连接器各自的采样半径
                var sampleDistStart = conn.ConnectorFrom.NavLinkSampleRadius > 0f
                    ? conn.ConnectorFrom.NavLinkSampleRadius
                    : linkSampleDistance;
                var sampleDistEnd = conn.ConnectorTo.NavLinkSampleRadius > 0f
                    ? conn.ConnectorTo.NavLinkSampleRadius
                    : linkSampleDistance;

                if (!TrySnapLinkEndpoint(rawStart, startSocket, startInward, sampleDistStart, out var startPos))
                {
                    DebugLog.Warning("PCG.NavMeshAssembler", $"[PcgNavMeshAssembler] Link 起点无法绑定到本房间 NavMesh！Edge={conn.NodeA}-{conn.NodeB}，Pos={rawStart}，SampleDist={sampleDistStart:F1}", this);
                    continue;
                }

                if (!TrySnapLinkEndpoint(rawEnd, endSocket, endInward, sampleDistEnd, out var endPos))
                {
                    DebugLog.Warning("PCG.NavMeshAssembler", $"[PcgNavMeshAssembler] Link 终点无法绑定到本房间 NavMesh！Edge={conn.NodeA}-{conn.NodeB}，Pos={rawEnd}，SampleDist={sampleDistEnd:F1}", this);
                    continue;
                }

                var linkLength = Vector3.Distance(startPos, endPos);
                if (linkLength < minimumLinkLength)
                {
                    DebugLog.Warning("PCG.NavMeshAssembler", $"[PcgNavMeshAssembler] Link 两端过近，疑似采样到同一侧 NavMesh，已跳过！" +
                                     $"Edge={conn.NodeA}-{conn.NodeB}，Length={linkLength:F3}m，" +
                                     $"Start={startPos}，End={endPos}", this);
                    continue;
                }

                // 使用连接器中较大的宽度作为 Link 宽度
                var linkWidth = Mathf.Max(conn.ConnectorFrom.NavLinkWidth, conn.ConnectorTo.NavLinkWidth);
                if (linkWidth <= 0f)
                    linkWidth = defaultLinkWidth;

                var link = CreateNavMeshLink(conn, startPos, endPos, linkWidth);
                if (link != null)
                {
                    _runtimeLinks.Add(link);

                    if (verboseLog)
                    {
                        DebugLog.Info("PCG.NavMeshAssembler", $"[PcgNavMeshAssembler] 创建 NavMeshLink: Node{conn.NodeA}-Node{conn.NodeB}，width={linkWidth:F1}", this);
                    }
                }
            }
        }

        private bool TrySnapLinkEndpoint(
            Vector3 sampleOrigin,
            Vector3 socketPosition,
            Vector3 inwardDirection,
            float sampleDistance,
            out Vector3 snappedPosition)
        {
            if (!TrySnapToNavMesh(sampleOrigin, out snappedPosition, sampleDistance))
            {
                return false;
            }

            var inward = Vector3.ProjectOnPlane(inwardDirection, Vector3.up).normalized;
            if (inward.sqrMagnitude < 0.0001f)
            {
                return true;
            }

            const float wrongSideTolerance = 0.05f;
            var signedDistance = Vector3.Dot(snappedPosition - socketPosition, inward);
            if (signedDistance < -wrongSideTolerance)
            {
                if (verboseLog)
                {
                    DebugLog.Warning("PCG.NavMeshAssembler", $"[PcgNavMeshAssembler] Link 端点采样到了门缝错误一侧：" +
                                     $"origin={sampleOrigin} snapped={snappedPosition} signedDistance={signedDistance:F3}", this);
                }

                snappedPosition = default;
                return false;
            }

            return true;
        }

        private bool TrySnapToNavMesh(Vector3 worldPosition, out Vector3 snappedPosition, float sampleDistance)
        {
            if (NavMesh.SamplePosition(worldPosition, out var hit, sampleDistance, CreateQueryFilter()))
            {
                snappedPosition = hit.position;
                var offset = Vector3.Distance(worldPosition, hit.position);
                if (offset > 0.5f)
                {
                    DebugLog.Info("PCG.NavMeshAssembler", $"[PcgNavMeshAssembler] SamplePosition 吸附偏移较大: raw={worldPosition} snapped={hit.position} offset={offset:F2}m", this);
                }
                return true;
            }

            snappedPosition = default;
            return false;
        }

        private NavMeshLink CreateNavMeshLink(PcgRoomConnection conn, Vector3 start, Vector3 end, float width)
        {
            var go = new GameObject($"NavLink_{conn.NodeA}_{conn.NodeB}");
            go.transform.SetParent(ResolveLinkRoot(), false);
            go.transform.position = start;

            var link = go.AddComponent<NavMeshLink>();
            link.agentTypeID = expectedAgentTypeId;
            link.width = width;
            link.bidirectional = bidirectionalLinks;
            link.area = 0;

            link.startPoint = Vector3.zero;
            link.endPoint = go.transform.InverseTransformPoint(end);

            // 运行时动态创建的 NavMeshLink 必须在设置属性后手动调用 UpdateLink，
            // 否则 NavMesh 系统不会识别该连接（尤其是跨 NavMeshDataInstance 的场景）。
            link.UpdateLink();

            if (verboseLog)
            {
                DebugLog.Info("PCG.NavMeshAssembler", $"[PcgNavMeshAssembler] NavMeshLink created: {go.name}, start={start}, end={end}, distance={Vector3.Distance(start, end):F2}m", this);
            }

            return link;
        }

        private NavMeshQueryFilter CreateQueryFilter()
        {
            return new NavMeshQueryFilter
            {
                agentTypeID = expectedAgentTypeId,
                areaMask = NavMesh.AllAreas
            };
        }

        private void ValidateSpawnPoints(PcgMapGenerationResult result)
        {
            _invalidSpawnPoints.Clear();

            foreach (var spawnPoint in result.SpawnPoints)
            {
                if (!TryResolveSpawnPoint(spawnPoint, out var snapped))
                {
                    _invalidSpawnPoints.Add(spawnPoint);

                    if (warnOnSpawnPointFailure)
                    {
                        Vector3 raw = spawnPoint.PointTransform != null ? spawnPoint.PointTransform.position : Vector3.zero;
                        DebugLog.Warning("PCG.NavMeshAssembler", $"[PcgNavMeshAssembler] 刷怪点采样失败！Node={spawnPoint.NodeId}, Category={spawnPoint.Category}, RawPos={raw}", this);
                    }
                }
            }

            if (verboseLog && _invalidSpawnPoints.Count > 0)
            {
                DebugLog.Warning("PCG.NavMeshAssembler", $"[PcgNavMeshAssembler] 共 {_invalidSpawnPoints.Count} 个刷怪点采样失败，已跳过。", this);
            }
        }

        private void ClearRuntimeNavMesh()
        {
            foreach (var instance in _navMeshInstances)
            {
                if (instance.valid)
                {
                    NavMesh.RemoveNavMeshData(instance);
                }
            }

            _navMeshInstances.Clear();

            var linkParent = runtimeLinkRoot ?? _internalLinkRoot;
            if (linkParent != null)
            {
                for (int i = linkParent.childCount - 1; i >= 0; i--)
                {
                    var child = linkParent.GetChild(i);
                    if (Application.isPlaying)
                    {
                        Destroy(child.gameObject);
                    }
                    else
                    {
                        DestroyImmediate(child.gameObject);
                    }
                }
            }

            _runtimeLinks.Clear();

            if (verboseLog)
            {
                Debug.Log("[PcgNavMeshAssembler] 已清理所有运行时 NavMesh 数据。", this);
            }
        }
    }
}
