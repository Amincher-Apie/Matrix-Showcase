using UnityEngine;

/// <summary>
/// 玩家逻辑对象，专注于业务逻辑并通过网络代理进行网络通信。
/// 当前阶段同时实现 IAttackableObject，用于接入敌人 AI 的统一感知注册体系。
/// </summary>
public class PlayerActor : LogicActor, IAttackableObject
{
    /// <summary>
    /// 玩家基础威胁优先级。
    /// 当前阶段所有玩家使用统一值，后续可扩展为职业、仇恨或任务权重。
    /// </summary>
    private const int DefaultThreatPriority = 100;

    /// <summary>
    /// AI 感知标志，独立于 NGO 的组件启用状态。
    /// 初始化为 true 后不再改变，因为玩家对象的"是否存在于场景中"
    /// 与 PlayerActor 组件是否被 NGO 禁用是两个独立的概念。
    /// </summary>
    private bool _aiDetectable = true;

    /// <summary>
    /// 网络代理引用。
    /// </summary>
    public PlayerNetworkProxy networkProxy;
    
    /// <summary>
    /// 对象唯一标识，从网络代理中获取。
    /// </summary>
    public override ulong ObjectId => networkProxy?.NetworkObjectId ?? 0;
    
    /// <summary>
    /// 角色配置 ID（向后兼容，HeroSO 接入前使用）。
    /// </summary>
    public string _playerConfigId;

    /// <summary>
    /// 当前英雄的 HeroSO 数据模板。
    /// </summary>
    public HeroSO HeroSO { get; private set; }
    
    /// <summary>
    /// 属性模块，逻辑层。
    /// </summary>
    public PlayerAttributeModule AttributeModule { get; private set; }
    
    /// <summary>
    /// 战斗模块，逻辑层。
    /// </summary>
    public PlayerCombatModule CombatModule { get; private set; }
    
    /// <summary>
    /// 当前玩家是否位于服务端。
    /// </summary>
    public bool IsServer => networkProxy?.IsServer ?? false;
    
    /// <summary>
    /// 当前玩家是否位于客户端。
    /// </summary>
    public bool IsClient => networkProxy?.IsClient ?? false;
    
    /// <summary>
    /// 当前玩家是否是本地拥有者。
    /// </summary>
    public bool IsOwner => networkProxy?.IsOwner ?? false;

    /// <summary>
    /// 获取该玩家作为可攻击对象时使用的根 Transform。
    /// </summary>
    public Transform TargetTransform => transform;

    /// <summary>
    /// 获取该玩家的可攻击对象类型。
    /// </summary>
    public AttackableObjectType TargetType => AttackableObjectType.Player;

    /// <summary>
    /// 获取该玩家当前是否允许被 AI 感知与攻击。
    /// 使用自定义标志 _aiDetectable 而非 isActiveAndEnabled，
    /// 因为 NGO 的 SpawnAsPlayerObject 可能在非拥有者端禁用该组件，
    /// 但玩家仍然存在于场景中，应当被视为有效目标。
    /// </summary>
    public bool IsActiveForAI => _aiDetectable && gameObject.activeInHierarchy;

    /// <summary>
    /// 是否存活（供 AI 过滤）。Dead/Spectating → false，仅 Alive 返回 true。
    /// Downed 玩家暂不可被攻击（后续可通过配置调整）。
    /// </summary>
    public bool IsAliveForAI
    {
        get
        {
            var attr = GetComponent<ServerPlayerAttributeModule>();
            if (attr == null) return IsActiveForAI;
            return attr.CurrentLifeState == PlayerLifeState.Alive;
        }
    }

    /// <summary>
    /// 获取该玩家的基础威胁优先级。
    /// </summary>
    public int ThreatPriority => DefaultThreatPriority;

    /// <summary>
    /// 玩家渲染对象。
    /// </summary>
    public PlayerRender PlayerRender => RenderObject as PlayerRender;
    
    /// <summary>
    /// 测试模块。
    /// </summary>
    public PlayerTestModule TestModule { get; private set; }

    /// <summary>
    /// 背包模块。
    /// </summary>
    public PlayerInventoryModule InventoryModule { get; private set; }

    /// <summary>
    /// 品质效果模块。
    /// </summary>
    public PlayerQualityEffectModule QualityEffectModule { get; private set; }
    
    /// <summary>
    /// Buff 模块。
    /// </summary>
    public PlayerBuffModule BuffModule { get; private set; }

    /// <summary>
    /// 技能模块。
    /// </summary>
    public PlayerSkillModule SkillModule { get; private set; }
    
    /// <summary>
    /// 第三人称玩家控制器。
    /// </summary>
    [field: SerializeField]
    public ThirdPersonPlayerController ThirdPersonPlayerController { get; private set; }
    
    /// <summary>
    /// 设置网络代理。
    /// </summary>
    /// <param name="networkProxy">要绑定的玩家网络代理。</param>
    public void SetNetworkProxy(PlayerNetworkProxy networkProxy)
    {
        this.networkProxy = networkProxy;
    }
    
    /// <summary>
    /// 设置角色配置（向后兼容，HeroSO 接入前使用）。
    /// </summary>
    public void SetPlayerConfig(string playerConfigId)
    {
        _playerConfigId = playerConfigId;
    }

    /// <summary>
    /// 设置英雄数据模板。在 RegisterModules 之前调用。
    /// </summary>
    public void SetHeroSO(HeroSO heroSO)
    {
        HeroSO = heroSO;
        _playerConfigId = heroSO != null ? heroSO.id : _playerConfigId;
    }
    
    /// <summary>
    /// 注册玩家逻辑模块。
    /// 优先使用 HeroSO 数据；若 HeroSO 为空则回退到 AttributeManager 查找。
    /// </summary>
    protected override void RegisterModules()
    {
        // 属性模块：优先 HeroSO
        if (HeroSO != null && HeroSO.attributeConfig != null)
        {
            AttributeModule = new PlayerAttributeModule(this, HeroSO.attributeConfig);
            AddModule(AttributeModule);
        }
        else if (!string.IsNullOrEmpty(_playerConfigId))
        {
            var attributeConfig = Framework.LogicLayer.AttributeSystem.AttributeManager.Instance.GetPlayerAttributeConfig(_playerConfigId);
            if (attributeConfig != null)
            {
                AttributeModule = new PlayerAttributeModule(this, attributeConfig);
                AddModule(AttributeModule);
            }
            else
            {
                Debug.LogError($"无法加载角色配置: {_playerConfigId}");
            }
        }
        else
        {
            Debug.LogWarning("玩家角色配置 ID 为空，使用默认选择");
            var defaultSelector = DefaultPlayerSelector.Instance;
            var selectedPlayer = defaultSelector.GetSelectedPlayer();
            if (selectedPlayer.AttributeConfig != null)
            {
                AttributeModule = new PlayerAttributeModule(this, selectedPlayer.AttributeConfig);
                AddModule(AttributeModule);
            }
        }

        CombatModule = new PlayerCombatModule(this);
        AddModule(CombatModule);

        TestModule = new PlayerTestModule(this);
        AddModule(TestModule);

        InventoryModule = new PlayerInventoryModule(this);
        AddModule(InventoryModule);

        BuffModule = new PlayerBuffModule(this);
        AddModule(BuffModule);

        SkillModule = new PlayerSkillModule(this);
        AddModule(SkillModule);

        QualityEffectModule = new PlayerQualityEffectModule(this);
        AddModule(QualityEffectModule);

        RegisterPassives();
    }

    /// <summary>
    /// 注册 HeroSO 中定义的被动能力 Executor。
    /// </summary>
    private void RegisterPassives()
    {
        if (HeroSO == null || HeroSO.passives == null) return;

        foreach (var passiveDef in HeroSO.passives)
        {
            var executor = passiveDef.passiveExecutor;
            if (executor != null)
            {
                executor.OnHeroSpawned(this);
            }
            else
            {
                Debug.LogWarning($"[PlayerActor] HeroSO[{HeroSO.id}] 被动未配置 PassiveExecutorSO。");
            }
        }
    }

    /// <summary>
    /// 注销被动能力 Executor。
    /// </summary>
    private void UnregisterPassives()
    {
        if (HeroSO == null || HeroSO.passives == null) return;

        foreach (var passiveDef in HeroSO.passives)
        {
            var executor = passiveDef.passiveExecutor;
            if (executor != null)
            {
                executor.OnHeroDestroyed(this);
            }
        }
    }

    public void OnEnable()
    {
        OnActivate();
    }

    private void OnDisable()
    {
    }

    public override void LocalDestroy()
    {
        UnregisterPassives();
        base.LocalDestroy();
    }

    /// <summary>
    /// 获取玩家作为感知目标时使用的世界空间参考点。
    /// 当前阶段直接返回角色根节点位置，后续可替换为胸口或头部挂点。
    /// </summary>
    /// <returns>返回玩家在世界空间中的目标点。</returns>
    public Vector3 GetTargetPoint()
    {
        return transform.position;
    }

    /// <summary>
    /// 将武器瞄准器绑定到第三人称视角控制器。
    /// </summary>
    /// <param name="weaponAimController">要绑定的武器瞄准控制器。</param>
    public void BindWeaponAimerToViewController(WeaponAimController weaponAimController)
    {
        ThirdPersonPlayerController.BindWeaponController(weaponAimController);
    }
}
