using UnityEngine;

namespace Framework.LogicLayer.Module.AIModule.Movement.Boids
{
    /// <summary>
    /// Boids 群体行为算法的核心配置。
    /// 该配置描述分离、聚集、对齐三大核心行为的控制参数，以及全局启用开关和性能优化参数。
    /// </summary>
    [System.Serializable]
    public class BoidsConfig
    {
        [Header("全局开关")]
        [Tooltip("是否启用 Boids 群体行为算法。关闭后回退到原有简单分离逻辑。")]
        public bool enableBoids = true;

        [Header("分离行为 (Separation)")]
        [Tooltip("是否启用分离行为。分离让每个个体主动远离过近的邻居，防止相互穿透和堆积。")]
        public bool enableSeparation = true;

        [Tooltip("触发分离修正的临界距离。小于此距离的邻居会产生分离力。")]
        public float separationRadius = 1.5f;

        [Tooltip("分离行为的权重系数。值越大，个体越倾向于远离邻居。")]
        public float separationWeight = 2.0f;

        [Tooltip("分离力计算时使用的缓冲区初始容量。")]
        public int separationBufferCapacity = 16;

        [Header("聚集行为 (Cohesion)")]
        [Tooltip("是否启用聚集行为。聚集让每个个体倾向移动到本地邻居群体的中心位置，形成自然集群。")]
        public bool enableCohesion = true;

        [Tooltip("参与聚集计算的邻居感知半径。")]
        public float cohesionRadius = 5f;

        [Tooltip("聚集行为的权重系数。值越大，个体越倾向于聚集到邻居中心。")]
        public float cohesionWeight = 1.0f;

        [Tooltip("聚集力计算时使用的缓冲区初始容量。")]
        public int cohesionBufferCapacity = 16;

        [Header("对齐行为 (Alignment)")]
        [Tooltip("是否启用对齐行为。对齐让每个个体的速度方向趋向于邻居的平均速度方向，产生方向一致的群体流动。")]
        public bool enableAlignment = true;

        [Tooltip("参与对齐计算的邻居感知半径。")]
        public float alignmentRadius = 5f;

        [Tooltip("对齐行为的权重系数。值越大，个体越倾向于与邻居保持方向一致。")]
        public float alignmentWeight = 1.0f;

        [Tooltip("对齐力计算时使用的缓冲区初始容量。")]
        public int alignmentBufferCapacity = 16;

        [Header("密度感知聚集抑制")]
        [Tooltip("分离邻居数超过此阈值时，按比例降低聚集力权重（打破门口死锁）。")]
        public int densityNeighborThreshold = 3;

        [Tooltip("每个超出阈值的邻居降低的聚集权重比例（0~1）。设为 0.3 意味着每多一个邻居，聚集力降低 30%。")]
        public float densityCohesionReductionPerNeighbor = 0.3f;

        [Header("行为优先级分层")]
        [Tooltip("当分离力过强时，是否限制最大分离力以防止群体四散。")]
        public bool clampSeparationForce = true;

        [Tooltip("分离力的最大向量长度上限。当 clampSeparationForce 为 true 时生效。")]
        public float maxSeparationMagnitude = 3f;

        [Tooltip("启用分层行为时，先计算基础方向再叠加 Boids 力，防止 Boids 主导原本的追击/巡逻行为。")]
        public bool layerOnBaseDirection = true;

        [Header("速度与质量参数")]
        [Tooltip("用于对齐计算时的速度缓存容量。该值应大于单帧最大邻居数以避免扩容。")]
        public int velocityCacheCapacity = 16;

        [Tooltip("个体质量参数。质量越大，加速度越小（加速度 = 力 / 质量）。")]
        public float agentMass = 1f;

        [Tooltip("最大速度上限。防止 Boids 力叠加后速度过快。")]
        public float maxSpeed = 5f;

        [Header("空间优化")]
        [Tooltip("是否启用空间哈希桶来加速邻居查询。强烈建议在敌人数量 > 10 时启用。")]
        public bool enableSpatialHashing = true;

        [Tooltip("空间哈希桶的网格尺寸。建议设置为最大感知半径的 0.5~1 倍。")]
        public float spatialHashCellSize = 2.5f;

        [Tooltip("是否仅对同房间（同区域 ID）内的敌人计算 Boids。对应 PCGMapTopologyService 的区域划分。")]
        public bool limitToSameRegion = true;

        [Header("调度相关")]
        [Tooltip("调度降频期间是否跳过 Boids 计算。设为 true 可在远距离时降低群体行为消耗。")]
        public bool skipBoidsWhenReducedSimulation = true;

        [Tooltip("仅在敌人距离玩家小于此值时才计算 Boids。当为 0 或负数时表示不限制。")]
        public float maxDistanceToActivateBoids = 0f;

        [Header("调试")]
        [Tooltip("是否绘制 Boids 调试辅助线。")]
        public bool drawDebugGizmos = false;

        [Tooltip("调试时绘制的辅助线最大长度。")]
        public float debugArrowLength = 1.5f;

        [Tooltip("调试时分离力用绿色箭头，聚集力用蓝色，对齐力用红色。")]
        public float debugForceScale = 0.5f;

        /// <summary>
        /// 返回一个带默认值的静默配置，适用于不需要 Boids 的场景。
        /// </summary>
        public static BoidsConfig CreateDisabled()
        {
            return new BoidsConfig { enableBoids = false };
        }

        /// <summary>
        /// 创建一个性能优先的配置。
        /// 适用于大规模敌人（> 50）或有性能瓶颈的场景。
        /// </summary>
        public static BoidsConfig CreatePerformanceFirst()
        {
            return new BoidsConfig
            {
                enableBoids = true,
                enableSeparation = true,
                separationRadius = 1.2f,
                separationWeight = 1.5f,
                enableCohesion = false,
                enableAlignment = false,
                densityNeighborThreshold = 2,
                densityCohesionReductionPerNeighbor = 0.4f,
                enableSpatialHashing = true,
                spatialHashCellSize = 2.5f,
                limitToSameRegion = true,
                skipBoidsWhenReducedSimulation = true,
                drawDebugGizmos = false
            };
        }

        /// <summary>
        /// 创建一个视觉优先的配置。
        /// 适用于追求华丽群体效果的场景。
        /// </summary>
        public static BoidsConfig CreateVisualFirst()
        {
            return new BoidsConfig
            {
                enableBoids = true,
                enableSeparation = true,
                separationRadius = 1.8f,
                separationWeight = 2.5f,
                enableCohesion = true,
                cohesionRadius = 6f,
                cohesionWeight = 1.2f,
                enableAlignment = true,
                alignmentRadius = 6f,
                alignmentWeight = 1.0f,
                densityNeighborThreshold = 3,
                densityCohesionReductionPerNeighbor = 0.3f,
                enableSpatialHashing = true,
                spatialHashCellSize = 3f,
                limitToSameRegion = false,
                skipBoidsWhenReducedSimulation = false,
                drawDebugGizmos = true
            };
        }
    }
}
