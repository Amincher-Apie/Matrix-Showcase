using System;
using System.Collections.Generic;
using UnityEngine;

namespace Matrix.Missions
{
    /// <summary>
    /// 任务类型对应的 UI 图标配置。
    /// </summary>
    [Serializable]
    public sealed class MissionTypeIconEntry
    {
        public MissionType MissionType;
        public Sprite Icon;
    }

    /// <summary>
    /// 管理任务列表 UI 的任务类型图标、主/次任务基底色和状态文字色。
    /// </summary>
    [CreateAssetMenu(fileName = "MissionUIConfig", menuName = "Matrix/Mission/Mission UI Config")]
    public sealed class MissionUIConfigSO : ScriptableObject
    {
        [Header("任务类型图标")]
        [SerializeField]
        private List<MissionTypeIconEntry> iconEntries = new List<MissionTypeIconEntry>();

        [Header("任务分类基底色")]
        [SerializeField]
        private Color mainMissionColor = new Color32(120, 24, 38, 255);

        [SerializeField]
        private Color secondaryMissionColor = new Color32(32, 92, 116, 255);

        [Header("任务状态文字色")]
        [SerializeField]
        private Color inactiveStateColor = Color.white;

        [SerializeField]
        private Color activeStateColor = new Color32(255, 216, 64, 255);

        [SerializeField]
        private Color completedStateColor = Color.white;

        [SerializeField]
        private Color failedStateColor = new Color32(64, 64, 64, 255);

        public IReadOnlyList<MissionTypeIconEntry> IconEntries => iconEntries;
        public Color MainMissionColor => mainMissionColor;
        public Color SecondaryMissionColor => secondaryMissionColor;
        public Color InactiveStateColor => inactiveStateColor;
        public Color ActiveStateColor => activeStateColor;
        public Color CompletedStateColor => completedStateColor;
        public Color FailedStateColor => failedStateColor;

        public bool TryGetEntry(MissionType type, out MissionTypeIconEntry entry)
        {
            for (int i = 0; i < iconEntries.Count; i++)
            {
                MissionTypeIconEntry candidate = iconEntries[i];
                if (candidate != null && candidate.MissionType == type)
                {
                    entry = candidate;
                    return true;
                }
            }

            entry = null;
            return false;
        }

        public Sprite ResolveIcon(MissionType type)
        {
            return TryGetEntry(type, out MissionTypeIconEntry entry) ? entry.Icon : null;
        }

        public Color ResolveBaseColor(MissionCategory category)
        {
            return category == MissionCategory.Primary ? mainMissionColor : secondaryMissionColor;
        }

        public Color ResolveStateTextColor(MissionState state)
        {
            switch (state)
            {
                case MissionState.Active:
                    return activeStateColor;
                case MissionState.Completed:
                    return completedStateColor;
                case MissionState.Failed:
                    return failedStateColor;
                default:
                    return inactiveStateColor;
            }
        }
    }
}
