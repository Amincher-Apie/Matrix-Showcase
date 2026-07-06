using System.Collections.Generic;
using Matrix.PCG;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace Framework.LogicLayer.Module.AIModule.Navigation
{
    /// <summary>
    /// PCG 地图导航网格管理器。
    ///
    /// 职责：
    /// 1. 订阅 PcgMapGenerator.OnGenerationCompleted 事件
    /// 2. PCG 地图生成完成后，创建房间连接处的 NavMeshLink
    /// 3. 调用 NavMeshSurface.BuildNavMesh()
    /// 4. 标记 IsReady = true 并触发 OnNavMeshReady 事件
    /// 5. 暴露 SpawnPoint 修正接口
    ///
    /// 本组件不再使用自写预烘焙 Tile、StitchingService 或 NavMeshRuntimeService，
    /// 统一走 Unity 官方 NavMeshSurface + NavMeshLink 体系。
    /// </summary>
    [RequireComponent(typeof(PCGNavMeshLinkBuilder))]
    public class PCGNavMeshManager : MonoBehaviour
    {
        [Header("NavMesh Surface")]
        [Tooltip("负责实际构建 NavMesh 的 NavMeshSurface 组件。")]
        [SerializeField]
        private NavMeshSurface navMeshSurface;

        [Header("PCG")]
        [Tooltip("PCG 地图生成器引用。留空则自动查找场景中的 PcgMapGenerator。")]
        [SerializeField]
        private PcgMapGenerator mapGenerator;

        [Tooltip("生成地图后自动触发 NavMesh 构建。")]
        [SerializeField]
        private bool buildOnMapGenerated = true;

        [Tooltip("NavMesh.SamplePosition 吸附到可走面时使用的最大采样距离（米）。")]
        [SerializeField]
        private float sampleDistance = 2f;

        [Tooltip("NavMesh 构建完成前是否阻止敌人生成。")]
        [SerializeField]
        private bool blockSpawnUntilReady = true;

        [Header("Links")]
        [Tooltip("已创建的所有运行时 NavMeshLink 的父级对象。")]
        [SerializeField]
        private Transform linksRoot;

        [Header("Debug")]
        [SerializeField]
        private bool verboseLog;

        [Header("Compatibility")]
        [Tooltip("若场景中存在 PcgNavMeshAssembler（预烘焙模式），则自动禁用本组件以避免冲突。")]
        [SerializeField]
        private bool autoDisableWithPrebakedSystem = true;

        private PCGNavMeshLinkBuilder _linkBuilder;
        private bool _isReady;
        private PcgMapGenerationResult _currentResult;
        private bool _prebakedSystemDetected;

        public bool IsReady => _isReady;

        public event System.Action OnNavMeshReady;

        private void Awake()
        {
            _linkBuilder = GetComponent<PCGNavMeshLinkBuilder>();
            DetectPrebakedSystem();
        }

        private void DetectPrebakedSystem()
        {
            if (!autoDisableWithPrebakedSystem)
                return;

            var prebakedAssembler = FindObjectOfType<Matrix.PCG.Navigation.PcgNavMeshAssembler>();
            if (prebakedAssembler != null)
            {
                _prebakedSystemDetected = true;
                if (verboseLog)
                {
                    AIDebug.LogChannel("AI.Navigation", $"[PCGNavMeshManager] 检测到 PcgNavMeshAssembler（预烘焙模式），本组件将跳过 NavMesh 构建以避免冲突。", this);
                }
            }
        }

        private void OnEnable()
        {
            ResolveMapGenerator();
            if (mapGenerator != null)
            {
                mapGenerator.OnGenerationCompleted += OnMapGenerationCompleted;
            }
        }

        private void OnDisable()
        {
            if (mapGenerator != null)
            {
                mapGenerator.OnGenerationCompleted -= OnMapGenerationCompleted;
            }
        }

        /// <summary>
        /// 当 PCG 地图生成完成时，自动触发 NavMesh 构建流程。
        /// </summary>
        private void OnMapGenerationCompleted(PcgMapGenerationResult result)
        {
            if (!buildOnMapGenerated)
                return;

            if (_prebakedSystemDetected)
            {
                if (verboseLog)
                {
                    AIDebug.LogChannel("AI.Navigation", $"[PCGNavMeshManager] 预烘焙模式已激活，跳过 NavMeshSurface.BuildNavMesh() 以避免覆盖预烘焙 NavMeshData。", this);
                }
                return;
            }

            if (verboseLog)
            {
                AIDebug.LogChannel("AI.Navigation", $"[PCGNavMeshManager] 收到地图生成完成事件，RoomCount={result.PlacedRooms.Count}，ConnectionCount={result.Connections.Count}", this);
            }

            _currentResult = result;
            RebuildAfterPCG();
        }

        /// <summary>
        /// 手动触发一次完整的 NavMesh 重建流程。
        /// 调用方负责确保 PCG 地图已经生成完成。
        /// </summary>
        public void RebuildAfterPCG()
        {
            if (navMeshSurface == null)
            {
                Debug.LogError("[PCGNavMeshManager] NavMeshSurface 未设置，无法构建 NavMesh！", this);
                return;
            }

            if (_currentResult == null)
            {
                Debug.LogError("[PCGNavMeshManager] PcgMapGenerationResult 为空，请确保地图已生成。", this);
                return;
            }

            _isReady = false;

            BuildLinks();

            if (verboseLog)
            {
                AIDebug.LogChannel("AI.Navigation", $"[PCGNavMeshManager] 开始构建 NavMesh...", this);
            }

            navMeshSurface.RemoveData();
            navMeshSurface.BuildNavMesh();

            _isReady = true;

            if (verboseLog)
            {
                AIDebug.LogChannel("AI.Navigation", "NavMesh 构建完成！", this);
            }

            OnNavMeshReady?.Invoke();
        }

        /// <summary>
        /// 根据 PCGConnections 在房间连接处创建 NavMeshLink。
        /// </summary>
        private void BuildLinks()
        {
            if (_currentResult == null)
                return;

            ClearLinks();

            var linksCreated = _linkBuilder.BuildLinks(
                _currentResult.Connections,
                linksRoot,
                sampleDistance,
                verboseLog);

            if (verboseLog)
            {
                AIDebug.LogChannel("AI.Navigation", $"[PCGNavMeshManager] 创建了 {linksCreated} 条 NavMeshLink", this);
            }
        }

        /// <summary>
        /// 清理之前创建的所有运行时 NavMeshLink。
        /// </summary>
        private void ClearLinks()
        {
            if (linksRoot == null)
                return;

            for (var i = linksRoot.childCount - 1; i >= 0; i--)
            {
                var child = linksRoot.GetChild(i);
                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }

        /// <summary>
        /// 将一个世界坐标吸附到最近的 NavMesh 可走面上。
        /// 常用于刷怪点生成后将 SpawnPoint 修正到 NavMesh 上。
        /// </summary>
        /// <param name="worldPosition">原始世界坐标。</param>
        /// <param name="hit">输出最近 NavMesh 采样结果。</param>
        /// <param name="maxDistance">最大采样距离。</param>
        /// <returns>返回 true 表示成功吸附。</returns>
        public bool TrySamplePosition(Vector3 worldPosition, out NavMeshHit hit, float maxDistance = -1f)
        {
            hit = default;
            if (!_isReady)
            {
                AIDebug.LogWarning("NavMesh 未就绪，无法 SamplePosition。", this);
                return false;
            }

            var distance = maxDistance > 0f ? maxDistance : sampleDistance;
            var areaMask = NavMesh.AllAreas;

            if (NavMesh.SamplePosition(worldPosition, out hit, distance, areaMask))
            {
                if (verboseLog)
                {
                    AIDebug.LogChannel("AI.Navigation", $"[PCGNavMeshManager] SamplePosition 吸附 {worldPosition} -> {hit.position}，距离={hit.distance:F3}", this);
                }
                return true;
            }

            AIDebug.LogWarning($"[PCGNavMeshManager] SamplePosition 在 {distance}m 范围内找不到 NavMesh，位置={worldPosition}", this);
            return false;
        }

        /// <summary>
        /// 批量将多个 SpawnPoint 修正到 NavMesh 上。
        /// </summary>
        /// <param name="spawnPoints">SpawnPoint 列表（会被原地修正）。</param>
        /// <param name="sampleDistanceOverride">覆盖默认采样距离。</param>
        /// <returns>成功修正的数量。</returns>
        public int SnapSpawnPointsToNavMesh(IList<Transform> spawnPoints, float sampleDistanceOverride = -1f)
        {
            if (spawnPoints == null || spawnPoints.Count == 0)
                return 0;

            var successCount = 0;
            var distance = sampleDistanceOverride > 0f ? sampleDistanceOverride : sampleDistance;

            foreach (var sp in spawnPoints)
            {
                if (sp == null)
                    continue;

                if (NavMesh.SamplePosition(sp.position, out var hit, distance, NavMesh.AllAreas))
                {
                    sp.position = hit.position;
                    successCount++;
                }
                else
                {
                    AIDebug.LogWarning($"[PCGNavMeshManager] SpawnPoint 无法吸附到 NavMesh: {sp.name}，位置={sp.position}", this);
                }
            }

            if (verboseLog)
            {
                AIDebug.LogChannel("AI.Navigation", $"[PCGNavMeshManager] SpawnPoint 修正完成：{successCount}/{spawnPoints.Count} 成功", this);
            }

            return successCount;
        }

        /// <summary>
        /// 判断当前是否允许生成敌人。
        /// 当 blockSpawnUntilReady = true 时，未就绪返回 false。
        /// </summary>
        public bool CanSpawnEnemies()
        {
            if (!blockSpawnUntilReady)
                return true;

            return _isReady;
        }

        /// <summary>
        /// 获取当前地图生成结果引用。
        /// </summary>
        public PcgMapGenerationResult GetCurrentResult()
        {
            return _currentResult;
        }

        /// <summary>
        /// 解析并缓存 PcgMapGenerator 引用。
        /// </summary>
        private void ResolveMapGenerator()
        {
            if (mapGenerator != null)
                return;

            mapGenerator = GetComponent<PcgMapGenerator>();
            if (mapGenerator != null)
                return;

            mapGenerator = FindObjectOfType<PcgMapGenerator>();
        }
    }
}
