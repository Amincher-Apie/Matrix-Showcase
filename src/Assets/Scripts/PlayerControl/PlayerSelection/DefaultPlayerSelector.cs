using System.Collections.Generic;
using Framework.LogicLayer.AttributeSystem;
using Framework.Singleton;
using UnityEngine;

    /// <summary>
    /// 默认角色选择器实现
    /// </summary>
    public class DefaultPlayerSelector : SingletonBase<DefaultPlayerSelector>, IPlayerSelector
    {
        #region 私有字段
        private string _selectedPlayerId;
        private Dictionary<string, PlayerSelectionInfo> _playerCache = new Dictionary<string, PlayerSelectionInfo>();
        #endregion

        #region 事件
        public event System.Action<PlayerSelectionInfo> OnPlayerSelected;
        #endregion

        #region 初始化
        protected override void Initialize()
        {
            base.Initialize();
            CachePlayerInfos();
            // 默认选择第一个可用的角色
            var availablePlayers = GetSelectablePlayers();
            if (availablePlayers.Count > 0)
            {
                SelectPlayer(availablePlayers[0].PlayerId);
            }
        }

        /// <summary>
        /// 缓存玩家信息。优先从 SOManager 加载 HeroSO，回退到 AttributeManager。
        /// </summary>
        private void CachePlayerInfos()
        {
            var heroList = SOManager.Instance.GetSOList<HeroSO>();
            if (heroList != null && heroList.Count > 0)
            {
                CacheFromHeroSOs(heroList);
            }
            else
            {
                CacheFromAttributeManager();
            }
        }

        private void CacheFromHeroSOs(List<HeroSO> heroList)
        {
            foreach (var heroSO in heroList)
            {
                if (heroSO == null || string.IsNullOrEmpty(heroSO.id)) continue;

                var selectionInfo = new PlayerSelectionInfo
                {
                    PlayerId = heroSO.id,
                    DisplayName = heroSO.name,
                    Description = heroSO.description ?? BuildAttributeDesc(heroSO.attributeConfig),
                    Icon = heroSO.icon,
                    AttributeConfig = heroSO.attributeConfig,
                    HeroSO = heroSO,
                    IsUnlocked = CheckPlayerUnlocked(heroSO.id),
                    RequiredLevel = GetRequiredLevel(heroSO.id)
                };

                _playerCache[heroSO.id] = selectionInfo;
            }
        }

        private void CacheFromAttributeManager()
        {
            var attributeManager = AttributeManager.Instance;
            var availableIds = attributeManager.GetAvailablePlayerIds();

            foreach (var playerId in availableIds)
            {
                var config = attributeManager.GetPlayerAttributeConfig(playerId);
                if (config)
                {
                    var selectionInfo = new PlayerSelectionInfo
                    {
                        PlayerId = playerId,
                        DisplayName = attributeManager.GetPlayerDisplayName(playerId),
                        Description = $"角色属性: 生命{config.baseHealth} 护盾{config.baseShield} 护甲{config.baseArmor}",
                        Icon = LoadPlayerIcon(playerId),
                        AttributeConfig = config,
                        HeroSO = null,
                        IsUnlocked = CheckPlayerUnlocked(playerId),
                        RequiredLevel = GetRequiredLevel(playerId)
                    };

                    _playerCache[playerId] = selectionInfo;
                }
            }
        }

        /// <summary>
        /// 加载角色图标（AttributeManager 回退路径用）。
        /// </summary>
        private Sprite LoadPlayerIcon(string playerId)
        {
            var iconPath = $"UI/PlayerIcons/{playerId}";
            var sprite = Resources.Load<Sprite>(iconPath);
            return sprite ? sprite : LoadDefaultIcon();
        }

        private Sprite LoadDefaultIcon()
        {
            return Resources.Load<Sprite>("UI/PlayerIcons/default");
        }

        /// <summary>
        /// 检查角色是否已解锁
        /// </summary>
        private bool CheckPlayerUnlocked(string playerId)
        {
            // 这里可以实现解锁逻辑，比如根据玩家等级、成就等
            // 暂时默认所有角色都解锁
            return true;
        }

        /// <summary>
        /// 获取解锁所需等级
        /// </summary>
        private int GetRequiredLevel(string playerId)
        {
            // 可以根据角色ID返回不同的解锁等级
            return 1; // 默认1级即可使用
        }
        #endregion

        #region IPlayerSelector实现
        public List<PlayerSelectionInfo> GetSelectablePlayers()
        {
            var selectablePlayers = new List<PlayerSelectionInfo>();
            foreach (var info in _playerCache.Values)
            {
                if (info.IsUnlocked)
                {
                    selectablePlayers.Add(info);
                }
            }
            return selectablePlayers;
        }

        public bool SelectPlayer(string playerId)
        {
            if (!_playerCache.ContainsKey(playerId))
            {
                Debug.LogError($"尝试选择不存在的角色: {playerId}");
                return false;
            }

            var playerInfo = _playerCache[playerId];
            if (!playerInfo.IsUnlocked)
            {
                Debug.LogWarning($"角色 {playerId} 尚未解锁");
                return false;
            }

            _selectedPlayerId = playerId;
            Debug.Log($"角色选择: {playerInfo.DisplayName}");

            OnPlayerSelected?.Invoke(playerInfo);
            return true;
        }

        public PlayerSelectionInfo GetSelectedPlayer()
        {
            if (string.IsNullOrEmpty(_selectedPlayerId) || !_playerCache.ContainsKey(_selectedPlayerId))
            {
                // 返回默认角色
                var defaultPlayers = GetSelectablePlayers();
                if (defaultPlayers.Count > 0)
                {
                    return defaultPlayers[0];
                }
                return new PlayerSelectionInfo(); // 空结构体
            }

            return _playerCache[_selectedPlayerId];
        }
        #endregion

        #region 工具方法
        /// <summary>
        /// 获取当前选择的角色属性配置。
        /// </summary>
        public PlayerAttributeConfig GetSelectedPlayerConfig()
        {
            var selectedPlayer = GetSelectedPlayer();
            return selectedPlayer.AttributeConfig;
        }

        /// <summary>
        /// 获取当前选择的 HeroSO（HeroSO 路径下有效，回退路径返回 null）。
        /// </summary>
        public HeroSO GetSelectedHeroSO()
        {
            var selectedPlayer = GetSelectedPlayer();
            return selectedPlayer.HeroSO;
        }

        private static string BuildAttributeDesc(PlayerAttributeConfig config)
        {
            if (config == null) return string.Empty;
            return $"生命{config.baseHealth} 护盾{config.baseShield} 护甲{config.baseArmor}";
        }

        /// <summary>
        /// 重新加载角色信息（用于解锁状态更新等）
        /// </summary>
        public void ReloadPlayerInfos()
        {
            _playerCache.Clear();
            CachePlayerInfos();
        }
        #endregion

        #region 生命周期
        public override void Release()
        {
            _playerCache.Clear();
            _selectedPlayerId = null;
            OnPlayerSelected = null;
            base.Release();
        }
        #endregion
    }
