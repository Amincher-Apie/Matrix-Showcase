using System;
using System.Collections.Generic;
using Matrix.PCG;

namespace Matrix.RunSystem
{
    /// <summary>
    /// 单次 Run 的运行时可变数据。
    /// </summary>
    public class RunSessionData
    {
        public int Seed;
        public RunDifficulty Difficulty;
        public DateTime StartTimeUtc;

        /// <summary>本局是否为胜利通关。</summary>
        public bool IsVictory;

        /// <summary>已清空的房间数。</summary>
        public int RoomsCleared;

        /// <summary>本次生成的地图结果。</summary>
        public PcgMapGenerationResult MapResult;

        /// <summary>玩家走过的房间节点链（按实际进入顺序）。</summary>
        public readonly List<int> RoomChain = new List<int>();

        /// <summary>玩家选择的英雄 ID（P0 为默认值）。</summary>
        public string SelectedHeroId = "DefaultHero";

        /// <summary>玩家选择的初始装备 ID（P0 为 null）。</summary>
        public string SelectedLoadoutId;

        /// <summary>当前房间进入战斗的时间戳（用于统计单房间战斗时长）。</summary>
        public DateTime CurrentRoomCombatStartUtc;

        /// <summary>已完成的支线任务数。</summary>
        public int SideTasksCompleted;

        /// <summary>已失败的支线任务数。</summary>
        public int SideTasksFailed;

        /// <summary>本局通过任务奖励获得的货币总额。</summary>
        public int TotalCurrencyEarned;

        /// <summary>单个任务的结果快照（用于结算写入 ArchiveManager）。</summary>
        public readonly List<MissionResultRecord> MissionResults = new List<MissionResultRecord>();

        public TimeSpan TotalDuration => DateTime.UtcNow - StartTimeUtc;
    }

    /// <summary>
    /// 单个任务对局结束时的结果快照。
    /// </summary>
    public struct MissionResultRecord
    {
        public string MissionType;
        public bool IsSuccess;
    }
}
