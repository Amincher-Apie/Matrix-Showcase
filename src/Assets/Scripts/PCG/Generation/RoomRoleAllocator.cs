
using System.Collections.Generic;
using UnityEngine;

namespace Matrix.PCG
{
    /// <summary>
    /// Assigns functional roles based on ring-first topology semantics.
    /// Hard constraints preserved from original logic:
    ///   - Boss room: ~90% along the main path from Start
    ///   - SideTask rooms: ~50% along main path, spatially on opposite sides (left vs right)
    ///   - Shop rooms: distributed along mid-path at ring corners
    ///   - All other rooms: Connector
    /// Task-trigger connections are added between task rooms and nearby graph edges.
    /// </summary>
    public static class RoomRoleAllocator
    {
        private const float BossMinProgress = 0.85f;
        private const float BossMaxProgress = 0.95f;
        private const float SideTaskMinProgress = 0.40f;
        private const float SideTaskMaxProgress = 0.60f;
        private const float SideTaskMinLateralRatio = 0.18f;
        private const float SideTaskMaxLateralRatio = 0.85f;

        public static void AssignRoles(
            RoomGraph graph,
            MapTaskInput taskInput,
            ref DeterministicRandom random,
            List<TaskTriggerConnection> taskTriggerConnections)
        {
            if (graph == null || graph.NodeCount == 0)
            {
                return;
            }

            if (taskTriggerConnections != null)
            {
                taskTriggerConnections.Clear();
            }

            ResetRoles(graph);

            List<int> primaryRingNodes = ResolvePrimaryRingNodes(graph);
            if (primaryRingNodes.Count >= 4 && TryAssignRolesWithRingTopology(
                    graph, primaryRingNodes, taskInput, ref random, taskTriggerConnections))
            {
                return;
            }

            AssignRolesLinearFallback(graph, taskInput, ref random, taskTriggerConnections);
        }

        private static void ResetRoles(RoomGraph graph)
        {
            for (int i = 0; i < graph.NodeCount; i++)
            {
                RoomGraphNode node = graph.GetNode(i);
                node.AssignedRole = RoomRole.Connector;
                node.HasAssignedSideTask = false;
            }
        }

        private static bool TryAssignRolesWithRingTopology(
            RoomGraph graph,
            List<int> primaryRingNodes,
            MapTaskInput taskInput,
            ref DeterministicRandom random,
            List<TaskTriggerConnection> taskTriggerConnections)
        {
            HashSet<int> ringSet = new HashSet<int>(primaryRingNodes);

            int startId = SelectStartRingNode(graph, primaryRingNodes, ringSet, ref random);
            if (startId < 0)
            {
                return false;
            }

            graph.GetNode(startId).AssignedRole = RoomRole.Start;

            int[] predecessor;
            int[] distFromStart = CalculateShortestDistance(graph, startId, out predecessor);
            int maxDist = FindMaxDistance(distFromStart, startId);
            if (maxDist <= 0)
            {
                return false;
            }

            int oppositeRingId = SelectOppositeRingNode(graph, startId, primaryRingNodes, distFromStart, ref random);
            int[] distToRing = CalculateDistanceToSet(graph, ringSet);
            int[] distFromOpposite = CalculateShortestDistance(graph, oppositeRingId, out _);

            int bossId = SelectBossNodeForRing(
                graph,
                startId,
                oppositeRingId,
                ringSet,
                distFromStart,
                distToRing,
                distFromOpposite,
                maxDist,
                ref random);

            graph.GetNode(bossId).AssignedRole = RoomRole.Boss;

            List<int> mainPath = BuildShortestPath(graph, startId, bossId, predecessor);

            AssignSideTasksForRing(
                graph,
                taskInput,
                startId,
                bossId,
                mainPath,
                ringSet,
                distFromStart,
                distToRing,
                maxDist,
                ref random,
                taskTriggerConnections);

            AssignShopRoomsForRing(
                graph,
                startId,
                bossId,
                ringSet,
                distFromStart,
                distToRing,
                maxDist,
                ref random);

            return true;
        }

        private static int SelectStartRingNode(
            RoomGraph graph,
            List<int> ringNodes,
            HashSet<int> ringSet,
            ref DeterministicRandom random)
        {
            if (ringNodes == null || ringNodes.Count == 0)
            {
                return -1;
            }

            int side = random.NextInt(4);
            int bestExtreme = int.MinValue;
            List<int> extremeCandidates = new List<int>();

            for (int i = 0; i < ringNodes.Count; i++)
            {
                int nodeId = ringNodes[i];
                Vector2Int pos = graph.GetNode(nodeId).GridPosition;

                int measure;
                switch (side)
                {
                    case 0: measure = -pos.x; break;
                    case 1: measure = pos.x; break;
                    case 2: measure = -pos.y; break;
                    default: measure = pos.y; break;
                }

                if (measure > bestExtreme)
                {
                    bestExtreme = measure;
                    extremeCandidates.Clear();
                    extremeCandidates.Add(nodeId);
                }
                else if (measure == bestExtreme)
                {
                    extremeCandidates.Add(nodeId);
                }
            }

            int bestId = extremeCandidates[0];
            int bestScore = int.MinValue;

            for (int i = 0; i < extremeCandidates.Count; i++)
            {
                int nodeId = extremeCandidates[i];
                int nonRingNeighbors = CountNonRingNeighbors(graph, nodeId, ringSet);

                int score = 0;
                score += random.NextInt(0, 24);
                score += graph.GetDegree(nodeId) <= 3 ? 16 : -12;
                score -= nonRingNeighbors * 14;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestId = nodeId;
                }
            }

            return bestId;
        }

        private static int SelectOppositeRingNode(
            RoomGraph graph,
            int startId,
            List<int> ringNodes,
            int[] distFromStart,
            ref DeterministicRandom random)
        {
            Vector2 center = ResolveCenter(graph, ringNodes);
            Vector2 startVector = (Vector2)graph.GetNode(startId).GridPosition - center;

            int bestId = startId;
            int bestScore = int.MinValue;

            for (int i = 0; i < ringNodes.Count; i++)
            {
                int nodeId = ringNodes[i];
                if (nodeId == startId)
                {
                    continue;
                }

                Vector2 nodeVector = (Vector2)graph.GetNode(nodeId).GridPosition - center;
                int score = random.NextInt(0, 20);

                if (distFromStart[nodeId] >= 0)
                {
                    score += distFromStart[nodeId] * 20;
                }

                if (startVector.sqrMagnitude > 0.0001f && nodeVector.sqrMagnitude > 0.0001f)
                {
                    float dot = Vector2.Dot(startVector.normalized, nodeVector.normalized);
                    score += Mathf.RoundToInt(-dot * 110f);
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestId = nodeId;
                }
            }

            return bestId;
        }

        private static int SelectBossNodeForRing(
            RoomGraph graph,
            int startId,
            int oppositeRingId,
            HashSet<int> ringSet,
            int[] distFromStart,
            int[] distToRing,
            int[] distFromOpposite,
            int maxDistance,
            ref DeterministicRandom random)
        {
            int bestId = oppositeRingId;
            int bestScore = int.MinValue;

            for (int i = 0; i < graph.NodeCount; i++)
            {
                if (i == startId)
                {
                    continue;
                }

                int distStart = distFromStart[i];
                if (distStart < 0)
                {
                    continue;
                }

                float progress = distStart / (float)maxDistance;
                if (progress < BossMinProgress || progress > BossMaxProgress)
                {
                    continue;
                }

                bool onRing = ringSet.Contains(i);
                int score = random.NextInt(0, 22);
                score += distStart * 14;

                if (onRing)
                {
                    score += i == oppositeRingId ? 95 : 26;
                    if (distFromOpposite[i] >= 0)
                    {
                        score -= distFromOpposite[i] * 8;
                    }
                }
                else
                {
                    int branchDepth = Mathf.Max(0, distToRing[i]);
                    score += branchDepth * 52;
                    score += graph.GetDegree(i) == 1 ? 30 : 0;

                    if (distFromOpposite[i] >= 0)
                    {
                        score += Mathf.Max(0, 42 - distFromOpposite[i] * 7);
                    }
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestId = i;
                }
            }

            if (bestId == startId)
            {
                return oppositeRingId;
            }

            return bestId;
        }

        private static void AssignSideTasksForRing(
            RoomGraph graph,
            MapTaskInput taskInput,
            int startId,
            int bossId,
            List<int> mainPath,
            HashSet<int> ringSet,
            int[] distFromStart,
            int[] distToRing,
            int maxDistance,
            ref DeterministicRandom random,
            List<TaskTriggerConnection> taskTriggerConnections)
        {
            if (taskInput == null || taskInput.SideTasks == null || taskInput.SideTasks.Count == 0)
            {
                return;
            }

            int sideTaskCount = taskInput.SideTasks.Count;
            Vector2 mainAxisDir = ComputeMainAxisDirection(graph, mainPath);

            List<SideTaskSlot> slots = BuildSideTaskSlots(sideTaskCount, ref random);

            for (int i = 0; i < taskInput.SideTasks.Count && i < slots.Count; i++)
            {
                SideTaskInput sideTask = taskInput.SideTasks[i];
                SideTaskSlot slot = slots[i];

                int sideNodeId = SelectSideTaskNodeForRing(
                    graph,
                    startId,
                    bossId,
                    mainPath,
                    mainAxisDir,
                    slot.TargetProgress,
                    slot.TargetLateralSign,
                    maxDistance,
                    ringSet,
                    distFromStart,
                    distToRing,
                    ref random);

                if (sideNodeId < 0)
                {
                    // 放宽约束重试：无需侧向偏移限制，任何可用 Connector 均可
                    sideNodeId = SelectSideTaskNodeRelaxed(
                        graph, startId, bossId, slot.TargetProgress,
                        maxDistance, ringSet, distFromStart, distToRing, ref random);
                }

                if (sideNodeId < 0)
                {
                    continue;
                }

                RoomGraphNode node = graph.GetNode(sideNodeId);
                node.AssignedRole = MapSideTaskToRole(sideTask.TaskType);
                node.HasAssignedSideTask = true;
                node.AssignedSideTask = sideTask.TaskType;

                if (taskTriggerConnections != null)
                {
                    GenerateTaskTriggerConnections(graph, sideNodeId, node.AssignedRole, taskTriggerConnections, ref random);
                }
            }
        }

        /// <summary>
        /// 放宽约束的侧线任务节点选择（去掉侧向偏移限制，仅按深度匹配）。
        /// 当标准选择找不到满足所有约束的节点时调用，确保任务房间角色能被分配到某个节点上。
        /// </summary>
        private static int SelectSideTaskNodeRelaxed(
            RoomGraph graph,
            int startId,
            int bossId,
            float targetProgress,
            int maxDistance,
            HashSet<int> ringSet,
            int[] distFromStart,
            int[] distToRing,
            ref DeterministicRandom random)
        {
            int bestId = -1;
            int bestScore = int.MinValue;

            for (int i = 0; i < graph.NodeCount; i++)
            {
                if (i == startId || i == bossId)
                {
                    continue;
                }

                RoomGraphNode node = graph.GetNode(i);
                if (node.AssignedRole != RoomRole.Connector)
                {
                    continue;
                }

                int dist = distFromStart[i];
                if (dist <= 0)
                {
                    continue;
                }

                float depth = dist / (float)maxDistance;

                // 放宽深度范围从 [0.40, 0.60] 到 [0.20, 0.80]
                const float relaxedMin = 0.20f;
                const float relaxedMax = 0.80f;
                if (depth < relaxedMin || depth > relaxedMax)
                {
                    continue;
                }

                // 不再要求侧向偏移，仅按深度匹配度 + 连通性评分
                int score = 0;
                score -= Mathf.RoundToInt(Mathf.Abs(depth - targetProgress) * 200f);
                score += graph.GetDegree(i) * 8;
                score += IsAdjacentToRing(graph, i, ringSet) ? 15 : 0;
                score += random.NextInt(0, 20);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestId = i;
                }
            }

            return bestId;
        }

        private static int SelectSideTaskNodeForRing(
            RoomGraph graph,
            int startId,
            int bossId,
            List<int> mainPath,
            Vector2 mainAxisDir,
            float targetProgress,
            float targetLateralSign,
            int maxDistance,
            HashSet<int> ringSet,
            int[] distFromStart,
            int[] distToRing,
            ref DeterministicRandom random)
        {
            int bestId = -1;
            int bestScore = int.MinValue;

            Vector2 bossPos = graph.GetNode(bossId).GridPosition;
            Vector2 startPos = graph.GetNode(startId).GridPosition;
            Vector2 midpoint = (startPos + bossPos) * 0.5f;

            for (int i = 0; i < graph.NodeCount; i++)
            {
                if (i == startId || i == bossId)
                {
                    continue;
                }

                RoomGraphNode node = graph.GetNode(i);
                if (node.AssignedRole != RoomRole.Connector)
                {
                    continue;
                }

                int dist = distFromStart[i];
                if (dist <= 0)
                {
                    continue;
                }

                float depth = dist / (float)maxDistance;
                if (depth < SideTaskMinProgress || depth > SideTaskMaxProgress)
                {
                    continue;
                }

                float lateral = ComputeLateralRatio(
                    graph.GetNode(i).GridPosition, midpoint, mainAxisDir);

                float lateralSign = mainAxisDir.magnitude > 0.0001f
                    ? Mathf.Sign(lateral)
                    : 1f;

                if (Mathf.Abs(lateral) < SideTaskMinLateralRatio)
                {
                    continue;
                }

                if (Mathf.Sign(lateral) != targetLateralSign)
                {
                    continue;
                }

                bool onRing = ringSet.Contains(i);
                int ringDepth = Mathf.Max(0, distToRing[i]);

                int score = 0;
                score -= Mathf.RoundToInt(Mathf.Abs(depth - targetProgress) * 260f);
                float lateralDist = Mathf.Abs(lateral);
                score -= Mathf.RoundToInt(Mathf.Abs(lateralDist - 0.50f) * 80f);
                score += onRing ? -110 : 44;
                score += ringDepth * 34;
                score += graph.GetDegree(i) == 1 ? 32 : 0;
                score += graph.GetDegree(i) == 2 ? 8 : 0;
                score += IsAdjacentToRing(graph, i, ringSet) ? 12 : 0;
                score += random.NextInt(0, 26);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestId = i;
                }
            }

            return bestId;
        }

        private static Vector2 ComputeMainAxisDirection(RoomGraph graph, List<int> mainPath)
        {
            if (mainPath == null || mainPath.Count < 2)
            {
                return Vector2.right;
            }

            Vector2 start = graph.GetNode(mainPath[0]).GridPosition;
            Vector2 end = graph.GetNode(mainPath[mainPath.Count - 1]).GridPosition;
            Vector2 dir = end - start;
            if (dir.sqrMagnitude > 0.0001f)
            {
                return dir.normalized;
            }

            return Vector2.right;
        }

        private static float ComputeLateralRatio(Vector2 nodePos, Vector2 midpoint, Vector2 axisDir)
        {
            if (axisDir.magnitude < 0.0001f)
            {
                return 0f;
            }

            Vector2 toNode = nodePos - midpoint;
            float along = Vector2.Dot(toNode, axisDir);
            Vector2 perp = toNode - along * axisDir;
            float lateral = perp.magnitude;
            if (Mathf.Abs(lateral) < 0.0001f)
            {
                return 0f;
            }

            return lateral * Mathf.Sign(perp.x * axisDir.y - perp.y * axisDir.x);
        }

        private struct SideTaskSlot
        {
            public float TargetProgress;
            public float TargetLateralSign;
        }

        private static List<SideTaskSlot> BuildSideTaskSlots(int count, ref DeterministicRandom random)
        {
            List<SideTaskSlot> slots = new List<SideTaskSlot>();

            if (count == 1)
            {
                slots.Add(new SideTaskSlot
                {
                    TargetProgress = 0.50f,
                    TargetLateralSign = random.Chance(0.5f) ? -1f : 1f
                });
            }
            else if (count >= 2)
            {
                float jitter = (random.NextFloat01() - 0.5f) * 0.08f;
                float progress = 0.50f + jitter;

                slots.Add(new SideTaskSlot
                {
                    TargetProgress = Mathf.Clamp(progress, SideTaskMinProgress, SideTaskMaxProgress),
                    TargetLateralSign = -1f
                });
                slots.Add(new SideTaskSlot
                {
                    TargetProgress = Mathf.Clamp(progress, SideTaskMinProgress, SideTaskMaxProgress),
                    TargetLateralSign = 1f
                });
            }

            for (int i = slots.Count; i < count; i++)
            {
                slots.Add(new SideTaskSlot
                {
                    TargetProgress = 0.50f,
                    TargetLateralSign = (i % 2 == 0) ? -1f : 1f
                });
            }

            return slots;
        }

        private static void AssignShopRoomsForRing(
            RoomGraph graph,
            int startId,
            int bossId,
            HashSet<int> ringSet,
            int[] distFromStart,
            int[] distToRing,
            int maxDistance,
            ref DeterministicRandom random)
        {
            if (maxDistance <= 2)
            {
                return;
            }

            int shopCount = EstimateShopCount(graph.NodeCount);
            if (shopCount <= 0)
            {
                return;
            }

            List<int> selected = new List<int>();
            int[] distFromBoss = CalculateShortestDistance(graph, bossId, out _);

            for (int i = 0; i < shopCount; i++)
            {
                float t = shopCount == 1 ? 0.54f : Mathf.Lerp(0.42f, 0.74f, i / (float)(shopCount - 1));
                int shopNodeId = SelectShopNodeForRing(
                    graph,
                    startId,
                    bossId,
                    t,
                    maxDistance,
                    ringSet,
                    selected,
                    distFromStart,
                    distToRing,
                    distFromBoss,
                    ref random);

                if (shopNodeId < 0)
                {
                    continue;
                }

                selected.Add(shopNodeId);
                graph.GetNode(shopNodeId).AssignedRole = RoomRole.Shop;
            }
        }

        private static int SelectShopNodeForRing(
            RoomGraph graph,
            int startId,
            int bossId,
            float targetDepthRatio,
            int maxDistance,
            HashSet<int> ringSet,
            List<int> alreadySelected,
            int[] distFromStart,
            int[] distToRing,
            int[] distFromBoss,
            ref DeterministicRandom random)
        {
            int bestId = -1;
            int bestScore = int.MinValue;

            for (int i = 0; i < graph.NodeCount; i++)
            {
                if (i == startId || i == bossId || alreadySelected.Contains(i))
                {
                    continue;
                }

                RoomGraphNode node = graph.GetNode(i);
                if (node.AssignedRole != RoomRole.Connector)
                {
                    continue;
                }

                int dist = distFromStart[i];
                if (dist <= 1)
                {
                    continue;
                }

                float depth = dist / (float)maxDistance;
                if (depth < 0.20f || depth > 0.90f)
                {
                    continue;
                }

                bool onRing = ringSet.Contains(i);
                int ringDepth = Mathf.Max(0, distToRing[i]);

                int score = 0;
                score -= Mathf.RoundToInt(Mathf.Abs(depth - targetDepthRatio) * 220f);
                score += onRing ? 86 : (ringDepth == 1 ? 36 : -56);
                score += IsRingCorner(graph, i, ringSet) ? 24 : 0;
                score += graph.GetDegree(i) >= 3 ? 20 : 0;
                score += CountNonRingNeighbors(graph, i, ringSet) * 10;

                if (distFromBoss[i] >= 0 && distFromBoss[i] < 2)
                {
                    score -= 40;
                }

                score += random.NextInt(0, 30);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestId = i;
                }
            }

            return bestId;
        }

        private static void GenerateTaskTriggerConnections(
            RoomGraph graph,
            int taskNodeId,
            RoomRole role,
            List<TaskTriggerConnection> output,
            ref DeterministicRandom random)
        {
            if (graph == null || output == null)
            {
                return;
            }

            List<int> neighbors = graph.GetNeighborsSorted(taskNodeId);
            int branchNeighborCount = 0;

            for (int i = 0; i < neighbors.Count; i++)
            {
                int neighborId = neighbors[i];
                RoomGraphEdge edge = FindEdge(graph, taskNodeId, neighborId);
                if (edge.IsLoopEdge)
                {
                    continue;
                }

                RoomGraphNode neighbor = graph.GetNode(neighborId);
                if (neighbor.AssignedRole == RoomRole.Connector)
                {
                    branchNeighborCount++;
                }
            }

            for (int i = 0; i < neighbors.Count; i++)
            {
                int neighborId = neighbors[i];
                RoomGraphEdge edge = FindEdge(graph, taskNodeId, neighborId);
                if (edge.IsLoopEdge)
                {
                    continue;
                }

                RoomGraphNode neighbor = graph.GetNode(neighborId);
                if (neighbor.AssignedRole == RoomRole.Connector)
                {
                    output.Add(new TaskTriggerConnection
                    {
                        TaskNodeId = taskNodeId,
                        TaskRole = role,
                        ConnectedNodeId = neighborId,
                        IsPrimaryTrigger = branchNeighborCount == 1 || random.Chance(0.6f)
                    });
                }
            }
        }

        private static RoomGraphEdge FindEdge(RoomGraph graph, int nodeA, int nodeB)
        {
            int low = Mathf.Min(nodeA, nodeB);
            int high = Mathf.Max(nodeA, nodeB);

            for (int i = 0; i < graph.Edges.Count; i++)
            {
                RoomGraphEdge edge = graph.Edges[i];
                if (edge.NodeA == low && edge.NodeB == high)
                {
                    return edge;
                }
            }

            return new RoomGraphEdge
            {
                NodeA = low,
                NodeB = high,
                IsLoopEdge = false
            };
        }

        private static bool IsRingCorner(RoomGraph graph, int nodeId, HashSet<int> ringSet)
        {
            if (!ringSet.Contains(nodeId))
            {
                return false;
            }

            List<int> neighbors = graph.GetNeighborsSorted(nodeId);
            List<int> ringNeighbors = new List<int>();
            for (int i = 0; i < neighbors.Count; i++)
            {
                if (ringSet.Contains(neighbors[i]))
                {
                    ringNeighbors.Add(neighbors[i]);
                }
            }

            if (ringNeighbors.Count < 2)
            {
                return false;
            }

            if (ringNeighbors.Count >= 3)
            {
                return true;
            }

            Vector2Int center = graph.GetNode(nodeId).GridPosition;
            Vector2Int v1 = graph.GetNode(ringNeighbors[0]).GridPosition - center;
            Vector2Int v2 = graph.GetNode(ringNeighbors[1]).GridPosition - center;
            return v1 != -v2;
        }

        private static bool IsAdjacentToRing(RoomGraph graph, int nodeId, HashSet<int> ringSet)
        {
            List<int> neighbors = graph.GetNeighborsSorted(nodeId);
            for (int i = 0; i < neighbors.Count; i++)
            {
                if (ringSet.Contains(neighbors[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static int CountNonRingNeighbors(RoomGraph graph, int nodeId, HashSet<int> ringSet)
        {
            int count = 0;
            List<int> neighbors = graph.GetNeighborsSorted(nodeId);
            for (int i = 0; i < neighbors.Count; i++)
            {
                if (!ringSet.Contains(neighbors[i]))
                {
                    count++;
                }
            }

            return count;
        }

        private static List<int> ResolvePrimaryRingNodes(RoomGraph graph)
        {
            List<int> result = new List<int>();
            if (graph == null || graph.NodeCount <= 0)
            {
                return result;
            }

            if (graph.PrimaryRingNodeIds != null && graph.PrimaryRingNodeIds.Count >= 4)
            {
                for (int i = 0; i < graph.PrimaryRingNodeIds.Count; i++)
                {
                    int nodeId = graph.PrimaryRingNodeIds[i];
                    if (nodeId >= 0 && nodeId < graph.NodeCount)
                    {
                        result.Add(nodeId);
                    }
                }

                if (result.Count >= 4)
                {
                    return result;
                }
            }

            HashSet<int> core = BuildTwoCore(graph);
            if (core.Count < 4)
            {
                return result;
            }

            if (core.Contains(0))
            {
                Queue<int> queue = new Queue<int>();
                HashSet<int> visited = new HashSet<int>();
                queue.Enqueue(0);
                visited.Add(0);

                while (queue.Count > 0)
                {
                    int current = queue.Dequeue();
                    result.Add(current);

                    List<int> neighbors = graph.GetNeighborsSorted(current);
                    for (int i = 0; i < neighbors.Count; i++)
                    {
                        int next = neighbors[i];
                        if (!core.Contains(next) || visited.Contains(next))
                        {
                            continue;
                        }

                        visited.Add(next);
                        queue.Enqueue(next);
                    }
                }
            }

            if (result.Count < 4)
            {
                foreach (int nodeId in core)
                {
                    result.Add(nodeId);
                }
            }

            return result;
        }

        private static HashSet<int> BuildTwoCore(RoomGraph graph)
        {
            HashSet<int> alive = new HashSet<int>();
            int[] degree = new int[graph.NodeCount];

            for (int i = 0; i < graph.NodeCount; i++)
            {
                degree[i] = graph.GetDegree(i);
                alive.Add(i);
            }

            Queue<int> queue = new Queue<int>();
            for (int i = 0; i < graph.NodeCount; i++)
            {
                if (degree[i] <= 1)
                {
                    queue.Enqueue(i);
                }
            }

            while (queue.Count > 0)
            {
                int nodeId = queue.Dequeue();
                if (!alive.Contains(nodeId))
                {
                    continue;
                }

                alive.Remove(nodeId);

                List<int> neighbors = graph.GetNeighborsSorted(nodeId);
                for (int i = 0; i < neighbors.Count; i++)
                {
                    int next = neighbors[i];
                    if (!alive.Contains(next))
                    {
                        continue;
                    }

                    degree[next]--;
                    if (degree[next] == 1)
                    {
                        queue.Enqueue(next);
                    }
                }
            }

            return alive;
        }

        private static int[] CalculateDistanceToSet(RoomGraph graph, HashSet<int> sourceSet)
        {
            int[] dist = new int[graph.NodeCount];
            for (int i = 0; i < dist.Length; i++)
            {
                dist[i] = -1;
            }

            Queue<int> queue = new Queue<int>();
            foreach (int nodeId in sourceSet)
            {
                if (nodeId < 0 || nodeId >= graph.NodeCount)
                {
                    continue;
                }

                dist[nodeId] = 0;
                queue.Enqueue(nodeId);
            }

            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                List<int> neighbors = graph.GetNeighborsSorted(current);
                for (int i = 0; i < neighbors.Count; i++)
                {
                    int next = neighbors[i];
                    if (dist[next] >= 0)
                    {
                        continue;
                    }

                    dist[next] = dist[current] + 1;
                    queue.Enqueue(next);
                }
            }

            return dist;
        }

        private static Vector2 ResolveCenter(RoomGraph graph, IList<int> nodeIds)
        {
            if (graph == null || nodeIds == null || nodeIds.Count == 0)
            {
                return Vector2.zero;
            }

            Vector2 sum = Vector2.zero;
            for (int i = 0; i < nodeIds.Count; i++)
            {
                int nodeId = nodeIds[i];
                if (nodeId < 0 || nodeId >= graph.NodeCount)
                {
                    continue;
                }

                sum += graph.GetNode(nodeId).GridPosition;
            }

            return sum / Mathf.Max(1, nodeIds.Count);
        }

        private static RoomRole MapSideTaskToRole(SideTaskType sideTask)
        {
            switch (sideTask)
            {
                case SideTaskType.Defense:
                    return RoomRole.SideDefense;
                case SideTaskType.Capture:
                    return RoomRole.SideCapture;
                case SideTaskType.Destroy:
                    return RoomRole.SideDestroy;
                default:
                    return RoomRole.SideElimination;
            }
        }

        private static int EstimateShopCount(int nodeCount)
        {
            if (nodeCount < 10)
            {
                return 1;
            }

            if (nodeCount < 18)
            {
                return 2;
            }

            return 3;
        }

        private static int[] CalculateShortestDistance(RoomGraph graph, int startNodeId, out int[] predecessor)
        {
            int[] dist = new int[graph.NodeCount];
            predecessor = new int[graph.NodeCount];
            for (int i = 0; i < dist.Length; i++)
            {
                dist[i] = -1;
                predecessor[i] = -1;
            }

            Queue<int> queue = new Queue<int>();
            queue.Enqueue(startNodeId);
            dist[startNodeId] = 0;

            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                List<int> neighbors = graph.GetNeighborsSorted(current);
                for (int i = 0; i < neighbors.Count; i++)
                {
                    int next = neighbors[i];
                    if (dist[next] >= 0)
                    {
                        continue;
                    }

                    dist[next] = dist[current] + 1;
                    predecessor[next] = current;
                    queue.Enqueue(next);
                }
            }

            return dist;
        }

        private static int FindMaxDistance(int[] distances, int startId)
        {
            int max = 0;
            for (int i = 0; i < distances.Length; i++)
            {
                if (i == startId || distances[i] < 0)
                {
                    continue;
                }

                if (distances[i] > max)
                {
                    max = distances[i];
                }
            }

            return max;
        }

        private static int FindFarthestNode(int[] distances, int fallback)
        {
            int farthest = fallback;
            int maxDistance = int.MinValue;

            for (int i = 0; i < distances.Length; i++)
            {
                if (distances[i] > maxDistance)
                {
                    maxDistance = distances[i];
                    farthest = i;
                }
            }

            return farthest;
        }

        private static List<int> BuildShortestPath(RoomGraph graph, int startId, int endId, int[] predecessor)
        {
            List<int> path = new List<int>();
            int cursor = endId;
            while (cursor >= 0)
            {
                path.Add(cursor);
                if (cursor == startId)
                {
                    break;
                }

                cursor = predecessor[cursor];
            }

            path.Reverse();
            return path;
        }

        private static void AssignRolesLinearFallback(
            RoomGraph graph,
            MapTaskInput taskInput,
            ref DeterministicRandom random,
            List<TaskTriggerConnection> taskTriggerConnections)
        {
            int startId = 0;
            graph.GetNode(startId).AssignedRole = RoomRole.Start;

            int[] predecessor;
            int[] distFromStart = CalculateShortestDistance(graph, startId, out predecessor);

            int bossId = FindBossNodeLinearFallback(graph, distFromStart, startId, maxDistance: 8, ref random);
            graph.GetNode(bossId).AssignedRole = RoomRole.Boss;

            List<int> mainPath = BuildShortestPath(graph, startId, bossId, predecessor);
            HashSet<int> mainPathSet = new HashSet<int>(mainPath);

            AssignSideTasksLinear(
                graph, taskInput, startId, bossId, mainPathSet,
                distFromStart, maxDistance: 8, ref random, taskTriggerConnections);

            AssignShopRoomsLinear(graph, startId, bossId, mainPath, mainPathSet, distFromStart, ref random);
        }

        private static int FindBossNodeLinearFallback(
            RoomGraph graph, int[] distances, int startNodeId,
            int maxDistance, ref DeterministicRandom random)
        {
            int maxDist = FindMaxDistance(distances, startNodeId);
            if (maxDist <= 0)
            {
                return FindFarthestNode(distances, startNodeId);
            }

            int targetDist = Mathf.Max(2, Mathf.RoundToInt(maxDist * 0.90f));
            int bestId = -1;
            int bestScore = int.MinValue;

            for (int i = 0; i < distances.Length; i++)
            {
                if (i == startNodeId || distances[i] < 0)
                {
                    continue;
                }

                int dist = distances[i];
                int score = 0;
                score += dist * 16;

                float progress = dist / (float)Mathf.Max(1, maxDist);
                if (progress >= BossMinProgress && progress <= BossMaxProgress)
                {
                    score += 80;
                }

                if (graph.GetDegree(i) == 1)
                {
                    score += 36;
                }
                else if (graph.GetDegree(i) == 2)
                {
                    score += 8;
                }

                score -= Mathf.Abs(dist - targetDist) * 12;
                score += random.NextInt(0, 16);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestId = i;
                }
            }

            return bestId >= 0 ? bestId : FindFarthestNode(distances, startNodeId);
        }

        private static void AssignSideTasksLinear(
            RoomGraph graph,
            MapTaskInput taskInput,
            int startId,
            int bossId,
            HashSet<int> mainPathSet,
            int[] distFromStart,
            int maxDistance,
            ref DeterministicRandom random,
            List<TaskTriggerConnection> taskTriggerConnections)
        {
            if (taskInput == null || taskInput.SideTasks == null || taskInput.SideTasks.Count == 0)
            {
                return;
            }

            for (int i = 0; i < taskInput.SideTasks.Count; i++)
            {
                SideTaskInput sideTask = taskInput.SideTasks[i];
                float targetDepth = 0.50f + (random.NextFloat01() - 0.5f) * 0.08f;

                int sideNodeId = SelectSideTaskNodeLinear(
                    graph,
                    startId,
                    bossId,
                    targetDepth,
                    maxDistance,
                    mainPathSet,
                    distFromStart,
                    ref random);

                if (sideNodeId < 0)
                {
                    // 放宽约束重试：扩大深度范围，确保任务房间角色能被分配
                    sideNodeId = SelectSideTaskNodeLinearRelaxed(
                        graph, startId, bossId, targetDepth,
                        maxDistance, mainPathSet, distFromStart, ref random);
                }

                if (sideNodeId < 0)
                {
                    continue;
                }

                RoomGraphNode node = graph.GetNode(sideNodeId);
                node.AssignedRole = MapSideTaskToRole(sideTask.TaskType);
                node.HasAssignedSideTask = true;
                node.AssignedSideTask = sideTask.TaskType;

                if (taskTriggerConnections != null)
                {
                    GenerateTaskTriggerConnections(graph, sideNodeId, node.AssignedRole, taskTriggerConnections, ref random);
                }
            }
        }

        /// <summary>
        /// 放宽约束的线性侧线任务节点选择（扩大深度范围到 [0.15, 0.85]）。
        /// 当标准选择找不到满足严格深度约束的节点时调用。
        /// </summary>
        private static int SelectSideTaskNodeLinearRelaxed(
            RoomGraph graph,
            int startId,
            int bossId,
            float targetDepthRatio,
            int maxDistance,
            HashSet<int> mainPathSet,
            int[] distFromStart,
            ref DeterministicRandom random)
        {
            int bestId = -1;
            int bestScore = int.MinValue;

            for (int i = 0; i < graph.NodeCount; i++)
            {
                if (i == startId || i == bossId)
                {
                    continue;
                }

                RoomGraphNode node = graph.GetNode(i);
                if (node.AssignedRole != RoomRole.Connector)
                {
                    continue;
                }

                int dist = distFromStart[i];
                if (dist <= 0)
                {
                    continue;
                }

                float depth = dist / (float)Mathf.Max(1, maxDistance);

                // 放宽深度范围到 [0.15, 0.85]
                const float relaxedMin = 0.15f;
                const float relaxedMax = 0.85f;
                if (depth < relaxedMin || depth > relaxedMax)
                {
                    continue;
                }

                int score = 0;
                score -= Mathf.RoundToInt(Mathf.Abs(depth - targetDepthRatio) * 200f);
                score += graph.GetDegree(i) * 8;
                score += IsAdjacentToMainPath(graph, i, mainPathSet) ? 15 : 0;
                score += random.NextInt(0, 20);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestId = i;
                }
            }

            return bestId;
        }

        private static int SelectSideTaskNodeLinear(
            RoomGraph graph,
            int startId,
            int bossId,
            float targetDepthRatio,
            int maxDistance,
            HashSet<int> mainPathSet,
            int[] distFromStart,
            ref DeterministicRandom random)
        {
            int bestId = -1;
            int bestScore = int.MinValue;

            for (int i = 0; i < graph.NodeCount; i++)
            {
                if (i == startId || i == bossId)
                {
                    continue;
                }

                RoomGraphNode node = graph.GetNode(i);
                if (node.AssignedRole != RoomRole.Connector)
                {
                    continue;
                }

                int dist = distFromStart[i];
                if (dist <= 0)
                {
                    continue;
                }

                float depth = dist / (float)Mathf.Max(1, maxDistance);
                if (depth < SideTaskMinProgress || depth > SideTaskMaxProgress)
                {
                    continue;
                }

                bool isOnMainPath = mainPathSet.Contains(i);
                bool adjacentToMainPath = IsAdjacentToMainPath(graph, i, mainPathSet);

                int score = 0;
                score -= Mathf.RoundToInt(Mathf.Abs(depth - targetDepthRatio) * 260f);
                score += isOnMainPath ? -26 : 42;
                score += adjacentToMainPath ? 20 : 0;
                score += Mathf.Min(3, graph.GetDegree(i)) * 7;
                score += random.NextInt(0, 24);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestId = i;
                }
            }

            return bestId;
        }

        private static void AssignShopRoomsLinear(
            RoomGraph graph,
            int startId,
            int bossId,
            List<int> mainPath,
            HashSet<int> mainPathSet,
            int[] distFromStart,
            ref DeterministicRandom random)
        {
            int maxDist = FindMaxDistance(distFromStart, startId);
            if (maxDist <= 2)
            {
                return;
            }

            int shopCount = EstimateShopCount(graph.NodeCount);
            if (shopCount <= 0)
            {
                return;
            }

            List<int> selected = new List<int>();
            for (int i = 0; i < shopCount; i++)
            {
                float t = shopCount == 1 ? 0.56f : Mathf.Lerp(0.45f, 0.78f, i / (float)(shopCount - 1));
                int shopNodeId = SelectShopNodeLinear(
                    graph,
                    startId,
                    bossId,
                    t,
                    maxDist,
                    mainPath,
                    mainPathSet,
                    selected,
                    distFromStart,
                    ref random);

                if (shopNodeId < 0)
                {
                    continue;
                }

                selected.Add(shopNodeId);
                graph.GetNode(shopNodeId).AssignedRole = RoomRole.Shop;
            }
        }

        private static int SelectShopNodeLinear(
            RoomGraph graph,
            int startId,
            int bossId,
            float targetDepthRatio,
            int maxDistance,
            List<int> mainPath,
            HashSet<int> mainPathSet,
            List<int> alreadySelected,
            int[] distFromStart,
            ref DeterministicRandom random)
        {
            int bestId = -1;
            int bestScore = int.MinValue;

            for (int i = 0; i < graph.NodeCount; i++)
            {
                if (i == startId || i == bossId || alreadySelected.Contains(i))
                {
                    continue;
                }

                RoomGraphNode node = graph.GetNode(i);
                if (node.AssignedRole != RoomRole.Connector)
                {
                    continue;
                }

                int dist = distFromStart[i];
                if (dist <= 1)
                {
                    continue;
                }

                float depth = dist / (float)maxDistance;
                if (depth < 0.20f || depth > 0.90f)
                {
                    continue;
                }

                bool onMainPath = mainPathSet.Contains(i);
                int connectivity = graph.GetDegree(i);
                int sideNeighbors = CountNonMainNeighbors(graph, i, mainPathSet);

                int score = 0;
                score -= Mathf.RoundToInt(Mathf.Abs(depth - targetDepthRatio) * 220f);
                score += onMainPath ? 22 : 8;
                score += connectivity * 14;
                score += sideNeighbors * 10;

                int pathIndex = mainPath.IndexOf(i);
                if (pathIndex >= 0)
                {
                    float pathProgress = pathIndex / Mathf.Max(1f, (mainPath.Count - 1f));
                    score -= Mathf.RoundToInt(Mathf.Abs(pathProgress - targetDepthRatio) * 120f);
                }

                score += random.NextInt(0, 30);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestId = i;
                }
            }

            return bestId;
        }

        private static bool IsAdjacentToMainPath(RoomGraph graph, int nodeId, HashSet<int> mainPathSet)
        {
            List<int> neighbors = graph.GetNeighborsSorted(nodeId);
            for (int i = 0; i < neighbors.Count; i++)
            {
                if (mainPathSet.Contains(neighbors[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static int CountNonMainNeighbors(RoomGraph graph, int nodeId, HashSet<int> mainPathSet)
        {
            int count = 0;
            List<int> neighbors = graph.GetNeighborsSorted(nodeId);
            for (int i = 0; i < neighbors.Count; i++)
            {
                if (!mainPathSet.Contains(neighbors[i]))
                {
                    count++;
                }
            }

            return count;
        }
    }

    /// <summary>
    /// Records a trigger connection from a task room to an adjacent connector room.
    /// Used by external systems (e.g. MissionManager) to know which graph edges are task-relevant.
    /// </summary>
    public struct TaskTriggerConnection
    {
        public int TaskNodeId;
        public RoomRole TaskRole;
        public int ConnectedNodeId;
        public bool IsPrimaryTrigger;
    }
}
