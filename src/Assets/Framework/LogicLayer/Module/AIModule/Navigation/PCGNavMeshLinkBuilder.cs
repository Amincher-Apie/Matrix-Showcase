using System.Collections.Generic;
using Matrix.PCG;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace Framework.LogicLayer.Module.AIModule.Navigation
{
    /// <summary>
    /// 根据 PcgMapGenerationResult.Connections 在房间连接处动态创建 NavMeshLink 的构建器。
    ///
    /// 工作流程：
    /// 1. 遍历所有 PcgRoomConnection
    /// 2. 读取 ConnectorFrom / ConnectorTo 的世界坐标
    /// 3. 用 NavMesh.SamplePosition 将 Socket 点吸附到可走面
    /// 4. 在吸附后的两点之间创建 NavMeshLink
    /// 5. 若采样失败则跳过该连接并输出警告
    ///
    /// 生成结构：
    /// linksRoot (Transform)
    ///   ├─ Link_RoomA_RoomB
    ///   ├─ Link_RoomB_RoomC
    ///   └─ ...
    /// </summary>
    public class PCGNavMeshLinkBuilder : MonoBehaviour
    {
        [Header("Link Defaults")]
        [Tooltip("默认链接宽度（门宽）。")]
        [SerializeField]
        private float defaultLinkWidth = 2f;

        [Tooltip("NavMeshLink 是否双向通行。")]
        [SerializeField]
        private bool bidirectional = true;

        [Tooltip("NavMeshLink 关联的 Agent Type ID。使用 -1 表示使用 NavMeshSurface 的默认类型。")]
        [SerializeField]
        private int agentTypeId = -1;

        [Tooltip("NavMeshLink 所在的区域类型（Area）。使用 0 为默认可行走区域。")]
        [SerializeField]
        private int areaId = 0;

        [Header("Sampling")]
        [Tooltip("Socket 点吸附到 NavMesh 时使用的最大采样距离（米）。")]
        [SerializeField]
        private float sampleDistance = 3f;

        [Tooltip("若 Socket 点在 NavMesh 表面以上最多允许偏移多少（垂直方向，Y 轴）。")]
        [SerializeField]
        private float verticalSnapTolerance = 1.5f;

        [Tooltip("若两端点之间距离超过此值，则不创建 Link（防止无效连接）。")]
        [SerializeField]
        private float maxLinkDistance = 20f;

        [Header("Auto-repair")]
        [Tooltip("是否自动修复因 Y 轴差导致无法采样的情况（取两点 Y 均值后重试）。")]
        [SerializeField]
        private bool autoRepairVerticalMisalignment = true;

        [Header("Debug")]
        [SerializeField]
        private bool verboseLog;

        /// <summary>
        /// 根据给定的房间连接列表，在 linksRoot 下创建运行时 NavMeshLink。
        /// </summary>
        /// <param name="connections">PCG 生成的房间连接列表。</param>
        /// <param name="linksRoot">创建的链接对象的父级 Transform。</param>
        /// <param name="sampleDistanceOverride">覆盖默认采样距离。</param>
        /// <param name="verbose">是否输出详细日志。</param>
        /// <returns>成功创建的链接数量。</returns>
        public int BuildLinks(
            IList<PcgRoomConnection> connections,
            Transform linksRoot,
            float sampleDistanceOverride = -1f,
            bool verbose = false)
        {
            if (connections == null || connections.Count == 0)
            {
                if (verbose)
                {
                    Debug.Log("[PCGNavMeshLinkBuilder] 连接列表为空，无需创建 NavMeshLink。", this);
                }
                return 0;
            }

            if (linksRoot == null)
            {
                Debug.LogError("[PCGNavMeshLinkBuilder] linksRoot 未设置，无法创建 NavMeshLink！", this);
                return 0;
            }

            var distance = sampleDistanceOverride > 0f ? sampleDistanceOverride : sampleDistance;
            var successCount = 0;
            var failedCount = 0;

            for (var i = 0; i < connections.Count; i++)
            {
                var conn = connections[i];
                if (conn == null || !conn.IsResolved)
                    continue;

                if (conn.ConnectorFrom == null || conn.ConnectorTo == null)
                    continue;

                var startPos = conn.ConnectorFrom.GetSocketWorldPoint(true);
                var endPos = conn.ConnectorTo.GetSocketWorldPoint(false);

                if (!SampleAndValidateEndpoints(ref startPos, ref endPos, distance, verbose, out var snapInfo))
                {
                    failedCount++;
                    continue;
                }

                var linkObj = CreateNavMeshLink(
                    conn.NodeA,
                    conn.NodeB,
                    startPos,
                    endPos,
                    linksRoot,
                    i);

                if (linkObj != null)
                {
                    successCount++;

                    if (verbose)
                    {
                        AIDebug.LogChannel("AI.Navigation", $"[PCGNavMeshLinkBuilder] 创建 Link[{i}]: Node{conn.NodeA}->Node{conn.NodeB}，" +
                                  $"start={snapInfo.snapStart:F2} end={snapInfo.snapEnd:F2}，" +
                                  $"dist={Vector3.Distance(startPos, endPos):F2}m", this);
                    }
                }
                else
                {
                    failedCount++;
                }
            }

            if (verbose || verboseLog)
            {
                AIDebug.LogChannel("AI.Navigation", $"[PCGNavMeshLinkBuilder] Link 创建完成：{successCount} 成功 / {failedCount} 失败", this);
            }

            return successCount;
        }

        private bool SampleAndValidateEndpoints(
            ref Vector3 startPos,
            ref Vector3 endPos,
            float distance,
            bool verbose,
            out SnapInfo snapInfo)
        {
            snapInfo = default;

            var rawDistance = Vector3.Distance(startPos, endPos);
            if (rawDistance > maxLinkDistance)
            {
                if (verbose || verboseLog)
                {
                    AIDebug.LogWarning($"[PCGNavMeshLinkBuilder] 两端点距离 {rawDistance:F2}m 超过上限 {maxLinkDistance}m，跳过。", this);
                }
                return false;
            }

            var startSnapped = TrySampleEndpoint(startPos, distance, verbose, out var startHit);
            var endSnapped = TrySampleEndpoint(endPos, distance, verbose, out var endHit);

            if (!startSnapped && !endSnapped)
            {
                if (verbose || verboseLog)
                {
                    AIDebug.LogWarning($"[PCGNavMeshLinkBuilder] 两端点都无法吸附到 NavMesh，start={startPos} end={endPos}", this);
                }
                return false;
            }

            if (!startSnapped)
            {
                if (autoRepairVerticalMisalignment && endSnapped)
                {
                    var repairedStart = startPos;
                    repairedStart.y = endHit.position.y;
                    if (NavMesh.SamplePosition(repairedStart, out startHit, distance, NavMesh.AllAreas))
                    {
                        startSnapped = true;
                        startPos = startHit.position;
                        if (verbose || verboseLog)
                        {
                            AIDebug.LogChannel("AI.Navigation", $"[PCGNavMeshLinkBuilder] 垂直修复 start 端：{startPos}", this);
                        }
                    }
                }

                if (!startSnapped)
                {
                    if (verbose || verboseLog)
                    {
                        AIDebug.LogWarning($"[PCGNavMeshLinkBuilder] start 端无法吸附到 NavMesh: {startPos}", this);
                    }
                    return false;
                }
            }
            else
            {
                startPos = startHit.position;
            }

            if (!endSnapped)
            {
                if (autoRepairVerticalMisalignment && startSnapped)
                {
                    var repairedEnd = endPos;
                    repairedEnd.y = startHit.position.y;
                    if (NavMesh.SamplePosition(repairedEnd, out endHit, distance, NavMesh.AllAreas))
                    {
                        endSnapped = true;
                        endPos = endHit.position;
                        if (verbose || verboseLog)
                        {
                            AIDebug.LogChannel("AI.Navigation", $"[PCGNavMeshLinkBuilder] 垂直修复 end 端：{endPos}", this);
                        }
                    }
                }

                if (!endSnapped)
                {
                    if (verbose || verboseLog)
                    {
                        AIDebug.LogWarning($"[PCGNavMeshLinkBuilder] end 端无法吸附到 NavMesh: {endPos}", this);
                    }
                    return false;
                }
            }
            else
            {
                endPos = endHit.position;
            }

            var finalDistance = Vector3.Distance(startPos, endPos);
            if (finalDistance > maxLinkDistance)
            {
                if (verbose || verboseLog)
                {
                    AIDebug.LogWarning($"[PCGNavMeshLinkBuilder] 吸附后距离 {finalDistance:F2}m 超过上限 {maxLinkDistance}m，跳过。", this);
                }
                return false;
            }

            snapInfo.snapStart = Vector3.Distance(startPos, startHit.position);
            snapInfo.snapEnd = Vector3.Distance(endPos, endHit.position);
            return true;
        }

        private bool TrySampleEndpoint(Vector3 worldPosition, float distance, bool verbose, out NavMeshHit hit)
        {
            if (NavMesh.SamplePosition(worldPosition, out hit, distance, NavMesh.AllAreas))
            {
                return true;
            }

            if (autoRepairVerticalMisalignment)
            {
                var repaired = worldPosition;
                repaired.y += verticalSnapTolerance;
                if (NavMesh.SamplePosition(repaired, out hit, distance + verticalSnapTolerance, NavMesh.AllAreas))
                {
                    if (verbose || verboseLog)
                    {
                        AIDebug.LogChannel("AI.Navigation", $"[PCGNavMeshLinkBuilder] 垂直偏移修复：{worldPosition} -> {hit.position}", this);
                    }
                    return true;
                }

                repaired = worldPosition;
                repaired.y -= verticalSnapTolerance;
                if (NavMesh.SamplePosition(repaired, out hit, distance + verticalSnapTolerance, NavMesh.AllAreas))
                {
                    if (verbose || verboseLog)
                    {
                        AIDebug.LogChannel("AI.Navigation", $"[PCGNavMeshLinkBuilder] 垂直偏移修复（下）：{worldPosition} -> {hit.position}", this);
                    }
                    return true;
                }
            }

            return false;
        }

        private GameObject CreateNavMeshLink(int nodeA, int nodeB, Vector3 startPos, Vector3 endPos, Transform parent, int index)
        {
            var linkObj = new GameObject($"Link_Node{nodeA}_Node{nodeB}_{index}");
            linkObj.transform.SetParent(parent);
            linkObj.transform.position = (startPos + endPos) * 0.5f;

            var link = linkObj.AddComponent<NavMeshLink>();
            link.startPoint = linkObj.transform.InverseTransformPoint(startPos);
            link.endPoint = linkObj.transform.InverseTransformPoint(endPos);
            link.width = defaultLinkWidth;
            link.bidirectional = bidirectional;
            link.area = areaId;

            if (agentTypeId >= 0)
            {
                link.agentTypeID = agentTypeId;
            }

            // 运行时动态创建的 NavMeshLink 必须在设置属性后手动调用 UpdateLink
            link.UpdateLink();

            return linkObj;
        }

        private struct SnapInfo
        {
            public float snapStart;
            public float snapEnd;
        }
    }
}
