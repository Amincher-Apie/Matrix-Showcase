using System;
using Matrix.Missions;
using Matrix.PCG;
using UnityEngine;

namespace Matrix.RunSystem
{
    /// <summary>
    /// Run 运行时依赖上下文（仿 MissionContext 模式）。
    /// </summary>
    public sealed class RunContext
    {
        public RunManager Manager { get; }
        public PcgMapGenerator MapGenerator { get; }
        public MissionManager MissionManager { get; }
        public Framework.LogicLayer.Module.SpawnSystem.MonsterSpawnManager SpawnManager { get; }
        public RunConfig Config { get; }

        public RunContext(
            RunManager manager,
            PcgMapGenerator mapGenerator,
            MissionManager missionManager,
            Framework.LogicLayer.Module.SpawnSystem.MonsterSpawnManager spawnManager,
            RunConfig config)
        {
            Manager = manager;
            MapGenerator = mapGenerator;
            MissionManager = missionManager;
            SpawnManager = spawnManager;
            Config = config;
        }

        /// <summary>
        /// 获取当前存活玩家数量（通过 AttackableObjectManager 查找所有 PlayerActor）。
        /// </summary>
        public int GetAlivePlayerCount()
        {
            if (AttackableObjectManager.Instance == null)
                return 0;

            int count = 0;
            var allRegistered = AttackableObjectManager.Instance.GetAllRegistered();
            for (int i = 0; i < allRegistered.Count; i++)
            {
                if (allRegistered[i] is PlayerActor player && IsPlayerAliveForRun(player))
                    count++;
            }

            return count; // 全灭时返回 0，由 RunManager 的 UnitDied 监听器做 TeamWipe 判定
        }

        private static bool IsPlayerAliveForRun(PlayerActor player)
        {
            if (player == null || !player.IsActiveForAI || !player.IsAliveForAI)
                return false;

            var attr = player.GetComponent<ServerPlayerAttributeModule>();
            if (attr == null)
                return true;

            return attr.GetAttribute(AttributeType.Health) > 0f;
        }

        /// <summary>
        /// 根据世界坐标查找玩家当前所在房间节点。
        /// </summary>
        public int FindPlayerRoomNode(Vector3 worldPos, PcgMapGenerationResult mapResult)
        {
            if (mapResult == null || mapResult.PlacedRooms == null)
                return -1;

            int bestNodeId = -1;
            float bestDist = float.MaxValue;

            for (int i = 0; i < mapResult.PlacedRooms.Count; i++)
            {
                PcgPlacedRoom room = mapResult.PlacedRooms[i];
                if (room?.RoomInstance == null) continue;

                if (room.RoomInstance.TryGetWorldBounds(out Bounds bounds, out _))
                {
                    if (bounds.Contains(worldPos))
                        return room.NodeId;

                    float dist = bounds.SqrDistance(worldPos);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestNodeId = room.NodeId;
                    }
                }
            }

            return bestNodeId;
        }

        /// <summary>
        /// 尝试收集某个房间内指定类别的刷怪点。
        /// </summary>
        public bool TryCollectSpawnPoints(int nodeId, SpawnPointCategory category, System.Collections.Generic.List<Transform> results)
        {
            if (results == null) return false;
            results.Clear();

            PcgMapGenerationResult mapResult = Manager?.Session?.MapResult;
            if (mapResult?.SpawnPoints == null) return false;

            for (int i = 0; i < mapResult.SpawnPoints.Count; i++)
            {
                PcgSpawnPointResult point = mapResult.SpawnPoints[i];
                if (point?.NodeId == nodeId && point.Category == category && point.PointTransform != null)
                    results.Add(point.PointTransform);
            }

            return results.Count > 0;
        }
    }
}
