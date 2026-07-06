using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Matrix.PCG
{
    /// <summary>
    /// Describes one step of a stepwise stitch: a single room was just connected.
    /// </summary>
    public struct StitchStep
    {
        public int AnchorNodeId;
        public int PlacedNodeId;
        public PcgRoomConnection Connection;
    }

    public static class RoomStitcher
    {
        private const float MinBoundsSize = 0.01f;
        private const float StaticResolveMaxSocketDistance = 2f;
        private const float StaticResolveMinFacingDot = 0.25f;
        private const float MaxAllowedSeamOverlapArea = 0.35f;
        private const float ConnectorColliderSeamGap = 0.05f;
        private const float MaxConnectorColliderSeparationPush = 4f;
        private const float MinConnectorColliderSeparationPush = 0.001f;

        private struct ConnectionCandidate
        {
            public PcgConnectorMarker AnchorConnector;
            public PcgConnectorMarker TargetConnector;
            public bool AnchorOutgoing;
            public bool TargetOutgoing;
            public int Score;
            public int TieBreaker;
        }

        public static bool Stitch(
            RoomGraph graph,
            Dictionary<int, PcgPlacedRoom> placedByNode,
            bool closeUnusedExits,
            ref DeterministicRandom random,
            List<PcgRoomConnection> connectionOutput,
            List<PcgClosedDoorRecord> closedDoorOutput,
            List<PcgStitchFailureRecord> failureOutput = null)
        {
            if (graph == null || placedByNode == null)
            {
                return false;
            }

            connectionOutput?.Clear();
            closedDoorOutput?.Clear();
            failureOutput?.Clear();

            int startNodeId = FindStartNodeId(placedByNode);
            if (startNodeId < 0 || !placedByNode.TryGetValue(startNodeId, out PcgPlacedRoom startRoom) || startRoom.RoomInstance == null)
            {
                Debug.LogError("[PCG] Stitch failed: start room is missing.");
                return false;
            }

            HashSet<int> physicallyPlaced = new HashSet<int>();
            Queue<int> queue = new Queue<int>();
            physicallyPlaced.Add(startNodeId);
            startRoom.IsPhysicallyPlaced = true;
            queue.Enqueue(startNodeId);

            Dictionary<ulong, PcgRoomConnection> resolvedConnectionMap = new Dictionary<ulong, PcgRoomConnection>();

            bool treePlacementSuccess = true;

            while (queue.Count > 0)
            {
                int anchorNodeId = queue.Dequeue();
                if (!placedByNode.TryGetValue(anchorNodeId, out PcgPlacedRoom anchorRoom) || anchorRoom.RoomInstance == null)
                {
                    continue;
                }

                List<int> neighbors = graph.GetNeighborsSorted(anchorNodeId);
                for (int i = 0; i < neighbors.Count; i++)
                {
                    int neighborNodeId = neighbors[i];
                    RoomGraphEdge treeEdge = FindEdge(graph, anchorNodeId, neighborNodeId);
                    if (treeEdge.IsLoopEdge)
                    {
                        // Loop edges are resolved in static pass; they should not drive tree placement.
                        continue;
                    }

                    ulong edgeKey = MakeEdgeKey(anchorNodeId, neighborNodeId);

                    if (!placedByNode.TryGetValue(neighborNodeId, out PcgPlacedRoom neighborRoom) || neighborRoom.RoomInstance == null)
                    {
                        treePlacementSuccess = false;
                        continue;
                    }

                    if (physicallyPlaced.Contains(neighborNodeId))
                    {
                        continue;
                    }

                    Vector3 desiredForward = BuildDesiredForward(anchorRoom, neighborRoom);

                    PcgConnectorMarker anchorSocket;
                    PcgConnectorMarker targetSocket;
                    bool anchorOutgoing;
                    bool targetOutgoing;
                    string placeFailReason;
                    bool placed = TryPlaceNeighborBySocket(
                        anchorRoom,
                        neighborRoom,
                        desiredForward,
                        placedByNode,
                        physicallyPlaced,
                        ref random,
                        out anchorSocket,
                        out targetSocket,
                        out anchorOutgoing,
                        out targetOutgoing,
                        out placeFailReason);

                    if (placed)
                    {
                        anchorRoom.UsedConnectors.Add(anchorSocket);
                        neighborRoom.UsedConnectors.Add(targetSocket);
                        neighborRoom.IsPhysicallyPlaced = true;
                        physicallyPlaced.Add(neighborNodeId);
                        queue.Enqueue(neighborNodeId);

                        resolvedConnectionMap[edgeKey] = CreateConnectionRecord(
                            treeEdge,
                            anchorNodeId,
                            anchorSocket,
                            targetSocket,
                            anchorOutgoing,
                            targetOutgoing,
                            true);
                    }
                    else
                    {
                        // Do not fail immediately here: target node may still be placeable from another already-placed neighbor.
                        AddFailureRecord(failureOutput, "TreePlacement", anchorNodeId, neighborNodeId, placeFailReason);
                        Debug.LogWarning($"[PCG] Stitch edge deferred. anchorNode={anchorNodeId}, targetNode={neighborNodeId}, reason={placeFailReason}");
                    }
                }
            }

            int recoveryPass = 0;
            int maxRecoveryPasses = Mathf.Max(4, graph.NodeCount);
            while (recoveryPass < maxRecoveryPasses)
            {
                recoveryPass++;
                bool recovered = TryRecoverDeferredTreePlacements(
                    graph,
                    placedByNode,
                    physicallyPlaced,
                    resolvedConnectionMap,
                    failureOutput,
                    ref random);

                if (!recovered)
                {
                    break;
                }
            }

            List<int> unplacedNodeIds = new List<int>();
            for (int i = 0; i < graph.Nodes.Count; i++)
            {
                int nodeId = graph.Nodes[i].Id;
                if (!physicallyPlaced.Contains(nodeId) && placedByNode.ContainsKey(nodeId))
                {
                    treePlacementSuccess = false;
                    unplacedNodeIds.Add(nodeId);
                }
            }

            if (unplacedNodeIds.Count > 0)
            {
                Debug.LogError($"[PCG] Stitch unresolved placed nodes: {string.Join(",", unplacedNodeIds)}");
            }

            for (int i = 0; i < graph.Edges.Count; i++)
            {
                RoomGraphEdge edge = graph.Edges[i];
                ulong edgeKey = MakeEdgeKey(edge.NodeA, edge.NodeB);

                if (resolvedConnectionMap.ContainsKey(edgeKey))
                {
                    continue;
                }

                bool bothPlaced = physicallyPlaced.Contains(edge.NodeA) && physicallyPlaced.Contains(edge.NodeB);
                if (bothPlaced &&
                    placedByNode.TryGetValue(edge.NodeA, out PcgPlacedRoom roomA) &&
                    placedByNode.TryGetValue(edge.NodeB, out PcgPlacedRoom roomB) &&
                    roomA.RoomInstance != null &&
                    roomB.RoomInstance != null)
                {
                    Vector3 desiredForward = BuildDesiredForward(roomA, roomB);

                    PcgConnectorMarker from;
                    PcgConnectorMarker to;
                    bool fromOutgoing;
                    bool toOutgoing;
                    bool resolved = TryResolveStaticConnection(roomA, roomB, desiredForward, ref random, out from, out to, out fromOutgoing, out toOutgoing);
                    if (resolved)
                    {
                        roomA.UsedConnectors.Add(from);
                        roomB.UsedConnectors.Add(to);
                    }

                    resolvedConnectionMap[edgeKey] = new PcgRoomConnection
                    {
                        NodeA = edge.NodeA,
                        NodeB = edge.NodeB,
                        IsLoopEdge = edge.IsLoopEdge,
                        IsResolved = resolved,
                        ConnectorFrom = resolved ? from : null,
                        ConnectorTo = resolved ? to : null,
                        ConnectorFromOutgoing = resolved && fromOutgoing,
                        ConnectorToOutgoing = resolved && toOutgoing
                    };
                }
                else
                {
                    resolvedConnectionMap[edgeKey] = new PcgRoomConnection
                    {
                        NodeA = edge.NodeA,
                        NodeB = edge.NodeB,
                        IsLoopEdge = edge.IsLoopEdge,
                        IsResolved = false,
                        ConnectorFrom = null,
                        ConnectorTo = null,
                        ConnectorFromOutgoing = false,
                        ConnectorToOutgoing = false
                    };
                }
            }

            if (connectionOutput != null)
            {
                for (int i = 0; i < graph.Edges.Count; i++)
                {
                    RoomGraphEdge edge = graph.Edges[i];
                    ulong edgeKey = MakeEdgeKey(edge.NodeA, edge.NodeB);
                    if (resolvedConnectionMap.TryGetValue(edgeKey, out PcgRoomConnection record))
                    {
                        connectionOutput.Add(record);
                    }
                }
            }

            if (closeUnusedExits)
            {
                ApplyUnusedExitClosing(placedByNode, closedDoorOutput);
            }

            return treePlacementSuccess;
        }

        /// <summary>
        /// Stepwise variant of Stitch that yields after each successful room placement.
        /// Designed for test visualization: callers drive the enumerator and reveal rooms as they are placed.
        /// Produces the same deterministic result as <see cref="Stitch"/>.
        /// </summary>
        public static IEnumerator StitchStepwise(
            RoomGraph graph,
            Dictionary<int, PcgPlacedRoom> placedByNode,
            bool closeUnusedExits,
            DeterministicRandom random,
            List<PcgRoomConnection> connectionOutput,
            List<PcgClosedDoorRecord> closedDoorOutput,
            List<PcgStitchFailureRecord> failureOutput = null)
        {
            if (graph == null || placedByNode == null)
            {
                yield break;
            }

            // Iterators cannot have ref parameters, so we copy the struct and mutate a local.
            DeterministicRandom mutableRandom = random;

            connectionOutput?.Clear();
            closedDoorOutput?.Clear();
            failureOutput?.Clear();

            int startNodeId = FindStartNodeId(placedByNode);
            if (startNodeId < 0 || !placedByNode.TryGetValue(startNodeId, out PcgPlacedRoom startRoom) || startRoom.RoomInstance == null)
            {
                Debug.LogError("[PCG] StitchStepwise failed: start room is missing.");
                yield break;
            }

            HashSet<int> physicallyPlaced = new HashSet<int>();
            Queue<int> queue = new Queue<int>();
            physicallyPlaced.Add(startNodeId);
            startRoom.IsPhysicallyPlaced = true;
            queue.Enqueue(startNodeId);

            Dictionary<ulong, PcgRoomConnection> resolvedConnectionMap = new Dictionary<ulong, PcgRoomConnection>();

            bool treePlacementSuccess = true;

            // --- Tree placement (BFS from start, yields after each successful placement) ---
            while (queue.Count > 0)
            {
                int anchorNodeId = queue.Dequeue();
                if (!placedByNode.TryGetValue(anchorNodeId, out PcgPlacedRoom anchorRoom) || anchorRoom.RoomInstance == null)
                {
                    continue;
                }

                List<int> neighbors = graph.GetNeighborsSorted(anchorNodeId);
                for (int i = 0; i < neighbors.Count; i++)
                {
                    int neighborNodeId = neighbors[i];
                    RoomGraphEdge treeEdge = FindEdge(graph, anchorNodeId, neighborNodeId);
                    if (treeEdge.IsLoopEdge)
                    {
                        continue;
                    }

                    ulong edgeKey = MakeEdgeKey(anchorNodeId, neighborNodeId);

                    if (!placedByNode.TryGetValue(neighborNodeId, out PcgPlacedRoom neighborRoom) || neighborRoom.RoomInstance == null)
                    {
                        treePlacementSuccess = false;
                        continue;
                    }

                    if (physicallyPlaced.Contains(neighborNodeId))
                    {
                        continue;
                    }

                    Vector3 desiredForward = BuildDesiredForward(anchorRoom, neighborRoom);

                    PcgConnectorMarker anchorSocket;
                    PcgConnectorMarker targetSocket;
                    bool anchorOutgoing;
                    bool targetOutgoing;
                    string placeFailReason;
                    bool placed = TryPlaceNeighborBySocket(
                        anchorRoom,
                        neighborRoom,
                        desiredForward,
                        placedByNode,
                        physicallyPlaced,
                        ref mutableRandom,
                        out anchorSocket,
                        out targetSocket,
                        out anchorOutgoing,
                        out targetOutgoing,
                        out placeFailReason);

                    if (placed)
                    {
                        anchorRoom.UsedConnectors.Add(anchorSocket);
                        neighborRoom.UsedConnectors.Add(targetSocket);
                        neighborRoom.IsPhysicallyPlaced = true;
                        physicallyPlaced.Add(neighborNodeId);
                        queue.Enqueue(neighborNodeId);

                        PcgRoomConnection record = CreateConnectionRecord(
                            treeEdge,
                            anchorNodeId,
                            anchorSocket,
                            targetSocket,
                            anchorOutgoing,
                            targetOutgoing,
                            true);

                        resolvedConnectionMap[edgeKey] = record;

                        // Yield after each successful placement so caller can reveal the room.
                        yield return new StitchStep
                        {
                            AnchorNodeId = anchorNodeId,
                            PlacedNodeId = neighborNodeId,
                            Connection = record
                        };
                    }
                    else
                    {
                        AddFailureRecord(failureOutput, "TreePlacement", anchorNodeId, neighborNodeId, placeFailReason);
                        Debug.LogWarning($"[PCG] StitchStepwise edge deferred. anchorNode={anchorNodeId}, targetNode={neighborNodeId}, reason={placeFailReason}");
                    }
                }
            }

            // --- Recovery pass (yields after each recovered placement) ---
            int recoveryPass = 0;
            int maxRecoveryPasses = Mathf.Max(4, graph.NodeCount);
            while (recoveryPass < maxRecoveryPasses)
            {
                recoveryPass++;
                bool recovered = false;

                List<int> anchorNodeIds = new List<int>();
                for (int i = 0; i < graph.Nodes.Count; i++)
                {
                    int nodeId = graph.Nodes[i].Id;
                    if (physicallyPlaced.Contains(nodeId))
                    {
                        anchorNodeIds.Add(nodeId);
                    }
                }

                for (int a = 0; a < anchorNodeIds.Count; a++)
                {
                    int anchorNodeId = anchorNodeIds[a];
                    if (!placedByNode.TryGetValue(anchorNodeId, out PcgPlacedRoom anchorRoom) || anchorRoom.RoomInstance == null)
                    {
                        continue;
                    }

                    List<int> neighbors = graph.GetNeighborsSorted(anchorNodeId);
                    for (int i = 0; i < neighbors.Count; i++)
                    {
                        int neighborNodeId = neighbors[i];
                        if (physicallyPlaced.Contains(neighborNodeId))
                        {
                            continue;
                        }

                        RoomGraphEdge treeEdge = FindEdge(graph, anchorNodeId, neighborNodeId);
                        if (treeEdge.IsLoopEdge)
                        {
                            continue;
                        }

                        ulong edgeKey = MakeEdgeKey(anchorNodeId, neighborNodeId);
                        if (resolvedConnectionMap.ContainsKey(edgeKey))
                        {
                            continue;
                        }

                        if (!placedByNode.TryGetValue(neighborNodeId, out PcgPlacedRoom neighborRoom) || neighborRoom.RoomInstance == null)
                        {
                            continue;
                        }

                        Vector3 desiredForward = BuildDesiredForward(anchorRoom, neighborRoom);

                        PcgConnectorMarker anchorSocket;
                        PcgConnectorMarker targetSocket;
                        bool anchorOutgoing;
                        bool targetOutgoing;
                        string placeFailReason;
                        bool placed = TryPlaceNeighborBySocket(
                            anchorRoom,
                            neighborRoom,
                            desiredForward,
                            placedByNode,
                            physicallyPlaced,
                            ref mutableRandom,
                            out anchorSocket,
                            out targetSocket,
                            out anchorOutgoing,
                            out targetOutgoing,
                            out placeFailReason);

                        if (!placed)
                        {
                            AddFailureRecord(failureOutput, "Recovery", anchorNodeId, neighborNodeId, placeFailReason);
                            continue;
                        }

                        anchorRoom.UsedConnectors.Add(anchorSocket);
                        neighborRoom.UsedConnectors.Add(targetSocket);
                        neighborRoom.IsPhysicallyPlaced = true;
                        physicallyPlaced.Add(neighborNodeId);
                        recovered = true;

                        PcgRoomConnection record = CreateConnectionRecord(
                            treeEdge,
                            anchorNodeId,
                            anchorSocket,
                            targetSocket,
                            anchorOutgoing,
                            targetOutgoing,
                            true);

                        resolvedConnectionMap[edgeKey] = record;

                        yield return new StitchStep
                        {
                            AnchorNodeId = anchorNodeId,
                            PlacedNodeId = neighborNodeId,
                            Connection = record
                        };
                    }
                }

                if (!recovered)
                {
                    break;
                }
            }

            // --- Report unplaced nodes ---
            List<int> unplacedNodeIds = new List<int>();
            for (int i = 0; i < graph.Nodes.Count; i++)
            {
                int nodeId = graph.Nodes[i].Id;
                if (!physicallyPlaced.Contains(nodeId) && placedByNode.ContainsKey(nodeId))
                {
                    treePlacementSuccess = false;
                    unplacedNodeIds.Add(nodeId);
                }
            }

            if (unplacedNodeIds.Count > 0)
            {
                Debug.LogError($"[PCG] StitchStepwise unresolved placed nodes: {string.Join(",", unplacedNodeIds)}");
            }

            // --- Static connections (loop edges, one-shot, no yield) ---
            for (int i = 0; i < graph.Edges.Count; i++)
            {
                RoomGraphEdge edge = graph.Edges[i];
                ulong edgeKey = MakeEdgeKey(edge.NodeA, edge.NodeB);

                if (resolvedConnectionMap.ContainsKey(edgeKey))
                {
                    continue;
                }

                bool bothPlaced = physicallyPlaced.Contains(edge.NodeA) && physicallyPlaced.Contains(edge.NodeB);
                if (bothPlaced &&
                    placedByNode.TryGetValue(edge.NodeA, out PcgPlacedRoom roomA) &&
                    placedByNode.TryGetValue(edge.NodeB, out PcgPlacedRoom roomB) &&
                    roomA.RoomInstance != null &&
                    roomB.RoomInstance != null)
                {
                    Vector3 desiredForward = BuildDesiredForward(roomA, roomB);

                    PcgConnectorMarker from;
                    PcgConnectorMarker to;
                    bool fromOutgoing;
                    bool toOutgoing;
                    bool resolved = TryResolveStaticConnection(roomA, roomB, desiredForward, ref mutableRandom, out from, out to, out fromOutgoing, out toOutgoing);
                    if (resolved)
                    {
                        roomA.UsedConnectors.Add(from);
                        roomB.UsedConnectors.Add(to);
                    }

                    resolvedConnectionMap[edgeKey] = new PcgRoomConnection
                    {
                        NodeA = edge.NodeA,
                        NodeB = edge.NodeB,
                        IsLoopEdge = edge.IsLoopEdge,
                        IsResolved = resolved,
                        ConnectorFrom = resolved ? from : null,
                        ConnectorTo = resolved ? to : null,
                        ConnectorFromOutgoing = resolved && fromOutgoing,
                        ConnectorToOutgoing = resolved && toOutgoing
                    };
                }
                else
                {
                    resolvedConnectionMap[edgeKey] = new PcgRoomConnection
                    {
                        NodeA = edge.NodeA,
                        NodeB = edge.NodeB,
                        IsLoopEdge = edge.IsLoopEdge,
                        IsResolved = false,
                        ConnectorFrom = null,
                        ConnectorTo = null,
                        ConnectorFromOutgoing = false,
                        ConnectorToOutgoing = false
                    };
                }
            }

            // --- Fill output lists ---
            if (connectionOutput != null)
            {
                for (int i = 0; i < graph.Edges.Count; i++)
                {
                    RoomGraphEdge edge = graph.Edges[i];
                    ulong edgeKey = MakeEdgeKey(edge.NodeA, edge.NodeB);
                    if (resolvedConnectionMap.TryGetValue(edgeKey, out PcgRoomConnection record))
                    {
                        connectionOutput.Add(record);
                    }
                }
            }

            if (closeUnusedExits)
            {
                ApplyUnusedExitClosing(placedByNode, closedDoorOutput);
            }

            // Let callers check treePlacementSuccess via LastResult or by inspecting connectionOutput.
            if (!treePlacementSuccess)
            {
                Debug.LogWarning("[PCG] StitchStepwise: some nodes were not placed during tree placement.");
            }
        }

        private static bool TryRecoverDeferredTreePlacements(
            RoomGraph graph,
            Dictionary<int, PcgPlacedRoom> placedByNode,
            HashSet<int> physicallyPlaced,
            Dictionary<ulong, PcgRoomConnection> resolvedConnectionMap,
            List<PcgStitchFailureRecord> failureOutput,
            ref DeterministicRandom random)
        {
            bool anyRecovered = false;

            List<int> anchorNodeIds = new List<int>();
            for (int i = 0; i < graph.Nodes.Count; i++)
            {
                int nodeId = graph.Nodes[i].Id;
                if (physicallyPlaced.Contains(nodeId))
                {
                    anchorNodeIds.Add(nodeId);
                }
            }

            for (int a = 0; a < anchorNodeIds.Count; a++)
            {
                int anchorNodeId = anchorNodeIds[a];
                if (!placedByNode.TryGetValue(anchorNodeId, out PcgPlacedRoom anchorRoom) || anchorRoom.RoomInstance == null)
                {
                    continue;
                }

                List<int> neighbors = graph.GetNeighborsSorted(anchorNodeId);
                for (int i = 0; i < neighbors.Count; i++)
                {
                    int neighborNodeId = neighbors[i];
                    if (physicallyPlaced.Contains(neighborNodeId))
                    {
                        continue;
                    }

                    RoomGraphEdge treeEdge = FindEdge(graph, anchorNodeId, neighborNodeId);
                    if (treeEdge.IsLoopEdge)
                    {
                        continue;
                    }

                    ulong edgeKey = MakeEdgeKey(anchorNodeId, neighborNodeId);
                    if (resolvedConnectionMap.ContainsKey(edgeKey))
                    {
                        continue;
                    }

                    if (!placedByNode.TryGetValue(neighborNodeId, out PcgPlacedRoom neighborRoom) || neighborRoom.RoomInstance == null)
                    {
                        continue;
                    }

                    Vector3 desiredForward = BuildDesiredForward(anchorRoom, neighborRoom);

                    PcgConnectorMarker anchorSocket;
                    PcgConnectorMarker targetSocket;
                    bool anchorOutgoing;
                    bool targetOutgoing;
                    string placeFailReason;
                    bool placed = TryPlaceNeighborBySocket(
                        anchorRoom,
                        neighborRoom,
                        desiredForward,
                        placedByNode,
                        physicallyPlaced,
                        ref random,
                        out anchorSocket,
                        out targetSocket,
                        out anchorOutgoing,
                        out targetOutgoing,
                        out placeFailReason);

                    if (!placed)
                    {
                        AddFailureRecord(failureOutput, "Recovery", anchorNodeId, neighborNodeId, placeFailReason);
                        continue;
                    }

                    anchorRoom.UsedConnectors.Add(anchorSocket);
                    neighborRoom.UsedConnectors.Add(targetSocket);
                    neighborRoom.IsPhysicallyPlaced = true;
                    physicallyPlaced.Add(neighborNodeId);
                    anyRecovered = true;

                    resolvedConnectionMap[edgeKey] = CreateConnectionRecord(
                        treeEdge,
                        anchorNodeId,
                        anchorSocket,
                        targetSocket,
                        anchorOutgoing,
                        targetOutgoing,
                        true);
                }
            }

            return anyRecovered;
        }

        private static void AddFailureRecord(
            List<PcgStitchFailureRecord> failureOutput,
            string phase,
            int anchorNodeId,
            int targetNodeId,
            string reason)
        {
            if (failureOutput == null)
            {
                return;
            }

            failureOutput.Add(new PcgStitchFailureRecord
            {
                AnchorNodeId = anchorNodeId,
                TargetNodeId = targetNodeId,
                Phase = phase,
                Reason = reason ?? string.Empty
            });
        }

        private static int FindStartNodeId(Dictionary<int, PcgPlacedRoom> placedByNode)
        {
            int startNodeId = -1;

            foreach (KeyValuePair<int, PcgPlacedRoom> pair in placedByNode)
            {
                if (pair.Value == null || pair.Value.RoomInstance == null)
                {
                    continue;
                }

                if (pair.Value.Role == RoomRole.Start)
                {
                    if (startNodeId < 0 || pair.Key < startNodeId)
                    {
                        startNodeId = pair.Key;
                    }
                }
            }

            if (startNodeId >= 0)
            {
                return startNodeId;
            }

            foreach (KeyValuePair<int, PcgPlacedRoom> pair in placedByNode)
            {
                if (pair.Value != null && pair.Value.RoomInstance != null)
                {
                    if (startNodeId < 0 || pair.Key < startNodeId)
                    {
                        startNodeId = pair.Key;
                    }
                }
            }

            return startNodeId;
        }

        private static bool TryPlaceNeighborBySocket(
            PcgPlacedRoom anchorRoom,
            PcgPlacedRoom targetRoom,
            Vector3 desiredForward,
            Dictionary<int, PcgPlacedRoom> placedByNode,
            HashSet<int> physicallyPlaced,
            ref DeterministicRandom random,
            out PcgConnectorMarker anchorSocket,
            out PcgConnectorMarker targetSocket,
            out bool anchorOutgoing,
            out bool targetOutgoing,
            out string failReason)
        {
            anchorSocket = null;
            targetSocket = null;
            anchorOutgoing = false;
            targetOutgoing = false;
            failReason = string.Empty;

            if (anchorRoom == null || targetRoom == null || anchorRoom.RoomInstance == null || targetRoom.RoomInstance == null)
            {
                failReason = "room instance missing";
                return false;
            }

            List<ConnectionCandidate> candidates = BuildPlacementCandidates(anchorRoom, targetRoom, desiredForward, ref random);
            if (candidates.Count == 0)
            {
                failReason = "no compatible socket pair";
                return false;
            }

            Transform targetTransform = targetRoom.RoomInstance.transform;
            Vector3 originalPos = targetTransform.position;
            Quaternion originalRot = targetTransform.rotation;

            int overlapRejectedCount = 0;
            for (int i = 0; i < candidates.Count; i++)
            {
                ConnectionCandidate candidate = candidates[i];
                AlignTargetRoomBySocket(candidate.AnchorConnector, candidate.TargetConnector, candidate.AnchorOutgoing, candidate.TargetOutgoing, targetTransform);

                if (!HasBlockingOverlap(targetRoom, placedByNode, physicallyPlaced, anchorRoom.NodeId))
                {
                    anchorSocket = candidate.AnchorConnector;
                    targetSocket = candidate.TargetConnector;
                    anchorOutgoing = candidate.AnchorOutgoing;
                    targetOutgoing = candidate.TargetOutgoing;
                    return true;
                }

                overlapRejectedCount++;
                targetTransform.SetPositionAndRotation(originalPos, originalRot);
            }

            targetTransform.SetPositionAndRotation(originalPos, originalRot);
            failReason = $"all candidates rejected by overlap (candidates={candidates.Count}, overlapRejected={overlapRejectedCount})";
            return false;
        }
        private static List<ConnectionCandidate> BuildPlacementCandidates(
            PcgPlacedRoom anchorRoom,
            PcgPlacedRoom targetRoom,
            Vector3 desiredForward,
            ref DeterministicRandom random)
        {
            List<ConnectionCandidate> candidates = new List<ConnectionCandidate>();
            CollectCandidates(anchorRoom, targetRoom, true, false, desiredForward, ref random, candidates);
            CollectCandidates(anchorRoom, targetRoom, false, true, desiredForward, ref random, candidates);

            candidates.Sort((a, b) =>
            {
                int scoreCompare = b.Score.CompareTo(a.Score);
                if (scoreCompare != 0)
                {
                    return scoreCompare;
                }

                return a.TieBreaker.CompareTo(b.TieBreaker);
            });

            return candidates;
        }

        private static void CollectCandidates(
            PcgPlacedRoom anchorRoom,
            PcgPlacedRoom targetRoom,
            bool anchorOutgoing,
            bool targetOutgoing,
            Vector3 desiredForward,
            ref DeterministicRandom random,
            List<ConnectionCandidate> output)
        {
            List<PcgConnectorMarker> anchorConnectors = anchorRoom.RoomInstance.GetAvailableConnectors(anchorOutgoing, anchorRoom.UsedConnectors);
            List<PcgConnectorMarker> targetConnectors = targetRoom.RoomInstance.GetAvailableConnectors(targetOutgoing, targetRoom.UsedConnectors);

            Vector3 desired = FlattenForward(desiredForward);

            for (int i = 0; i < anchorConnectors.Count; i++)
            {
                PcgConnectorMarker anchor = anchorConnectors[i];
                if (anchor == null)
                {
                    continue;
                }

                for (int j = 0; j < targetConnectors.Count; j++)
                {
                    PcgConnectorMarker target = targetConnectors[j];
                    if (target == null)
                    {
                        continue;
                    }

                    if (!anchor.IsSocketCompatible(target))
                    {
                        continue;
                    }

                    Vector3 anchorNormal = anchor.GetSocketNormal(anchorOutgoing);
                    Vector3 targetNormal = target.GetSocketNormal(targetOutgoing);

                    float anchorDot = Vector3.Dot(anchorNormal, desired);
                    float targetDot = Vector3.Dot(targetNormal, -desired);

                    int score = Mathf.RoundToInt(anchorDot * 1000f + targetDot * 700f);

                    output.Add(new ConnectionCandidate
                    {
                        AnchorConnector = anchor,
                        TargetConnector = target,
                        AnchorOutgoing = anchorOutgoing,
                        TargetOutgoing = targetOutgoing,
                        Score = score,
                        TieBreaker = random.NextInt(1, int.MaxValue)
                    });
                }
            }
        }

        private static void AlignTargetRoomBySocket(
            PcgConnectorMarker anchorSocket,
            PcgConnectorMarker targetSocket,
            bool anchorOutgoing,
            bool targetOutgoing,
            Transform targetRoomRoot)
        {
            if (anchorSocket == null || targetSocket == null || targetRoomRoot == null)
            {
                return;
            }

            Vector3 localSocketPos = targetRoomRoot.InverseTransformPoint(targetSocket.GetSocketBaseWorldPoint());
            Vector3 targetWorldNormalBefore = targetSocket.GetSocketNormal(targetOutgoing);
            Vector3 localSocketNormal = targetRoomRoot.InverseTransformDirection(targetWorldNormalBefore);

            Vector3 fromNormal = targetWorldNormalBefore;
            Vector3 toNormal = -anchorSocket.GetSocketNormal(anchorOutgoing);

            float yaw = 0f;
            if (fromNormal.sqrMagnitude > 0.0001f && toNormal.sqrMagnitude > 0.0001f)
            {
                yaw = Vector3.SignedAngle(fromNormal, toNormal, Vector3.up);
            }

            Quaternion newRotation = Quaternion.AngleAxis(yaw, Vector3.up) * targetRoomRoot.rotation;

            Vector3 anchorEffectivePoint = anchorSocket.GetSocketWorldPoint(anchorOutgoing);
            Vector3 targetNormalAfter = newRotation * localSocketNormal;

            Vector3 newPosition = anchorEffectivePoint - (newRotation * localSocketPos) - targetNormalAfter * targetSocket.SocketOffset;
            targetRoomRoot.SetPositionAndRotation(newPosition, newRotation);

            ApplyConnectorColliderSeparation(anchorSocket, targetSocket, anchorOutgoing, targetRoomRoot);
        }

        private static void ApplyConnectorColliderSeparation(
            PcgConnectorMarker anchorSocket,
            PcgConnectorMarker targetSocket,
            bool anchorOutgoing,
            Transform targetRoomRoot)
        {
            if (anchorSocket == null || targetSocket == null || targetRoomRoot == null)
            {
                return;
            }

            Vector3 anchorNormal = FlattenForward(anchorSocket.GetSocketNormal(anchorOutgoing));
            if (anchorNormal.sqrMagnitude < 0.0001f)
            {
                return;
            }

            if (!anchorSocket.TryGetSocketColliderOutwardDistance(anchorNormal, out float anchorDepth))
            {
                return;
            }

            if (!targetSocket.TryGetSocketColliderOutwardDistance(-anchorNormal, out float targetDepth))
            {
                return;
            }

            float currentBaseSeparation = Vector3.Dot(
                targetSocket.GetSocketBaseWorldPoint() - anchorSocket.GetSocketBaseWorldPoint(),
                anchorNormal);

            float desiredBaseSeparation = anchorDepth + targetDepth + ConnectorColliderSeamGap;
            float pushDistance = desiredBaseSeparation - currentBaseSeparation;
            if (pushDistance <= MinConnectorColliderSeparationPush)
            {
                return;
            }

            pushDistance = Mathf.Min(pushDistance, MaxConnectorColliderSeparationPush);
            targetRoomRoot.position += anchorNormal * pushDistance;
        }

        private static bool HasBlockingOverlap(
            PcgPlacedRoom candidateRoom,
            Dictionary<int, PcgPlacedRoom> placedByNode,
            HashSet<int> physicallyPlaced,
            int ignoredNodeId)
        {
            if (candidateRoom == null || candidateRoom.RoomInstance == null)
            {
                return true;
            }

            if (!candidateRoom.RoomInstance.TryGetWorldBounds(out Bounds candidateBounds, out float candidatePadding))
            {
                return false;
            }

            Bounds shrunkCandidate = ShrinkBounds(candidateBounds, candidatePadding);

            foreach (int nodeId in physicallyPlaced)
            {
                if (nodeId == ignoredNodeId)
                {
                    continue;
                }

                if (!placedByNode.TryGetValue(nodeId, out PcgPlacedRoom otherRoom))
                {
                    continue;
                }

                if (otherRoom == null || otherRoom.RoomInstance == null)
                {
                    continue;
                }

                if (otherRoom.NodeId == candidateRoom.NodeId)
                {
                    continue;
                }

                if (!otherRoom.RoomInstance.TryGetWorldBounds(out Bounds otherBounds, out float otherPadding))
                {
                    continue;
                }

                Bounds shrunkOther = ShrinkBounds(otherBounds, otherPadding);
                if (shrunkCandidate.Intersects(shrunkOther))
                {
                    float overlapArea = ComputeHorizontalOverlapArea(shrunkCandidate, shrunkOther);
                    if (overlapArea > MaxAllowedSeamOverlapArea)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool TryResolveStaticConnection(
            PcgPlacedRoom roomA,
            PcgPlacedRoom roomB,
            Vector3 desiredForward,
            ref DeterministicRandom random,
            out PcgConnectorMarker from,
            out PcgConnectorMarker to,
            out bool fromOutgoing,
            out bool toOutgoing)
        {
            from = null;
            to = null;
            fromOutgoing = false;
            toOutgoing = false;

            if (roomA == null || roomB == null || roomA.RoomInstance == null || roomB.RoomInstance == null)
            {
                return false;
            }

            List<ConnectionCandidate> candidates = new List<ConnectionCandidate>();
            CollectCandidates(roomA, roomB, true, false, desiredForward, ref random, candidates);
            CollectCandidates(roomA, roomB, false, true, desiredForward, ref random, candidates);

            candidates.Sort((a, b) =>
            {
                int scoreCompare = b.Score.CompareTo(a.Score);
                if (scoreCompare != 0)
                {
                    return scoreCompare;
                }

                return a.TieBreaker.CompareTo(b.TieBreaker);
            });

            for (int i = 0; i < candidates.Count; i++)
            {
                ConnectionCandidate candidate = candidates[i];
                PcgConnectorMarker socketA = candidate.AnchorConnector;
                PcgConnectorMarker socketB = candidate.TargetConnector;

                Vector3 posA = socketA.GetSocketWorldPoint(candidate.AnchorOutgoing);
                Vector3 posB = socketB.GetSocketWorldPoint(candidate.TargetOutgoing);

                float distance = Vector3.Distance(posA, posB);
                if (distance > StaticResolveMaxSocketDistance)
                {
                    continue;
                }

                float facing = Vector3.Dot(socketA.GetSocketNormal(candidate.AnchorOutgoing), -socketB.GetSocketNormal(candidate.TargetOutgoing));
                if (facing < StaticResolveMinFacingDot)
                {
                    continue;
                }

                from = socketA;
                to = socketB;
                fromOutgoing = candidate.AnchorOutgoing;
                toOutgoing = candidate.TargetOutgoing;
                return true;
            }

            return false;
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

        private static PcgRoomConnection CreateConnectionRecord(
            RoomGraphEdge edge,
            int anchorNodeId,
            PcgConnectorMarker anchorSocket,
            PcgConnectorMarker targetSocket,
            bool anchorOutgoing,
            bool targetOutgoing,
            bool resolved)
        {
            if (anchorNodeId == edge.NodeA)
            {
                return new PcgRoomConnection
                {
                    NodeA = edge.NodeA,
                    NodeB = edge.NodeB,
                    IsLoopEdge = edge.IsLoopEdge,
                    IsResolved = resolved,
                    ConnectorFrom = anchorSocket,
                    ConnectorTo = targetSocket,
                    ConnectorFromOutgoing = anchorOutgoing,
                    ConnectorToOutgoing = targetOutgoing
                };
            }

            return new PcgRoomConnection
            {
                NodeA = edge.NodeA,
                NodeB = edge.NodeB,
                IsLoopEdge = edge.IsLoopEdge,
                IsResolved = resolved,
                ConnectorFrom = targetSocket,
                ConnectorTo = anchorSocket,
                ConnectorFromOutgoing = targetOutgoing,
                ConnectorToOutgoing = anchorOutgoing
            };
        }

        private static void ApplyUnusedExitClosing(
            Dictionary<int, PcgPlacedRoom> placedByNode,
            List<PcgClosedDoorRecord> closedDoorOutput)
        {
            foreach (KeyValuePair<int, PcgPlacedRoom> entry in placedByNode)
            {
                int nodeId = entry.Key;
                PcgPlacedRoom room = entry.Value;
                if (room == null || room.RoomInstance == null)
                {
                    continue;
                }

                IReadOnlyList<PcgConnectorMarker> connectors = room.RoomInstance.Connectors;
                for (int i = 0; i < connectors.Count; i++)
                {
                    PcgConnectorMarker connector = connectors[i];
                    if (connector == null)
                    {
                        continue;
                    }

                    bool used = room.UsedConnectors.Contains(connector);
                    if (!used && connector.SupportsOutgoing)
                    {
                        connector.SetPermanentlyClosed(true);
                        closedDoorOutput?.Add(new PcgClosedDoorRecord
                        {
                            NodeId = nodeId,
                            ConnectorId = connector.ConnectorId
                        });
                    }
                    else
                    {
                        connector.SetPermanentlyClosed(false);
                    }
                }
            }
        }

        private static float ComputeHorizontalOverlapArea(Bounds a, Bounds b)
        {
            float minX = Mathf.Max(a.min.x, b.min.x);
            float maxX = Mathf.Min(a.max.x, b.max.x);
            float minZ = Mathf.Max(a.min.z, b.min.z);
            float maxZ = Mathf.Min(a.max.z, b.max.z);

            float dx = maxX - minX;
            float dz = maxZ - minZ;
            if (dx <= 0f || dz <= 0f)
            {
                return 0f;
            }

            return dx * dz;
        }

        private static Bounds ShrinkBounds(Bounds bounds, float padding)
        {
            float p = Mathf.Max(0f, padding);
            Vector3 size = bounds.size - Vector3.one * (2f * p);
            size.x = Mathf.Max(MinBoundsSize, size.x);
            size.y = Mathf.Max(MinBoundsSize, size.y);
            size.z = Mathf.Max(MinBoundsSize, size.z);
            return new Bounds(bounds.center, size);
        }

        private static Vector3 BuildDesiredForward(PcgPlacedRoom fromRoom, PcgPlacedRoom toRoom)
        {
            Vector2Int gridDelta = toRoom.GridPosition - fromRoom.GridPosition;
            Vector3 desired = new Vector3(gridDelta.x, 0f, gridDelta.y);
            return FlattenForward(desired);
        }

        private static Vector3 FlattenForward(Vector3 forward)
        {
            Vector3 result = new Vector3(forward.x, 0f, forward.z);
            if (result.sqrMagnitude < 0.0001f)
            {
                return Vector3.forward;
            }

            return result.normalized;
        }

        private static ulong MakeEdgeKey(int nodeA, int nodeB)
        {
            uint low = (uint)Mathf.Min(nodeA, nodeB);
            uint high = (uint)Mathf.Max(nodeA, nodeB);
            return ((ulong)low << 32) | high;
        }
    }
}
