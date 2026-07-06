using System.Collections.Generic;
using UnityEngine;

namespace Matrix.PCG
{
    /// <summary>
    /// Clones serializable PCG request models to avoid mutating ScriptableObject-backed config assets at runtime.
    /// </summary>
    public static class PcgRequestCloneUtility
    {
        public static MapGenerationRequest CloneRequest(MapGenerationRequest source)
        {
            MapGenerationRequest clone = new MapGenerationRequest();
            if (source == null)
            {
                EnsureNonNullCollections(clone);
                return clone;
            }

            clone.Seed = source.Seed;
            clone.TaskInput = CloneTaskInput(source.TaskInput);
            clone.ScaleSettings = CloneScaleSettings(source.ScaleSettings);
            clone.RoomPrefabPools = CloneRoomPrefabPools(source.RoomPrefabPools);
            clone.ResourceSpawnOptions = CloneResourceSpawnOptions(source.ResourceSpawnOptions);
            clone.WorldOrigin = source.WorldOrigin;
            clone.CloseUnusedExits = source.CloseUnusedExits;
            clone.SpawnResources = source.SpawnResources;
            clone.MinimapIcons = CloneMinimapIcons(source.MinimapIcons);

            EnsureNonNullCollections(clone);
            return clone;
        }

        public static PcgStyleOptions CloneStyleOptions(PcgStyleOptions source)
        {
            PcgStyleOptions clone = new PcgStyleOptions();
            if (source == null)
            {
                EnsureNonNullStyleOptions(clone);
                return clone;
            }

            clone.ScaleSettings = CloneScaleSettings(source.ScaleSettings);
            clone.RoomPrefabPools = CloneRoomPrefabPools(source.RoomPrefabPools);
            clone.ResourceSpawnOptions = CloneResourceSpawnOptions(source.ResourceSpawnOptions);
            clone.WorldOrigin = source.WorldOrigin;
            clone.CloseUnusedExits = source.CloseUnusedExits;
            clone.SpawnResources = source.SpawnResources;
            clone.MinimapIcons = CloneMinimapIcons(source.MinimapIcons);
            clone.Version = source.Version;

            EnsureNonNullStyleOptions(clone);
            return clone;
        }

        public static List<MinimapIconEntry> CloneMinimapIcons(List<MinimapIconEntry> source)
        {
            var clone = new List<MinimapIconEntry>();
            if (source == null) return clone;

            for (int i = 0; i < source.Count; i++)
            {
                var entry = source[i];
                if (entry == null) continue;
                clone.Add(new MinimapIconEntry { Role = entry.Role, Icon = entry.Icon });
            }
            return clone;
        }

        public static void EnsureNonNullStyleOptions(PcgStyleOptions options)
        {
            if (options == null)
            {
                return;
            }

            if (options.ScaleSettings == null)
            {
                options.ScaleSettings = new MapScaleSettings();
            }

            if (options.RoomPrefabPools == null)
            {
                options.RoomPrefabPools = new List<RoomPrefabPool>();
            }

            if (options.ResourceSpawnOptions == null)
            {
                options.ResourceSpawnOptions = new List<ResourceSpawnOption>();
            }

            if (options.MinimapIcons == null)
            {
                options.MinimapIcons = new List<MinimapIconEntry>();
            }
        }

        public static MapTaskInput CloneTaskInput(MapTaskInput source)
        {
            MapTaskInput clone = new MapTaskInput();
            if (source == null)
            {
                clone.PrimaryTask = new PrimaryTaskInput();
                clone.SideTasks = new List<SideTaskInput>();
                return clone;
            }

            clone.PrimaryTask = ClonePrimaryTask(source.PrimaryTask);
            clone.SideTasks = CloneSideTasks(source.SideTasks);
            clone.TaskProvider = source.TaskProvider;
            clone.ProviderPayloadJson = source.ProviderPayloadJson;
            return clone;
        }

        public static PrimaryTaskInput ClonePrimaryTask(PrimaryTaskInput source)
        {
            PrimaryTaskInput clone = new PrimaryTaskInput();
            if (source == null)
            {
                return clone;
            }

            clone.TaskType = source.TaskType;
            clone.ExternalTaskId = source.ExternalTaskId;
            return clone;
        }

        public static List<SideTaskInput> CloneSideTasks(List<SideTaskInput> source)
        {
            List<SideTaskInput> clone = new List<SideTaskInput>();
            if (source == null)
            {
                return clone;
            }

            for (int i = 0; i < source.Count; i++)
            {
                SideTaskInput item = source[i];
                if (item == null)
                {
                    continue;
                }

                clone.Add(new SideTaskInput
                {
                    TaskType = item.TaskType,
                    ExternalTaskId = item.ExternalTaskId
                });
            }

            return clone;
        }

        public static MapScaleSettings CloneScaleSettings(MapScaleSettings source)
        {
            MapScaleSettings clone = new MapScaleSettings();
            if (source == null)
            {
                return clone;
            }

            clone.TargetRoomCount = source.TargetRoomCount;
            clone.ExtraLoopCount = source.ExtraLoopCount;
            clone.MaxNodeDegree = source.MaxNodeDegree;
            clone.PrimaryRingRatio = source.PrimaryRingRatio;
            clone.BranchDensity = source.BranchDensity;
            clone.SecondaryBranchChance = source.SecondaryBranchChance;
            clone.MaxPrimaryBranchLength = source.MaxPrimaryBranchLength;
            clone.MaxSecondaryBranchLength = source.MaxSecondaryBranchLength;
            clone.StructuredSecondaryLoopCount = source.StructuredSecondaryLoopCount;
            clone.SecondaryLoopChance = source.SecondaryLoopChance;
            clone.RoomCellSize = source.RoomCellSize;
            return clone;
        }

        public static List<RoomPrefabPool> CloneRoomPrefabPools(List<RoomPrefabPool> source)
        {
            List<RoomPrefabPool> clone = new List<RoomPrefabPool>();
            if (source == null)
            {
                return clone;
            }

            for (int i = 0; i < source.Count; i++)
            {
                RoomPrefabPool pool = source[i];
                if (pool == null)
                {
                    continue;
                }

                RoomPrefabPool copiedPool = new RoomPrefabPool
                {
                    Role = pool.Role,
                    Prefabs = new List<PcgRoomRoot>()
                };

                if (pool.Prefabs != null)
                {
                    for (int p = 0; p < pool.Prefabs.Count; p++)
                    {
                        PcgRoomRoot prefab = pool.Prefabs[p];
                        if (prefab != null)
                        {
                            copiedPool.Prefabs.Add(prefab);
                        }
                    }
                }

                clone.Add(copiedPool);
            }

            return clone;
        }

        public static List<ResourceSpawnOption> CloneResourceSpawnOptions(List<ResourceSpawnOption> source)
        {
            List<ResourceSpawnOption> clone = new List<ResourceSpawnOption>();
            if (source == null)
            {
                return clone;
            }

            for (int i = 0; i < source.Count; i++)
            {
                ResourceSpawnOption option = source[i];
                if (option == null)
                {
                    continue;
                }

                clone.Add(new ResourceSpawnOption
                {
                    ResourcePrefab = option.ResourcePrefab,
                    Weight = option.Weight
                });
            }

            return clone;
        }

        public static void EnsureNonNullCollections(MapGenerationRequest request)
        {
            if (request == null)
            {
                return;
            }

            if (request.TaskInput == null)
            {
                request.TaskInput = new MapTaskInput();
            }

            if (request.TaskInput.PrimaryTask == null)
            {
                request.TaskInput.PrimaryTask = new PrimaryTaskInput();
            }

            if (request.TaskInput.SideTasks == null)
            {
                request.TaskInput.SideTasks = new List<SideTaskInput>();
            }

            if (request.ScaleSettings == null)
            {
                request.ScaleSettings = new MapScaleSettings();
            }

            if (request.RoomPrefabPools == null)
            {
                request.RoomPrefabPools = new List<RoomPrefabPool>();
            }

            if (request.ResourceSpawnOptions == null)
            {
                request.ResourceSpawnOptions = new List<ResourceSpawnOption>();
            }

            if (request.MinimapIcons == null)
            {
                request.MinimapIcons = new List<MinimapIconEntry>();
            }
        }
    }
}
