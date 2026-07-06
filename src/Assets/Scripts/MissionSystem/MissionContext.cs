using System.Collections.Generic;
using Matrix.PCG;
using UnityEngine;

namespace Matrix.Missions
{
    public sealed class MissionContext
    {
        /// <summary>
        /// 构造任务运行时上下文，并注入任务系统共享依赖。
        /// </summary>
        public MissionContext(
            MissionManager manager,
            MissionLibrary missionLibrary,
            PcgMapGenerator mapGenerator,
            PcgMapGenerationResult mapResult,
            EnemySpawnService enemySpawnService,
            MissionPointerManager pointerManager)
        {
            Manager = manager;
            MissionLibrary = missionLibrary;
            MapGenerator = mapGenerator;
            MapResult = mapResult;
            EnemySpawnService = enemySpawnService;
            PointerManager = pointerManager;
        }

        public MissionManager Manager { get; }
        public MissionLibrary MissionLibrary { get; }
        public PcgMapGenerator MapGenerator { get; }
        public PcgMapGenerationResult MapResult { get; }
        public EnemySpawnService EnemySpawnService { get; }
        public MissionPointerManager PointerManager { get; }

        /// <summary>
        /// 尝试按照房间节点编号拿到已生成房间。
        /// </summary>
        public bool TryGetPlacedRoom(int nodeId, out PcgPlacedRoom placedRoom)
        {
            placedRoom = null;

            if (MapResult == null || MapResult.PlacedRooms == null)
            {
                return false;
            }

            for (int i = 0; i < MapResult.PlacedRooms.Count; i++)
            {
                PcgPlacedRoom room = MapResult.PlacedRooms[i];
                if (room == null)
                {
                    continue;
                }

                if (room.NodeId == nodeId)
                {
                    placedRoom = room;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 尝试解析指定房间的世界空间中心点。
        /// </summary>
        public bool TryResolveRoomCenter(int nodeId, out Vector3 center)
        {
            center = Vector3.zero;
            if (!TryGetPlacedRoom(nodeId, out PcgPlacedRoom room) || room.RoomInstance == null)
            {
                return false;
            }

            if (room.RoomInstance.TryGetWorldBounds(out Bounds bounds, out _))
            {
                center = bounds.center;
                return true;
            }

            center = room.RoomInstance.transform.position;
            return true;
        }

        /// <summary>
        /// 收集某个房间内指定类别的刷怪或目标点。
        /// </summary>
        public bool TryCollectSpawnPoints(int nodeId, SpawnPointCategory category, List<Transform> results)
        {
            if (results == null)
            {
                return false;
            }

            results.Clear();

            if (MapResult == null || MapResult.SpawnPoints == null)
            {
                return false;
            }

            for (int i = 0; i < MapResult.SpawnPoints.Count; i++)
            {
                PcgSpawnPointResult point = MapResult.SpawnPoints[i];
                if (point == null || point.NodeId != nodeId || point.Category != category || point.PointTransform == null)
                {
                    continue;
                }

                results.Add(point.PointTransform);
            }

            return results.Count > 0;
        }

        /// <summary>
        /// 搜索当前客户端所拥有的本地玩家逻辑体。
        /// </summary>
        public PlayerActor FindLocalPlayerActor()
        {
            PlayerNetworkProxy[] proxies = Object.FindObjectsOfType<PlayerNetworkProxy>();
            for (int i = 0; i < proxies.Length; i++)
            {
                PlayerNetworkProxy proxy = proxies[i];
                if (proxy != null && proxy.IsOwner)
                {
                    return proxy.PlayerActor != null ? proxy.PlayerActor : proxy.GetComponent<PlayerActor>();
                }
            }

            return null;
        }
    }
}
