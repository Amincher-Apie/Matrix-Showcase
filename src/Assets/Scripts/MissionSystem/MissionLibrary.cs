using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Matrix.Missions
{
    [CreateAssetMenu(fileName = "MissionLibrary", menuName = "Matrix/Mission/Mission Library")]
    public sealed class MissionLibrary : ScriptableObject
    {
        [SerializeField]
        private List<MissionConfig> missions = new List<MissionConfig>();

        public IReadOnlyList<MissionConfig> Missions => missions;

        /// <summary>
        /// 通过任务唯一标识查找配置。
        /// </summary>
        public MissionConfig FindById(string missionId)
        {
            if (string.IsNullOrWhiteSpace(missionId))
            {
                return null;
            }

            for (int i = 0; i < missions.Count; i++)
            {
                MissionConfig config = missions[i];
                if (config == null)
                {
                    continue;
                }

                if (string.Equals(config.MissionId, missionId, StringComparison.OrdinalIgnoreCase))
                {
                    return config;
                }
            }

            return null;
        }

        /// <summary>
        /// 尝试构造一个“1 主 2 次”的随机任务组。
        /// </summary>
        public bool TryBuildRandomGroup(int seed, out MissionGroupRuntimeData missionGroup, int sideMissionCount = 2)
        {
            missionGroup = null;

            List<MissionConfig> primaryCandidates = missions
                .Where(config => config != null && config.MissionCategory == MissionCategory.Primary)
                .ToList();

            List<MissionConfig> secondaryCandidates = missions
                .Where(config => config != null && config.MissionCategory == MissionCategory.Secondary)
                .ToList();

            if (primaryCandidates.Count == 0 || secondaryCandidates.Count == 0)
            {
                return false;
            }

            System.Random random = new System.Random(seed);
            MissionGroupRuntimeData group = new MissionGroupRuntimeData
            {
                GroupId = $"mission_group_{seed}",
                ProviderTag = "MissionLibrary.Random",
                Seed = seed
            };

            MissionConfig primary = primaryCandidates[random.Next(primaryCandidates.Count)];
            group.Missions.Add(new MissionSelectionData
            {
                SlotIndex = 0,
                MissionId = primary.MissionId,
                ExternalTaskId = primary.ExternalTaskId,
                MissionType = primary.MissionType,
                MissionCategory = primary.MissionCategory
            });

            List<MissionConfig> shuffledSecondary = secondaryCandidates
                .OrderBy(_ => random.Next())
                .ToList();

            HashSet<MissionType> usedTypes = new HashSet<MissionType>();
            int targetSideCount = Mathf.Max(1, sideMissionCount);
            int slotIndex = 1;

            for (int i = 0; i < shuffledSecondary.Count && slotIndex <= targetSideCount; i++)
            {
                MissionConfig candidate = shuffledSecondary[i];
                if (candidate == null)
                {
                    continue;
                }

                if (!usedTypes.Add(candidate.MissionType))
                {
                    continue;
                }

                group.Missions.Add(new MissionSelectionData
                {
                    SlotIndex = slotIndex,
                    MissionId = candidate.MissionId,
                    ExternalTaskId = candidate.ExternalTaskId,
                    MissionType = candidate.MissionType,
                    MissionCategory = candidate.MissionCategory
                });

                slotIndex++;
            }

            if (group.Missions.Count < targetSideCount + 1)
            {
                for (int i = 0; i < shuffledSecondary.Count && slotIndex <= targetSideCount; i++)
                {
                    MissionConfig candidate = shuffledSecondary[i];
                    if (candidate == null)
                    {
                        continue;
                    }

                    if (group.Missions.Exists(item => item != null && item.MissionId == candidate.MissionId))
                    {
                        continue;
                    }

                    group.Missions.Add(new MissionSelectionData
                    {
                        SlotIndex = slotIndex,
                        MissionId = candidate.MissionId,
                        ExternalTaskId = candidate.ExternalTaskId,
                        MissionType = candidate.MissionType,
                        MissionCategory = candidate.MissionCategory
                    });

                    slotIndex++;
                }
            }

            if (!group.IsValidGroup())
            {
                return false;
            }

            missionGroup = group;
            return true;
        }
    }
}
