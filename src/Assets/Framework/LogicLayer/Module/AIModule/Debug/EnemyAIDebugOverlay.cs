using System.Collections.Generic;
using System.Text;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 服务端 EnemyAI 调试面板。
/// 该组件仅用于开发期查看敌人状态机、仿真 LOD、路径结果与兴趣热点，
/// 不参与任何权威 AI 逻辑。
/// </summary>
public class EnemyAIDebugOverlay : MonoBehaviour
{
    /// <summary>
    /// 单条敌人调试快照。
    /// 该结构体用于缓存 OnGUI 绘制所需的字符串与排序字段，
    /// 避免在 GUI 阶段重复访问大量对象状态。
    /// </summary>
    private struct EnemyDebugEntry
    {
        /// <summary>
        /// 敌人显示名称。
        /// </summary>
        public string displayName;

        /// <summary>
        /// 敌人当前状态机状态名。
        /// </summary>
        public string stateName;

        /// <summary>
        /// 敌人当前仿真级别。
        /// </summary>
        public AISimulationLevel simulationLevel;

        /// <summary>
        /// 当前调度决策原因。
        /// </summary>
        public string decisionReason;

        /// <summary>
        /// 当前目标显示文本。
        /// </summary>
        public string targetLabel;

        /// <summary>
        /// 最近目标距离显示文本。
        /// </summary>
        public string nearestDistanceLabel;

        /// <summary>
        /// 当前路径摘要文本。
        /// </summary>
        public string pathSummary;

        /// <summary>
        /// 当前群体移动修正摘要文本。
        /// </summary>
        public string steeringSummary;

        /// <summary>
        /// 当前总摘要文本。
        /// </summary>
        public string summary;

        /// <summary>
        /// 用于排序的仿真级别优先级。
        /// </summary>
        public int sortPriority;
    }

    /// <summary>
    /// 当前是否显示调试面板。
    /// </summary>
    [Header("显示设置")]
    [SerializeField] private bool showOverlay = true;

    /// <summary>
    /// 调试面板开关键。
    /// </summary>
    [SerializeField] private KeyCode toggleKey = KeyCode.F8;

    /// <summary>
    /// 是否仅在服务端显示调试面板。
    /// 默认保持开启，避免客户端误以为自己也在跑权威 AI。
    /// </summary>
    [SerializeField] private bool serverOnly = true;

    /// <summary>
    /// 是否显示 Dormant 级别的敌人。
    /// </summary>
    [SerializeField] private bool showDormantEnemies = true;

    /// <summary>
    /// 面板刷新缓存的间隔。
    /// 调试信息不是权威逻辑，因此不需要每帧全量扫描。
    /// </summary>
    [Header("刷新设置")]
    [SerializeField] private float refreshInterval = 0.5f;

    /// <summary>
    /// 最多显示的敌人数。
    /// 该限制用于避免多人局中敌人数很多时调试窗口过于臃肿。
    /// </summary>
    [SerializeField] private int maxEnemyRows = 32;

    /// <summary>
    /// 调试窗口位置与尺寸。
    /// </summary>
    [Header("窗口设置")]
    [SerializeField] private Rect windowRect = new Rect(20f, 440f, 760f, 560f);

    /// <summary>
    /// 敌人列表滚动位置。
    /// </summary>
    private Vector2 _enemyScrollPosition;

    /// <summary>
    /// 热点列表滚动位置。
    /// </summary>
    private Vector2 _hotspotScrollPosition;

    /// <summary>
    /// 下一次刷新调试缓存的时间戳。
    /// </summary>
    private float _nextRefreshTime;

    /// <summary>
    /// 当前缓存的敌人调试条目。
    /// </summary>
    private readonly List<EnemyDebugEntry> _enemyEntries = new List<EnemyDebugEntry>();

    /// <summary>
    /// 当前缓存的兴趣热点调试条目。
    /// </summary>
    private readonly List<InterestRegionDebugInfo> _hotspotEntries = new List<InterestRegionDebugInfo>();

    /// <summary>
    /// 每帧更新调试面板开关与缓存刷新。
    /// </summary>
    private void Update()
    {
        HandleToggleInput();

        if (!showOverlay)
            return;

        if (!CanDrawOverlay())
            return;

        if (Time.unscaledTime >= _nextRefreshTime)
        {
            RefreshDebugData();
        }
    }

    /// <summary>
    /// 绘制调试面板。
    /// 该界面仅读取缓存结果，不会在 GUI 阶段主动推进 AI。
    /// </summary>
    private void OnGUI()
    {
        if (!showOverlay)
            return;

        if (!CanDrawOverlay())
            return;

        windowRect = GUI.Window(GetInstanceID(), windowRect, DrawWindow, "EnemyAI Server Debug");
    }

    /// <summary>
    /// 处理快捷键开关。
    /// </summary>
    private void HandleToggleInput()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            showOverlay = !showOverlay;
        }
    }

    /// <summary>
    /// 判断当前是否允许绘制调试面板。
    /// </summary>
    /// <returns>返回 true 表示当前网络状态允许显示该调试工具。</returns>
    private bool CanDrawOverlay()
    {
        if (!serverOnly)
            return true;

        var networkManager = NetworkManager.Singleton;
        if (networkManager == null)
            return false;

        return networkManager.IsServer;
    }

    /// <summary>
    /// 刷新调试缓存。
    /// 该方法会重新收集当前场景中的敌人信息与兴趣热点信息。
    /// </summary>
    private void RefreshDebugData()
    {
        _nextRefreshTime = Time.unscaledTime + Mathf.Max(0.1f, refreshInterval);
        RefreshEnemyEntries();
        RefreshHotspotEntries();
    }

    /// <summary>
    /// 刷新敌人调试条目。
    /// 当前阶段使用场景扫描收集敌人，仅用于开发调试，不影响正式权威逻辑。
    /// </summary>
    private void RefreshEnemyEntries()
    {
        _enemyEntries.Clear();

        var enemies = FindObjectsOfType<EnemyActor>();
        for (var i = 0; i < enemies.Length; i++)
        {
            var enemy = enemies[i];
            if (enemy == null || enemy.AIModule == null)
                continue;

            if (!TryBuildEnemyDebugEntry(enemy, out var entry))
                continue;

            if (!showDormantEnemies && entry.simulationLevel == AISimulationLevel.Dormant)
                continue;

            _enemyEntries.Add(entry);
        }

        _enemyEntries.Sort(CompareEnemyEntry);
        if (_enemyEntries.Count > maxEnemyRows)
        {
            _enemyEntries.RemoveRange(maxEnemyRows, _enemyEntries.Count - maxEnemyRows);
        }
    }

    /// <summary>
    /// 刷新兴趣热点调试条目。
    /// </summary>
    private void RefreshHotspotEntries()
    {
        InterestRegionManager.Instance.GetDebugRegions(_hotspotEntries);
        _hotspotEntries.Sort(CompareHotspotEntry);
    }

    /// <summary>
    /// 构建单个敌人的调试条目。
    /// </summary>
    /// <param name="enemy">要读取的敌人对象。</param>
    /// <param name="entry">输出调试条目。</param>
    /// <returns>返回 true 表示成功构建调试条目。</returns>
    private bool TryBuildEnemyDebugEntry(EnemyActor enemy, out EnemyDebugEntry entry)
    {
        entry = default;

        var aiModule = enemy.AIModule;
        if (aiModule == null)
            return false;

        var hasSchedulerInfo = aiModule.TryGetSchedulerDebugInfo(out var schedulerInfo);
        var hasSteeringInfo = aiModule.TryGetLastSteeringDebugInfo(out var steeringDebugInfo);
        var target = aiModule.GetCurrentTarget();
        var targetLabel = target != null ? $"{target.TargetType}({target.ObjectId})" : "None";
        var nearestDistanceLabel = hasSchedulerInfo && !float.IsPositiveInfinity(schedulerInfo.nearestTargetDistance)
            ? schedulerInfo.nearestTargetDistance.ToString("F1")
            : "INF";

        entry = new EnemyDebugEntry
        {
            displayName = $"{enemy.name}({enemy.ObjectId})",
            stateName = aiModule.GetCurrentStateName(),
            simulationLevel = hasSchedulerInfo ? schedulerInfo.currentLevel : AISimulationLevel.Full,
            decisionReason = hasSchedulerInfo ? schedulerInfo.decisionReason : "NoSchedulerState",
            targetLabel = targetLabel,
            nearestDistanceLabel = nearestDistanceLabel,
            pathSummary = "Path=N/A",
            steeringSummary = hasSteeringInfo ? BuildSteeringSummary(steeringDebugInfo) : "Steer=None",
            summary = BuildEnemySummary(
                enemy,
                aiModule,
                hasSchedulerInfo,
                schedulerInfo,
                hasSteeringInfo,
                steeringDebugInfo,
                targetLabel,
                nearestDistanceLabel),
            sortPriority = GetSimulationPriority(hasSchedulerInfo ? schedulerInfo.currentLevel : AISimulationLevel.Full)
        };

        return true;
    }

    /// <summary>
    /// 构建敌人的调试摘要文本。
    /// </summary>
    /// <param name="enemy">要读取的敌人对象。</param>
    /// <param name="aiModule">敌人的 AI 模块。</param>
    /// <param name="hasSchedulerInfo">是否读取到了调度调试信息。</param>
    /// <param name="schedulerInfo">调度调试信息。</param>
    /// <param name="hasSteeringInfo">是否读取到了群体移动修正调试信息。</param>
    /// <param name="steeringDebugInfo">群体移动修正调试信息。</param>
    /// <param name="targetLabel">目标显示文本。</param>
    /// <param name="nearestDistanceLabel">最近目标距离文本。</param>
    /// <returns>返回可直接绘制的调试摘要。</returns>
    private string BuildEnemySummary(
        EnemyActor enemy,
        EnemyAIModule aiModule,
        bool hasSchedulerInfo,
        AISchedulerDebugInfo schedulerInfo,
        bool hasSteeringInfo,
        AISteeringDebugInfo steeringDebugInfo,
        string targetLabel,
        string nearestDistanceLabel)
    {
        var builder = new StringBuilder(320);
        builder.Append(enemy.name)
            .Append(" | State=").Append(aiModule.GetCurrentStateName())
            .Append(" | Sim=").Append(hasSchedulerInfo ? schedulerInfo.currentLevel.ToString() : "Unknown")
            .Append(" | Target=").Append(targetLabel)
            .Append(" | Nearest=").Append(nearestDistanceLabel);

        if (hasSchedulerInfo)
        {
            builder.Append(" | Reason=").Append(schedulerInfo.decisionReason)
                .Append(" | Interest=").Append(schedulerInfo.hasNearbyInterest ? "Y" : "N")
                .Append(" | Combat=").Append(schedulerInfo.isInCombat ? "Y" : "N")
                .Append(" | RecentHit=").Append(schedulerInfo.wasRecentlyDamaged ? "Y" : "N");
        }

        builder.Append(" | Path=N/A");

        if (hasSteeringInfo)
        {
            builder.Append(" | ").Append(BuildSteeringSummary(steeringDebugInfo));
        }
        else
        {
            builder.Append(" | Steer=None");
        }

        if (aiModule.HasRecentTargetMemory())
        {
            var lastKnownPosition = aiModule.GetLastKnownTargetPosition();
            builder.Append(" | Memory=(")
                .Append(lastKnownPosition.x.ToString("F1")).Append(",")
                .Append(lastKnownPosition.y.ToString("F1")).Append(",")
                .Append(lastKnownPosition.z.ToString("F1")).Append(")");
        }
        else
        {
            builder.Append(" | Memory=None");
        }

        return builder.ToString();
    }

    /// <summary>
    /// 构建群体移动修正调试摘要文本。
    /// </summary>
    /// <param name="steeringDebugInfo">要显示的 steering 调试信息。</param>
    /// <returns>返回可直接绘制的 steering 摘要文本。</returns>
    private string BuildSteeringSummary(AISteeringDebugInfo steeringDebugInfo)
    {
        var builder = new StringBuilder(192);
        builder.Append("Steer=N").Append(steeringDebugInfo.neighborCount)
            .Append(steeringDebugInfo.usedSpatialBuckets ? "/Bucket" : "/NoBucket")
            .Append(steeringDebugInfo.usedSameRegionFilter ? "/SameRoom" : "/AllRoom")
            .Append("/Obs=").Append(steeringDebugInfo.obstacleDetected ? "Y" : "N")
            .Append("/Sep=(")
            .Append(steeringDebugInfo.separationDirection.x.ToString("F1")).Append(",")
            .Append(steeringDebugInfo.separationDirection.z.ToString("F1")).Append(")")
            .Append("/Avoid=(")
            .Append(steeringDebugInfo.obstacleAvoidanceDirection.x.ToString("F1")).Append(",")
            .Append(steeringDebugInfo.obstacleAvoidanceDirection.z.ToString("F1")).Append(")");

        return builder.ToString();
    }

    /// <summary>
    /// 根据仿真级别返回排序优先级。
    /// Full 敌人优先显示，其次 Reduced，最后 Dormant。
    /// </summary>
    /// <param name="simulationLevel">要转换的仿真级别。</param>
    /// <returns>返回用于排序的整数优先级。</returns>
    private int GetSimulationPriority(AISimulationLevel simulationLevel)
    {
        return simulationLevel switch
        {
            AISimulationLevel.Full => 0,
            AISimulationLevel.Reduced => 1,
            AISimulationLevel.Dormant => 2,
            _ => 3
        };
    }

    /// <summary>
    /// 比较两个敌人调试条目。
    /// </summary>
    /// <param name="left">左侧条目。</param>
    /// <param name="right">右侧条目。</param>
    /// <returns>返回排序结果。</returns>
    private int CompareEnemyEntry(EnemyDebugEntry left, EnemyDebugEntry right)
    {
        var priorityCompare = left.sortPriority.CompareTo(right.sortPriority);
        if (priorityCompare != 0)
            return priorityCompare;

        return string.Compare(left.displayName, right.displayName, System.StringComparison.Ordinal);
    }

    /// <summary>
    /// 比较两个兴趣热点调试条目。
    /// 剩余时间更长的热点优先显示，便于观察当前活跃区。
    /// </summary>
    /// <param name="left">左侧热点条目。</param>
    /// <param name="right">右侧热点条目。</param>
    /// <returns>返回排序结果。</returns>
    private int CompareHotspotEntry(InterestRegionDebugInfo left, InterestRegionDebugInfo right)
    {
        return right.remainingTime.CompareTo(left.remainingTime);
    }

    /// <summary>
    /// 绘制调试窗口内容。
    /// </summary>
    /// <param name="windowId">GUI 窗口 ID。</param>
    private void DrawWindow(int windowId)
    {
        DrawHeader();
        GUILayout.Space(8f);
        DrawEnemySection();
        GUILayout.Space(8f);
        DrawHotspotSection();
        GUI.DragWindow(new Rect(0f, 0f, windowRect.width, 24f));
    }

    /// <summary>
    /// 绘制窗口头部信息。
    /// </summary>
    private void DrawHeader()
    {
        GUILayout.BeginVertical("box");
        GUILayout.Label($"NetworkMode: {(NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer ? "ServerAuthority" : "NonServer")}");
        GUILayout.Label($"EnemyRows: {_enemyEntries.Count} | HotspotRows: {_hotspotEntries.Count}");

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("RefreshNow", GUILayout.Width(100f)))
        {
            RefreshDebugData();
        }

        showDormantEnemies = GUILayout.Toggle(showDormantEnemies, "ShowDormant");
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
    }

    /// <summary>
    /// 绘制敌人调试区域。
    /// </summary>
    private void DrawEnemySection()
    {
        GUILayout.BeginVertical("box");
        GUILayout.Label("=== Enemy AI ===");

        _enemyScrollPosition = GUILayout.BeginScrollView(_enemyScrollPosition, GUILayout.Height(300f));
        for (var i = 0; i < _enemyEntries.Count; i++)
        {
            GUILayout.Label(_enemyEntries[i].summary);
        }

        if (_enemyEntries.Count == 0)
        {
            GUILayout.Label("No enemy debug rows.");
        }

        GUILayout.EndScrollView();
        GUILayout.EndVertical();
    }

    /// <summary>
    /// 绘制兴趣热点调试区域。
    /// </summary>
    private void DrawHotspotSection()
    {
        GUILayout.BeginVertical("box");
        GUILayout.Label("=== Interest Hotspots ===");

        _hotspotScrollPosition = GUILayout.BeginScrollView(_hotspotScrollPosition, GUILayout.Height(180f));
        for (var i = 0; i < _hotspotEntries.Count; i++)
        {
            GUILayout.Label(BuildHotspotSummary(_hotspotEntries[i]));
        }

        if (_hotspotEntries.Count == 0)
        {
            GUILayout.Label("No active hotspots.");
        }

        GUILayout.EndScrollView();
        GUILayout.EndVertical();
    }

    /// <summary>
    /// 构建兴趣热点摘要文本。
    /// </summary>
    /// <param name="hotspotInfo">要显示的热点调试信息。</param>
    /// <returns>返回可直接绘制的热点摘要文本。</returns>
    private string BuildHotspotSummary(InterestRegionDebugInfo hotspotInfo)
    {
        var builder = new StringBuilder(160);
        builder.Append('#').Append(hotspotInfo.id)
            .Append(" | Type=").Append(hotspotInfo.sourceType)
            .Append(" | Source=").Append(hotspotInfo.sourceObjectId)
            .Append(" | R=").Append(hotspotInfo.radius.ToString("F1"))
            .Append(" | Remain=").Append(hotspotInfo.remainingTime.ToString("F1"));

        if (!string.IsNullOrEmpty(hotspotInfo.debugTag))
        {
            builder.Append(" | Tag=").Append(hotspotInfo.debugTag);
        }

        builder.Append(" | Pos=(")
            .Append(hotspotInfo.center.x.ToString("F1")).Append(",")
            .Append(hotspotInfo.center.y.ToString("F1")).Append(",")
            .Append(hotspotInfo.center.z.ToString("F1")).Append(")");

        return builder.ToString();
    }
}
