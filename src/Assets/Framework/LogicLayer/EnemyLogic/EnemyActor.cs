using Unity.Netcode;
using UnityEngine;
using Framework.LogicLayer.AttributeSystem;

/// <summary>
/// 敌人逻辑对象，负责属性/战斗/AI 模块的注册与激活。
/// </summary>
public class EnemyActor : LogicActor
{
    /// <summary>
    /// 敌人配置ID（Inspector 填写）
    /// </summary>
    [SerializeField] private string _enemyConfigId;

    /// <summary>
    /// AI 配置资源路径（Inspector 填写）
    /// </summary>
    [SerializeField] private string _aiConfigPath = "Configs/AI/EnemyAI_Default";

    [SerializeField] private string _selfPrefabPath;  // "Prefab/Enemy/001"

    /// <summary>
    /// 敌人物理承载体。
    /// 用于 AI 系统获取实际物理位置，避免逻辑脚本位置与物理位置不一致导致的感知和寻路问题。
    /// 如果未设置，将回退到使用 transform.position。
    /// </summary>
    [Header("物理配置")]
    [Tooltip("敌人的物理承载体（包含 CapsuleCollider 和 Rigidbody 的子物体）。用于 AI 获取真实物理位置。")]
    public Transform physicsCarrier;

    /// <summary>
    /// 缓存的网络对象引用，用于提供稳定的 NetworkObjectId。
    /// </summary>
    private NetworkObject _networkObject;

    /// <summary>
    /// 获取敌人用于感知和移动的世界位置。
    /// 优先使用物理载体位置，如果未设置则使用自身 transform。
    /// </summary>
    public Vector3 WorldPosition
    {
        get
        {
            if (physicsCarrier != null)
                return physicsCarrier.position;
            return transform.position;
        }
    }

    /// <summary>
    /// 获取敌人用于感知和移动的世界旋转。
    /// 优先使用物理载体旋转，如果未设置则使用自身 transform。
    /// </summary>
    public Quaternion WorldRotation
    {
        get
        {
            if (physicsCarrier != null)
                return physicsCarrier.rotation;
            return transform.rotation;
        }
        set
        {
            if (physicsCarrier != null)
                physicsCarrier.rotation = value;
            else
                transform.rotation = value;
        }
    }

    /// <summary>
    /// 对象唯一标识。
    /// 优先使用网络分配的稳定 ID，避免对象池复用时 GetInstanceID 错位导致
    /// AIScheduler / AttackableObjectManager 的字典 key 失效。
    /// </summary>
    public override ulong ObjectId => _networkObject != null ? _networkObject.NetworkObjectId : (ulong)GetInstanceID();

    /// <summary>
    /// 敌人属性模块
    /// </summary>
    public EnemyAttributeModule AttributeModule { get; private set; }
    
    /// <summary>
    /// 敌人战斗模块
    /// </summary>
    public EnemyCombatModule CombatModule { get; private set; }
    
    /// <summary>
    /// 敌人 AI 模块
    /// </summary>
    public EnemyAIModule AIModule { get; private set; }
    
    /// <summary>
    /// 设置敌人配置
    /// </summary>
    public void SetEnemyConfig(string enemyConfigId)
    {
        if (_enemyConfigId == enemyConfigId)
            return;

        _enemyConfigId = enemyConfigId;
        MarkModulesDirtyIfBuilt();
    }
    
    /// <summary>
    /// 设置 AI 配置路径
    /// </summary>
    public void SetAIConfigPath(string configPath)
    {
        if (_aiConfigPath == configPath)
            return;

        _aiConfigPath = configPath;
        MarkModulesDirtyIfBuilt();
    }

    private bool _activated;
    private bool _modulesDirty;
    private string _registeredEnemyConfigId;
    private string _registeredAIConfigPath;
    /// <summary>
    /// 启用时激活所有模块
    /// </summary>
    // ❌ 不再在 OnEnable 自动激活（池化复用会重复激活）
    private void OnEnable()
    {
        _activated = false;
    }
    
    private void OnDisable()
    {
        // ✅ 回收/失活必须清理（取消订阅、清状态机、清目标等）
        OnDeactivate();
    }
    
    public void ConfigureForSpawn(string enemyId, string prefabPath, string aiConfigPath)
    {
        var nextAIConfigPath = !string.IsNullOrWhiteSpace(aiConfigPath) ? aiConfigPath : _aiConfigPath;
        var configChanged = _enemyConfigId != enemyId || _aiConfigPath != nextAIConfigPath;

        _enemyConfigId = enemyId;
        _selfPrefabPath = prefabPath;
        _aiConfigPath = nextAIConfigPath;

        if (configChanged)
            MarkModulesDirtyIfBuilt();
    }

    /// <summary>
    /// 供 EnemyNetworkProxy 调用，在网络对象组件就绪时缓存引用，
    /// 使 ObjectId 可以返回稳定的 NetworkObjectId。
    /// </summary>
    internal void TryResolveNetworkObject(NetworkObject networkObject)
    {
        _networkObject = networkObject;
    }

    public void ActivateAfterSpawn()
    {
        TryResolveNetworkObject(GetComponent<NetworkObject>());
        EnsureModulesReadyForSpawn();

        if (_activated) return;
        _activated = true;

        // 这里调用你原本的 OnActivate 内容
        OnActivate();
    }

    private void MarkModulesDirtyIfBuilt()
    {
        if (_isLocalInited && Modules.Count > 0)
            _modulesDirty = true;
    }

    private void EnsureModulesReadyForSpawn()
    {
        var configMismatch = _registeredEnemyConfigId != _enemyConfigId ||
                             _registeredAIConfigPath != _aiConfigPath;
        var missingRequiredModules = AIModule == null ||
                                     CombatModule == null ||
                                     (!string.IsNullOrEmpty(_enemyConfigId) && AttributeModule == null);

        if (_isLocalInited &&
            Modules.Count > 0 &&
            !missingRequiredModules &&
            !_modulesDirty &&
            !configMismatch)
        {
            return;
        }

        RebuildModulesForSpawn();
    }

    private void RebuildModulesForSpawn()
    {
        for (int i = 0; i < Modules.Count; i++)
        {
            Modules[i]?.LocalDestroy();
        }

        Modules.Clear();
        AIModule = null;
        AttributeModule = null;
        CombatModule = null;

        RenderObject = GetComponent<RenderObject>();
        RegisterModules();

        for (int i = 0; i < Modules.Count; i++)
        {
            Modules[i]?.LocalInit();
        }

        _isLocalInited = true;
        _modulesDirty = false;
    }

    protected override void RegisterModules()
    {
        // 从AttributeManager获取敌人配置并创建属性模块
        if (!string.IsNullOrEmpty(_enemyConfigId))
        {
            var attributeConfig = AttributeManager.Instance.GetEnemyAttributeConfig(_enemyConfigId);
            if (attributeConfig != null)
            {
                AttributeModule = new EnemyAttributeModule(this, attributeConfig);
                AddModule(AttributeModule);
            }
            else
            {
                Debug.LogError($"无法加载敌人配置: {_enemyConfigId}");
            }
        }
        else
        {
            Debug.LogWarning("敌人配置ID为空，无法创建属性模块");
        }
        
        // 创建并注册战斗模块
        CombatModule = new EnemyCombatModule(this);
        AddModule(CombatModule);
        
        // 创建并注册 AI 模块
        EnemyAIConfig aiConfig = null;
        if (!string.IsNullOrEmpty(_aiConfigPath))
        {
            aiConfig = Resources.Load<EnemyAIConfig>(_aiConfigPath);
        }
        
        // 如果加载失败，创建一个默认配置
        if (aiConfig == null)
        {
            Debug.LogWarning($"[EnemyActor] 无法加载 AI 配置: {_aiConfigPath}，使用默认配置");
            aiConfig = ScriptableObject.CreateInstance<EnemyAIConfig>();
        }
        
        AIModule = new EnemyAIModule(this, aiConfig);
        AddModule(AIModule);

        _registeredEnemyConfigId = _enemyConfigId;
        _registeredAIConfigPath = _aiConfigPath;
        _modulesDirty = false;
    }
    
    private void OnDeactivate()
    {
        base.LocalDestroy();
        AIModule = null;
        AttributeModule = null;
        CombatModule = null;
        _networkObject = null;
        _activated = false;
        _modulesDirty = false;
    }

    public string GetSelfPrefabPath() => _selfPrefabPath;
    public string GetEnemyConfigId() => _enemyConfigId;
    public string GetAIConfigPath() => _aiConfigPath;
    
}
