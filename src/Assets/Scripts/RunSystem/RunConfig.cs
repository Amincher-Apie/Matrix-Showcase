using UnityEngine;

namespace Matrix.RunSystem
{
    [CreateAssetMenu(fileName = "RunConfig", menuName = "Matrix/Run/RunConfig")]
    public class RunConfig : ScriptableObject
    {
        [Tooltip("默认地图风格 key，对应 PcgGenerationProfileRegistry。")]
        public string DefaultStyleKey = "Default";

        [Tooltip("默认难度。")]
        public RunDifficulty DefaultDifficulty = RunDifficulty.Normal;

        [Tooltip("最小开局人数。")]
        [Min(1)]
        public int MinPlayersToStart = 1;

        [Tooltip("最大玩家数。")]
        [Min(1)]
        public int MaxPlayers = 4;

        [Tooltip("测试用：是否跳过 Lobby 直接进入 RunInit。")]
        public bool SkipLobbyForTesting = true;

        [Tooltip("测试用：是否跳过 HeroSelect。")]
        public bool SkipHeroSelectForTesting = true;

        [Tooltip("PCG 生成失败时，最多尝试多少个不同的随机种子。")]
        [Min(1)]
        public int MaxPcgSeedAttempts = 10;
    }
}
