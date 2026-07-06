using System;
using System.Collections.Generic;
using Matrix.PCG;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Matrix.Missions
{
    public enum MissionType
    {
        Boss = 0,
        Eliminate = 1,
        Defense = 2,
        Capture = 3,
        Destroy = 4
    }

    public enum MissionCategory
    {
        Primary = 0,
        Secondary = 1
    }

    public enum MissionState
    {
        Inactive = 0,
        Ready = 1,
        Active = 2,
        Completed = 3,
        Failed = 4
    }

    [Serializable]
    public sealed class MissionRewardEntry
    {
        public string RewardId = string.Empty;
        public int Amount = 1;
    }

    [Serializable]
    public sealed class MissionSpawnEntry
    {
        [Tooltip("Resources 相对路径，如 \"Boss/Boss\" → 加载 Prefab/Enemy/Boss/Boss.prefab。Boss 使用行为树，普通敌人使用 EnemyActor + EnemyAIConfig 状态机。")]
        public string EnemyPrefabAddress = string.Empty;
        public int Count = 1;

        [Tooltip("仅对普通敌人（EnemyActor 状态机 AI）生效。Boss 使用 Behavior Designer 行为树，AI 行为由 prefab 内嵌的 BehaviorTree 组件定义，此字段可留空。")]
        public string AiConfigPath = string.Empty;
    }

    [Serializable]
    public sealed class MissionDestroyRoundConfig
    {
        public GameObject TargetPrefab;
        public int TargetCount = 1;
        public int GoldReward = 0;
    }

    [Serializable]
    public sealed class MissionSelectionData
    {
        public int SlotIndex;
        public string MissionId = string.Empty;
        public string ExternalTaskId = string.Empty;
        public MissionType MissionType;
        public MissionCategory MissionCategory;
    }

    public struct MissionHudEntry
    {
        public int SlotIndex;
        public string MissionId;
        public MissionType MissionType;
        public MissionCategory MissionCategory;
        public MissionState State;
        public int CurrentProgress;
        public int TargetProgress;
        public string DisplayName;
        public string StatusText;
    }

    public static class MissionHudStatusFormatter
    {
        public static string ResolveStatusText(MissionType missionType, MissionState state, int currentProgress, int targetProgress)
        {
            switch (state)
            {
                case MissionState.Inactive:
                case MissionState.Ready:
                    return "待触发";
                case MissionState.Completed:
                    return "已完成";
                case MissionState.Failed:
                    return "已失败";
            }

            int remaining = Mathf.Max(0, targetProgress - currentProgress);
            switch (missionType)
            {
                case MissionType.Boss:
                    return "击败目标";
                case MissionType.Defense:
                    return $"还需守护 {FormatDuration(Mathf.Max(0, currentProgress))}";
                case MissionType.Capture:
                    return remaining > 0 ? $"还需回收 {remaining} 个" : "等待回收";
                case MissionType.Destroy:
                    return $"还需摧毁 {remaining} 个";
                default:
                    return $"还需击杀 {remaining} 个";
            }
        }

        public static string FormatDuration(int totalSeconds)
        {
            totalSeconds = Mathf.Max(0, totalSeconds);
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;
            return minutes > 0 ? $"{minutes}分{seconds:00}秒" : $"{seconds}秒";
        }
    }

    [Serializable]
    public sealed class MissionGroupRuntimeData
    {
        public string GroupId = string.Empty;
        public string ProviderTag = string.Empty;
        public int Seed;
        public List<MissionSelectionData> Missions = new List<MissionSelectionData>();

        /// <summary>
        /// 将当前局任务组转换为 PCG 可消费的任务输入。
        /// </summary>
        public MapTaskInput CreatePcgTaskInput()
        {
            MapTaskInput taskInput = new MapTaskInput
            {
                TaskProvider = ProviderTag
            };

            for (int i = 0; i < Missions.Count; i++)
            {
                MissionSelectionData selection = Missions[i];
                if (selection == null)
                {
                    continue;
                }

                if (selection.MissionCategory == MissionCategory.Primary)
                {
                    taskInput.PrimaryTask = new PrimaryTaskInput
                    {
                        TaskType = PrimaryTaskType.BossBattle,
                        ExternalTaskId = selection.ExternalTaskId ?? string.Empty
                    };

                    continue;
                }

                taskInput.SideTasks.Add(new SideTaskInput
                {
                    TaskType = MapToSideTaskType(selection.MissionType),
                    ExternalTaskId = selection.ExternalTaskId ?? string.Empty
                });
            }

            return taskInput;
        }

        /// <summary>
        /// 检查当前任务组是否已经满足 1 主 2 次的最低结构。
        /// </summary>
        public bool IsValidGroup()
        {
            int primaryCount = 0;
            int secondaryCount = 0;

            for (int i = 0; i < Missions.Count; i++)
            {
                MissionSelectionData selection = Missions[i];
                if (selection == null)
                {
                    continue;
                }

                if (selection.MissionCategory == MissionCategory.Primary)
                {
                    primaryCount++;
                }
                else
                {
                    secondaryCount++;
                }
            }

            return primaryCount == 1 && secondaryCount >= 2;
        }

        /// <summary>
        /// 将运行时任务类型映射为 PCG 的次任务类型。
        /// </summary>
        public static SideTaskType MapToSideTaskType(MissionType missionType)
        {
            switch (missionType)
            {
                case MissionType.Defense:
                    return SideTaskType.Defense;
                case MissionType.Capture:
                    return SideTaskType.Capture;
                case MissionType.Destroy:
                    return SideTaskType.Destroy;
                default:
                    return SideTaskType.Elimination;
            }
        }
    }

    [Serializable]
    public sealed class MissionPullContext
    {
        public ulong RequesterClientId;
        public bool IsServer;
        public bool IsHost;
        public bool HasExistingGroup;
        public int ConnectedClientCount;
        public int SuggestedSeed;
    }

    public interface IMissionGroupProvider
    {
        /// <summary>
        /// 从服务器侧任务源拉取本局任务组。
        /// </summary>
        bool TryPullMissionGroup(MissionPullContext context, MissionLibrary missionLibrary, out MissionGroupRuntimeData missionGroup);
    }

    public interface IMissionLobbyForwarder
    {
        /// <summary>
        /// 尝试从大厅转发层读取已经确定的任务组。
        /// </summary>
        bool TryGetForwardedMissionGroup(MissionPullContext context, MissionLibrary missionLibrary, out MissionGroupRuntimeData missionGroup);

        /// <summary>
        /// 当主机首次决定任务组后，将结果写回大厅转发层。
        /// </summary>
        void ForwardHostMissionGroup(MissionGroupRuntimeData missionGroup);
    }

    public struct MissionNetState : INetworkSerializable, IEquatable<MissionNetState>
    {
        public int SlotIndex;
        public MissionType MissionType;
        public MissionCategory MissionCategory;
        public MissionState State;
        public int RoomNodeId;
        public int CurrentProgress;
        public int TargetProgress;
        public FixedString128Bytes MissionId;
        public FixedString128Bytes ExternalTaskId;

        /// <summary>
        /// 执行 NGO 所需的任务状态序列化。
        /// </summary>
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref SlotIndex);
            serializer.SerializeValue(ref MissionType);
            serializer.SerializeValue(ref MissionCategory);
            serializer.SerializeValue(ref State);
            serializer.SerializeValue(ref RoomNodeId);
            serializer.SerializeValue(ref CurrentProgress);
            serializer.SerializeValue(ref TargetProgress);
            serializer.SerializeValue(ref MissionId);
            serializer.SerializeValue(ref ExternalTaskId);
        }

        /// <summary>
        /// 判断两个同步快照是否完全相同。
        /// </summary>
        public bool Equals(MissionNetState other)
        {
            return SlotIndex == other.SlotIndex &&
                   MissionType == other.MissionType &&
                   MissionCategory == other.MissionCategory &&
                   State == other.State &&
                   RoomNodeId == other.RoomNodeId &&
                   CurrentProgress == other.CurrentProgress &&
                   TargetProgress == other.TargetProgress &&
                   MissionId.Equals(other.MissionId) &&
                   ExternalTaskId.Equals(other.ExternalTaskId);
        }

        /// <summary>
        /// 从配置构造一份初始网络快照。
        /// </summary>
        public static MissionNetState CreateInitial(int slotIndex, MissionConfig config, string externalTaskId)
        {
            MissionNetState state = new MissionNetState
            {
                SlotIndex = slotIndex,
                MissionType = config != null ? config.MissionType : MissionType.Eliminate,
                MissionCategory = config != null ? config.MissionCategory : MissionCategory.Secondary,
                State = MissionState.Inactive,
                RoomNodeId = -1,
                CurrentProgress = 0,
                TargetProgress = config != null ? config.ResolveTargetProgress() : 0,
                MissionId = config != null ? new FixedString128Bytes(config.MissionId ?? string.Empty) : default,
                ExternalTaskId = new FixedString128Bytes(externalTaskId ?? string.Empty)
            };

            return state;
        }
    }
}
