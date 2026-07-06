using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Debug 通道总控管理器。
/// 挂载在 GameFlowBootstrapper 所在 GameObject 上，通过 Inspector 统一管理所有 Debug 开关。
/// 支持 PlayerPrefs 持久化，运行时修改 Inspector Toggle 即时生效。
/// </summary>
[DisallowMultipleComponent]
public class DebugManager : MonoBehaviour
{
    public static DebugManager Instance { get; private set; }

    // ════════════════════════════════════════════════════
    // Inspector 可见的通道列表
    // ════════════════════════════════════════════════════

    [Header("AI Debug")]
    [SerializeField] public bool aiStateMachine = true;
    [SerializeField] public bool aiEnemyAIModule = true;
    [SerializeField] public bool aiScheduler = true;
    [SerializeField] public bool aiAttackState = true;
    [SerializeField] public bool aiChaseState = true;
    [SerializeField] public bool aiIdleState = true;
    [SerializeField] public bool aiPatrolState = true;
    [SerializeField] public bool aiNavigation = false;
    [SerializeField] public bool aiSteering = false;

    [Header("PCG / 地图生成 Debug")]
    [SerializeField] public bool pcgMapGenerator = false;
    [SerializeField] public bool pcgNavMeshAssembler = false;

    [Header("Combat / 战斗 Debug")]
    [SerializeField] public bool combatHitScan = false;
    [SerializeField] public bool combatEnemyModule = false;

    [Header("Network / 服务器 Debug")]
    [SerializeField] public bool networkServerAttribute = false;

    [Header("Test / 启动 Debug")]
    [SerializeField] public bool testBootstrap = false;
    [SerializeField] public bool testInventory = false;
    [SerializeField] public bool testSkillBuff = false;

    // ════════════════════════════════════════════════════
    // 全部开关（一键控制）
    // ════════════════════════════════════════════════════

    [Header("Master Switch")]
    [Tooltip("关闭此开关可禁用所有 Debug 输出（包括 AI/PCG/Combat/Network/Test）。")]
    [SerializeField] public bool masterDebugEnabled = true;

    // ════════════════════════════════════════════════════
    // 内部
    // ════════════════════════════════════════════════════

    private Dictionary<string, bool> _channelDefaults;
    private Dictionary<string, bool> _overrides;

    public bool IsMasterEnabled => masterDebugEnabled;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildDefaults();
        LoadFromPlayerPrefs();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void OnValidate()
    {
        if (_channelDefaults == null) return;
        SaveToPlayerPrefs();
    }

    public bool IsEnabled(string key)
    {
        if (!masterDebugEnabled) return false;
        if (_overrides != null && _overrides.TryGetValue(key, out var val)) return val;
        return GetFieldValue(key);
    }

    public void SetEnabled(string key, bool enabled)
    {
        if (_overrides == null) _overrides = new Dictionary<string, bool>();
        _overrides[key] = enabled;
        SaveToPlayerPrefs();
    }

    [ContextMenu("Disable All Channels")]
    public void DisableAll()
    {
        foreach (var k in GetAllKeys()) SetEnabled(k, false);
        masterDebugEnabled = false;
        Debug.Log("[DebugManager] 所有 Debug 通道已关闭");
    }

    [ContextMenu("Enable All Channels")]
    public void EnableAll()
    {
        foreach (var k in GetAllKeys()) SetEnabled(k, true);
        masterDebugEnabled = true;
        Debug.Log("[DebugManager] 所有 Debug 通道已开启");
    }

    [ContextMenu("Reset To Defaults")]
    public void ResetToDefaults()
    {
        _overrides?.Clear();
        PlayerPrefs.DeleteKey("DebugManager.Overrides");
        masterDebugEnabled = true;
        Debug.Log("[DebugManager] 所有 Debug 通道已重置为默认值");
    }

    private void BuildDefaults()
    {
        _channelDefaults = new Dictionary<string, bool>
        {
            ["AI"] = true,
            ["AI.StateMachine"] = true, ["AI.EnemyAIModule"] = true,
            ["AI.Scheduler"] = true, ["AI.AttackState"] = true,
            ["AI.ChaseState"] = true, ["AI.IdleState"] = true,
            ["AI.PatrolState"] = true, ["AI.Navigation"] = false,
            ["AI.Steering"] = false,
            ["PCG.MapGenerator"] = false, ["PCG.NavMeshAssembler"] = false,
            ["Combat.HitScan"] = false, ["Combat.EnemyModule"] = false,
            ["Network.ServerAttribute"] = false,
            ["Test.Bootstrap"] = false, ["Test.Inventory"] = false,
            ["Test.SkillBuff"] = false,
        };
    }

    private string[] GetAllKeys() => _channelDefaults.Keys.ToArray();

    private bool GetFieldValue(string key) => key switch
    {
        "AI" => true,
        "AI.StateMachine" => aiStateMachine, "AI.EnemyAIModule" => aiEnemyAIModule,
        "AI.Scheduler" => aiScheduler, "AI.AttackState" => aiAttackState,
        "AI.ChaseState" => aiChaseState, "AI.IdleState" => aiIdleState,
        "AI.PatrolState" => aiPatrolState, "AI.Navigation" => aiNavigation,
        "AI.Steering" => aiSteering,
        "PCG.MapGenerator" => pcgMapGenerator, "PCG.NavMeshAssembler" => pcgNavMeshAssembler,
        "Combat.HitScan" => combatHitScan, "Combat.EnemyModule" => combatEnemyModule,
        "Network.ServerAttribute" => networkServerAttribute,
        "Test.Bootstrap" => testBootstrap, "Test.Inventory" => testInventory,
        "Test.SkillBuff" => testSkillBuff,
        _ => true,
    };

    private static readonly string[] _prefabKeys =
    {
        "AI.StateMachine","AI.EnemyAIModule","AI.Scheduler","AI.AttackState",
        "AI.ChaseState","AI.IdleState","AI.PatrolState","AI.Navigation",
        "AI.Steering","Test.Bootstrap","Test.Inventory","Test.SkillBuff",
        "PCG.MapGenerator","PCG.NavMeshAssembler","Combat.HitScan",
        "Combat.EnemyModule","Network.ServerAttribute",
    };

    private void SaveToPlayerPrefs()
    {
        var vals = new[]
        {
            aiStateMachine,aiEnemyAIModule,aiScheduler,aiAttackState,
            aiChaseState,aiIdleState,aiPatrolState,aiNavigation,
            aiSteering,testBootstrap,testInventory,testSkillBuff,
            pcgMapGenerator,pcgNavMeshAssembler,combatHitScan,
            combatEnemyModule,networkServerAttribute,
        };
        for (int i = 0; i < _prefabKeys.Length; i++)
            PlayerPrefs.SetInt("DM." + _prefabKeys[i], vals[i] ? 1 : 0);
        PlayerPrefs.SetInt("DM.master", masterDebugEnabled ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void LoadFromPlayerPrefs()
    {
        aiStateMachine = PlayerPrefs.GetInt("DM.AI.StateMachine", 1) == 1;
        aiEnemyAIModule = PlayerPrefs.GetInt("DM.AI.EnemyAIModule", 1) == 1;
        aiScheduler = PlayerPrefs.GetInt("DM.AI.Scheduler", 1) == 1;
        aiAttackState = PlayerPrefs.GetInt("DM.AI.AttackState", 1) == 1;
        aiChaseState = PlayerPrefs.GetInt("DM.AI.ChaseState", 1) == 1;
        aiIdleState = PlayerPrefs.GetInt("DM.AI.IdleState", 1) == 1;
        aiPatrolState = PlayerPrefs.GetInt("DM.AI.PatrolState", 1) == 1;
        aiNavigation = PlayerPrefs.GetInt("DM.AI.Navigation", 0) == 1;
        aiSteering = PlayerPrefs.GetInt("DM.AI.Steering", 0) == 1;
        pcgMapGenerator = PlayerPrefs.GetInt("DM.PCG.MapGenerator", 0) == 1;
        pcgNavMeshAssembler = PlayerPrefs.GetInt("DM.PCG.NavMeshAssembler", 0) == 1;
        combatHitScan = PlayerPrefs.GetInt("DM.Combat.HitScan", 0) == 1;
        combatEnemyModule = PlayerPrefs.GetInt("DM.Combat.EnemyModule", 0) == 1;
        networkServerAttribute = PlayerPrefs.GetInt("DM.Network.ServerAttribute", 0) == 1;
        testBootstrap = PlayerPrefs.GetInt("DM.Test.Bootstrap", 0) == 1;
        testInventory = PlayerPrefs.GetInt("DM.Test.Inventory", 0) == 1;
        testSkillBuff = PlayerPrefs.GetInt("DM.Test.SkillBuff", 0) == 1;
        masterDebugEnabled = PlayerPrefs.GetInt("DM.master", 1) == 1;
    }
}
