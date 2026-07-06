using UnityEngine;

namespace Matrix.SpawnSystem
{
    /// <summary>
    /// Global runtime configuration for the monster spawn system.
    /// Created as a ScriptableObject asset under Assets/Resources/Configs/Spawn/.
    /// </summary>
    [CreateAssetMenu(fileName = "MonsterSpawnConfig", menuName = "Matrix/Spawn/MonsterSpawnConfig")]
    public class MonsterSpawnConfig : ScriptableObject
    {
        [Header("Timing")]
        [Tooltip("Spawn tick interval in seconds. Default: 2")]
        public float spawnTickInterval = 2f;

        [Header("Global Limits")]
        public int globalMaxActiveMonsters = 200;

        [Header("Player Count Multipliers (index = player count - 1)")]
        [Tooltip("Applied to monster cap. 1P=1.0, 2P=1.5, 3P=2.0, 4P=2.5")]
        public float[] playerCountMultipliersForCap = new float[] { 1.0f, 1.5f, 2.0f, 2.5f };

        [Tooltip("Applied to spawn rate per tick. 1P=1.0, 2P=1.2, 3P=1.5, 4P=2.0")]
        public float[] playerCountMultipliersForRate = new float[] { 1.0f, 1.2f, 1.5f, 2.0f };

        [Header("Task Multipliers")]
        [Tooltip("First active task cap multiplier. Default: 1.5")]
        public float firstTaskCapMultiplier = 1.5f;

        [Tooltip("Each additional task beyond the first adds this to cap multiplier. Default: 0.5")]
        public float additionalTaskCapMultiplier = 0.5f;

        [Tooltip("First active task rate multiplier. Default: 2.0")]
        public float firstTaskRateMultiplier = 2.0f;

        [Tooltip("Each additional task beyond the first adds this to rate multiplier. Default: 1.0")]
        public float additionalTaskRateMultiplier = 1.0f;

        [Header("Task Multiplier Caps")]
        [Tooltip("Upper bound for task cap multiplier. Prevents unbounded growth. Default: 3")]
        public float maxTaskCapMultiplier = 3f;

        [Tooltip("Upper bound for task rate multiplier. Prevents unbounded growth. Default: 4")]
        public float maxTaskRateMultiplier = 4f;

        [Header("Region Weights (BFS shortest distance from nearest player-occupied block)")]
        [Tooltip("Distance=1 (adjacent block). Default: 0.5")]
        public float distance1Weight = 0.5f;

        [Tooltip("Distance=2 (one block away, highest weight). Default: 1.0")]
        public float distance2Weight = 1.0f;

        [Tooltip("Distance=3. Default: 0.5")]
        public float distance3Weight = 0.5f;

        [Tooltip("Distance>=4 or player is in block: silent (no spawn). Default: 0")]
        public float distance4PlusWeight = 0f;
    }
}
