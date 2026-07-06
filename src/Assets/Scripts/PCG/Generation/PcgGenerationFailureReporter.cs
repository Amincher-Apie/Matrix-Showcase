using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Matrix.PCG
{
    [Serializable]
    public sealed class PcgGenerationFailureReport
    {
        public string SchemaVersion = "pcg-generation-failure@1";
        public string CreatedUtc;
        public string RequestSource;
        public int InputSeed;
        public int FixedSeed;
        public int OriginalTargetRooms;
        public int MinTargetRooms;
        public int MaxGraphAttemptsPerBudget;
        public PcgFailureRequestReport Request;
        public List<PcgFailureAttemptReport> Attempts = new List<PcgFailureAttemptReport>();
    }

    [Serializable]
    public sealed class PcgFailureRequestReport
    {
        public string PrimaryTask;
        public string TaskProvider;
        public int SideTaskCount;
        public List<string> SideTasks = new List<string>();
        public int TargetRoomCount;
        public int ExtraLoopCount;
        public int MaxNodeDegree;
        public float PrimaryRingRatio;
        public float BranchDensity;
        public float SecondaryBranchChance;
        public int MaxPrimaryBranchLength;
        public int MaxSecondaryBranchLength;
        public int StructuredSecondaryLoopCount;
        public float SecondaryLoopChance;
        public float RoomCellSize;
        public bool CloseUnusedExits;
        public bool SpawnResources;
        public List<PcgFailurePoolReport> RoomPools = new List<PcgFailurePoolReport>();
    }

    [Serializable]
    public sealed class PcgFailurePoolReport
    {
        public string Role;
        public int PrefabCount;
        public List<string> Prefabs = new List<string>();
    }

    [Serializable]
    public sealed class PcgFailureAttemptReport
    {
        public int BudgetAttempt;
        public int GraphAttempt;
        public int CurrentTargetRooms;
        public int GraphSeed;
        public int RoleSeed;
        public int StitchSeed;
        public int NodeCount;
        public int EdgeCount;
        public int PrimaryRingCount;
        public int InstantiatedRoomCount;
        public int PhysicallyPlacedRoomCount;
        public int UnplacedRoomCount;
        public int ResolvedConnectionCount;
        public int UnresolvedConnectionCount;
        public int StitchFailureCount;
        public int TaskTriggerCount;
        public List<int> PrimaryRingNodeIds = new List<int>();
        public List<PcgFailureNodeReport> Nodes = new List<PcgFailureNodeReport>();
        public List<PcgFailureEdgeReport> Edges = new List<PcgFailureEdgeReport>();
        public List<PcgFailureRoomReport> Rooms = new List<PcgFailureRoomReport>();
        public List<PcgFailureStitchReport> StitchFailures = new List<PcgFailureStitchReport>();
    }

    [Serializable]
    public sealed class PcgFailureNodeReport
    {
        public int Id;
        public int X;
        public int Y;
        public string Role;
        public bool HasSideTask;
        public string SideTask;
        public int Degree;
    }

    [Serializable]
    public sealed class PcgFailureEdgeReport
    {
        public int NodeA;
        public int NodeB;
        public bool IsLoopEdge;
        public bool IsResolved;
        public string ConnectorFrom;
        public string ConnectorTo;
    }

    [Serializable]
    public sealed class PcgFailureRoomReport
    {
        public int NodeId;
        public string Role;
        public int GridX;
        public int GridY;
        public string InstanceName;
        public bool IsPhysicallyPlaced;
        public int ConnectorCount;
        public int UsedConnectorCount;
    }

    [Serializable]
    public sealed class PcgFailureStitchReport
    {
        public int AnchorNodeId;
        public int TargetNodeId;
        public string Phase;
        public string Reason;
    }

    public static class PcgGenerationFailureReporter
    {
        private const string DefaultDirectoryName = "PCGFailureReports";

        public static PcgGenerationFailureReport CreateReport(
            MapGenerationRequest request,
            string requestSource,
            int fixedSeed,
            int originalTargetRooms,
            int minTargetRooms,
            int maxGraphAttempts)
        {
            return new PcgGenerationFailureReport
            {
                CreatedUtc = DateTime.UtcNow.ToString("o"),
                RequestSource = requestSource ?? string.Empty,
                InputSeed = request != null ? request.Seed : 0,
                FixedSeed = fixedSeed,
                OriginalTargetRooms = originalTargetRooms,
                MinTargetRooms = minTargetRooms,
                MaxGraphAttemptsPerBudget = maxGraphAttempts,
                Request = BuildRequestReport(request, originalTargetRooms)
            };
        }

        public static PcgFailureAttemptReport BuildAttemptReport(
            int budgetAttempt,
            int graphAttempt,
            int currentTargetRooms,
            int graphSeed,
            int roleSeed,
            int stitchSeed,
            RoomGraph graph,
            PcgMapGenerationResult result,
            Dictionary<int, PcgPlacedRoom> placedByNode,
            List<PcgStitchFailureRecord> stitchFailures)
        {
            PcgFailureAttemptReport attempt = new PcgFailureAttemptReport
            {
                BudgetAttempt = budgetAttempt,
                GraphAttempt = graphAttempt,
                CurrentTargetRooms = currentTargetRooms,
                GraphSeed = graphSeed,
                RoleSeed = roleSeed,
                StitchSeed = stitchSeed,
                NodeCount = graph != null ? graph.NodeCount : 0,
                EdgeCount = graph != null ? graph.Edges.Count : 0,
                InstantiatedRoomCount = result != null ? result.PlacedRooms.Count : 0,
                TaskTriggerCount = result != null && result.TaskTriggerConnections != null ? result.TaskTriggerConnections.Count : 0
            };

            AddGraph(attempt, graph);
            AddRooms(attempt, placedByNode);
            AddConnections(attempt, graph, result);
            AddStitchFailures(attempt, stitchFailures);

            return attempt;
        }

        public static string WriteReport(PcgGenerationFailureReport report, string directoryName)
        {
            if (report == null)
            {
                return string.Empty;
            }

            string root = ResolveDirectory(directoryName);
            Directory.CreateDirectory(root);

            string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            string source = SanitizeFileName(report.RequestSource);
            if (string.IsNullOrWhiteSpace(source))
            {
                source = "Unknown";
            }

            string fileName = $"pcg_failure_seed_{report.FixedSeed}_{source}_{timestamp}.json";
            string path = Path.Combine(root, fileName);
            string json = JsonUtility.ToJson(report, true);
            File.WriteAllText(path, json);
            return path;
        }

        private static PcgFailureRequestReport BuildRequestReport(MapGenerationRequest request, int originalTargetRooms)
        {
            PcgFailureRequestReport report = new PcgFailureRequestReport();
            if (request == null)
            {
                return report;
            }

            MapTaskInput taskInput = request.TaskInput;
            if (taskInput != null)
            {
                report.PrimaryTask = taskInput.PrimaryTask != null ? taskInput.PrimaryTask.TaskType.ToString() : string.Empty;
                report.TaskProvider = taskInput.TaskProvider ?? string.Empty;

                if (taskInput.SideTasks != null)
                {
                    report.SideTaskCount = taskInput.SideTasks.Count;
                    for (int i = 0; i < taskInput.SideTasks.Count; i++)
                    {
                        SideTaskInput sideTask = taskInput.SideTasks[i];
                        report.SideTasks.Add(sideTask != null ? sideTask.TaskType.ToString() : string.Empty);
                    }
                }
            }

            MapScaleSettings scale = request.ScaleSettings;
            if (scale != null)
            {
                report.TargetRoomCount = originalTargetRooms;
                report.ExtraLoopCount = scale.ExtraLoopCount;
                report.MaxNodeDegree = scale.MaxNodeDegree;
                report.PrimaryRingRatio = scale.PrimaryRingRatio;
                report.BranchDensity = scale.BranchDensity;
                report.SecondaryBranchChance = scale.SecondaryBranchChance;
                report.MaxPrimaryBranchLength = scale.MaxPrimaryBranchLength;
                report.MaxSecondaryBranchLength = scale.MaxSecondaryBranchLength;
                report.StructuredSecondaryLoopCount = scale.StructuredSecondaryLoopCount;
                report.SecondaryLoopChance = scale.SecondaryLoopChance;
                report.RoomCellSize = scale.RoomCellSize;
            }

            report.CloseUnusedExits = request.CloseUnusedExits;
            report.SpawnResources = request.SpawnResources;

            if (request.RoomPrefabPools != null)
            {
                for (int i = 0; i < request.RoomPrefabPools.Count; i++)
                {
                    RoomPrefabPool pool = request.RoomPrefabPools[i];
                    if (pool == null)
                    {
                        continue;
                    }

                    PcgFailurePoolReport poolReport = new PcgFailurePoolReport
                    {
                        Role = pool.Role.ToString(),
                        PrefabCount = pool.Prefabs != null ? pool.Prefabs.Count : 0
                    };

                    if (pool.Prefabs != null)
                    {
                        for (int p = 0; p < pool.Prefabs.Count; p++)
                        {
                            PcgRoomRoot prefab = pool.Prefabs[p];
                            poolReport.Prefabs.Add(prefab != null ? prefab.name : string.Empty);
                        }
                    }

                    report.RoomPools.Add(poolReport);
                }
            }

            return report;
        }

        private static void AddGraph(PcgFailureAttemptReport attempt, RoomGraph graph)
        {
            if (attempt == null || graph == null)
            {
                return;
            }

            attempt.PrimaryRingCount = graph.PrimaryRingNodeIds != null ? graph.PrimaryRingNodeIds.Count : 0;
            if (graph.PrimaryRingNodeIds != null)
            {
                for (int i = 0; i < graph.PrimaryRingNodeIds.Count; i++)
                {
                    attempt.PrimaryRingNodeIds.Add(graph.PrimaryRingNodeIds[i]);
                }
            }

            for (int i = 0; i < graph.Nodes.Count; i++)
            {
                RoomGraphNode node = graph.Nodes[i];
                if (node == null)
                {
                    continue;
                }

                attempt.Nodes.Add(new PcgFailureNodeReport
                {
                    Id = node.Id,
                    X = node.GridPosition.x,
                    Y = node.GridPosition.y,
                    Role = node.AssignedRole.ToString(),
                    HasSideTask = node.HasAssignedSideTask,
                    SideTask = node.HasAssignedSideTask ? node.AssignedSideTask.ToString() : string.Empty,
                    Degree = graph.GetDegree(node.Id)
                });
            }
        }

        private static void AddRooms(PcgFailureAttemptReport attempt, Dictionary<int, PcgPlacedRoom> placedByNode)
        {
            if (attempt == null || placedByNode == null)
            {
                return;
            }

            foreach (KeyValuePair<int, PcgPlacedRoom> pair in placedByNode)
            {
                PcgPlacedRoom room = pair.Value;
                if (room == null)
                {
                    attempt.UnplacedRoomCount++;
                    continue;
                }

                if (room.IsPhysicallyPlaced)
                {
                    attempt.PhysicallyPlacedRoomCount++;
                }
                else
                {
                    attempt.UnplacedRoomCount++;
                }

                PcgRoomRoot root = room.RoomInstance;
                attempt.Rooms.Add(new PcgFailureRoomReport
                {
                    NodeId = room.NodeId,
                    Role = room.Role.ToString(),
                    GridX = room.GridPosition.x,
                    GridY = room.GridPosition.y,
                    InstanceName = root != null ? root.name : string.Empty,
                    IsPhysicallyPlaced = room.IsPhysicallyPlaced,
                    ConnectorCount = root != null ? root.ConnectorCount : 0,
                    UsedConnectorCount = room.UsedConnectors != null ? room.UsedConnectors.Count : 0
                });
            }

            attempt.Rooms.Sort((a, b) => a.NodeId.CompareTo(b.NodeId));
        }

        private static void AddConnections(PcgFailureAttemptReport attempt, RoomGraph graph, PcgMapGenerationResult result)
        {
            if (attempt == null || graph == null)
            {
                return;
            }

            Dictionary<ulong, PcgRoomConnection> byEdge = new Dictionary<ulong, PcgRoomConnection>();
            if (result != null && result.Connections != null)
            {
                for (int i = 0; i < result.Connections.Count; i++)
                {
                    PcgRoomConnection connection = result.Connections[i];
                    if (connection != null)
                    {
                        byEdge[MakeEdgeKey(connection.NodeA, connection.NodeB)] = connection;
                    }
                }
            }

            for (int i = 0; i < graph.Edges.Count; i++)
            {
                RoomGraphEdge edge = graph.Edges[i];
                PcgRoomConnection connection = null;
                byEdge.TryGetValue(MakeEdgeKey(edge.NodeA, edge.NodeB), out connection);

                bool resolved = connection != null && connection.IsResolved;
                if (resolved)
                {
                    attempt.ResolvedConnectionCount++;
                }
                else
                {
                    attempt.UnresolvedConnectionCount++;
                }

                attempt.Edges.Add(new PcgFailureEdgeReport
                {
                    NodeA = edge.NodeA,
                    NodeB = edge.NodeB,
                    IsLoopEdge = edge.IsLoopEdge,
                    IsResolved = resolved,
                    ConnectorFrom = connection != null && connection.ConnectorFrom != null ? connection.ConnectorFrom.ConnectorId : string.Empty,
                    ConnectorTo = connection != null && connection.ConnectorTo != null ? connection.ConnectorTo.ConnectorId : string.Empty
                });
            }
        }

        private static void AddStitchFailures(PcgFailureAttemptReport attempt, List<PcgStitchFailureRecord> stitchFailures)
        {
            if (attempt == null || stitchFailures == null)
            {
                return;
            }

            attempt.StitchFailureCount = stitchFailures.Count;
            for (int i = 0; i < stitchFailures.Count; i++)
            {
                PcgStitchFailureRecord failure = stitchFailures[i];
                if (failure == null)
                {
                    continue;
                }

                attempt.StitchFailures.Add(new PcgFailureStitchReport
                {
                    AnchorNodeId = failure.AnchorNodeId,
                    TargetNodeId = failure.TargetNodeId,
                    Phase = failure.Phase ?? string.Empty,
                    Reason = failure.Reason ?? string.Empty
                });
            }
        }

        private static string ResolveDirectory(string directoryName)
        {
            string dir = string.IsNullOrWhiteSpace(directoryName) ? DefaultDirectoryName : directoryName.Trim();
            if (Path.IsPathRooted(dir))
            {
                return dir;
            }

            return Path.Combine(Application.persistentDataPath, dir);
        }

        private static string SanitizeFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            char[] invalidChars = Path.GetInvalidFileNameChars();
            string result = value.Trim();
            for (int i = 0; i < invalidChars.Length; i++)
            {
                result = result.Replace(invalidChars[i], '_');
            }

            result = result.Replace('|', '_').Replace(':', '_');
            return result;
        }

        private static ulong MakeEdgeKey(int nodeA, int nodeB)
        {
            uint low = (uint)Mathf.Min(nodeA, nodeB);
            uint high = (uint)Mathf.Max(nodeA, nodeB);
            return ((ulong)low << 32) | high;
        }
    }
}
