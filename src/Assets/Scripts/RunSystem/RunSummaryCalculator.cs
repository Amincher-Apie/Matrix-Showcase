using System;
using ArchiveSystem;
using UnityEngine;

namespace Matrix.RunSystem
{
    /// <summary>
    /// Run 结算统计计算器。聚合 RunSessionData 并写入 ArchiveManager。
    /// </summary>
    public sealed class RunSummaryCalculator : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private string careerName = "Default";
        [SerializeField] private string modeName = "Standard";

        [Header("Debug")]
        [SerializeField] private bool logSummaryToConsole = true;

        /// <summary>
        /// 计算并写入本次 Run 的结算数据到 ArchiveManager。
        /// </summary>
        public void CalculateAndRecord(RunSessionData sessionData, RunConfig config)
        {
            if (sessionData == null)
            {
                Debug.LogError("[RunSummaryCalculator] SessionData is null.");
                return;
            }

            TimeSpan totalDuration = sessionData.TotalDuration;
            int totalKills = Framework.LogicLayer.Module.SpawnSystem.MonsterRegistry.Instance?.TotalKilledCount ?? 0;

            // 1. 记录对局基础统计
            ArchiveManager.Instance.RegisterSession(new SessionSummaryPayload
            {
                Result = sessionData.IsVictory ? SessionResult.Success : SessionResult.Failure,
                TotalDuration = totalDuration,
                CombatDuration = TimeSpan.FromSeconds(totalDuration.TotalSeconds * 0.7),
                LoadingDuration = TimeSpan.FromSeconds(5),
                Career = careerName,
                Mode = modeName,
                Difficulty = (int)sessionData.Difficulty,
                Highlight = sessionData.IsVictory ? "Boss Defeated" : "Team Wiped",
                TimestampUtc = sessionData.StartTimeUtc
            });

            // 2. 记录战斗统计
            ArchiveManager.Instance.RecordCombatSnapshot(new CombatSnapshotPayload
            {
                NormalKills = totalKills,
                DamageDealt = 0,
                DamageTaken = 0
            });

            // 3. 记录社交统计
            ArchiveManager.Instance.RecordSocialSnapshot(new SocialSnapshotPayload
            {
                IsCoop = false,
                TeamSize = 1
            });

            // 4. 记录任务结果
            if (sessionData.MissionResults != null)
            {
                foreach (var mr in sessionData.MissionResults)
                {
                    ArchiveManager.Instance.RecordMissionResult(new MissionResultPayload
                    {
                        MissionType = mr.MissionType,
                        IsSuccess = mr.IsSuccess,
                        RemainingHpPercent = 0f,
                        DurationMinutes = 0f,
                        FailureReason = string.Empty
                    });
                }
            }

            // 5. 记录货币收益
            ArchiveManager.Instance.RecordGrowthSnapshot(new GrowthSnapshotPayload
            {
                ResourceEarned = sessionData.TotalCurrencyEarned,
                ResourceSpent = 0
            });

            if (logSummaryToConsole)
            {
                Debug.Log($"[RunSummary] ===== Run Summary =====\n" +
                          $"  Result: {(sessionData.IsVictory ? "VICTORY" : "DEFEAT")}\n" +
                          $"  Duration: {totalDuration.TotalMinutes:F1} min\n" +
                          $"  Kills: {totalKills}\n" +
                          $"  Rooms Cleared: {sessionData.RoomsCleared}/{sessionData.MapResult?.Graph?.NodeCount ?? 0}\n" +
                          $"  Seed: {sessionData.Seed}\n" +
                          $"  Difficulty: {sessionData.Difficulty}\n" +
                          $"  Hero: {sessionData.SelectedHeroId}\n" +
                          $"===========================");
            }
        }
    }
}
