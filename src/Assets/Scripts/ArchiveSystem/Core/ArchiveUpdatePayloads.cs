using System;
using System.Collections.Generic;

namespace ArchiveSystem
{
	/// <summary>
	/// 对局结果。
	/// </summary>
	public enum SessionResult
	{
		Success,
		Failure,
		Abort
	}

	/// <summary>
	/// 一局结算时的基础统计快照。
	/// </summary>
	public class SessionSummaryPayload
	{
		public SessionResult Result;
		public TimeSpan TotalDuration;
		public TimeSpan CombatDuration;
		public TimeSpan LoadingDuration;
		public string Career = string.Empty;
		public string Mode = string.Empty;
		public int Difficulty;
		public string Highlight = string.Empty;
		public DateTime? TimestampUtc;
	}

	/// <summary>
	/// 战斗维度快照。
	/// </summary>
	public class CombatSnapshotPayload
	{
		public long NormalKills;
		public long EliteKills;
		public long BossKills;
		public long DamageDealt;
		public long WeaponDamage;
		public long SkillDamage;
		public long StatusDamage;
		public long DamageTaken;
		public long EnvironmentDamageTaken;
		public long FriendlyFireDamageTaken;
		public long Healing;
		public long SelfHealing;
		public long AllyHealing;
		public long EnvironmentHealing;
		public long HighestSingleRunDamage;
		public long HighestSingleRunKills;
		public IEnumerable<BossKillDetailPayload> BossKillDetails;
	}

	/// <summary>
	/// 单个 Boss 击杀详情。
	/// </summary>
	public class BossKillDetailPayload
	{
		public string BossId = string.Empty;
		public long CountIncrement = 1;
		public float DurationSeconds;
		public bool MarkFirstKill;
		public DateTime? FirstKillTimeUtc;
	}

	/// <summary>
	/// 探索维度快照。
	/// </summary>
	public class ExplorationSnapshotPayload
	{
		public IEnumerable<string> NewlyUnlockedMaps;
		public Dictionary<string, float> MapProgressOverride;
		public double DistanceKm;
		public Dictionary<string, double> DistanceByMap;
		public Dictionary<string, double> ExplorationMinutesByMap;
		public Dictionary<string, int> HiddenAreaDiscoveries;
		public int EnvironmentInteractions;
	}

	/// <summary>
	/// 任务结果快照。
	/// </summary>
	public class MissionResultPayload
	{
		public string MissionType = string.Empty;
		public bool IsSuccess;
		public float RemainingHpPercent;
		public float DurationMinutes;
		public string FailureReason = string.Empty;
	}

	/// <summary>
	/// 社交/组队事件快照。
	/// </summary>
	public class SocialSnapshotPayload
	{
		public bool IsCoop;
		public int TeamSize = 1;
		public float DamageShare;
		public int ReviveGiven;
		public int ReviveReceived;
		public string PartnerId = string.Empty;
		public bool? PartnerWin;
		public float PartnerDurationMinutes;
		public int ResourceGiven;
		public int ResourceReceived;
	}

	/// <summary>
	/// 成长/解锁维度快照。
	/// </summary>
	public class GrowthSnapshotPayload
	{
		public Dictionary<string, UnlockRecord> UnlockProgressDelta;
		public Dictionary<string, int> CareerUsageDelta;
		public long ResourceEarned;
		public long ResourceSpent;
		public Dictionary<string, long> ResourceGainBySource;
		public Dictionary<string, long> ResourceSpentByType;
		public Dictionary<string, FacilityRecord> FacilityDelta;
		public Dictionary<string, ChallengeRecord> ChallengeDelta;
	}

	/// <summary>
	/// 趣味事件快照。
	/// </summary>
	public class FunEventPayload
	{
		public IEnumerable<FunnyRankEntryPayload> KillerUpdates;
		public int? HighestStack;
		public string HighestStackItem = string.Empty;
		public int ClutchKillIncrement;
		public FunnyRecord LongestKillRecord;
		public int AirKillIncrement;
		public int AirKillFlyingIncrement;
		public int AirKillGroundIncrement;
		public int NearMissIncrement;
		public int MapSwitchValue;
		public IEnumerable<FunnyRankEntryPayload> TrapTriggerUpdates;
		public int ConsecutiveNoLegendary;
		public int MaxConsecutiveNoLegendary;
		public Dictionary<string, int> OopsCountsIncrement;
	}

	/// <summary>
	/// 趣味榜单增量。
	/// </summary>
	public class FunnyRankEntryPayload
	{
		public string Key = string.Empty;
		public int Increment = 1;
		public DateTime? LastTimeUtc;
		public int MaxEntries = 5;
	}
}
