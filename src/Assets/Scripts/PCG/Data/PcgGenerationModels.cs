using System;
using System.Collections.Generic;
using UnityEngine;

namespace Matrix.PCG
{
    [Serializable]
    public sealed class MapScaleSettings
    {
        [Tooltip("Approximate target room count before topology adjustment.")]
        [Min(2)]
        public int TargetRoomCount = 12;

        [Tooltip("Extra loop edges to add after base connected topology is built.")]
        [Min(0)]
        public int ExtraLoopCount = 2;

        [Tooltip("Maximum graph degree per room node.")]
        [Range(2, 8)]
        public int MaxNodeDegree = 4;

        [Tooltip("Target ratio (0..1) of rooms used by the primary ring skeleton.")]
        [Range(0.20f, 0.80f)]
        public float PrimaryRingRatio = 0.50f;

        [Tooltip("How much remaining room budget is consumed by first-layer ring branches.")]
        [Range(0f, 1f)]
        public float BranchDensity = 0.72f;

        [Tooltip("Chance for non-ring branch nodes to sprout short secondary branches.")]
        [Range(0f, 1f)]
        public float SecondaryBranchChance = 0.35f;

        [Tooltip("Upper bound of first-layer branch length. Most branches are still sampled short.")]
        [Min(1)]
        public int MaxPrimaryBranchLength = 6;

        [Tooltip("Upper bound of secondary branch length.")]
        [Min(1)]
        public int MaxSecondaryBranchLength = 3;

        [Tooltip("Structured secondary loop budget added on top of ExtraLoopCount.")]
        [Min(0)]
        public int StructuredSecondaryLoopCount = 1;

        [Tooltip("Extra chance-driven secondary loops to create local detours.")]
        [Range(0f, 1f)]
        public float SecondaryLoopChance = 0.32f;

        [Tooltip("Grid cell size (world units) for room placement.")]
        [Min(1f)]
        public float RoomCellSize = 30f;
    }

    [Serializable]
    public sealed class RoomPrefabPool
    {
        [Tooltip("Room role bound to this prefab pool.")]
        public RoomRole Role = RoomRole.Connector;

        [Tooltip("Candidate prefabs for this role.")]
        public List<PcgRoomRoot> Prefabs = new List<PcgRoomRoot>();
    }

    [Serializable]
    public sealed class ResourceSpawnOption
    {
        [Tooltip("Resource prefab spawned at resource marker points.")]
        public GameObject ResourcePrefab;

        [Tooltip("Weighted random weight for this resource option.")]
        [Min(1)]
        public int Weight = 1;
    }

    [Serializable]
    public sealed class MinimapIconEntry
    {
        [Tooltip("Room role this icon represents.")]
        public RoomRole Role;

        [Tooltip("Icon texture stamped at the room origin on the minimap.")]
        public Texture2D Icon;
    }

    [Serializable]
    public sealed class PcgStyleOptions
    {
        [Tooltip("Generation options — room count bounds, topology ratios, branch lengths, cell size, etc.")]
        public MapScaleSettings ScaleSettings = new MapScaleSettings();

        [Tooltip("Room prefab pools by role.")]
        public List<RoomPrefabPool> RoomPrefabPools = new List<RoomPrefabPool>();

        [Tooltip("Available resource spawn options with weights.")]
        public List<ResourceSpawnOption> ResourceSpawnOptions = new List<ResourceSpawnOption>();

        [Tooltip("World origin offset for all generated rooms.")]
        public Vector3 WorldOrigin = Vector3.zero;

        [Tooltip("Whether to close unused exit connectors after stitching.")]
        public bool CloseUnusedExits = true;

        [Tooltip("Whether to spawn resources at resource marker points.")]
        public bool SpawnResources = true;

        [Header("Minimap")]
        [Tooltip("Room-role → icon texture mapping for minimap stamping.")]
        public List<MinimapIconEntry> MinimapIcons = new List<MinimapIconEntry>();

        [Tooltip("Internal version for editor migration tracking.")]
        internal int Version = 1;
    }

    /// <summary>
    /// Final concrete request consumed by graph generation and room instantiation logic.
    /// Built at runtime from PcgStyleOptions (profile) + Seed + TaskInput (package).
    /// </summary>
    [Serializable]
    public sealed class MapGenerationRequest
    {
        [Header("Deterministic Seed")]
        public int Seed = 1;

        [Header("Task Input")]
        public MapTaskInput TaskInput = new MapTaskInput();

        [Header("Style Options (from Profile)")]
        public MapScaleSettings ScaleSettings = new MapScaleSettings();

        [Header("Room/Resource Pools (from Profile)")]
        public List<RoomPrefabPool> RoomPrefabPools = new List<RoomPrefabPool>();
        public List<ResourceSpawnOption> ResourceSpawnOptions = new List<ResourceSpawnOption>();

        [Header("Generation Options (from Profile)")]
        public Vector3 WorldOrigin = Vector3.zero;
        public bool CloseUnusedExits = true;
        public bool SpawnResources = true;

        [Header("Minimap")]
        public List<MinimapIconEntry> MinimapIcons = new List<MinimapIconEntry>();
    }
}
