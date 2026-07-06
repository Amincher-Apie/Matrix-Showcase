using System.Collections;
using System.Collections.Generic;
using Matrix.PCG.Instances;
using UnityEngine;

namespace Matrix.PCG
{
    /// <summary>
    /// PCG map generation entry.
    /// Strict runtime flow (no fallback): GeneratePackage -> Registry(style->Profile) -> StyleOptions -> FinalRequest.
    /// Profile holds no Seed, no TaskInput — those are runtime-only inputs from the package.
    /// </summary>
    public sealed class PcgMapGenerator : MonoBehaviour
    {
        [Header("Config Assets")]
        [Tooltip("Optional profile only for manual editor test generation.")]
        [SerializeField]
        private PcgGenerationProfile defaultProfile;

        [Tooltip("Required runtime mapping table: style key -> profile.")]
        [SerializeField]
        internal PcgGenerationProfileRegistry profileRegistry;

        [Header("Generated Content")]
        [SerializeField]
        internal Transform generatedRoomRoot;

        [SerializeField]
        internal Transform generatedResourceRoot;

        [Header("Runtime Controls")]
        [SerializeField]
        private bool clearGeneratedContentBeforeBuild = true;

        [SerializeField]
        private bool generateOnStart;

        [SerializeField]
        private bool verboseLog;

        [SerializeField]
        [Min(1)]
        private int maxStitchAttempts = 4;

        [Header("Failure Diagnostics")]
        [Tooltip("Write a JSON report when all budget and graph variants fail.")]
        [SerializeField]
        private bool writeFailureDiagnostics = true;

        [Tooltip("Relative to Application.persistentDataPath unless an absolute path is provided.")]
        [SerializeField]
        private string failureDiagnosticsDirectory = "PCGFailureReports";

        [Header("Test Mode (Editor)")]
        [Tooltip("Enable to use test configuration below instead of MissionSystem package.")]
        [SerializeField]
        private bool useTestMode;

        [Tooltip("Style key resolved via the profile registry.")]
        [SerializeField]
        private string testStyleKey = string.Empty;

        [Tooltip("Seed for this test run. 0 = use timestamp at generation time.")]
        [SerializeField]
        private int testSeed = 1;

        [Tooltip("Enable to fix the seed. Disable to randomize every run.")]
        [SerializeField]
        private bool testUseDeterministicSeed = true;

        [Tooltip("Side task types for this test run. Max 4 slots.")]
        [SerializeField]
        private SideTaskType[] testSideTasks = new SideTaskType[0];

        [Header("Test Step Generation")]
        [Tooltip("Only affects GenerateTest in Play Mode. The runtime Generate(...) entry stays synchronous.")]
        [SerializeField]
        private bool testStepGeneration;

        [Tooltip("Delay after each visible test generation step. 0 = one frame per step.")]
        [SerializeField]
        [Min(0f)]
        private float testStepDelaySeconds = 0.15f;

        private Coroutine testGenerationCoroutine;
        private readonly Dictionary<PcgRoomRoot, TestRoomVisibilityState> testVisibilityStates = new Dictionary<PcgRoomRoot, TestRoomVisibilityState>();

        private sealed class TestRoomVisibilityState
        {
            public Renderer[] Renderers;
            public bool[] OriginalEnabled;
        }

        public PcgMapGenerationResult LastResult { get; private set; }

        /// <summary>
        /// 当地图生成成功完成时触发。
        /// 使用此事件来驱动后续流程，如烘焙 NavMesh、生成敌人等。
        /// </summary>
        public event System.Action<PcgMapGenerationResult> OnGenerationCompleted;

        /// <summary>
        /// The deterministic seed used in the most recent generation attempt.
        /// Valid after a successful call to Generate().
        /// </summary>
        public int CurrentSeed => LastResult != null ? LastResult.Seed : 0;

        /// <summary>
        /// Returns the default profile used for generation. Available at all times.
        /// </summary>
        public PcgGenerationProfile DefaultProfile => defaultProfile;

        private void Start()
        {
            if (!generateOnStart)
            {
                return;
            }

            if (useTestMode)
            {
                if (string.IsNullOrWhiteSpace(testStyleKey))
                {
                    Debug.LogError("[PCG] Test mode is enabled but StyleKey is empty.", this);
                    LastResult = null;
                    return;
                }

                GenerateTest();
                return;
            }

            if (defaultProfile == null)
            {
                Debug.LogError("[PCG] generateOnStart is enabled but default profile is not assigned.", this);
                LastResult = null;
                return;
            }

            GenerateWithDefaultProfile();
        }

        [ContextMenu("Generate With Default Profile")]
        public void GenerateWithDefaultProfile()
        {
            StopTestStepGenerationIfRunning();

            if (defaultProfile == null)
            {
                Debug.LogError("[PCG] GenerateWithDefaultProfile failed: default profile is not assigned.", this);
                LastResult = null;
                return;
            }

            PcgStyleOptions styleOptions = defaultProfile.CreateRuntimeStyleOptions();
            int seed = ResolveDefaultSeed();
            MapGenerationRequest request = BuildRequest(styleOptions, seed, CreateDefaultTaskInput());
            NormalizeRequest(request);

            if (!ValidateRequest(request, "DefaultProfile:" + defaultProfile.name))
            {
                LastResult = null;
                return;
            }

            LastResult = GenerateResolvedRequest(request, "DefaultProfile:" + defaultProfile.name);
        }

        [ContextMenu("Randomize Seed")]
        public void RandomizeSeed()
        {
#if UNITY_EDITOR
            testSeed = unchecked((int)System.DateTime.Now.Ticks);
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        [ContextMenu("Next Seed")]
        public void NextSeed()
        {
#if UNITY_EDITOR
            testSeed += 1;
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        [ContextMenu("Generate (Test Mode)")]
        public void GenerateTest()
        {
            PcgGeneratePackage package = BuildTestPackage();

            if (testStepGeneration && Application.isPlaying)
            {
                StartTestStepGeneration(package);
                return;
            }

            StopTestStepGenerationIfRunning();

            if (testStepGeneration && !Application.isPlaying)
            {
                Debug.LogWarning("[PCG Test] Step generation requires Play Mode. Running immediate generation instead.", this);
            }

            LastResult = Generate(package);
            if (LastResult != null)
            {
                DebugLog.Info("PCG.MapGenerator", $"[PCG Test] Done. Seed={LastResult.Seed}, Rooms={LastResult.PlacedRooms.Count}", this);
            }
        }

        private PcgGeneratePackage BuildTestPackage()
        {
            int seed = testUseDeterministicSeed ? testSeed : unchecked((int)System.DateTime.Now.Ticks);

            var sideTasks = new List<SideTaskInput>();
            foreach (var t in testSideTasks)
            {
                sideTasks.Add(new SideTaskInput { TaskType = t });
            }

            return new PcgGeneratePackage
            {
                StyleKey = testStyleKey,
                Seed = seed,
                TaskInput = new MapTaskInput
                {
                    PrimaryTask = new PrimaryTaskInput { TaskType = PrimaryTaskType.BossBattle },
                    SideTasks = sideTasks,
                    TaskProvider = "TestMode"
                },
                RequestSource = "TestMode"
            };
        }

        // Legacy compatibility entry for existing inspector buttons or external calls.
        public void GenerateWithDefaultRequest()
        {
            GenerateWithDefaultProfile();
        }

        /// <summary>
        /// Main runtime entry for external systems.
        /// </summary>
        public PcgMapGenerationResult Generate(PcgGeneratePackage package)
        {
            StopTestStepGenerationIfRunning();

            MapGenerationRequest request;
            string requestSource;
            if (!TryBuildFinalRequest(package, out request, out requestSource))
            {
                LastResult = null;
                return null;
            }

            LastResult = GenerateResolvedRequest(request, requestSource);
            return LastResult;
        }

        /// <summary>
        /// Compatibility entry for callers that still build MapGenerationRequest directly.
        /// </summary>
        public PcgMapGenerationResult Generate(MapGenerationRequest request)
        {
            StopTestStepGenerationIfRunning();

            if (request == null)
            {
                Debug.LogError("[PCG] MapGenerationRequest is null.", this);
                return null;
            }

            MapGenerationRequest runtimeRequest = PcgRequestCloneUtility.CloneRequest(request);
            NormalizeRequest(runtimeRequest);

            if (!ValidateRequest(runtimeRequest, "DirectRequest"))
            {
                return null;
            }

            LastResult = GenerateResolvedRequest(runtimeRequest, "DirectRequest");
            return LastResult;
        }

        private bool TryBuildFinalRequest(PcgGeneratePackage package, out MapGenerationRequest request, out string requestSource)
        {
            request = null;
            requestSource = "Unknown";

            if (package == null)
            {
                Debug.LogError("[PCG] Generate failed: package is null.", this);
                return false;
            }

            if (profileRegistry == null)
            {
                Debug.LogError("[PCG] Generate failed: profile registry is not assigned.", this);
                return false;
            }

            string styleKey = package.StyleKey != null ? package.StyleKey.Trim() : string.Empty;
            if (string.IsNullOrWhiteSpace(styleKey))
            {
                Debug.LogError("[PCG] Generate failed: package style key is empty.", this);
                return false;
            }

            string reason;
            PcgGenerationProfile profile;
            if (!profileRegistry.TryGetProfile(styleKey, out profile, out reason))
            {
                Debug.LogError("[PCG] Generate failed: " + reason, this);
                return false;
            }

            if (!string.IsNullOrWhiteSpace(reason))
            {
                Debug.LogWarning("[PCG] " + reason, this);
            }

            if (package.TaskInput == null)
            {
                Debug.LogError("[PCG] Generate failed: package TaskInput is null.", this);
                return false;
            }

            PcgStyleOptions styleOptions = profile.CreateRuntimeStyleOptions();
            request = BuildRequest(styleOptions, package.Seed, package.TaskInput);

            NormalizeRequest(request);

            requestSource = "Style:" + styleKey;
            if (!string.IsNullOrWhiteSpace(package.RequestSource))
            {
                requestSource = requestSource + "|Caller:" + package.RequestSource;
            }

            if (!ValidateRequest(request, requestSource))
            {
                return false;
            }

            return true;
        }

        private MapGenerationRequest BuildRequest(PcgStyleOptions styleOptions, int seed, MapTaskInput taskInput)
        {
            MapGenerationRequest request = new MapGenerationRequest
            {
                Seed = seed,
                TaskInput = PcgRequestCloneUtility.CloneTaskInput(taskInput),
                ScaleSettings = PcgRequestCloneUtility.CloneScaleSettings(styleOptions.ScaleSettings),
                RoomPrefabPools = PcgRequestCloneUtility.CloneRoomPrefabPools(styleOptions.RoomPrefabPools),
                ResourceSpawnOptions = PcgRequestCloneUtility.CloneResourceSpawnOptions(styleOptions.ResourceSpawnOptions),
                WorldOrigin = styleOptions.WorldOrigin,
                CloseUnusedExits = styleOptions.CloseUnusedExits,
                SpawnResources = styleOptions.SpawnResources,
                MinimapIcons = PcgRequestCloneUtility.CloneMinimapIcons(styleOptions.MinimapIcons)
            };

            PcgRequestCloneUtility.EnsureNonNullCollections(request);
            return request;
        }

        /// <summary>
        /// Derives the exact TargetRoomCount from the input seed using the system's deterministic random,
        /// within the hard bounds of [2x unique prefab count, 50].
        /// </summary>
        private int ComputeTargetRoomCount(MapGenerationRequest request, int seed)
        {
            DeterministicRandom rng = new DeterministicRandom(seed);

            int uniquePrefabCount = CountUniquePrefabs(request.RoomPrefabPools);
            int minRooms = Mathf.Max(4, uniquePrefabCount * 2);
            int maxRooms = 50;

            int range = Mathf.Max(0, maxRooms - minRooms);
            if (range == 0)
            {
                return minRooms;
            }

            int sampled = rng.NextInt(range + 1);
            return minRooms + sampled;
        }

        private static int CountUniquePrefabs(List<RoomPrefabPool> pools)
        {
            if (pools == null)
            {
                return 0;
            }

            HashSet<PcgRoomRoot> unique = new HashSet<PcgRoomRoot>();
            for (int i = 0; i < pools.Count; i++)
            {
                RoomPrefabPool pool = pools[i];
                if (pool == null || pool.Prefabs == null)
                {
                    continue;
                }

                for (int p = 0; p < pool.Prefabs.Count; p++)
                {
                    if (pool.Prefabs[p] != null)
                    {
                        unique.Add(pool.Prefabs[p]);
                    }
                }
            }

            return Mathf.Max(1, unique.Count);
        }

        private static int ResolveDefaultSeed()
        {
            return unchecked((int)System.DateTime.Now.Ticks);
        }

        private static MapTaskInput CreateDefaultTaskInput()
        {
            return new MapTaskInput
            {
                PrimaryTask = new PrimaryTaskInput { TaskType = PrimaryTaskType.BossBattle },
                SideTasks = new List<SideTaskInput>()
            };
        }

        private PcgMapGenerationResult GenerateResolvedRequest(MapGenerationRequest request, string requestSource)
        {
            int fixedSeed = request.Seed != 0 ? request.Seed : unchecked((int)System.DateTime.Now.Ticks);
            int originalTargetRooms = request.ScaleSettings.TargetRoomCount;

            const int degradeStep = 4;
            int sideTaskCount = request.TaskInput?.SideTasks?.Count ?? 0;
            int minTargetRooms = Mathf.Max(12, 4 + sideTaskCount + 2);
            int maxGraphAttempts = Mathf.Max(1, maxStitchAttempts);
            PcgGenerationFailureReport failureReport = writeFailureDiagnostics
                ? PcgGenerationFailureReporter.CreateReport(request, requestSource, fixedSeed, originalTargetRooms, minTargetRooms, maxGraphAttempts)
                : null;

            if (verboseLog)
            {
                DebugLog.Info("PCG.MapGenerator", $"[PCG] Start | InputSeed={request.Seed}, FixedSeed={fixedSeed}, OriginalTargetRooms={originalTargetRooms}, MaxGraphAttemptsPerBudget={maxGraphAttempts}, MinTargetRooms={minTargetRooms}, Source={requestSource}", this);
            }

            for (int budgetAttempt = 0; ; budgetAttempt++)
            {
                int currentTargetRooms = originalTargetRooms - (budgetAttempt * degradeStep);
                if (currentTargetRooms < minTargetRooms)
                    currentTargetRooms = minTargetRooms;

                request.ScaleSettings.TargetRoomCount = currentTargetRooms;

                if (verboseLog && budgetAttempt > 0)
                {
                    DebugLog.Info("PCG.MapGenerator", $"[PCG] Budget degrade | TargetRooms={currentTargetRooms}, BudgetAttempt={budgetAttempt}", this);
                }

                for (int graphAttempt = 0; graphAttempt < maxGraphAttempts; graphAttempt++)
                {
                    if (clearGeneratedContentBeforeBuild || budgetAttempt > 0 || graphAttempt > 0)
                        ClearGeneratedContent();

                    int graphSeed = DeriveSeed(fixedSeed, 0, budgetAttempt, graphAttempt);
                    DeterministicRandom graphRng = new DeterministicRandom(graphSeed);
                    RoomGraph graph = RoomGraphBuilder.Build(request, ref graphRng);

                    int roleSeed = DeriveSeed(fixedSeed, 1, budgetAttempt, graphAttempt);
                    DeterministicRandom roleRng = new DeterministicRandom(roleSeed);
                    List<TaskTriggerConnection> taskTriggerConnections = new List<TaskTriggerConnection>();
                    RoomRoleAllocator.AssignRoles(graph, request.TaskInput, ref roleRng, taskTriggerConnections);

                    PcgMapGenerationResult result = new PcgMapGenerationResult
                    {
                        Seed = fixedSeed,
                        Request = request,
                        Graph = graph,
                        TaskTriggerConnections = taskTriggerConnections,
                        RoomRoot = generatedRoomRoot,
                        RequestedTargetRooms = originalTargetRooms,
                        FinalTargetRooms = currentTargetRooms,
                        BudgetAttempt = budgetAttempt,
                        GraphAttempt = graphAttempt
                    };

                    int stitchSeed = DeriveSeed(fixedSeed, 2, budgetAttempt, graphAttempt);
                    DeterministicRandom stitchRng = new DeterministicRandom(stitchSeed);
                    Dictionary<int, PcgPlacedRoom> placedByNode = InstantiateRooms(request, graph, ref stitchRng, result);
                    List<PcgStitchFailureRecord> stitchFailures = new List<PcgStitchFailureRecord>();
                    bool stitchSuccess = RoomStitcher.Stitch(graph, placedByNode, request.CloseUnusedExits, ref stitchRng, result.Connections, result.ClosedDoors, stitchFailures);

                    if (!stitchSuccess)
                    {
                        AddFailureAttemptReport(
                            failureReport,
                            budgetAttempt,
                            graphAttempt,
                            currentTargetRooms,
                            graphSeed,
                            roleSeed,
                            stitchSeed,
                            graph,
                            result,
                            placedByNode,
                            stitchFailures);

                        DebugLog.Warning("PCG.MapGenerator", $"[PCG] Stitch failed | TargetRooms={currentTargetRooms}, BudgetAttempt={budgetAttempt}, GraphAttempt={graphAttempt}, FixedSeed={fixedSeed}", this);
                        continue;
                    }

                    CollectSpawnPoints(result);
                    if (request.SpawnResources)
                    {
                        int resourceSeed = DeriveSeed(fixedSeed, 3, budgetAttempt, graphAttempt);
                        DeterministicRandom resourceRng = new DeterministicRandom(resourceSeed);
                        SpawnResources(request, result, ref resourceRng);
                    }

                    DebugLog.Info("PCG.MapGenerator", $"[PCG] Generation succeeded | FixedSeed={fixedSeed}, RequestedTargetRooms={originalTargetRooms}, FinalTargetRooms={currentTargetRooms}, BudgetAttempt={budgetAttempt}, GraphAttempt={graphAttempt}, Rooms={result.PlacedRooms.Count}, Connections={result.Connections.Count}, TaskTriggers={result.TaskTriggerConnections.Count}", this);

                    OnGenerationCompleted?.Invoke(result);
                    return result;
                }

                if (currentTargetRooms <= minTargetRooms)
                    break;
            }

            ClearGeneratedContent();
            WriteFailureDiagnosticsIfNeeded(failureReport, "[PCG]");
            Debug.LogError($"[PCG] Map generation aborted | FixedSeed={fixedSeed}, OriginalTargetRooms={originalTargetRooms}, MinTargetRooms={minTargetRooms}, all budget+graph variants exhausted.", this);
            return null;
        }

        private void StartTestStepGeneration(PcgGeneratePackage package)
        {
            StopTestStepGenerationIfRunning();
            testVisibilityStates.Clear();
            LastResult = null;
            testGenerationCoroutine = StartCoroutine(GenerateTestStepByStep(package));
        }

        private IEnumerator GenerateTestStepByStep(PcgGeneratePackage package)
        {
            MapGenerationRequest request;
            string requestSource;
            if (!TryBuildFinalRequest(package, out request, out requestSource))
            {
                LastResult = null;
                testGenerationCoroutine = null;
                yield break;
            }

            yield return GenerateResolvedRequestStepByStep(request, requestSource);

            if (LastResult != null)
            {
                DebugLog.Info("PCG.MapGenerator", $"[PCG Test] Done. Seed={LastResult.Seed}, Rooms={LastResult.PlacedRooms.Count}", this);
            }

            testGenerationCoroutine = null;
        }

        private IEnumerator GenerateResolvedRequestStepByStep(MapGenerationRequest request, string requestSource)
        {
            int fixedSeed = request.Seed != 0 ? request.Seed : unchecked((int)System.DateTime.Now.Ticks);
            int originalTargetRooms = request.ScaleSettings.TargetRoomCount;

            const int degradeStep = 4;
            int sideTaskCount = request.TaskInput?.SideTasks?.Count ?? 0;
            int minTargetRooms = Mathf.Max(12, 4 + sideTaskCount + 2);
            int maxGraphAttempts = Mathf.Max(1, maxStitchAttempts);
            PcgGenerationFailureReport failureReport = writeFailureDiagnostics
                ? PcgGenerationFailureReporter.CreateReport(request, requestSource, fixedSeed, originalTargetRooms, minTargetRooms, maxGraphAttempts)
                : null;

            DebugLog.Info("PCG.MapGenerator", $"[PCG Test] Step generation started | InputSeed={request.Seed}, FixedSeed={fixedSeed}, OriginalTargetRooms={originalTargetRooms}, MaxGraphAttemptsPerBudget={maxGraphAttempts}, MinTargetRooms={minTargetRooms}, Source={requestSource}", this);
            yield return WaitForTestStep("Request prepared");

            for (int budgetAttempt = 0; ; budgetAttempt++)
            {
                int currentTargetRooms = originalTargetRooms - (budgetAttempt * degradeStep);
                if (currentTargetRooms < minTargetRooms)
                    currentTargetRooms = minTargetRooms;

                request.ScaleSettings.TargetRoomCount = currentTargetRooms;

                if (budgetAttempt > 0)
                {
                    DebugLog.Info("PCG.MapGenerator", $"[PCG Test] Budget degrade | TargetRooms={currentTargetRooms}, BudgetAttempt={budgetAttempt}", this);
                    yield return WaitForTestStep("Budget degraded");
                }

                for (int graphAttempt = 0; graphAttempt < maxGraphAttempts; graphAttempt++)
                {
                    if (clearGeneratedContentBeforeBuild || budgetAttempt > 0 || graphAttempt > 0)
                    {
                        ClearGeneratedContent();
                        testVisibilityStates.Clear();
                        yield return WaitForTestStep("Cleared generated content");
                    }

                    int graphSeed = DeriveSeed(fixedSeed, 0, budgetAttempt, graphAttempt);
                    DeterministicRandom graphRng = new DeterministicRandom(graphSeed);
                    RoomGraph graph = RoomGraphBuilder.Build(request, ref graphRng);
                    DebugLog.Info("PCG.MapGenerator", $"[PCG Test] Graph built | Nodes={graph.NodeCount}, BudgetAttempt={budgetAttempt}, GraphAttempt={graphAttempt}", this);
                    yield return WaitForTestStep("Graph built");

                    int roleSeed = DeriveSeed(fixedSeed, 1, budgetAttempt, graphAttempt);
                    DeterministicRandom roleRng = new DeterministicRandom(roleSeed);
                    List<TaskTriggerConnection> taskTriggerConnections = new List<TaskTriggerConnection>();
                    RoomRoleAllocator.AssignRoles(graph, request.TaskInput, ref roleRng, taskTriggerConnections);
                    DebugLog.Info("PCG.MapGenerator", $"[PCG Test] Roles assigned | TaskTriggers={taskTriggerConnections.Count}", this);
                    yield return WaitForTestStep("Roles assigned");

                    PcgMapGenerationResult result = new PcgMapGenerationResult
                    {
                        Seed = fixedSeed,
                        Request = request,
                        Graph = graph,
                        TaskTriggerConnections = taskTriggerConnections,
                        RoomRoot = generatedRoomRoot,
                        RequestedTargetRooms = originalTargetRooms,
                        FinalTargetRooms = currentTargetRooms,
                        BudgetAttempt = budgetAttempt,
                        GraphAttempt = graphAttempt
                    };

                    int stitchSeed = DeriveSeed(fixedSeed, 2, budgetAttempt, graphAttempt);
                    DeterministicRandom stitchRng = new DeterministicRandom(stitchSeed);
                    Transform roomParent = ResolveRoomRoot();
                    result.RoomRoot = roomParent;

                    Dictionary<int, PcgPlacedRoom> placedByNode = new Dictionary<int, PcgPlacedRoom>();

                    for (int i = 0; i < graph.NodeCount; i++)
                    {
                        RoomGraphNode node = graph.GetNode(i);
                        TryInstantiateRoom(request, graph, node, ref stitchRng, result, placedByNode, roomParent);

                        if (placedByNode.TryGetValue(node.Id, out PcgPlacedRoom placedRoom) && placedRoom.RoomInstance != null)
                        {
                            HideRoomVisualForTest(placedRoom.RoomInstance, testVisibilityStates);
                        }
                    }

                    yield return WaitForTestStep($"Rooms prepared hidden {result.PlacedRooms.Count}/{graph.NodeCount}");

                    // Reveal start room before stitching begins.
                    int startNodeId = FindStartPlacedNodeId(placedByNode);
                    if (startNodeId >= 0 && placedByNode.TryGetValue(startNodeId, out PcgPlacedRoom startPlacedRoom) && startPlacedRoom.RoomInstance != null)
                    {
                        RestoreRoomVisualForTest(startPlacedRoom.RoomInstance, testVisibilityStates);
                        yield return WaitForTestStep($"Start room revealed Node={startNodeId}");
                    }

                    // Stepwise stitch: reveal each room immediately after it is connected.
                    int placedCount = 1; // start room already counted
                    List<PcgStitchFailureRecord> stitchFailures = new List<PcgStitchFailureRecord>();
                    var stepper = RoomStitcher.StitchStepwise(graph, placedByNode, request.CloseUnusedExits, stitchRng, result.Connections, result.ClosedDoors, stitchFailures);
                    while (stepper.MoveNext())
                    {
                        var step = (StitchStep)stepper.Current;
                        if (placedByNode.TryGetValue(step.PlacedNodeId, out PcgPlacedRoom placedRoom) && placedRoom.RoomInstance != null)
                        {
                            RestoreRoomVisualForTest(placedRoom.RoomInstance, testVisibilityStates);
                            placedCount++;
                            yield return WaitForTestStep($"Room placed {placedCount}/{graph.NodeCount} Node={step.PlacedNodeId} Role={placedRoom.Role}");
                        }
                    }

                    int unplacedRoomCount = CountUnplacedRooms(placedByNode);
                    bool stitchSuccess = unplacedRoomCount == 0;
                    if (!stitchSuccess)
                    {
                        AddFailureAttemptReport(
                            failureReport,
                            budgetAttempt,
                            graphAttempt,
                            currentTargetRooms,
                            graphSeed,
                            roleSeed,
                            stitchSeed,
                            graph,
                            result,
                            placedByNode,
                            stitchFailures);

                        DebugLog.Warning("PCG.MapGenerator", $"[PCG Test] Stitch failed | TargetRooms={currentTargetRooms}, BudgetAttempt={budgetAttempt}, GraphAttempt={graphAttempt}, FixedSeed={fixedSeed}, UnplacedRooms={unplacedRoomCount}", this);
                        yield return WaitForTestStep("Stitch failed");
                        continue;
                    }

                    DebugLog.Info("PCG.MapGenerator", $"[PCG Test] Stitch succeeded | Connections={result.Connections.Count}, ClosedDoors={result.ClosedDoors.Count}", this);
                    yield return WaitForTestStep("Stitch succeeded");

                    CollectSpawnPoints(result);
                    yield return WaitForTestStep($"Spawn points collected {result.SpawnPoints.Count}");

                    if (request.SpawnResources)
                    {
                        int resourceSeed = DeriveSeed(fixedSeed, 3, budgetAttempt, graphAttempt);
                        DeterministicRandom resourceRng = new DeterministicRandom(resourceSeed);
                        SpawnResources(request, result, ref resourceRng);
                        yield return WaitForTestStep($"Resources spawned {result.ResourceSpawns.Count}");
                    }

                    LastResult = result;
                    DebugLog.Info("PCG.MapGenerator", $"[PCG Test] Generation succeeded | FixedSeed={fixedSeed}, RequestedTargetRooms={originalTargetRooms}, FinalTargetRooms={currentTargetRooms}, BudgetAttempt={budgetAttempt}, GraphAttempt={graphAttempt}, Rooms={result.PlacedRooms.Count}, Connections={result.Connections.Count}, TaskTriggers={result.TaskTriggerConnections.Count}", this);

                    OnGenerationCompleted?.Invoke(result);
                    yield break;
                }

                if (currentTargetRooms <= minTargetRooms)
                    break;
            }

            ClearGeneratedContent();
            testVisibilityStates.Clear();
            LastResult = null;
            WriteFailureDiagnosticsIfNeeded(failureReport, "[PCG Test]");
            Debug.LogError($"[PCG Test] Map generation aborted | FixedSeed={fixedSeed}, OriginalTargetRooms={originalTargetRooms}, MinTargetRooms={minTargetRooms}, all budget+graph variants exhausted.", this);
        }

        private void AddFailureAttemptReport(
            PcgGenerationFailureReport failureReport,
            int budgetAttempt,
            int graphAttempt,
            int currentTargetRooms,
            int graphSeed,
            int roleSeed,
            int stitchSeed,
            RoomGraph graph,
            PcgMapGenerationResult result,
            Dictionary<int, PcgPlacedRoom> placedByNode,
            List<PcgStitchFailureRecord> stitchFailures)
        {
            if (failureReport == null)
            {
                return;
            }

            failureReport.Attempts.Add(PcgGenerationFailureReporter.BuildAttemptReport(
                budgetAttempt,
                graphAttempt,
                currentTargetRooms,
                graphSeed,
                roleSeed,
                stitchSeed,
                graph,
                result,
                placedByNode,
                stitchFailures));
        }

        private void WriteFailureDiagnosticsIfNeeded(PcgGenerationFailureReport failureReport, string logPrefix)
        {
            if (!writeFailureDiagnostics || failureReport == null || failureReport.Attempts == null || failureReport.Attempts.Count == 0)
            {
                return;
            }

            try
            {
                string path = PcgGenerationFailureReporter.WriteReport(failureReport, failureDiagnosticsDirectory);
                Debug.LogError($"{logPrefix} Failure diagnostics written: {path}", this);
            }
            catch (System.Exception ex)
            {
                DebugLog.Warning("PCG.MapGenerator", $"{logPrefix} Failed to write failure diagnostics: {ex.Message}", this);
            }
        }

        private IEnumerator WaitForTestStep(string stepName)
        {
            if (verboseLog && !string.IsNullOrWhiteSpace(stepName))
            {
                Debug.Log("[PCG Test Step] " + stepName, this);
            }

            float delay = Mathf.Max(0f, testStepDelaySeconds);
            if (delay <= 0f)
            {
                yield return null;
                yield break;
            }

            yield return new WaitForSecondsRealtime(delay);
        }

        private void StopTestStepGenerationIfRunning()
        {
            if (testGenerationCoroutine == null)
            {
                return;
            }

            StopCoroutine(testGenerationCoroutine);
            testGenerationCoroutine = null;
            RestoreAllTestRoomVisuals();
        }

        private void RestoreAllTestRoomVisuals()
        {
            if (testVisibilityStates.Count == 0)
            {
                return;
            }

            List<PcgRoomRoot> rooms = new List<PcgRoomRoot>(testVisibilityStates.Keys);
            for (int i = 0; i < rooms.Count; i++)
            {
                RestoreRoomVisualForTest(rooms[i], testVisibilityStates);
            }

            testVisibilityStates.Clear();
        }

        private static int CountUnplacedRooms(Dictionary<int, PcgPlacedRoom> placedByNode)
        {
            if (placedByNode == null)
            {
                return 0;
            }

            int count = 0;
            foreach (KeyValuePair<int, PcgPlacedRoom> entry in placedByNode)
            {
                PcgPlacedRoom room = entry.Value;
                if (room == null || room.RoomInstance == null || !room.IsPhysicallyPlaced)
                {
                    count++;
                }
            }

            return count;
        }

        private static void HideRoomVisualForTest(
            PcgRoomRoot room,
            Dictionary<PcgRoomRoot, TestRoomVisibilityState> visibilityStates)
        {
            if (room == null || visibilityStates == null || visibilityStates.ContainsKey(room))
            {
                return;
            }

            Renderer[] renderers = room.GetComponentsInChildren<Renderer>(true);
            bool[] originalEnabled = new bool[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                originalEnabled[i] = renderer.enabled;
                renderer.enabled = false;
            }

            visibilityStates[room] = new TestRoomVisibilityState
            {
                Renderers = renderers,
                OriginalEnabled = originalEnabled
            };
        }

        private static void RestoreRoomVisualForTest(
            PcgRoomRoot room,
            Dictionary<PcgRoomRoot, TestRoomVisibilityState> visibilityStates)
        {
            if (room == null || visibilityStates == null || !visibilityStates.TryGetValue(room, out TestRoomVisibilityState state))
            {
                return;
            }

            if (state.Renderers != null && state.OriginalEnabled != null)
            {
                int count = Mathf.Min(state.Renderers.Length, state.OriginalEnabled.Length);
                for (int i = 0; i < count; i++)
                {
                    Renderer renderer = state.Renderers[i];
                    if (renderer != null)
                    {
                        renderer.enabled = state.OriginalEnabled[i];
                    }
                }
            }

            visibilityStates.Remove(room);
        }

        private static List<int> BuildStartRoomRevealOrder(RoomGraph graph, Dictionary<int, PcgPlacedRoom> placedByNode)
        {
            List<int> order = new List<int>();
            if (graph == null || placedByNode == null || placedByNode.Count == 0)
            {
                return order;
            }

            int startNodeId = FindStartPlacedNodeId(placedByNode);
            if (startNodeId < 0)
            {
                return order;
            }

            HashSet<int> visited = new HashSet<int>();
            Queue<int> queue = new Queue<int>();
            visited.Add(startNodeId);
            queue.Enqueue(startNodeId);

            while (queue.Count > 0)
            {
                int nodeId = queue.Dequeue();
                order.Add(nodeId);

                List<int> neighbors = graph.GetNeighborsSorted(nodeId);
                for (int i = 0; i < neighbors.Count; i++)
                {
                    int neighborNodeId = neighbors[i];
                    if (visited.Contains(neighborNodeId) || !placedByNode.ContainsKey(neighborNodeId))
                    {
                        continue;
                    }

                    visited.Add(neighborNodeId);
                    queue.Enqueue(neighborNodeId);
                }
            }

            List<int> remainingNodeIds = new List<int>(placedByNode.Keys);
            remainingNodeIds.Sort();
            for (int i = 0; i < remainingNodeIds.Count; i++)
            {
                int nodeId = remainingNodeIds[i];
                if (!visited.Contains(nodeId))
                {
                    order.Add(nodeId);
                }
            }

            return order;
        }

        private static int FindStartPlacedNodeId(Dictionary<int, PcgPlacedRoom> placedByNode)
        {
            int startNodeId = -1;

            foreach (KeyValuePair<int, PcgPlacedRoom> pair in placedByNode)
            {
                PcgPlacedRoom room = pair.Value;
                if (room == null || room.RoomInstance == null)
                {
                    continue;
                }

                if (room.Role == RoomRole.Start && (startNodeId < 0 || pair.Key < startNodeId))
                {
                    startNodeId = pair.Key;
                }
            }

            if (startNodeId >= 0)
            {
                return startNodeId;
            }

            foreach (KeyValuePair<int, PcgPlacedRoom> pair in placedByNode)
            {
                PcgPlacedRoom room = pair.Value;
                if (room != null && room.RoomInstance != null && (startNodeId < 0 || pair.Key < startNodeId))
                {
                    startNodeId = pair.Key;
                }
            }

            return startNodeId;
        }

        private static int DeriveSeed(int baseSeed, int substream, int budgetAttempt, int graphAttempt)
        {
            unchecked
            {
                return baseSeed ^ (substream * (int)0x9E3779B9u) ^ (substream << 11) ^ (budgetAttempt << 17) ^ (graphAttempt << 7);
            }
        }

        private Dictionary<int, PcgPlacedRoom> InstantiateRooms(
            MapGenerationRequest request,
            RoomGraph graph,
            ref DeterministicRandom random,
            PcgMapGenerationResult result)
        {
            Dictionary<int, PcgPlacedRoom> placedByNode = new Dictionary<int, PcgPlacedRoom>();
            Transform roomParent = ResolveRoomRoot();

            for (int i = 0; i < graph.NodeCount; i++)
            {
                RoomGraphNode node = graph.GetNode(i);
                TryInstantiateRoom(request, graph, node, ref random, result, placedByNode, roomParent);
            }

            return placedByNode;
        }

        private bool TryInstantiateRoom(
            MapGenerationRequest request,
            RoomGraph graph,
            RoomGraphNode node,
            ref DeterministicRandom random,
            PcgMapGenerationResult result,
            Dictionary<int, PcgPlacedRoom> placedByNode,
            Transform roomParent)
        {
            int degree = graph.GetDegree(node.Id);

            PcgRoomRoot prefab = SelectRoomPrefab(request, node.AssignedRole, degree, ref random);
            if (prefab == null)
            {
                DebugLog.Warning("PCG.MapGenerator", $"[PCG] Missing prefab for role {node.AssignedRole}. Node={node.Id}", this);
                return false;
            }

            if (prefab.DefaultRole != node.AssignedRole)
            {
                DebugLog.Warning("PCG.MapGenerator", $"[PCG] Role prefab mismatch | Node={node.Id}, AssignedRole={node.AssignedRole}, Prefab={prefab.name}, PrefabDefaultRole={prefab.DefaultRole}", this);
            }

            Vector3 worldPos = request.WorldOrigin + new Vector3(
                node.GridPosition.x * request.ScaleSettings.RoomCellSize,
                0f,
                node.GridPosition.y * request.ScaleSettings.RoomCellSize);

            PcgRoomRoot roomInstance = Instantiate(prefab, worldPos, Quaternion.identity, roomParent);
            roomInstance.name = $"Room_{node.Id:D2}_{node.AssignedRole}_{prefab.name}";
            roomInstance.RefreshNodeCache();

            PcgPlacedRoom placed = new PcgPlacedRoom
            {
                NodeId = node.Id,
                Role = node.AssignedRole,
                GridPosition = node.GridPosition,
                RoomInstance = roomInstance
            };

            placedByNode[node.Id] = placed;
            result.PlacedRooms.Add(placed);
            return true;
        }

        private void CollectSpawnPoints(PcgMapGenerationResult result)
        {
            for (int i = 0; i < result.PlacedRooms.Count; i++)
            {
                PcgPlacedRoom room = result.PlacedRooms[i];
                PcgRoomRoot root = room.RoomInstance;
                if (root == null)
                {
                    continue;
                }

                IReadOnlyList<PcgSpawnPointMarker> normalSpawnPoints = root.SpawnPoints;
                for (int p = 0; p < normalSpawnPoints.Count; p++)
                {
                    PcgSpawnPointMarker marker = normalSpawnPoints[p];
                    if (marker == null)
                    {
                        continue;
                    }

                    result.SpawnPoints.Add(new PcgSpawnPointResult
                    {
                        NodeId = room.NodeId,
                        Category = SpawnPointCategory.NormalEnemy,
                        PointTransform = marker.transform
                    });
                }

                IReadOnlyList<PcgBossSpawnPointMarker> bossSpawnPoints = root.BossSpawnPoints;
                for (int p = 0; p < bossSpawnPoints.Count; p++)
                {
                    PcgBossSpawnPointMarker marker = bossSpawnPoints[p];
                    if (marker == null)
                    {
                        continue;
                    }

                    result.SpawnPoints.Add(new PcgSpawnPointResult
                    {
                        NodeId = room.NodeId,
                        Category = SpawnPointCategory.BossEnemy,
                        PointTransform = marker.transform
                    });
                }

                IReadOnlyList<PcgDefenseObjectivePointMarker> defensePoints = root.DefenseObjectivePoints;
                for (int p = 0; p < defensePoints.Count; p++)
                {
                    PcgDefenseObjectivePointMarker marker = defensePoints[p];
                    if (marker == null)
                    {
                        continue;
                    }

                    result.SpawnPoints.Add(new PcgSpawnPointResult
                    {
                        NodeId = room.NodeId,
                        Category = SpawnPointCategory.DefenseObjective,
                        PointTransform = marker.transform
                    });
                }
            }
        }

        private void SpawnResources(MapGenerationRequest request, PcgMapGenerationResult result, ref DeterministicRandom random)
        {
            Transform resourceParent = ResolveResourceRoot();
            for (int i = 0; i < result.PlacedRooms.Count; i++)
            {
                PcgPlacedRoom room = result.PlacedRooms[i];
                PcgRoomRoot root = room.RoomInstance;
                if (root == null)
                {
                    continue;
                }

                IReadOnlyList<PcgResourcePointMarker> resourcePoints = root.ResourcePoints;
                for (int p = 0; p < resourcePoints.Count; p++)
                {
                    PcgResourcePointMarker marker = resourcePoints[p];
                    if (marker == null)
                    {
                        continue;
                    }

                    ResourceSpawnOption option = PickResourceOption(request.ResourceSpawnOptions, ref random);
                    if (option == null || option.ResourcePrefab == null)
                    {
                        continue;
                    }

                    GameObject instance = Instantiate(option.ResourcePrefab, marker.transform.position, marker.transform.rotation, resourceParent);
                    result.ResourceSpawns.Add(new PcgResourceSpawnResult
                    {
                        NodeId = room.NodeId,
                        Marker = marker,
                        ResourcePrefab = option.ResourcePrefab,
                        ResourceInstance = instance
                    });
                }
            }
        }

        private PcgRoomRoot SelectRoomPrefab(MapGenerationRequest request, RoomRole role, int requiredConnectorCount, ref DeterministicRandom random)
        {
            List<PcgRoomRoot> candidates = new List<PcgRoomRoot>();
            CollectRoomCandidates(request.RoomPrefabPools, role, requiredConnectorCount, candidates);

            if (candidates.Count == 0 && requiredConnectorCount > 0)
            {
                CollectRoomCandidates(request.RoomPrefabPools, role, 0, candidates);
            }

            if (candidates.Count == 0 && role != RoomRole.Connector)
            {
                CollectRoomCandidates(request.RoomPrefabPools, RoomRole.Connector, requiredConnectorCount, candidates);

                if (candidates.Count == 0 && requiredConnectorCount > 0)
                {
                    CollectRoomCandidates(request.RoomPrefabPools, RoomRole.Connector, 0, candidates);
                }
            }

            if (candidates.Count == 0)
            {
                return null;
            }

            return candidates[random.NextInt(candidates.Count)];
        }

        private static void CollectRoomCandidates(
            List<RoomPrefabPool> pools,
            RoomRole role,
            int requiredConnectorCount,
            List<PcgRoomRoot> output)
        {
            if (pools == null || output == null)
            {
                return;
            }

            for (int i = 0; i < pools.Count; i++)
            {
                RoomPrefabPool pool = pools[i];
                if (pool == null || pool.Role != role || pool.Prefabs == null)
                {
                    continue;
                }

                for (int p = 0; p < pool.Prefabs.Count; p++)
                {
                    PcgRoomRoot prefab = pool.Prefabs[p];
                    if (prefab == null)
                    {
                        continue;
                    }

                    if (requiredConnectorCount > 0 && prefab.ConnectorCount < requiredConnectorCount)
                    {
                        continue;
                    }

                    if (!output.Contains(prefab))
                    {
                        output.Add(prefab);
                    }
                }
            }
        }

        private static ResourceSpawnOption PickResourceOption(List<ResourceSpawnOption> options, ref DeterministicRandom random)
        {
            if (options == null || options.Count == 0)
            {
                return null;
            }

            int totalWeight = 0;
            for (int i = 0; i < options.Count; i++)
            {
                ResourceSpawnOption option = options[i];
                if (option == null || option.ResourcePrefab == null)
                {
                    continue;
                }

                totalWeight += Mathf.Max(1, option.Weight);
            }

            if (totalWeight <= 0)
            {
                return null;
            }

            int roll = random.NextInt(totalWeight);
            int accum = 0;
            for (int i = 0; i < options.Count; i++)
            {
                ResourceSpawnOption option = options[i];
                if (option == null || option.ResourcePrefab == null)
                {
                    continue;
                }

                accum += Mathf.Max(1, option.Weight);
                if (roll < accum)
                {
                    return option;
                }
            }

            return null;
        }

        private void NormalizeRequest(MapGenerationRequest request)
        {
            PcgRequestCloneUtility.EnsureNonNullCollections(request);

            if (request.ScaleSettings.ExtraLoopCount < 0)
            {
                request.ScaleSettings.ExtraLoopCount = 0;
            }

            request.ScaleSettings.MaxNodeDegree = Mathf.Clamp(request.ScaleSettings.MaxNodeDegree, 2, 8);

            if (request.ScaleSettings.PrimaryRingRatio <= 0f)
            {
                request.ScaleSettings.PrimaryRingRatio = 0.50f;
            }

            if (request.ScaleSettings.BranchDensity < 0f)
            {
                request.ScaleSettings.BranchDensity = 0.72f;
            }

            if (request.ScaleSettings.MaxPrimaryBranchLength <= 0)
            {
                request.ScaleSettings.MaxPrimaryBranchLength = 6;
            }

            if (request.ScaleSettings.MaxSecondaryBranchLength <= 0)
            {
                request.ScaleSettings.MaxSecondaryBranchLength = 3;
            }

            request.ScaleSettings.PrimaryRingRatio = Mathf.Clamp(request.ScaleSettings.PrimaryRingRatio, 0.20f, 0.80f);
            request.ScaleSettings.BranchDensity = Mathf.Clamp01(request.ScaleSettings.BranchDensity);
            request.ScaleSettings.SecondaryBranchChance = Mathf.Clamp01(request.ScaleSettings.SecondaryBranchChance);
            request.ScaleSettings.MaxPrimaryBranchLength = Mathf.Max(1, request.ScaleSettings.MaxPrimaryBranchLength);
            request.ScaleSettings.MaxSecondaryBranchLength = Mathf.Max(1, request.ScaleSettings.MaxSecondaryBranchLength);
            request.ScaleSettings.StructuredSecondaryLoopCount = Mathf.Max(0, request.ScaleSettings.StructuredSecondaryLoopCount);
            request.ScaleSettings.SecondaryLoopChance = Mathf.Clamp01(request.ScaleSettings.SecondaryLoopChance);

            request.ScaleSettings.RoomCellSize = Mathf.Max(1f, request.ScaleSettings.RoomCellSize);

            for (int i = 0; i < request.ResourceSpawnOptions.Count; i++)
            {
                ResourceSpawnOption option = request.ResourceSpawnOptions[i];
                if (option != null && option.Weight < 1)
                {
                    option.Weight = 1;
                }
            }

            request.ScaleSettings.TargetRoomCount = ComputeTargetRoomCount(request, request.Seed);

            request.TaskInput.PrimaryTask.TaskType = PrimaryTaskType.BossBattle;
        }

        private bool ValidateRequest(MapGenerationRequest request, string requestSource)
        {
            if (request == null)
            {
                Debug.LogError("[PCG] Final request is null.", this);
                return false;
            }

            if (request.RoomPrefabPools == null || request.RoomPrefabPools.Count == 0)
            {
                Debug.LogError($"[PCG] Final request '{requestSource}' has no RoomPrefabPools configured.", this);
                return false;
            }

            bool hasAnyPrefab = false;
            for (int i = 0; i < request.RoomPrefabPools.Count; i++)
            {
                RoomPrefabPool pool = request.RoomPrefabPools[i];
                if (pool == null || pool.Prefabs == null)
                {
                    continue;
                }

                for (int p = 0; p < pool.Prefabs.Count; p++)
                {
                    if (pool.Prefabs[p] != null)
                    {
                        hasAnyPrefab = true;
                        break;
                    }
                }

                if (hasAnyPrefab)
                {
                    break;
                }
            }

            if (!hasAnyPrefab)
            {
                Debug.LogError($"[PCG] Final request '{requestSource}' has RoomPrefabPools but no valid room prefab references.", this);
                return false;
            }

            return true;
        }

        private void ClearGeneratedContent()
        {
            ClearChildren(ResolveRoomRoot());
            ClearChildren(ResolveResourceRoot());
        }

        private static void ClearChildren(Transform parent)
        {
            if (parent == null)
            {
                return;
            }

            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(child.gameObject);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
                }
            }
        }

        private Transform ResolveRoomRoot()
        {
            if (generatedRoomRoot != null)
            {
                return generatedRoomRoot;
            }

            GameObject root = new GameObject("GeneratedRooms");
            root.transform.SetParent(transform, false);
            generatedRoomRoot = root.transform;
            return generatedRoomRoot;
        }

        private Transform ResolveResourceRoot()
        {
            if (generatedResourceRoot != null)
            {
                return generatedResourceRoot;
            }

            GameObject root = new GameObject("GeneratedResources");
            root.transform.SetParent(transform, false);
            generatedResourceRoot = root.transform;
            return generatedResourceRoot;
        }

        private void OnDisable()
        {
            StopTestStepGenerationIfRunning();
        }
    }
}
