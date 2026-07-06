using System;
using System.Collections.Generic;

namespace ArchiveSystem
{
	/// <summary>
	/// 根数据对象，承载一名玩家的所有档案统计。
	/// </summary>
	[Serializable]
	public class PlayerArchiveData
	{
		/// <summary>元数据：版本、玩家 ID、时间戳。</summary>
		public ArchiveMeta Meta = new ArchiveMeta();

		/// <summary>基础数据：时长、局数、通关率等。</summary>
		public BaseStatsData BaseStats = new BaseStatsData();

		/// <summary>战斗数据：击杀、伤害、Boss 记录。</summary>
		public CombatStatsData CombatStats = new CombatStatsData();

		/// <summary>探索数据：地图进度、距离、互动等。</summary>
		public ExplorationStatsData ExplorationStats = new ExplorationStatsData();

		/// <summary>任务数据：触发、完成、失败原因。</summary>
		public MissionStatsData MissionStats = new MissionStatsData();

		/// <summary>社交数据：组队、救援、合作伙伴等。</summary>
		public SocialStatsData SocialStats = new SocialStatsData();

		/// <summary>养成数据：解锁、资源收支、基地、挑战。</summary>
		public GrowthStatsData GrowthStats = new GrowthStatsData();

		/// <summary>趣味/黑历史数据。</summary>
		public FunStatsData FunStats = new FunStatsData();

		/// <summary>运行历史列表（按时间排序）。</summary>
		public List<SessionHistoryEntry> SessionHistory = new List<SessionHistoryEntry>();
	}

	/// <summary>
	/// 档案元信息。
	/// </summary>
	[Serializable]
	public class ArchiveMeta
	{
		/// <summary>数据结构版本号，便于迁移。</summary>
		public int Version = 1;

		/// <summary>玩家唯一 ID。</summary>
		public string PlayerId = string.Empty;

		/// <summary>档案创建 UTC 时间。</summary>
		public DateTime CreatedAtUtc = DateTime.UtcNow;

		/// <summary>最近一次保存 UTC 时间。</summary>
		public DateTime LastSaveTimeUtc = DateTime.UtcNow;
	}

#region Base Stats

	/// <summary>
	/// 玩家基础统计。
	/// </summary>
	[Serializable]
	public class BaseStatsData
	{
		/// <summary>累计游玩分钟数（含所有阶段）。</summary>
		public double TotalPlayMinutes;

		/// <summary>累计战斗分钟数。</summary>
		public double TotalCombatMinutes;

		/// <summary>进行的总局数。</summary>
		public double TotalRuns;

		/// <summary>通关局数。</summary>
		public double SuccessfulRuns;

		/// <summary>失败局数。</summary>
		public double FailedRuns;

		/// <summary>中途退出局数。</summary>
		public double AbortedRuns;

		/// <summary>最长单局时长（秒）。</summary>
		public double LongestRunSeconds;

		/// <summary>累计加载/结算时间。</summary>
		public double TotalLoadingMinutes;

		/// <summary>平均单局时长（剔除加载时间）。</summary>
		public double AverageRunMinutes => TotalRuns <= 0 ? 0 : (TotalPlayMinutes - TotalLoadingMinutes) / TotalRuns;

		/// <summary>通关率。</summary>
		public double ClearRate => TotalRuns <= 0 ? 0 : SuccessfulRuns / TotalRuns;
	}

#endregion

#region Combat Stats

	/// <summary>
	/// 战斗相关统计。
	/// </summary>
	[Serializable]
	public class CombatStatsData
	{
		/// <summary>总击杀数。</summary>
		public long TotalKills;

		/// <summary>精英击杀数。</summary>
		public long EliteKills;

		/// <summary>Boss 击杀数。</summary>
		public long BossKills;

		/// <summary>单局最高击杀。</summary>
		public long HighestSingleRunKills;

		/// <summary>单局最高伤害。</summary>
		public long HighestSingleRunDamage;

		/// <summary>累计造成伤害。</summary>
		public long TotalDamageDealt;

		/// <summary>武器伤害占比。</summary>
		public long TotalWeaponDamage;

		/// <summary>技能伤害总量。</summary>
		public long TotalSkillDamage;

		/// <summary>异常状态伤害总量。</summary>
		public long TotalStatusDamage;

		/// <summary>承受伤害总量。</summary>
		public long TotalDamageTaken;

		/// <summary>环境伤害。</summary>
		public long EnvironmentDamageTaken;

		/// <summary>友伤。</summary>
		public long FriendlyFireDamageTaken;

		/// <summary>获得的治疗总量。</summary>
		public long TotalHealing;

		public long SelfHealing;

		public long AllyHealing;

		public long EnvironmentHealing;

		/// <summary>Boss 详情记录。</summary>
		public Dictionary<string, BossRecord> BossKillRecords = new Dictionary<string, BossRecord>();
	}

	/// <summary>
	/// 单个 Boss 的击杀记录。
	/// </summary>
	[Serializable]
	public class BossRecord
	{
		public long KillCount;
		public DateTime FirstKillTimeUtc;
		public float FastestKillSeconds;
	}

#endregion

#region Exploration Stats

	/// <summary>
	/// 探索维度统计。
	/// </summary>
	[Serializable]
	public class ExplorationStatsData
	{
		public HashSet<string> UnlockedMaps = new HashSet<string>();
		public Dictionary<string, float> MapExplorationProgress = new Dictionary<string, float>();
		public double TotalDistanceKm;
		public Dictionary<string, double> MapDistanceKm = new Dictionary<string, double>();
		public Dictionary<string, double> MapExplorationTimeMinutes = new Dictionary<string, double>();
		public Dictionary<string, int> HiddenAreaDiscoveries = new Dictionary<string, int>();
		public int EnvironmentInteractionCount;
	}

#endregion

#region Mission Stats

	/// <summary>
	/// 任务维度统计。
	/// </summary>
	[Serializable]
	public class MissionStatsData
	{
		public Dictionary<string, MissionRecord> MissionRecords = new Dictionary<string, MissionRecord>();
		public Dictionary<string, FailureReasonRecord> FailureReasons = new Dictionary<string, FailureReasonRecord>();
		public Dictionary<string, float> AverageMissionDurationMinutes = new Dictionary<string, float>();
	}

	/// <summary>
	/// 某任务类型的完成情况。
	/// </summary>
	[Serializable]
	public class MissionRecord
	{
		public int TriggerCount;
		public int CompleteCount;
		public float AverageRemainingHealth;
	}

	/// <summary>
	/// 失败原因统计。
	/// </summary>
	[Serializable]
	public class FailureReasonRecord
	{
		public int Count;
	}

#endregion

#region Social Stats

	/// <summary>
	/// 社交维度统计。
	/// </summary>
	[Serializable]
	public class SocialStatsData
	{
		public int CoopRuns;
		public int SoloRuns;
		public Dictionary<int, int> TeamSizeDistribution = new Dictionary<int, int>();
		public int ReviveTeammateCount;
		public int RevivedByTeammateCount;
		public Dictionary<int, float> AvgDamageShareByTeamSize = new Dictionary<int, float>();
		public List<PartnerRecord> PartnerRecords = new List<PartnerRecord>();
		public int ResourceShareGiven;
		public int ResourceShareReceived;
	}

	/// <summary>
	/// 与固定队友的合作记录。
	/// </summary>
	[Serializable]
	public class PartnerRecord
	{
		public string PartnerId = string.Empty;
		public int CoopRuns;
		public float WinRate;
		public float AverageDurationMinutes;
	}

#endregion

#region Growth Stats

	/// <summary>
	/// 养成/外围成长统计。
	/// </summary>
	[Serializable]
	public class GrowthStatsData
	{
		public Dictionary<string, UnlockRecord> UnlockRecords = new Dictionary<string, UnlockRecord>();
		public Dictionary<string, int> CareerStats = new Dictionary<string, int>();
		public long PermanentResourceEarned;
		public long PermanentResourceSpent;
		public Dictionary<string, long> ResourceGainBySource = new Dictionary<string, long>();
		public Dictionary<string, long> ResourceSpentByType = new Dictionary<string, long>();
		public Dictionary<string, FacilityRecord> FacilityUpgrades = new Dictionary<string, FacilityRecord>();
		public Dictionary<string, ChallengeRecord> ChallengeRecords = new Dictionary<string, ChallengeRecord>();
	}

	/// <summary>
	/// 解锁进度记录。
	/// </summary>
	[Serializable]
	public class UnlockRecord
	{
		public int Current;
		public int Total;
		public string Requirement = string.Empty;
	}

	/// <summary>
	/// 基地设施升级数据。
	/// </summary>
	[Serializable]
	public class FacilityRecord
	{
		public int Level;
		public string EffectDesc = string.Empty;
		public int TriggeredTimes;
	}

	/// <summary>
	/// 挑战完成度记录。
	/// </summary>
	[Serializable]
	public class ChallengeRecord
	{
		public int Completed;
		public int Total;
		public int Failed;
		public DateTime LastCompletedTimeUtc;
	}

#endregion

#region Fun Stats

	/// <summary>
	/// 趣味/黑历史统计。
	/// </summary>
	[Serializable]
	public class FunStatsData
	{
		public List<FunnyRankEntry> PlayerKillers = new List<FunnyRankEntry>();
		public int HighestItemStack;
		public string HighestItemStackName = string.Empty;
		public int ClutchKills;
		public FunnyRecord LongestKillDuration = new FunnyRecord();
		public int AirKills;
		public int AirKillsFlying;
		public int AirKillsGround;
		public int NearMissCount;
		public int MapSwitchMax;
		public List<FunnyRankEntry> TrapTriggerTop = new List<FunnyRankEntry>();
		public int ConsecutiveNoLegendary;
		public int MaxConsecutiveNoLegendary;
		public Dictionary<string, int> OopsCounts = new Dictionary<string, int>();
	}

	/// <summary>
	/// 趣味榜单条目。
	/// </summary>
	[Serializable]
	public class FunnyRankEntry
	{
		public string Key = string.Empty;
		public int Count;
		public DateTime LastTimeUtc;
	}

	/// <summary>
	/// 趣味记录描述。
	/// </summary>
	[Serializable]
	public class FunnyRecord
	{
		public string Description = string.Empty;
		public float DurationSeconds;
	}

#endregion

#region Session History

	/// <summary>
	/// 单条运行历史记录。
	/// </summary>
	[Serializable]
	public class SessionHistoryEntry
	{
		public DateTime TimestampUtc;
		public string Career = string.Empty;
		public string Mode = string.Empty;
		public bool IsSuccess;
		public TimeSpan Duration;
		public int DifficultyLevel;
		public string Highlight = string.Empty;
	}
}

#endregion

