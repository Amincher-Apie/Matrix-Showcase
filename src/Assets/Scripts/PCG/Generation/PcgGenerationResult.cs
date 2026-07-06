using System.Collections.Generic;
using Matrix.PCG.Instances;
using UnityEngine;

namespace Matrix.PCG
{
    public enum SpawnPointCategory
    {
        NormalEnemy = 0,
        BossEnemy = 1,
        DefenseObjective = 2,
    }

    public sealed class PcgPlacedRoom
    {
        public int NodeId;
        public RoomRole Role;
        public Vector2Int GridPosition;
        public PcgRoomRoot RoomInstance;

        public bool IsPhysicallyPlaced;

        internal readonly HashSet<PcgConnectorMarker> UsedConnectors = new HashSet<PcgConnectorMarker>();
    }

    public sealed class PcgRoomConnection
    {
        public int NodeA;
        public int NodeB;
        public bool IsLoopEdge;
        public bool IsResolved;

        public PcgConnectorMarker ConnectorFrom;
        public PcgConnectorMarker ConnectorTo;

        // Semantic direction relative to connector marker usage in this connection.
        public bool ConnectorFromOutgoing;
        public bool ConnectorToOutgoing;
    }

    public sealed class PcgClosedDoorRecord
    {
        public int NodeId;
        public string ConnectorId;
    }

    public sealed class PcgStitchFailureRecord
    {
        public int AnchorNodeId;
        public int TargetNodeId;
        public string Phase;
        public string Reason;
    }

    public sealed class PcgResourceSpawnResult
    {
        public int NodeId;
        public PcgResourcePointMarker Marker;
        public GameObject ResourcePrefab;
        public GameObject ResourceInstance;
    }

    public sealed class PcgSpawnPointResult
    {
        public int NodeId;
        public SpawnPointCategory Category;
        public Transform PointTransform;
    }

    public sealed class PcgMapGenerationResult
    {
        public int Seed;
        public MapGenerationRequest Request;
        public RoomGraph Graph;

        public readonly List<PcgPlacedRoom> PlacedRooms = new List<PcgPlacedRoom>();
        public readonly List<PcgRoomConnection> Connections = new List<PcgRoomConnection>();
        public readonly List<PcgClosedDoorRecord> ClosedDoors = new List<PcgClosedDoorRecord>();
        public readonly List<PcgResourceSpawnResult> ResourceSpawns = new List<PcgResourceSpawnResult>();
        public readonly List<PcgSpawnPointResult> SpawnPoints = new List<PcgSpawnPointResult>();

        [Tooltip("Task-trigger connections generated during role allocation.")]
        public List<TaskTriggerConnection> TaskTriggerConnections = new List<TaskTriggerConnection>();

        /// <summary>
        /// 所有生成房间对象的父级 Transform。
        /// 用于 NavMesh 运行时烘焙。
        /// </summary>
        public Transform RoomRoot { get; internal set; }

        // ── Generation Metadata ──

        /// <summary>
        /// 原始随机出的目标房间数（seed-deterministic，降级前）。
        /// </summary>
        public int RequestedTargetRooms;

        /// <summary>
        /// 最终使用的目标房间数（可能经过降级）。
        /// </summary>
        public int FinalTargetRooms;

        /// <summary>
        /// 成功时的 budget 降级步数（0 = 未降级）。
        /// </summary>
        public int BudgetAttempt;

        /// <summary>
        /// 成功时在对应 budget 内的 graph variant 索引。
        /// </summary>
        public int GraphAttempt;
    }
}
