using System.Collections;
using Framework.LogicLayer.Module.SpawnSystem;
using Matrix.Missions;
using Matrix.PCG;
using Matrix.RunSystem;
using Matrix.SpawnSystem;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 正式游戏流程启动器。
/// 在 NetworkTestScene 中自动串联：网络就绪 → 创建子系统 → PCG → 玩家/敌人生成 → 对局开始。
///
/// 使用方式：
/// 1. 在 NetworkTestScene 中创建空 GameObject，挂载此脚本
/// 2. 在 Inspector 中赋值 SO 引用（profileRegistry / missionLibrary / runConfig / monsterSpawnConfig）
/// 3. 可选赋值 playerPrefab
/// 4. 运行 —— 自动启动 Host → 地图生成 → 刷玩家 → 刷怪 → 开始对局
///
/// 与 FlowTestBootstrap 的区别：本脚本使用 SerializeField 直接赋值（无反射），
/// 且主动监听 NetworkManager.OnServerStarted 事件，不依赖协程等待。
/// </summary>
[DisallowMultipleComponent]
public sealed class GameFlowBootstrapper : MonoBehaviour
{
    // ════════════════════════════════════════════════════
    // ScriptableObject 引用（在 Inspector 中赋值）
    // ════════════════════════════════════════════════════
    [Header("ScriptableObject 配置")]
    [Tooltip("Assets/Resources/Configs/PCGCongfig/PcgGenerationProfileRegistry.asset")]
    [SerializeField] private PcgGenerationProfileRegistry profileRegistry;

    [Tooltip("Assets/Resources/Configs/Missions/Libraries/MissionLibrary_Default.asset")]
    [SerializeField] private MissionLibrary missionLibrary;

    [Tooltip("RunConfig (Create via: Create → Matrix → Run → RunConfig)")]
    [SerializeField] private RunConfig runConfig;

    [Tooltip("MonsterSpawnConfig (Create via: Create → Matrix → Spawn → MonsterSpawnConfig)")]
    [SerializeField] private MonsterSpawnConfig monsterSpawnConfig;

    [Header("测试参数")]
    [Tooltip("启动后自动以 Host 模式建网并触发 Run 流程。")]
    [SerializeField] private bool autoStartHost = true;

    [Tooltip("回退玩家 Prefab（HeroSO.heroPrefab 为空时使用）。")]
    [SerializeField] private GameObject playerPrefab;

    [Tooltip("默认英雄数据模板。优先使用 heroPrefab 生成玩家。")]
    [SerializeField] private HeroSO defaultHeroSO;

    [Header("调试")]
    [SerializeField] private bool verboseLog = true;

    // ════════════════════════════════════════════════════
    // 内部引用
    // ════════════════════════════════════════════════════
    private GameObject _gameFlowGo;
    private GameObject _spawnServicesGo;

    private NetworkManager _networkManager;
    private PcgMapGenerator _pcgMapGenerator;
    private RunManager _runManager;
    private MissionManager _missionManager;
    private RunSummaryCalculator _runSummaryCalculator;
    private MonsterSpawnManager _monsterSpawnManager;
    private EnemySpawnService _enemySpawnService;
    private PlayerSpawnManager _playerSpawnManager;

    private bool _serverStarted;

    // ════════════════════════════════════════════════════
    // Unity Lifecycle
    // ════════════════════════════════════════════════════

    private void Awake()
    {
        EnsureEventSystem();
        EnsureNetworkManager();

        if (_networkManager != null)
            _networkManager.OnServerStarted += OnServerStarted;

        // 提前创建子系统 GO（组件等到 OnServerStarted 再添加）
        CreateSpawnServices();
        CreateGameFlow();
    }

    private IEnumerator Start()
    {
        // 如果 NetworkManager 已经启动（场景中预先启动），直接走就绪流程
        if (_networkManager != null && _networkManager.IsListening && _networkManager.IsServer)
        {
            OnServerStarted();
            yield break;
        }

        // 如果没自动启动，等待 NetworkAutoStarter 完成
        if (autoStartHost && _networkManager != null && !_networkManager.IsListening)
        {
            _networkManager.NetworkConfig.PlayerPrefab = null;
            bool ok = _networkManager.StartHost();
            Log(ok ? "StartHost 成功" : "StartHost 失败");

            if (ok)
            {
                float timeout = 10f;
                float elapsed = 0f;
                while (!_serverStarted && elapsed < timeout)
                {
                    yield return new WaitForSeconds(0.1f);
                    elapsed += 0.1f;
                }

                if (!_serverStarted)
                    Debug.LogError("[GameFlowBootstrapper] 服务器启动超时！");
            }
        }
    }

    private void OnDestroy()
    {
        if (_networkManager != null)
            _networkManager.OnServerStarted -= OnServerStarted;
    }

    // ════════════════════════════════════════════════════
    // 环境准备
    // ════════════════════════════════════════════════════

    private void EnsureEventSystem()
    {
        if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() != null)
            return;

        var esGo = new GameObject("EventSystem");
        esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
        esGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        Log("已创建 EventSystem");
    }

    private void EnsureNetworkManager()
    {
        _networkManager = NetworkManager.Singleton;
        if (_networkManager != null) return;

        var nmGo = new GameObject("NetworkManager");
        _networkManager = nmGo.AddComponent<NetworkManager>();
        _networkManager.NetworkConfig.PlayerPrefab = null;
        Log("已创建 NetworkManager");
    }

    // ════════════════════════════════════════════════════
    // 创建子系统
    // ════════════════════════════════════════════════════

    private void CreateSpawnServices()
    {
        _spawnServicesGo = new GameObject("SpawnServices");
        _enemySpawnService = _spawnServicesGo.AddComponent<EnemySpawnService>();
        _monsterSpawnManager = _spawnServicesGo.AddComponent<MonsterSpawnManager>();
        _playerSpawnManager = _spawnServicesGo.AddComponent<PlayerSpawnManager>();
        Log("已创建 SpawnServices (EnemySpawnService + MonsterSpawnManager + PlayerSpawnManager)");
    }

    private void CreateGameFlow()
    {
        _gameFlowGo = new GameObject("GameFlow");
        _gameFlowGo.AddComponent<NetworkObject>();

        _pcgMapGenerator = _gameFlowGo.AddComponent<PcgMapGenerator>();
        _runManager = _gameFlowGo.AddComponent<RunManager>();
        _missionManager = _gameFlowGo.AddComponent<MissionManager>();
        _runSummaryCalculator = _gameFlowGo.AddComponent<RunSummaryCalculator>();

        Log("已创建 GameFlow (PcgMapGenerator + RunManager + MissionManager + Summary)");
    }

    // ════════════════════════════════════════════════════
    // 服务器就绪 → 连线 + 启动对局
    // ════════════════════════════════════════════════════

    private void OnServerStarted()
    {
        if (_serverStarted) return;
        _serverStarted = true;

        Log("服务器就绪，开始连线子系统...");

        WireReferences();

        // 等待 RunManager 的 NetworkObject 生成后触发流程
        StartCoroutine(WaitForSpawnThenStart());
    }

    private IEnumerator WaitForSpawnThenStart()
    {
        float timeout = 10f;
        float elapsed = 0f;

        while (!_runManager.IsSpawned && elapsed < timeout)
        {
            yield return new WaitForSeconds(0.1f);
            elapsed += 0.1f;
        }

        if (!_runManager.IsSpawned)
        {
            Debug.LogError("[GameFlowBootstrapper] RunManager NetworkObject 未在超时时间内生成！");
            yield break;
        }

        Log("RunManager 网络对象就绪，开始对局流程: MainMenu → Lobby → RunInit → PCG → ...");
        _runManager.TransitionTo(RunState.Lobby);
    }

    // ════════════════════════════════════════════════════
    // 连线引用（直接赋值，不用反射）
    // ════════════════════════════════════════════════════

    private void WireReferences()
    {
        // ── PcgMapGenerator ──
        _pcgMapGenerator.profileRegistry = profileRegistry;
        var roomRoot = new GameObject("GeneratedRooms");
        roomRoot.transform.SetParent(_gameFlowGo.transform);
        var resourceRoot = new GameObject("GeneratedResources");
        resourceRoot.transform.SetParent(_gameFlowGo.transform);
        _pcgMapGenerator.generatedRoomRoot = roomRoot.transform;
        _pcgMapGenerator.generatedResourceRoot = resourceRoot.transform;

        // ── PlayerSpawnManager ──
        _playerSpawnManager.playerPrefab = playerPrefab;
        _playerSpawnManager.defaultHeroSO = defaultHeroSO;

        // ── RunManager ──
        _runManager.pcgMapGenerator = _pcgMapGenerator;
        _runManager.missionManager = _missionManager;
        _runManager.monsterSpawnManager = _monsterSpawnManager;
        _runManager.playerSpawnManager = _playerSpawnManager;
        _runManager.runSummaryCalculator = _runSummaryCalculator;
        _runManager.runConfig = runConfig;

        // ── MissionManager ──
        _missionManager.missionLibrary = missionLibrary;
        _missionManager.pcgMapGenerator = _pcgMapGenerator;
        _missionManager.enemySpawnService = _enemySpawnService;
        _missionManager.monsterSpawnManager = _monsterSpawnManager;

        // ── MonsterSpawnManager ──
        _monsterSpawnManager.missionManager = _missionManager;
        _monsterSpawnManager.enemySpawnService = _enemySpawnService;
        _monsterSpawnManager.config = monsterSpawnConfig;

        Log("引用连线完成（直接赋值，无反射）");
    }

    // ════════════════════════════════════════════════════
    // 日志
    // ════════════════════════════════════════════════════

    private void Log(string msg)
    {
        if (verboseLog)
            Debug.Log($"[GameFlowBootstrapper] {msg}");
    }
}
