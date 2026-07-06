
using System.Collections.Generic;
using UnityEngine;

namespace Matrix.PCG
{
    /// <summary>
    /// Per-style monster spawn configuration (a and b baseline values).
    /// </summary>
    [System.Serializable]
    public struct MonsterSpawnSettings
    {
        [Tooltip("Base monster cap per region (a). Total cap = a * regionCount * playerMult * taskMult")]
        public int baseCapPerRegion;

        [Tooltip("Base monsters spawned per tick (b). Per-tick budget = b * playerMult * taskMult")]
        public int baseSpawnRatePerTick;

        [Tooltip("Maximum total active monsters across all regions (hard upper bound). Overrides dynamic cap.")]
        public int globalMaxCap;
    }

    /// <summary>
    /// One style profile: owns style options (generation parameters + room/resource references).
    /// Seed and TaskInput are runtime-only concerns injected by PcgGeneratePackage,
    /// never stored or authored in this asset.
    /// </summary>
    [CreateAssetMenu(fileName = "PcgGenerationProfile", menuName = "Matrix/PCG/Generation Profile")]
    public sealed class PcgGenerationProfile : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Style key used by registry and runtime package lookup, e.g. BioLab / Industrial.")]
        [SerializeField]
        private string styleKey = "Style_01";

        [Tooltip("Readable style display name.")]
        [SerializeField]
        private string displayName = "Default Profile";

        [TextArea]
        [SerializeField]
        private string description = string.Empty;

        [Header("Style Options")]
        [Tooltip("Style-owned generation parameters: topology, room pools, resource pools, world origin, etc.")]
        [SerializeField]
        private PcgStyleOptions styleOptions = new PcgStyleOptions();

        [Header("Monster Spawn Settings")]
        [Tooltip("Baseline monster cap (a) and spawn rate (b) for this level style.")]
        [SerializeField]
        private MonsterSpawnSettings monsterSpawnSettings = new MonsterSpawnSettings
        {
            baseCapPerRegion = 20,
            baseSpawnRatePerTick = 3,
            globalMaxCap = 100
        };

        [Header("Enemy Pool")]
        [Tooltip("此地图风格下可刷新的敌人类型列表。用于 UI 预览和刷怪过滤。")]
        [SerializeField]
        private List<EnemySO> availableEnemies = new List<EnemySO>();

        public string StyleKey => styleKey;
        public string DisplayName => displayName;
        public string Description => description;
        public PcgStyleOptions StyleOptions => styleOptions;
        public MonsterSpawnSettings MonsterSpawn => monsterSpawnSettings;
        public IReadOnlyList<EnemySO> AvailableEnemies => availableEnemies;

        public PcgStyleOptions CreateRuntimeStyleOptions()
        {
            return PcgRequestCloneUtility.CloneStyleOptions(styleOptions);
        }

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(styleKey))
            {
                styleKey = name;
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayName = name;
            }

            if (styleOptions == null)
            {
                styleOptions = new PcgStyleOptions();
            }

            PcgRequestCloneUtility.EnsureNonNullStyleOptions(styleOptions);
        }
    }
}
