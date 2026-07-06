
using System.Collections.Generic;
using Framework.Singleton;
using UnityEngine;

namespace Framework.LogicLayer.AttributeSystem // 外部需导入才可用
{
    /// <summary>
    /// 属性配置管理器 - 统一管理所有角色的属性配置
    /// </summary>
    public class AttributeManager : SingletonBase<AttributeManager>
    {
        #region 配置路径常量
        private const string PLAYER_ATTRIBUTE_PATH = "Data/SO/AttributeSO/Player/";
        private const string ENEMY_ATTRIBUTE_PATH = "Data/SO/AttributeSO/Enemy/";
        private const string DEFAULT_PLAYER_ATTRIBUTE_ID = "0";
        #endregion

        #region 私有字段
        private Dictionary<string, PlayerAttributeConfig> _playerAttributes = new Dictionary<string, PlayerAttributeConfig>();
        private Dictionary<string, EnemyAttributeConfig> _enemyAttributes = new Dictionary<string, EnemyAttributeConfig>();
        private readonly HashSet<string> _warnedNormalizedEnemyIds = new HashSet<string>();
        private bool _isInitialized = false; 
        #endregion

        #region 初始化
        protected override void Initialize()
        {
            base.Initialize();
            LoadAllAttributes();
            _isInitialized = true;
            Debug.Log("[AttributeManager] 初始化完成");
        }

        /// <summary>
        /// 加载所有属性配置
        /// </summary>
        private void LoadAllAttributes()
        {
            LoadPlayerAttributes();
            LoadEnemyAttributes();
        }

        /// <summary>
        /// 加载玩家属性配置
        /// </summary>
        private void LoadPlayerAttributes()
        {
            var playerConfigs = Resources.LoadAll<PlayerAttributeConfig>(PLAYER_ATTRIBUTE_PATH);
            foreach (var config in playerConfigs)
            {
                if (!string.IsNullOrEmpty(config.id))
                {
                    _playerAttributes[config.id] = config;
                    Debug.Log($"加载玩家属性配置: {config.id} - {config.name}");
                }
                else
                {
                    Debug.LogWarning($"发现无效的玩家属性配置: {config.name}");
                }
            }
            Debug.Log($"总共加载 {_playerAttributes.Count} 个玩家属性配置");
        }

        /// <summary>
        /// 加载敌人属性配置
        /// </summary>
        private void LoadEnemyAttributes()
        {
            var enemyConfigs = Resources.LoadAll<EnemyAttributeConfig>(ENEMY_ATTRIBUTE_PATH);
            foreach (var config in enemyConfigs)
            {
                if (!string.IsNullOrEmpty(config.id))
                {
                    _enemyAttributes[config.id] = config;
                    Debug.Log($"加载敌人属性配置: {config.id} - {config.name}");
                }
                else
                {
                    Debug.LogWarning($"发现无效的敌人属性配置: {config.name}");
                }
            }
            Debug.Log($"总共加载 {_enemyAttributes.Count} 个敌人属性配置");
        }
        #endregion

        #region 公开方法 - 玩家属性
        /// <summary>
        /// 获取玩家属性配置
        /// </summary>
        public PlayerAttributeConfig GetPlayerAttributeConfig(string playerId)
        {
            if (!_isInitialized)
            {
                Debug.LogError("[AttributeManager] 管理器未初始化");
                return null;
            }

            if (_playerAttributes.TryGetValue(playerId, out var config))
            {
                return config;
            }

            Debug.LogWarning($"未找到玩家属性配置: {playerId}，使用默认配置");
            return GetDefaultPlayerAttributeConfig();
        }

        /// <summary>
        /// 获取所有可用的玩家角色ID（用于角色选择界面）
        /// </summary>
        public List<string> GetAvailablePlayerIds()
        {
            return new List<string>(_playerAttributes.Keys);
        }

        /// <summary>
        /// 检查玩家角色是否可用
        /// </summary>
        public bool IsPlayerAvailable(string playerId)
        {
            return _playerAttributes.ContainsKey(playerId);
        }

        /// <summary>
        /// 获取玩家角色显示名称
        /// </summary>
        public string GetPlayerDisplayName(string playerId)
        {
            var config = GetPlayerAttributeConfig(playerId);
            return config?.name ?? "未知角色";
        }
        #endregion

        #region 公开方法 - 敌人属性
        /// <summary>
        /// 获取敌人属性配置
        /// </summary>
        public EnemyAttributeConfig GetEnemyAttributeConfig(string enemyId)
        {
            if (!_isInitialized)
            {
                Debug.LogError("[AttributeManager] 管理器未初始化");
                return null;
            }

            if (_enemyAttributes.TryGetValue(enemyId, out var config))
            {
                return config;
            }

            string normalizedId = ExtractEnemyAttributeId(enemyId);
            if (!string.IsNullOrEmpty(normalizedId) &&
                normalizedId != enemyId &&
                _enemyAttributes.TryGetValue(normalizedId, out config))
            {
                if (_warnedNormalizedEnemyIds.Add(enemyId))
                {
                    Debug.LogWarning($"[AttributeManager] 敌人属性配置使用归一化 ID: {enemyId} -> {normalizedId}");
                }
                return config;
            }

            Debug.LogError($"未找到敌人属性配置: {enemyId}");
            return null;
        }

        /// <summary>
        /// 根据怪物等级获取随机敌人配置
        /// </summary>
        public EnemyAttributeConfig GetRandomEnemyByRank(MonsterRank rank)
        {
            var eligibleEnemies = new List<EnemyAttributeConfig>();
            foreach (var config in _enemyAttributes.Values)
            {
                if (config.baseMonsterRank == rank)
                {
                    eligibleEnemies.Add(config);
                }
            }

            if (eligibleEnemies.Count > 0)
            {
                return eligibleEnemies[Random.Range(0, eligibleEnemies.Count)];
            }

            Debug.LogWarning($"未找到等级为 {rank} 的敌人配置");
            return null;
        }

        /// <summary>
        /// 获取所有敌人ID
        /// </summary>
        public List<string> GetAllEnemyIds()
        {
            return new List<string>(_enemyAttributes.Keys);
        }
        #endregion

        #region 工具方法
        /// <summary>
        /// 获取默认玩家属性配置
        /// </summary>
        private PlayerAttributeConfig GetDefaultPlayerAttributeConfig()
        {
            if (_playerAttributes.TryGetValue(DEFAULT_PLAYER_ATTRIBUTE_ID, out var defaultConfig))
            {
                return defaultConfig;
            }

            // 如果找不到默认配置，返回第一个配置
            foreach (var config in _playerAttributes.Values)
            {
                return config;
            }

            // 如果没有任何配置，创建紧急默认配置
            Debug.LogError("未找到任何玩家属性配置，创建紧急默认配置");
            return CreateEmergencyPlayerConfig();
        }

        /// <summary>
        /// 创建紧急默认配置（防止游戏崩溃）
        /// </summary>
        private PlayerAttributeConfig CreateEmergencyPlayerConfig()
        {
            var config = ScriptableObject.CreateInstance<PlayerAttributeConfig>();
            config.id = DEFAULT_PLAYER_ATTRIBUTE_ID;
            config.name = "紧急默认角色";
            config.baseHealth = 100f;
            config.baseShield = 50f;
            config.baseArmor = 10f;
            config.baseMoveSpeed = 5f;
            return config;
        }

        private static string ExtractEnemyAttributeId(string enemyId)
        {
            if (string.IsNullOrWhiteSpace(enemyId))
                return string.Empty;

            var trimmedId = enemyId.Trim();
            var separatorIndex = trimmedId.LastIndexOfAny(new[] { '/', '\\' });
            return separatorIndex >= 0 && separatorIndex < trimmedId.Length - 1
                ? trimmedId.Substring(separatorIndex + 1)
                : trimmedId;
        }

        /// <summary>
        /// 重新加载所有属性配置（用于热重载）
        /// </summary>
        public void ReloadAllAttributes()
        {
            _playerAttributes.Clear();
            _enemyAttributes.Clear();
            _warnedNormalizedEnemyIds.Clear();
            LoadAllAttributes();
            Debug.Log("[AttributeManager] 重新加载所有属性配置完成");
        }
        #endregion

        #region 生命周期
        public override void Release()
        {
            _playerAttributes.Clear();
            _enemyAttributes.Clear();
            _warnedNormalizedEnemyIds.Clear();
            _isInitialized = false;
            base.Release();
            Debug.Log("[AttributeManager] 已释放");
        }
        #endregion
    }
}
