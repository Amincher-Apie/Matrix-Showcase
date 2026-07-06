using System;
using Matrix.PCG;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Matrix.RunSystem
{
    /// <summary>
    /// Run 状态机顶层状态枚举。
    /// 0-9 = Meta 层（局外），10-19 = Run 层（局内），20+ = 终止态。
    /// </summary>
    public enum RunState
    {
        MainMenu = 0,
        Lobby = 1,
        HeroSelect = 2,
        MetaProgression = 3,
        RunInit = 10,
        RoomEnter = 11,
        // 12-14: 已废弃（RoomCombat / RoomClear / PathChoice），房间制已被开放地图取代
        BossFight = 15,
        Exploring = 16,       // 自由探索状态（MonsterSpawnManager 按玩家位置刷怪）
        RunVictory = 20,
        RunDefeat = 21,
        RunSummary = 30,
    }

    /// <summary>
    /// 单个房间在 Run 流程中的状态。
    /// </summary>
    public enum RoomRunState
    {
        Unreachable = 0,
        Locked = 1,
        Available = 2,
        Active = 3,
        Cleared = 4,
    }

    public enum RunDifficulty
    {
        Easy = 0,
        Normal = 1,
        Hard = 2,
        Nightmare = 3,
    }

    /// <summary>
    /// 网络同步的单个房间 Run 状态（仿 MissionNetState 模式）。
    /// </summary>
    public struct RoomNetState : INetworkSerializable, IEquatable<RoomNetState>
    {
        public int RoomNodeId;
        public RoomRunState State;
        public RoomRole Role;
        public int EnemyCountAlive;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref RoomNodeId);
            serializer.SerializeValue(ref State);
            serializer.SerializeValue(ref Role);
            serializer.SerializeValue(ref EnemyCountAlive);
        }

        public bool Equals(RoomNetState other)
        {
            return RoomNodeId == other.RoomNodeId &&
                   State == other.State &&
                   Role == other.Role &&
                   EnemyCountAlive == other.EnemyCountAlive;
        }
    }

    /// <summary>
    /// 用于 RunSummary 的非网络数据结构。
    /// </summary>
    public class RunSummaryData
    {
        public bool IsVictory;
        public int Seed;
        public RunDifficulty Difficulty;
        public TimeSpan TotalDuration;
        public int TotalKills;
        public int RoomsCleared;
        public int TotalRooms;
        public string MapStyle;
    }

}
