using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Framework.Mono;
using Framework.Singleton;
using UnityEngine;

namespace ArchiveSystem
{
	/// <summary>
	/// 玩家档案数据核心管理器，负责数据加载、保存与增量更新。
	/// </summary>
	public class ArchiveManager : SingletonBase<ArchiveManager>
	{
		private const float AutoSaveInterval = 120f;
		private const int MaxHistoryEntries = 100;

		private readonly object _dataLock = new object();

		private PlayerArchiveData _data;
		private IArchiveStorage _storage;
		private string _playerId = string.Empty;

		private bool _initialized;
		private bool _isDirty;
		private float _timer;
		private bool _isSaving;
		
		public bool SaveOnlyOnSessionEnd = true;

		// 新增：本次运行期间是否发生过结算点
		private bool _sessionEndedThisRun = false;

		/// <summary>
		/// 框架单例初始化时构建默认存储实现。
		/// </summary>
		protected override void Initialize()
		{
			base.Initialize();
			_storage = new JsonArchiveStorage();
		}
		
		

		/// <summary>
		/// 初始化档案系统，绑定玩家 ID 并加载数据。
		/// </summary>
		public void Setup(string playerId, IArchiveStorage overrideStorage = null)
		{
			if (string.IsNullOrEmpty(playerId))
			{
				Debug.LogError("[ArchiveManager] playerId 不能为空");
				return;
			}

			if (_initialized && _playerId == playerId)
			{
				return;
			}

			_storage = overrideStorage ?? _storage ?? new JsonArchiveStorage();
			_playerId = playerId;

			LoadOrCreateArchive();
			RegisterLifecycleHooks();
			_initialized = true;
		}

		/// <summary>
		/// 绑定 Update/退出 事件，确保自动保存生效。
		/// </summary>
		private void RegisterLifecycleHooks()
		{
			MonoManager.Instance.OnUpdate -= Tick;
			MonoManager.Instance.OnUpdate += Tick;
			Application.quitting -= OnApplicationQuitting;
			Application.quitting += OnApplicationQuitting;
			// 只在结算保存：不挂 Tick 自动保存
			MonoManager.Instance.OnUpdate -= Tick;
			if (!SaveOnlyOnSessionEnd)
			{
				MonoManager.Instance.OnUpdate += Tick;
			}

			Application.quitting -= OnApplicationQuitting;
			Application.quitting += OnApplicationQuitting;
		}

		/// <summary>
		/// 从磁盘加载档案；若不存在则创建默认数据。
		/// </summary>
		private void LoadOrCreateArchive()
		{
			try
			{
				_data = _storage.Load(_playerId);
				if (_data == null)
				{
					_data = CreateDefaultArchive();
				}
				else if (string.IsNullOrEmpty(_data.Meta.PlayerId))
				{
					_data.Meta.PlayerId = _playerId;
				}

				_data.Meta.LastSaveTimeUtc = DateTime.UtcNow;
				Debug.Log($"[ArchiveManager] 已加载档案：{_playerId}");
			}
			catch (Exception ex)
			{
				Debug.LogError($"[ArchiveManager] 加载档案失败，使用默认数据：{ex.Message}");
				_data = CreateDefaultArchive();
			}
		}

		/// <summary>
		/// 创建默认档案并写入基础元数据。
		/// </summary>
		private PlayerArchiveData CreateDefaultArchive()
		{
			return new PlayerArchiveData
			{
				Meta =
				{
					PlayerId = _playerId,
					CreatedAtUtc = DateTime.UtcNow,
					LastSaveTimeUtc = DateTime.UtcNow
				}
			};
		}

		/// <summary>
		/// 每帧调用，负责脏数据自动保存。
		/// </summary>
		private void Tick()
		{
			// 只在结算保存：Tick 直接不工作
			if (SaveOnlyOnSessionEnd) return;

			if (!_initialized || !_isDirty) return;

			_timer += Time.unscaledDeltaTime;
			if (_timer >= AutoSaveInterval && !_isSaving)
			{
				_ = SaveAsync();
			}
		}

		private void OnApplicationQuitting()
		{
			// 关键：避免“只是打开账户页/没结算就退出”把默认0覆盖写回
			if (SaveOnlyOnSessionEnd)
			{
				if (_sessionEndedThisRun)
					Save();
				return;
			}

			Save();
		}

		/// <summary>
		/// 获取当前档案的引用（调用方需只读使用）。
		/// </summary>
		public PlayerArchiveData GetDataSnapshot()
		{
			lock (_dataLock)
			{
				return _data;
			}
		}

		/// <summary>
		/// 核心写入口，所有修改都通过该方法封装，保证线程安全与脏标记。
		/// </summary>
		public void UpdateData(Action<PlayerArchiveData> updater)
		{
			if (!_initialized)
			{
				Debug.LogWarning("[ArchiveManager] 未初始化，忽略数据更新");
				return;
			}

			lock (_dataLock)
			{
				updater?.Invoke(_data);
				MarkDirty();
			}
		}

		/// <summary>
		/// 记录单局结算信息（基础时长、结果、历史列表等）。
		/// </summary>
		public void RegisterSession(SessionSummaryPayload payload)
		{
		if (payload == null) return;

		UpdateData(data =>
		{
			BaseStatsData stats = data.BaseStats;
			stats.TotalRuns++;
			stats.TotalPlayMinutes += payload.TotalDuration.TotalMinutes;
			stats.TotalCombatMinutes += payload.CombatDuration.TotalMinutes;
			stats.TotalLoadingMinutes += payload.LoadingDuration.TotalMinutes;

			switch (payload.Result)
			{
				case SessionResult.Success:
					stats.SuccessfulRuns++;
					break;
				case SessionResult.Failure:
					stats.FailedRuns++;
					break;
				case SessionResult.Abort:
					stats.AbortedRuns++;
					break;
			}

			double durationSeconds = payload.TotalDuration.TotalSeconds;
			if (durationSeconds > stats.LongestRunSeconds)
			{
				stats.LongestRunSeconds = durationSeconds;
			}

			// 维护运行历史
			var entry = new SessionHistoryEntry
			{
				Career = payload.Career,
				Mode = payload.Mode,
				IsSuccess = payload.Result == SessionResult.Success,
				Duration = payload.TotalDuration,
				DifficultyLevel = payload.Difficulty,
				Highlight = payload.Highlight,
				TimestampUtc = payload.TimestampUtc ?? DateTime.UtcNow
			};

			data.SessionHistory.Add(entry);
			if (data.SessionHistory.Count > MaxHistoryEntries)
			{
				data.SessionHistory.RemoveAt(0);
			}
		});
		// 结算点：保存一次
		_sessionEndedThisRun = true;
		Save(); // 或者：_ = SaveAsync();
	}

		/// <summary>
		/// 注入战斗维度的统计快照。
		/// </summary>
		public void RecordCombatSnapshot(CombatSnapshotPayload payload)
	{
		if (payload == null)
		{
			return;
		}

		UpdateData(data =>
		{
			var combat = data.CombatStats;
			long totalKills = payload.NormalKills + payload.EliteKills + payload.BossKills;
			combat.TotalKills += totalKills;
			combat.EliteKills += payload.EliteKills;
			combat.BossKills += payload.BossKills;
			combat.TotalDamageDealt += payload.DamageDealt;
			combat.TotalWeaponDamage += payload.WeaponDamage;
			combat.TotalSkillDamage += payload.SkillDamage;
			combat.TotalStatusDamage += payload.StatusDamage;
			combat.TotalDamageTaken += payload.DamageTaken;
			combat.EnvironmentDamageTaken += payload.EnvironmentDamageTaken;
			combat.FriendlyFireDamageTaken += payload.FriendlyFireDamageTaken;
			combat.TotalHealing += payload.Healing;
			combat.SelfHealing += payload.SelfHealing;
			combat.AllyHealing += payload.AllyHealing;
			combat.EnvironmentHealing += payload.EnvironmentHealing;

			if (payload.HighestSingleRunDamage > combat.HighestSingleRunDamage)
			{
				combat.HighestSingleRunDamage = payload.HighestSingleRunDamage;
			}

			if (payload.HighestSingleRunKills > combat.HighestSingleRunKills)
			{
				combat.HighestSingleRunKills = payload.HighestSingleRunKills;
			}

			if (payload.BossKillDetails != null)
			{
				foreach (var detail in payload.BossKillDetails)
				{
					if (detail == null || string.IsNullOrEmpty(detail.BossId))
					{
						continue;
					}

					if (!combat.BossKillRecords.TryGetValue(detail.BossId, out BossRecord record))
					{
						record = new BossRecord
						{
							FirstKillTimeUtc = detail.MarkFirstKill
								? (detail.FirstKillTimeUtc ?? DateTime.UtcNow)
								: default,
							FastestKillSeconds = detail.DurationSeconds > 0 ? detail.DurationSeconds : 0
						};
						combat.BossKillRecords[detail.BossId] = record;
					}

					record.KillCount += Math.Max(1, detail.CountIncrement);

					if (detail.MarkFirstKill && record.FirstKillTimeUtc == default)
					{
						record.FirstKillTimeUtc = detail.FirstKillTimeUtc ?? DateTime.UtcNow;
					}

					if (detail.DurationSeconds > 0 &&
					    (record.FastestKillSeconds <= 0 || detail.DurationSeconds < record.FastestKillSeconds))
					{
						record.FastestKillSeconds = detail.DurationSeconds;
					}
				}
			}
		});
	}

		/// <summary>
		/// 注入探索维度快照。
		/// </summary>
		public void RecordExplorationSnapshot(ExplorationSnapshotPayload payload)
	{
		if (payload == null)
		{
			return;
		}

		UpdateData(data =>
		{
			var exploration = data.ExplorationStats;
			exploration.TotalDistanceKm += payload.DistanceKm;
			exploration.EnvironmentInteractionCount += payload.EnvironmentInteractions;

			if (payload.NewlyUnlockedMaps != null)
			{
				foreach (string map in payload.NewlyUnlockedMaps)
				{
					if (!string.IsNullOrEmpty(map))
					{
						exploration.UnlockedMaps.Add(map);
					}
				}
			}

			ApplyDictionaryDelta(exploration.MapDistanceKm, payload.DistanceByMap);
			ApplyDictionaryDelta(exploration.MapExplorationTimeMinutes, payload.ExplorationMinutesByMap);

			if (payload.MapProgressOverride != null)
			{
				foreach (var pair in payload.MapProgressOverride)
				{
					exploration.MapExplorationProgress[pair.Key] = Mathf.Clamp01(pair.Value);
				}
			}

			ApplyDictionaryDelta(exploration.HiddenAreaDiscoveries, payload.HiddenAreaDiscoveries);
		});
	}

		/// <summary>
		/// 注入任务完成/失败数据。
		/// </summary>
		public void RecordMissionResult(MissionResultPayload payload)
	{
		if (payload == null || string.IsNullOrEmpty(payload.MissionType))
		{
			return;
		}

		UpdateData(data =>
		{
			if (!data.MissionStats.MissionRecords.TryGetValue(payload.MissionType, out MissionRecord record))
			{
				record = new MissionRecord();
				data.MissionStats.MissionRecords[payload.MissionType] = record;
			}

			record.TriggerCount++;
			if (payload.IsSuccess)
			{
				record.CompleteCount++;
				record.AverageRemainingHealth = Mathf.Lerp(
					record.AverageRemainingHealth,
					payload.RemainingHpPercent,
					1f / record.CompleteCount);
			}
			else if (!string.IsNullOrEmpty(payload.FailureReason))
			{
				if (!data.MissionStats.FailureReasons.TryGetValue(payload.FailureReason, out FailureReasonRecord reason))
				{
					reason = new FailureReasonRecord();
					data.MissionStats.FailureReasons[payload.FailureReason] = reason;
				}

				reason.Count++;
			}

			if (payload.DurationMinutes > 0)
			{
				if (!data.MissionStats.AverageMissionDurationMinutes.TryGetValue(payload.MissionType, out float current))
				{
					data.MissionStats.AverageMissionDurationMinutes[payload.MissionType] = payload.DurationMinutes;
				}
				else
				{
					float count = record.CompleteCount == 0 ? record.TriggerCount : record.CompleteCount;
					data.MissionStats.AverageMissionDurationMinutes[payload.MissionType] =
						Mathf.Lerp(current, payload.DurationMinutes, 1f / Mathf.Max(1f, count));
				}
			}
		});
	}

		/// <summary>
		/// 注入社交维度的统计快照。
		/// </summary>
		public void RecordSocialSnapshot(SocialSnapshotPayload payload)
	{
		if (payload == null)
		{
			return;
		}

		UpdateData(data =>
		{
			var social = data.SocialStats;
			if (payload.IsCoop)
			{
				social.CoopRuns++;
			}
			else
			{
				social.SoloRuns++;
			}

			if (payload.TeamSize <= 0)
			{
				payload.TeamSize = 1;
			}

			if (!social.TeamSizeDistribution.ContainsKey(payload.TeamSize))
			{
				social.TeamSizeDistribution[payload.TeamSize] = 0;
			}

			social.TeamSizeDistribution[payload.TeamSize]++;

			if (payload.DamageShare > 0)
			{
				if (!social.AvgDamageShareByTeamSize.TryGetValue(payload.TeamSize, out float avg))
				{
					social.AvgDamageShareByTeamSize[payload.TeamSize] = payload.DamageShare;
				}
				else
				{
					social.AvgDamageShareByTeamSize[payload.TeamSize] = Mathf.Lerp(avg, payload.DamageShare, 0.2f);
				}
			}

			social.ReviveTeammateCount += payload.ReviveGiven;
			social.RevivedByTeammateCount += payload.ReviveReceived;
			social.ResourceShareGiven += payload.ResourceGiven;
			social.ResourceShareReceived += payload.ResourceReceived;

			if (!string.IsNullOrEmpty(payload.PartnerId))
			{
				PartnerRecord partner = social.PartnerRecords.Find(p => p.PartnerId == payload.PartnerId);
				if (partner == null)
				{
					partner = new PartnerRecord { PartnerId = payload.PartnerId };
					social.PartnerRecords.Add(partner);
				}

				partner.CoopRuns++;
				if (payload.PartnerWin.HasValue)
				{
					float wins = partner.WinRate * Math.Max(1, partner.CoopRuns - 1);
					if (payload.PartnerWin.Value)
					{
						wins += 1;
					}

					partner.WinRate = wins / partner.CoopRuns;
				}

				if (payload.PartnerDurationMinutes > 0)
				{
					partner.AverageDurationMinutes = Mathf.Lerp(
						partner.AverageDurationMinutes,
						payload.PartnerDurationMinutes,
						1f / partner.CoopRuns);
				}
			}
		});
	}

		/// <summary>
		/// 注入养成/解锁相关数据。
		/// </summary>
		public void RecordGrowthSnapshot(GrowthSnapshotPayload payload)
	{
		if (payload == null)
		{
			return;
		}

		UpdateData(data =>
		{
			var growth = data.GrowthStats;
			growth.PermanentResourceEarned += payload.ResourceEarned;
			growth.PermanentResourceSpent += payload.ResourceSpent;

			ApplyDictionaryDelta(growth.ResourceGainBySource, payload.ResourceGainBySource);
			ApplyDictionaryDelta(growth.ResourceSpentByType, payload.ResourceSpentByType);

			if (payload.UnlockProgressDelta != null)
			{
				foreach (var pair in payload.UnlockProgressDelta)
				{
					growth.UnlockRecords[pair.Key] = pair.Value;
				}
			}

			if (payload.CareerUsageDelta != null)
			{
				ApplyDictionaryDelta(growth.CareerStats, payload.CareerUsageDelta);
			}

			if (payload.FacilityDelta != null)
			{
				foreach (var pair in payload.FacilityDelta)
				{
					growth.FacilityUpgrades[pair.Key] = pair.Value;
				}
			}

			if (payload.ChallengeDelta != null)
			{
				foreach (var pair in payload.ChallengeDelta)
				{
					growth.ChallengeRecords[pair.Key] = pair.Value;
				}
			}
		});
	}

		/// <summary>
		/// 注入趣味事件数据。
		/// </summary>
		public void RecordFunEvent(FunEventPayload payload)
	{
		if (payload == null)
		{
			return;
		}

		UpdateData(data =>
		{
			var fun = data.FunStats;
			if (payload.HighestStack.HasValue && payload.HighestStack.Value > fun.HighestItemStack)
			{
				fun.HighestItemStack = payload.HighestStack.Value;
				fun.HighestItemStackName = payload.HighestStackItem;
			}

			fun.ClutchKills += payload.ClutchKillIncrement;
			fun.AirKills += payload.AirKillIncrement;
			fun.AirKillsFlying += payload.AirKillFlyingIncrement;
			fun.AirKillsGround += payload.AirKillGroundIncrement;
			fun.NearMissCount += payload.NearMissIncrement;

			if (payload.MapSwitchValue > fun.MapSwitchMax)
			{
				fun.MapSwitchMax = payload.MapSwitchValue;
			}

			if (payload.LongestKillRecord != null &&
			    payload.LongestKillRecord.DurationSeconds > fun.LongestKillDuration.DurationSeconds)
			{
				fun.LongestKillDuration = payload.LongestKillRecord;
			}

			if (payload.ConsecutiveNoLegendary >= 0)
			{
				fun.ConsecutiveNoLegendary = payload.ConsecutiveNoLegendary;
			}

			if (payload.MaxConsecutiveNoLegendary > fun.MaxConsecutiveNoLegendary)
			{
				fun.MaxConsecutiveNoLegendary = payload.MaxConsecutiveNoLegendary;
			}

			ApplyRankUpdates(fun.PlayerKillers, payload.KillerUpdates);
			ApplyRankUpdates(fun.TrapTriggerTop, payload.TrapTriggerUpdates);
			ApplyDictionaryDelta(fun.OopsCounts, payload.OopsCountsIncrement);
		});
	}

		/// <summary>
		/// 同步保存（通常用于退出或调试）。
		/// </summary>
		public void Save()
	{
		if (!_initialized || _data == null)
		{
			return;
		}

		lock (_dataLock)
		{
			_storage.Save(_data);
			_isDirty = false;
			_timer = 0f;
		}
	}

		/// <summary>
		/// 异步保存，供自动保存逻辑调用。
		/// </summary>
		public Task SaveAsync(CancellationToken token = default)
	{
		if (_isSaving || !_initialized || _data == null)
		{
			return Task.CompletedTask;
		}

		_isSaving = true;
		return Task.Run(() =>
		{
			lock (_dataLock)
			{
				_storage.Save(_data);
				_isDirty = false;
				_timer = 0f;
			}
		}, token).ContinueWith(_ => { _isSaving = false; });
	}

		/// <inheritdoc />
		public override void Release()
	{
		MonoManager.Instance.OnUpdate -= Tick;
		Application.quitting -= OnApplicationQuitting;
		_data = null;
		_initialized = false;
		base.Release();
	}

		/// <summary>
		/// 标记数据已修改，并重置计时器。
		/// </summary>
		private void MarkDirty()
	{
		_isDirty = true;
		_timer = 0f;
	}

		/// <summary>
		/// 对 long 字典做累加合并。
		/// </summary>
		private static void ApplyDictionaryDelta(IDictionary<string, long> target, IDictionary<string, long> delta)
	{
		if (target == null || delta == null)
		{
			return;
		}

		foreach (var pair in delta)
		{
			if (!target.ContainsKey(pair.Key))
			{
				target[pair.Key] = 0;
			}

			target[pair.Key] += pair.Value;
		}
	}

		/// <summary>
		/// 对 double 字典做累加合并。
		/// </summary>
		private static void ApplyDictionaryDelta(IDictionary<string, double> target, IDictionary<string, double> delta)
	{
		if (target == null || delta == null)
		{
			return;
		}

		foreach (var pair in delta)
		{
			if (!target.ContainsKey(pair.Key))
			{
				target[pair.Key] = 0;
			}

			target[pair.Key] += pair.Value;
		}
	}

		/// <summary>
		/// 对 int 字典做累加合并。
		/// </summary>
		private static void ApplyDictionaryDelta(IDictionary<string, int> target, IDictionary<string, int> delta)
	{
		if (target == null || delta == null)
		{
			return;
		}

		foreach (var pair in delta)
		{
			if (!target.ContainsKey(pair.Key))
			{
				target[pair.Key] = 0;
			}

			target[pair.Key] += pair.Value;
		}
	}

		/// <summary>
		/// 更新趣味榜单并保持排序/长度。
		/// </summary>
		private static void ApplyRankUpdates(List<FunnyRankEntry> list, IEnumerable<FunnyRankEntryPayload> updates)
		{
			if (list == null || updates == null)
			{
				return;
			}

			foreach (var update in updates)
			{
				if (update == null || string.IsNullOrEmpty(update.Key))
				{
					continue;
				}

				FunnyRankEntry entry = list.Find(e => e.Key == update.Key);
				if (entry == null)
				{
					entry = new FunnyRankEntry { Key = update.Key };
					list.Add(entry);
				}

				entry.Count += Math.Max(1, update.Increment);
				entry.LastTimeUtc = update.LastTimeUtc ?? DateTime.UtcNow;

				list.Sort((a, b) => b.Count.CompareTo(a.Count));
				if (list.Count > update.MaxEntries)
				{
					list.RemoveRange(update.MaxEntries, list.Count - update.MaxEntries);
				}
			}
		}
	}
}

