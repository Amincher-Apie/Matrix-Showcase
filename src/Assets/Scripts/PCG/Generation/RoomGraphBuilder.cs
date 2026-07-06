
using System.Collections.Generic;
using UnityEngine;

namespace Matrix.PCG
{
    /// <summary>
    /// Builds a connected topology with ring-first strategy:
    /// 1) primary ring, 2) ring branches, 3) secondary branches, 4) structured local loops.
    /// </summary>
    public static class RoomGraphBuilder
    {
        private struct DirectionCandidate
        {
            public Vector2Int Direction;
            public int Score;
            public int TieBreaker;
        }

        private struct LoopEdgeCandidate
        {
            public int NodeA;
            public int NodeB;
            public int Score;
            public int TieBreaker;
        }

        private struct TopologyRuntimeSettings
        {
            public int TargetRingRooms;
            public float BranchDensity;
            public float SecondaryBranchChance;
            public int MaxPrimaryBranchLength;
            public int MaxSecondaryBranchLength;
            public int StructuredSecondaryLoopCount;
            public float SecondaryLoopChance;
        }

        private static readonly Vector2Int[] ExpansionDirections =
        {
            Vector2Int.up,
            Vector2Int.right,
            Vector2Int.down,
            Vector2Int.left
        };

        private static readonly Vector2Int[] LoopScanDirections =
        {
            Vector2Int.up,
            Vector2Int.right
        };

        public static RoomGraph Build(MapGenerationRequest request, ref DeterministicRandom random)
        {
            RoomGraph graph = new RoomGraph();
            if (request == null)
            {
                return graph;
            }

            int sideTaskCount = request.TaskInput != null && request.TaskInput.SideTasks != null
                ? request.TaskInput.SideTasks.Count
                : 0;

            int maxNodeDegree = Mathf.Max(2, request.ScaleSettings.MaxNodeDegree);
            // Hard minimum: enough rooms for Start + Boss + 1 SideTask + 1 Shop + a few connectors.
            int absoluteMinRooms = Mathf.Max(6, 4 + sideTaskCount);
            int targetRoomCount = Mathf.Max(absoluteMinRooms, request.ScaleSettings.TargetRoomCount);

            TopologyRuntimeSettings topology = ResolveTopologySettings(request.ScaleSettings, targetRoomCount, sideTaskCount);

            Dictionary<Vector2Int, int> occupied = new Dictionary<Vector2Int, int>();
            int radiusLimit = Mathf.Max(4, Mathf.CeilToInt(Mathf.Sqrt(targetRoomCount) * 2.2f));

            List<int> primaryRingNodes = BuildPrimaryRing(graph, occupied, topology.TargetRingRooms, radiusLimit, ref random);
            if (primaryRingNodes.Count == 0)
            {
                int startId = graph.AddNode(Vector2Int.zero);
                occupied[Vector2Int.zero] = startId;
                primaryRingNodes.Add(startId);
            }

            graph.SetPrimaryRingNodes(primaryRingNodes);
            HashSet<int> primaryRingSet = new HashSet<int>(primaryRingNodes);
            HashSet<int> branchNodeSet = new HashSet<int>();

            int remainingAfterRing = targetRoomCount - graph.NodeCount;
            int primaryBranchRoomBudget = Mathf.Clamp(
                Mathf.RoundToInt(remainingAfterRing * topology.BranchDensity),
                0,
                remainingAfterRing);

            GrowBranchesFromRing(
                graph,
                occupied,
                primaryRingNodes,
                branchNodeSet,
                primaryBranchRoomBudget,
                targetRoomCount,
                maxNodeDegree,
                radiusLimit,
                topology.MaxPrimaryBranchLength,
                ref random);

            int remainingAfterPrimaryBranch = targetRoomCount - graph.NodeCount;
            GrowSecondaryBranches(
                graph,
                occupied,
                primaryRingNodes,
                branchNodeSet,
                remainingAfterPrimaryBranch,
                targetRoomCount,
                maxNodeDegree,
                radiusLimit,
                topology.SecondaryBranchChance,
                topology.MaxSecondaryBranchLength,
                ref random);

            if (graph.NodeCount < targetRoomCount)
            {
                FillRemainingWithCapillaryBranches(
                    graph,
                    occupied,
                    primaryRingNodes,
                    branchNodeSet,
                    targetRoomCount,
                    maxNodeDegree,
                    radiusLimit,
                    ref random);
            }

            AddStructuredSecondaryLoops(
                graph,
                occupied,
                primaryRingSet,
                request.ScaleSettings.ExtraLoopCount,
                topology.StructuredSecondaryLoopCount,
                topology.SecondaryLoopChance,
                maxNodeDegree,
                ref random);

            return graph;
        }

        private static TopologyRuntimeSettings ResolveTopologySettings(MapScaleSettings scaleSettings, int targetRoomCount, int sideTaskCount)
        {
            MapScaleSettings settings = scaleSettings ?? new MapScaleSettings();

            float ringRatio = Mathf.Clamp(settings.PrimaryRingRatio, 0.20f, 0.80f);
            int reserveBranchRooms = Mathf.Max(2, sideTaskCount + 1);
            int maxRingRooms = Mathf.Max(4, targetRoomCount - reserveBranchRooms);

            int targetRingRooms = Mathf.Clamp(Mathf.RoundToInt(targetRoomCount * ringRatio), 4, maxRingRooms);
            if ((targetRingRooms & 1) == 1)
            {
                targetRingRooms += targetRingRooms < maxRingRooms ? 1 : -1;
            }

            return new TopologyRuntimeSettings
            {
                TargetRingRooms = Mathf.Clamp(targetRingRooms, 4, Mathf.Max(4, targetRoomCount - 1)),
                BranchDensity = Mathf.Clamp01(settings.BranchDensity),
                SecondaryBranchChance = Mathf.Clamp01(settings.SecondaryBranchChance),
                MaxPrimaryBranchLength = Mathf.Max(1, settings.MaxPrimaryBranchLength),
                MaxSecondaryBranchLength = Mathf.Max(1, settings.MaxSecondaryBranchLength),
                StructuredSecondaryLoopCount = Mathf.Max(0, settings.StructuredSecondaryLoopCount),
                SecondaryLoopChance = Mathf.Clamp01(settings.SecondaryLoopChance)
            };
        }

        private static List<int> BuildPrimaryRing(
            RoomGraph graph,
            Dictionary<Vector2Int, int> occupied,
            int targetRingRooms,
            int radiusLimit,
            ref DeterministicRandom random)
        {
            List<int> ringNodeIds = new List<int>();
            if (!TryResolveRingDimensions(targetRingRooms, ref random, out int width, out int height))
            {
                return ringNodeIds;
            }

            List<Vector2Int> perimeter = BuildRectanglePerimeter(width, height);
            if (perimeter.Count < 4)
            {
                return ringNodeIds;
            }

            if (random.Chance(0.5f))
            {
                perimeter.Reverse();
            }

            int startOffset = random.NextInt(perimeter.Count);
            Vector2Int centerOffset = new Vector2Int(
                -Mathf.RoundToInt((width - 1) * 0.5f),
                -Mathf.RoundToInt((height - 1) * 0.5f));

            for (int i = 0; i < perimeter.Count; i++)
            {
                int sourceIndex = (startOffset + i) % perimeter.Count;
                Vector2Int pos = perimeter[sourceIndex] + centerOffset;

                if (Mathf.Abs(pos.x) > radiusLimit || Mathf.Abs(pos.y) > radiusLimit || occupied.ContainsKey(pos))
                {
                    return new List<int>();
                }

                int nodeId = graph.AddNode(pos);
                occupied[pos] = nodeId;
                ringNodeIds.Add(nodeId);

                if (ringNodeIds.Count > 1)
                {
                    graph.AddEdge(ringNodeIds[ringNodeIds.Count - 2], nodeId, false);
                }
            }

            if (ringNodeIds.Count >= 3)
            {
                graph.AddEdge(ringNodeIds[ringNodeIds.Count - 1], ringNodeIds[0], false);
            }

            return ringNodeIds;
        }

        private static bool TryResolveRingDimensions(int targetRingRooms, ref DeterministicRandom random, out int width, out int height)
        {
            width = 0;
            height = 0;

            int bestScore = int.MinValue;
            int maxDimension = Mathf.Max(4, targetRingRooms / 2 + 3);

            for (int w = 2; w <= maxDimension; w++)
            {
                for (int h = 2; h <= maxDimension; h++)
                {
                    int perimeter = 2 * (w + h) - 4;
                    if (perimeter < 4)
                    {
                        continue;
                    }

                    int score = 0;
                    score -= Mathf.Abs(perimeter - targetRingRooms) * 48;
                    score -= Mathf.Abs(w - h) * 6;

                    if (perimeter > targetRingRooms + 4)
                    {
                        score -= 180;
                    }

                    score += random.NextInt(0, 20);

                    if (score > bestScore)
                    {
                        bestScore = score;
                        width = w;
                        height = h;
                    }
                }
            }

            return width >= 2 && height >= 2;
        }

        private static List<Vector2Int> BuildRectanglePerimeter(int width, int height)
        {
            List<Vector2Int> result = new List<Vector2Int>(Mathf.Max(4, 2 * (width + height) - 4));

            for (int x = 0; x < width; x++)
            {
                result.Add(new Vector2Int(x, 0));
            }

            for (int y = 1; y < height; y++)
            {
                result.Add(new Vector2Int(width - 1, y));
            }

            for (int x = width - 2; x >= 0; x--)
            {
                result.Add(new Vector2Int(x, height - 1));
            }

            for (int y = height - 2; y >= 1; y--)
            {
                result.Add(new Vector2Int(0, y));
            }

            return result;
        }

        private static void GrowBranchesFromRing(
            RoomGraph graph,
            Dictionary<Vector2Int, int> occupied,
            List<int> primaryRingNodes,
            HashSet<int> branchNodeSet,
            int roomBudget,
            int targetRoomCount,
            int maxNodeDegree,
            int radiusLimit,
            int maxBranchLength,
            ref DeterministicRandom random)
        {
            if (roomBudget <= 0 || primaryRingNodes == null || primaryRingNodes.Count == 0)
            {
                return;
            }

            Vector2 ringCenter = ResolveRingCenter(graph, primaryRingNodes);
            List<int> expandableAnchors = new List<int>();
            List<int> addedNodeIds = new List<int>();

            int addedRooms = 0;
            int attempts = 0;
            int maxAttempts = Mathf.Max(48, roomBudget * 16);

            while (addedRooms < roomBudget && graph.NodeCount < targetRoomCount && attempts < maxAttempts)
            {
                attempts++;

                expandableAnchors.Clear();
                CollectExpandableNodes(graph, occupied, primaryRingNodes, maxNodeDegree, radiusLimit, expandableAnchors);
                if (expandableAnchors.Count == 0)
                {
                    break;
                }

                int anchorId = expandableAnchors[random.NextInt(expandableAnchors.Count)];
                int budget = Mathf.Min(roomBudget - addedRooms, targetRoomCount - graph.NodeCount);
                if (budget <= 0)
                {
                    break;
                }

                int branchLength = Mathf.Clamp(SamplePrimaryBranchLength(maxBranchLength, ref random), 1, budget);
                Vector2Int preferredDirection = ResolveRingRelativeDirection(
                    graph.GetNode(anchorId).GridPosition,
                    ringCenter,
                    0.25f,
                    ref random);

                int added = GrowBranch(
                    graph,
                    occupied,
                    anchorId,
                    branchLength,
                    preferredDirection,
                    maxNodeDegree,
                    radiusLimit,
                    ref random,
                    addedNodeIds);

                if (added <= 0)
                {
                    continue;
                }

                addedRooms += added;
                for (int i = 0; i < addedNodeIds.Count; i++)
                {
                    branchNodeSet.Add(addedNodeIds[i]);
                }
            }
        }

        private static void GrowSecondaryBranches(
            RoomGraph graph,
            Dictionary<Vector2Int, int> occupied,
            List<int> primaryRingNodes,
            HashSet<int> branchNodeSet,
            int roomBudget,
            int targetRoomCount,
            int maxNodeDegree,
            int radiusLimit,
            float secondaryBranchChance,
            int maxSecondaryBranchLength,
            ref DeterministicRandom random)
        {
            if (roomBudget <= 0 || branchNodeSet == null || branchNodeSet.Count == 0)
            {
                return;
            }

            Vector2 ringCenter = ResolveRingCenter(graph, primaryRingNodes);
            List<int> branchSeeds = new List<int>();
            List<int> addedNodeIds = new List<int>();

            int addedRooms = 0;
            int attempts = 0;
            int maxAttempts = Mathf.Max(64, roomBudget * 20);

            while (addedRooms < roomBudget && graph.NodeCount < targetRoomCount && attempts < maxAttempts)
            {
                attempts++;

                branchSeeds.Clear();
                CollectExpandableNodes(graph, occupied, branchNodeSet, maxNodeDegree, radiusLimit, branchSeeds);
                if (branchSeeds.Count == 0)
                {
                    break;
                }

                int anchorId = branchSeeds[random.NextInt(branchSeeds.Count)];
                if (!random.Chance(secondaryBranchChance))
                {
                    continue;
                }

                int budget = Mathf.Min(roomBudget - addedRooms, targetRoomCount - graph.NodeCount);
                if (budget <= 0)
                {
                    break;
                }

                int branchLength = Mathf.Clamp(SampleSecondaryBranchLength(maxSecondaryBranchLength, ref random), 1, budget);
                Vector2Int preferredDirection = ResolveRingRelativeDirection(
                    graph.GetNode(anchorId).GridPosition,
                    ringCenter,
                    0.48f,
                    ref random);

                int added = GrowBranch(
                    graph,
                    occupied,
                    anchorId,
                    branchLength,
                    preferredDirection,
                    maxNodeDegree,
                    radiusLimit,
                    ref random,
                    addedNodeIds);

                if (added <= 0)
                {
                    continue;
                }

                addedRooms += added;
                for (int i = 0; i < addedNodeIds.Count; i++)
                {
                    branchNodeSet.Add(addedNodeIds[i]);
                }
            }
        }

        private static void FillRemainingWithCapillaryBranches(
            RoomGraph graph,
            Dictionary<Vector2Int, int> occupied,
            List<int> primaryRingNodes,
            HashSet<int> branchNodeSet,
            int targetRoomCount,
            int maxNodeDegree,
            int radiusLimit,
            ref DeterministicRandom random)
        {
            Vector2 ringCenter = ResolveRingCenter(graph, primaryRingNodes);

            List<int> allExpandable = new List<int>();
            List<int> branchExpandable = new List<int>();
            List<int> addedNodeIds = new List<int>();

            int attempts = 0;
            int maxAttempts = Mathf.Max(96, targetRoomCount * 20);

            while (graph.NodeCount < targetRoomCount && attempts < maxAttempts)
            {
                attempts++;

                allExpandable.Clear();
                for (int i = 0; i < graph.NodeCount; i++)
                {
                    int nodeId = graph.GetNode(i).Id;
                    if (graph.GetDegree(nodeId) >= maxNodeDegree)
                    {
                        continue;
                    }

                    if (CountFreeNeighbors(graph.GetNode(nodeId).GridPosition, occupied, radiusLimit) <= 0)
                    {
                        continue;
                    }

                    allExpandable.Add(nodeId);
                }

                if (allExpandable.Count == 0)
                {
                    break;
                }

                branchExpandable.Clear();
                for (int i = 0; i < allExpandable.Count; i++)
                {
                    int nodeId = allExpandable[i];
                    if (branchNodeSet.Contains(nodeId))
                    {
                        branchExpandable.Add(nodeId);
                    }
                }

                int anchorId;
                if (branchExpandable.Count > 0 && random.Chance(0.72f))
                {
                    anchorId = branchExpandable[random.NextInt(branchExpandable.Count)];
                }
                else
                {
                    anchorId = allExpandable[random.NextInt(allExpandable.Count)];
                }

                int budget = targetRoomCount - graph.NodeCount;
                int branchLength = Mathf.Clamp(random.NextInt(1, 3), 1, budget);
                Vector2Int preferredDirection = ResolveRingRelativeDirection(
                    graph.GetNode(anchorId).GridPosition,
                    ringCenter,
                    0.50f,
                    ref random);

                int added = GrowBranch(
                    graph,
                    occupied,
                    anchorId,
                    branchLength,
                    preferredDirection,
                    maxNodeDegree,
                    radiusLimit,
                    ref random,
                    addedNodeIds);

                if (added <= 0)
                {
                    continue;
                }

                for (int i = 0; i < addedNodeIds.Count; i++)
                {
                    branchNodeSet.Add(addedNodeIds[i]);
                }
            }
        }

        private static int SamplePrimaryBranchLength(int maxLength, ref DeterministicRandom random)
        {
            int safeMax = Mathf.Max(1, maxLength);
            float roll = random.NextFloat01();

            if (roll < 0.68f)
            {
                return Mathf.Clamp(random.NextInt(1, 3), 1, safeMax);
            }

            if (roll < 0.92f)
            {
                return Mathf.Clamp(random.NextInt(3, 5), 1, safeMax);
            }

            int longMin = Mathf.Min(5, safeMax);
            return Mathf.Clamp(random.NextInt(longMin, safeMax + 1), 1, safeMax);
        }

        private static int SampleSecondaryBranchLength(int maxLength, ref DeterministicRandom random)
        {
            int safeMax = Mathf.Max(1, maxLength);
            float roll = random.NextFloat01();

            if (roll < 0.78f)
            {
                return Mathf.Clamp(random.NextInt(1, 3), 1, safeMax);
            }

            return Mathf.Clamp(random.NextInt(2, 4), 1, safeMax);
        }

        private static int GrowBranch(
            RoomGraph graph,
            Dictionary<Vector2Int, int> occupied,
            int anchorNodeId,
            int branchLength,
            Vector2Int preferredDirection,
            int maxNodeDegree,
            int radiusLimit,
            ref DeterministicRandom random,
            List<int> addedNodeIds)
        {
            if (addedNodeIds != null)
            {
                addedNodeIds.Clear();
            }

            if (branchLength <= 0 || graph.GetDegree(anchorNodeId) >= maxNodeDegree)
            {
                return 0;
            }

            int added = 0;
            int currentId = anchorNodeId;
            Vector2Int currentPos = graph.GetNode(anchorNodeId).GridPosition;
            Vector2Int previousDirection = Vector2Int.zero;
            Vector2Int runningPreferred = preferredDirection;

            for (int step = 0; step < branchLength; step++)
            {
                if (graph.GetDegree(currentId) >= maxNodeDegree)
                {
                    break;
                }

                if (step > 0 && previousDirection != Vector2Int.zero && random.Chance(0.72f))
                {
                    runningPreferred = previousDirection;
                }

                if (!TryPickBranchDirection(
                        currentPos,
                        previousDirection,
                        runningPreferred,
                        occupied,
                        radiusLimit,
                        ref random,
                        out Vector2Int direction))
                {
                    break;
                }

                Vector2Int nextPos = currentPos + direction;
                int nextId = graph.AddNode(nextPos);
                occupied[nextPos] = nextId;
                graph.AddEdge(currentId, nextId, false);

                added++;
                addedNodeIds?.Add(nextId);

                currentId = nextId;
                currentPos = nextPos;
                previousDirection = direction;

                if (step == 0 && preferredDirection == Vector2Int.zero)
                {
                    runningPreferred = direction;
                }
            }

            return added;
        }

        private static bool TryPickBranchDirection(
            Vector2Int fromPos,
            Vector2Int previousDirection,
            Vector2Int preferredDirection,
            Dictionary<Vector2Int, int> occupied,
            int radiusLimit,
            ref DeterministicRandom random,
            out Vector2Int selectedDirection)
        {
            selectedDirection = Vector2Int.zero;

            List<DirectionCandidate> candidates = CollectDirectionCandidates(
                fromPos,
                previousDirection,
                preferredDirection,
                occupied,
                radiusLimit,
                ref random);

            if (candidates.Count == 0)
            {
                return false;
            }

            candidates.Sort((a, b) =>
            {
                int scoreCompare = b.Score.CompareTo(a.Score);
                if (scoreCompare != 0)
                {
                    return scoreCompare;
                }

                return a.TieBreaker.CompareTo(b.TieBreaker);
            });

            selectedDirection = candidates[0].Direction;
            return true;
        }

        private static List<DirectionCandidate> CollectDirectionCandidates(
            Vector2Int fromPos,
            Vector2Int previousDirection,
            Vector2Int preferredDirection,
            Dictionary<Vector2Int, int> occupied,
            int radiusLimit,
            ref DeterministicRandom random)
        {
            List<DirectionCandidate> candidates = new List<DirectionCandidate>();

            for (int i = 0; i < ExpansionDirections.Length; i++)
            {
                Vector2Int dir = ExpansionDirections[i];
                Vector2Int nextPos = fromPos + dir;

                if (occupied.ContainsKey(nextPos))
                {
                    continue;
                }

                if (Mathf.Abs(nextPos.x) > radiusLimit || Mathf.Abs(nextPos.y) > radiusLimit)
                {
                    continue;
                }

                int score = random.NextInt(0, 80);

                if (previousDirection != Vector2Int.zero)
                {
                    if (dir == previousDirection)
                    {
                        score += 30;
                    }
                    else if (dir == -previousDirection)
                    {
                        score -= 160;
                    }
                    else
                    {
                        score += 12;
                    }
                }

                if (preferredDirection != Vector2Int.zero)
                {
                    if (dir == preferredDirection)
                    {
                        score += 48;
                    }
                    else if (dir == -preferredDirection)
                    {
                        score -= 36;
                    }
                    else
                    {
                        score += 8;
                    }
                }

                int freeAfterStep = CountFreeNeighbors(nextPos, occupied, radiusLimit);
                int occupiedAround = CountOccupiedNeighbors(nextPos, occupied);

                score += freeAfterStep * 16;
                score -= occupiedAround * 14;

                if (occupiedAround >= 3)
                {
                    score -= 90;
                }

                candidates.Add(new DirectionCandidate
                {
                    Direction = dir,
                    Score = score,
                    TieBreaker = random.NextInt(1, int.MaxValue)
                });
            }

            return candidates;
        }

        private static void CollectExpandableNodes(
            RoomGraph graph,
            Dictionary<Vector2Int, int> occupied,
            IList<int> sourceNodeIds,
            int maxNodeDegree,
            int radiusLimit,
            List<int> output)
        {
            if (output == null || sourceNodeIds == null)
            {
                return;
            }

            for (int i = 0; i < sourceNodeIds.Count; i++)
            {
                int nodeId = sourceNodeIds[i];
                if (nodeId < 0 || nodeId >= graph.NodeCount)
                {
                    continue;
                }

                if (graph.GetDegree(nodeId) >= maxNodeDegree)
                {
                    continue;
                }

                if (CountFreeNeighbors(graph.GetNode(nodeId).GridPosition, occupied, radiusLimit) <= 0)
                {
                    continue;
                }

                output.Add(nodeId);
            }
        }

        private static void CollectExpandableNodes(
            RoomGraph graph,
            Dictionary<Vector2Int, int> occupied,
            HashSet<int> sourceNodeIds,
            int maxNodeDegree,
            int radiusLimit,
            List<int> output)
        {
            if (output == null || sourceNodeIds == null)
            {
                return;
            }

            foreach (int nodeId in sourceNodeIds)
            {
                if (nodeId < 0 || nodeId >= graph.NodeCount)
                {
                    continue;
                }

                if (graph.GetDegree(nodeId) >= maxNodeDegree)
                {
                    continue;
                }

                if (CountFreeNeighbors(graph.GetNode(nodeId).GridPosition, occupied, radiusLimit) <= 0)
                {
                    continue;
                }

                output.Add(nodeId);
            }
        }

        private static Vector2 ResolveRingCenter(RoomGraph graph, IList<int> ringNodeIds)
        {
            if (ringNodeIds == null || ringNodeIds.Count == 0)
            {
                return Vector2.zero;
            }

            Vector2 sum = Vector2.zero;
            for (int i = 0; i < ringNodeIds.Count; i++)
            {
                int nodeId = ringNodeIds[i];
                if (nodeId < 0 || nodeId >= graph.NodeCount)
                {
                    continue;
                }

                sum += graph.GetNode(nodeId).GridPosition;
            }

            return sum / Mathf.Max(1, ringNodeIds.Count);
        }

        private static Vector2Int ResolveRingRelativeDirection(
            Vector2Int anchorPosition,
            Vector2 ringCenter,
            float inwardChance,
            ref DeterministicRandom random)
        {
            Vector2 fromCenter = (Vector2)anchorPosition - ringCenter;
            if (fromCenter.sqrMagnitude < 0.0001f)
            {
                return ExpansionDirections[random.NextInt(ExpansionDirections.Length)];
            }

            Vector2Int outward;
            if (Mathf.Abs(fromCenter.x) >= Mathf.Abs(fromCenter.y))
            {
                outward = new Vector2Int(fromCenter.x >= 0f ? 1 : -1, 0);
            }
            else
            {
                outward = new Vector2Int(0, fromCenter.y >= 0f ? 1 : -1);
            }

            if (random.Chance(inwardChance))
            {
                outward = -outward;
            }

            if (outward == Vector2Int.zero)
            {
                outward = ExpansionDirections[random.NextInt(ExpansionDirections.Length)];
            }

            return outward;
        }

        private static int CountFreeNeighbors(Vector2Int center, Dictionary<Vector2Int, int> occupied, int radiusLimit)
        {
            int count = 0;
            for (int i = 0; i < ExpansionDirections.Length; i++)
            {
                Vector2Int p = center + ExpansionDirections[i];
                if (Mathf.Abs(p.x) > radiusLimit || Mathf.Abs(p.y) > radiusLimit)
                {
                    continue;
                }

                if (!occupied.ContainsKey(p))
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountOccupiedNeighbors(Vector2Int center, Dictionary<Vector2Int, int> occupied)
        {
            int count = 0;
            for (int i = 0; i < ExpansionDirections.Length; i++)
            {
                if (occupied.ContainsKey(center + ExpansionDirections[i]))
                {
                    count++;
                }
            }

            return count;
        }

        private static void AddStructuredSecondaryLoops(
            RoomGraph graph,
            Dictionary<Vector2Int, int> occupied,
            HashSet<int> primaryRingSet,
            int requestedLoops,
            int structuredSecondaryLoopCount,
            float secondaryLoopChance,
            int maxNodeDegree,
            ref DeterministicRandom random)
        {
            int nonRingCount = Mathf.Max(0, graph.NodeCount - (primaryRingSet != null ? primaryRingSet.Count : 0));
            int probabilisticLoops = Mathf.RoundToInt(nonRingCount * secondaryLoopChance * 0.22f);
            int loopBudget = Mathf.Max(0, requestedLoops) + Mathf.Max(0, structuredSecondaryLoopCount) + Mathf.Max(0, probabilisticLoops);
            loopBudget = Mathf.Clamp(loopBudget, 0, Mathf.Max(0, graph.NodeCount / 2));

            if (loopBudget <= 0)
            {
                return;
            }

            List<LoopEdgeCandidate> candidates = CollectSecondaryLoopCandidates(
                graph,
                occupied,
                primaryRingSet,
                maxNodeDegree,
                ref random);

            candidates.Sort((a, b) =>
            {
                int scoreCompare = b.Score.CompareTo(a.Score);
                if (scoreCompare != 0)
                {
                    return scoreCompare;
                }

                return a.TieBreaker.CompareTo(b.TieBreaker);
            });

            int added = 0;
            for (int i = 0; i < candidates.Count && added < loopBudget; i++)
            {
                LoopEdgeCandidate candidate = candidates[i];
                if (graph.GetDegree(candidate.NodeA) >= maxNodeDegree || graph.GetDegree(candidate.NodeB) >= maxNodeDegree)
                {
                    continue;
                }

                if (graph.HasEdge(candidate.NodeA, candidate.NodeB))
                {
                    continue;
                }

                if (graph.AddEdge(candidate.NodeA, candidate.NodeB, true))
                {
                    added++;
                }
            }
        }

        private static List<LoopEdgeCandidate> CollectSecondaryLoopCandidates(
            RoomGraph graph,
            Dictionary<Vector2Int, int> occupied,
            HashSet<int> primaryRingSet,
            int maxNodeDegree,
            ref DeterministicRandom random)
        {
            List<LoopEdgeCandidate> candidates = new List<LoopEdgeCandidate>();

            for (int i = 0; i < graph.NodeCount; i++)
            {
                RoomGraphNode node = graph.GetNode(i);
                if (node.Id == 0)
                {
                    continue;
                }

                if (graph.GetDegree(node.Id) >= maxNodeDegree)
                {
                    continue;
                }

                for (int d = 0; d < LoopScanDirections.Length; d++)
                {
                    Vector2Int neighborPos = node.GridPosition + LoopScanDirections[d];
                    if (!occupied.TryGetValue(neighborPos, out int neighborId))
                    {
                        continue;
                    }

                    if (neighborId == 0 || node.Id >= neighborId)
                    {
                        continue;
                    }

                    if (graph.GetDegree(neighborId) >= maxNodeDegree || graph.HasEdge(node.Id, neighborId))
                    {
                        continue;
                    }

                    bool nodeOnRing = primaryRingSet != null && primaryRingSet.Contains(node.Id);
                    bool neighborOnRing = primaryRingSet != null && primaryRingSet.Contains(neighborId);
                    if (nodeOnRing && neighborOnRing)
                    {
                        continue;
                    }

                    if (!TryGetShortestPathLength(graph, node.Id, neighborId, 8, out int shortestPath))
                    {
                        continue;
                    }

                    if (shortestPath < 2 || shortestPath > 7)
                    {
                        continue;
                    }

                    int score = random.NextInt(0, 40);
                    score += shortestPath * 22;

                    if (shortestPath >= 3 && shortestPath <= 5)
                    {
                        score += 40;
                    }

                    if (!nodeOnRing && !neighborOnRing)
                    {
                        score += 28;
                    }

                    if (graph.GetDegree(node.Id) == 1 || graph.GetDegree(neighborId) == 1)
                    {
                        score += 16;
                    }

                    score += (maxNodeDegree - graph.GetDegree(node.Id)) * 6;
                    score += (maxNodeDegree - graph.GetDegree(neighborId)) * 6;

                    candidates.Add(new LoopEdgeCandidate
                    {
                        NodeA = node.Id,
                        NodeB = neighborId,
                        Score = score,
                        TieBreaker = random.NextInt(1, int.MaxValue)
                    });
                }
            }

            return candidates;
        }

        private static bool TryGetShortestPathLength(RoomGraph graph, int startNodeId, int targetNodeId, int maxDepth, out int shortestLength)
        {
            shortestLength = -1;
            if (startNodeId == targetNodeId)
            {
                shortestLength = 0;
                return true;
            }

            int[] dist = new int[graph.NodeCount];
            for (int i = 0; i < dist.Length; i++)
            {
                dist[i] = -1;
            }

            Queue<int> queue = new Queue<int>();
            queue.Enqueue(startNodeId);
            dist[startNodeId] = 0;

            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                int baseDistance = dist[current];

                if (baseDistance >= maxDepth)
                {
                    continue;
                }

                List<int> neighbors = graph.GetNeighborsSorted(current);
                for (int i = 0; i < neighbors.Count; i++)
                {
                    int next = neighbors[i];
                    if (dist[next] >= 0)
                    {
                        continue;
                    }

                    int nextDistance = baseDistance + 1;
                    dist[next] = nextDistance;

                    if (next == targetNodeId)
                    {
                        shortestLength = nextDistance;
                        return true;
                    }

                    queue.Enqueue(next);
                }
            }

            return false;
        }
    }
}
