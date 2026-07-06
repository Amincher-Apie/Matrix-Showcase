using System.Collections.Generic;
using Matrix.PCG;
using UnityEngine;

namespace Matrix.Missions
{
    [CreateAssetMenu(fileName = "MissionConfig", menuName = "Matrix/Mission/Mission Config")]
    public sealed class MissionConfig : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField]
        private string missionId = "mission_001";

        [SerializeField]
        private string displayName = "新任务";

        [TextArea]
        [SerializeField]
        private string description = string.Empty;

        [SerializeField]
        private MissionType missionType = MissionType.Eliminate;

        [SerializeField]
        private MissionCategory missionCategory = MissionCategory.Secondary;

        [SerializeField]
        private string externalTaskId = string.Empty;

        [Header("Trigger")]
        [SerializeField]
        private bool triggerOnRoomEnter = true;

        [Tooltip("历史字段。当前默认进房触发范围由任务房 PcgRoomBounds.boundsCollider 决定。")]
        [SerializeField]
        private float triggerHeight = 6f;

        [SerializeField]
        private string pointerLabel = string.Empty;

        [Header("Combat/Objectives")]
        [Tooltip("Boss 任务只需 EnemyPrefabAddress 和 Count；AiConfigPath 对 Boss 无效（Boss AI 由 prefab 行为树定义）。")]
        [SerializeField]
        private List<MissionSpawnEntry> spawnEntries = new List<MissionSpawnEntry>();

        [SerializeField]
        private GameObject objectivePrefab;

        [SerializeField]
        private int killTargetCount = 10;

        [SerializeField]
        private float defenseDurationSeconds = 60f;

        [SerializeField]
        private float captureRequiredProgress = 100f;

        [Header("Capture")]
        [Tooltip("捕获任务标靶死亡后生成的拾取物 Prefab。需要挂载 NetworkObject + PickupItem 并注册到 NetworkPrefabs。")]
        [SerializeField]
        private GameObject capturePickupPrefab;

        [Tooltip("捕获任务拾取物对应的道具 SO ID。")]
        [SerializeField]
        private string captureItemId = string.Empty;

        [SerializeField]
        private int captureItemAmount = 1;

        [SerializeField]
        private string capturePickupPrompt = "按 F 拾取";

        [Header("Defense")]
        [SerializeField]
        private float defenseObjectiveMaxHealth = 500f;

        [SerializeField]
        private float defenseObjectiveShield = 100f;

        [SerializeField]
        private int defenseObjectiveThreatPriority = 100;

        [SerializeField]
        private List<MissionDestroyRoundConfig> destroyRounds = new List<MissionDestroyRoundConfig>();

        [Header("Rewards")]
        [Tooltip("完成任务后发放的局内货币数量。")]
        [SerializeField]
        private int currencyReward;

        [Tooltip("额外发放的道具奖励列表（可选）。")]
        [SerializeField]
        private List<MissionRewardEntry> rewards = new List<MissionRewardEntry>();

        public string MissionId => missionId;
        public string DisplayName => displayName;
        public string Description => description;
        public MissionType MissionType => missionType;
        public MissionCategory MissionCategory => missionCategory;
        public string ExternalTaskId => externalTaskId;
        public bool TriggerOnRoomEnter => triggerOnRoomEnter;
        public float TriggerHeight => Mathf.Max(1f, triggerHeight);
        public string PointerLabel => pointerLabel;
        public IReadOnlyList<MissionSpawnEntry> SpawnEntries => spawnEntries;
        public GameObject ObjectivePrefab => objectivePrefab;
        public int KillTargetCount => Mathf.Max(1, killTargetCount);
        public float DefenseDurationSeconds => Mathf.Max(1f, defenseDurationSeconds);
        public float CaptureRequiredProgress => Mathf.Max(1f, captureRequiredProgress);
        public GameObject CapturePickupPrefab => capturePickupPrefab;
        public string CaptureItemId => captureItemId;
        public int CaptureItemAmount => Mathf.Max(1, captureItemAmount);
        public string CapturePickupPrompt => capturePickupPrompt;
        public float DefenseObjectiveMaxHealth => Mathf.Max(1f, defenseObjectiveMaxHealth);
        public float DefenseObjectiveShield => Mathf.Max(0f, defenseObjectiveShield);
        public int DefenseObjectiveThreatPriority => Mathf.Max(1, defenseObjectiveThreatPriority);
        public IReadOnlyList<MissionDestroyRoundConfig> DestroyRounds => destroyRounds;
        public int CurrencyReward => Mathf.Max(0, currencyReward);
        public IReadOnlyList<MissionRewardEntry> Rewards => rewards;

        /// <summary>
        /// 根据任务类型推导该任务应落到哪类房间角色。
        /// </summary>
        public RoomRole ResolveRoomRole()
        {
            switch (missionType)
            {
                case MissionType.Boss:
                    return RoomRole.Boss;
                case MissionType.Defense:
                    return RoomRole.SideDefense;
                case MissionType.Capture:
                    return RoomRole.SideCapture;
                case MissionType.Destroy:
                    return RoomRole.SideDestroy;
                default:
                    return RoomRole.SideElimination;
            }
        }

        /// <summary>
        /// 解析该任务初始应显示的目标进度。
        /// </summary>
        public int ResolveTargetProgress()
        {
            switch (missionType)
            {
                case MissionType.Boss:
                    return 1;
                case MissionType.Defense:
                    return Mathf.CeilToInt(DefenseDurationSeconds);
                case MissionType.Capture:
                    return 1;
                case MissionType.Destroy:
                    return ResolveDestroyTargetCount();
                default:
                    return KillTargetCount;
            }
        }

        /// <summary>
        /// 统计破坏任务总共需要被摧毁的目标数量。
        /// </summary>
        public int ResolveDestroyTargetCount()
        {
            int total = 0;
            for (int i = 0; i < destroyRounds.Count; i++)
            {
                MissionDestroyRoundConfig round = destroyRounds[i];
                if (round == null)
                {
                    continue;
                }

                total += Mathf.Max(1, round.TargetCount);
            }

            return Mathf.Max(1, total);
        }

        /// <summary>
        /// 返回适合用于 UI 指引器的标题文本。
        /// </summary>
        public string ResolvePointerLabel()
        {
            if (!string.IsNullOrWhiteSpace(pointerLabel))
            {
                return pointerLabel;
            }

            if (!string.IsNullOrWhiteSpace(displayName))
            {
                return displayName;
            }

            return missionId;
        }

        /// <summary>
        /// 在资源改名或首次创建时自动补全基础字段。
        /// </summary>
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(missionId))
            {
                missionId = name;
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayName = name;
            }
        }
    }
}
