using System.Collections.Generic;
using UnityEngine;

namespace Framework.LogicLayer.Module.AIModule.Movement.Boids
{
    /// <summary>
    /// Boids 中央控制器的调试信息快照。
    /// 用于编辑器面板、运行时可视化或日志记录。
    /// </summary>
    public struct BoidsDebugInfo
    {
        /// <summary>
        /// 当前帧参与 Boids 计算的活跃个体总数。
        /// </summary>
        public int activeAgentCount;

        /// <summary>
        /// 当前帧实际执行了 Boids 计算的个体数量。
        /// 与 activeAgentCount 的差异主要来自调度降频和距离过滤。
        /// </summary>
        public int processedAgentCount;

        /// <summary>
        /// 当前帧空间哈希缓存命中率。
        /// 1.0 表示本帧所有邻居查询都命中了已构建的哈希桶。
        /// </summary>
        public float spatialHashHitRate;

        /// <summary>
        /// 当前帧计算的总邻居查询次数。
        /// </summary>
        public int totalNeighborQueries;

        /// <summary>
        /// 本帧 Boids 计算消耗的估计时间（毫秒）。
        /// </summary>
        public float estimatedComputeTimeMs;

        /// <summary>
        /// 调试辅助：最近一次处理的代表性个体的分离力。
        /// </summary>
        public Vector3 lastSeparationForce;

        /// <summary>
        /// 调试辅助：最近一次处理的代表性个体的聚集力。
        /// </summary>
        public Vector3 lastCohesionForce;

        /// <summary>
        /// 调试辅助：最近一次处理的代表性个体的对齐力。
        /// </summary>
        public Vector3 lastAlignmentForce;

        /// <summary>
        /// 调试辅助：最近一次处理的代表性个体的最终合力方向。
        /// </summary>
        public Vector3 lastFinalDirection;

        /// <summary>
        /// 当前帧是否成功构建了空间哈希桶。
        /// </summary>
        public bool spatialHashBuiltThisFrame;

        /// <summary>
        /// 当前帧 Boids 系统是否启用。
        /// </summary>
        public bool boidsEnabled;

        /// <summary>
        /// 调度级别。用于判断是否因调度降频跳过了 Boids 计算。
        /// </summary>
        public AISimulationLevel currentSimulationLevel;

        /// <summary>
        /// 最后一次更新的时间戳（秒）。
        /// </summary>
        public float lastUpdateTime;

        /// <summary>
        /// 返回默认的调试信息。
        /// </summary>
        public static BoidsDebugInfo CreateDefault()
        {
            return new BoidsDebugInfo
            {
                activeAgentCount = 0,
                processedAgentCount = 0,
                spatialHashHitRate = 0f,
                totalNeighborQueries = 0,
                estimatedComputeTimeMs = 0f,
                lastSeparationForce = Vector3.zero,
                lastCohesionForce = Vector3.zero,
                lastAlignmentForce = Vector3.zero,
                lastFinalDirection = Vector3.zero,
                spatialHashBuiltThisFrame = false,
                boidsEnabled = false,
                currentSimulationLevel = AISimulationLevel.Full,
                lastUpdateTime = 0f
            };
        }

        /// <summary>
        /// 返回描述当前状态的可读字符串。
        /// </summary>
        public string ToSummaryString()
        {
            return $"Boids[{Time.time:F2}s]: " +
                   $"活跃={activeAgentCount}, " +
                   $"处理={processedAgentCount}, " +
                   $"启用={boidsEnabled}, " +
                   $"空间哈希命中={spatialHashHitRate:P0}, " +
                   $"查询数={totalNeighborQueries}, " +
                   $"耗时≈{estimatedComputeTimeMs:F3}ms, " +
                   $"SimLevel={currentSimulationLevel}";
        }
    }

    /// <summary>
    /// 单个个体的 Boids 调试快照。
    /// 用于编辑器中选中敌人时显示其个人 Boids 状态。
    /// </summary>
    public struct BoidsAgentDebugInfo
    {
        /// <summary>
        /// 该个体对应的网络对象 ID。
        /// </summary>
        public ulong objectId;

        /// <summary>
        /// 该个体的原始期望移动方向（输入）。
        /// </summary>
        public Vector3 originalDirection;

        /// <summary>
        /// 该个体计算得到的分离力。
        /// </summary>
        public Vector3 separationForce;

        /// <summary>
        /// 该个体计算得到的聚集力。
        /// </summary>
        public Vector3 cohesionForce;

        /// <summary>
        /// 该个体计算得到的对齐力。
        /// </summary>
        public Vector3 alignmentForce;

        /// <summary>
        /// 该个体最终叠加后的 Boids 合力方向（输出）。
        /// </summary>
        public Vector3 finalDirection;

        /// <summary>
        /// 分离行为的有效邻居数量。
        /// </summary>
        public int separationNeighbors;

        /// <summary>
        /// 聚集行为的有效邻居数量。
        /// </summary>
        public int cohesionNeighbors;

        /// <summary>
        /// 对齐行为的有效邻居数量。
        /// </summary>
        /// <summary>
        /// 是否命中了空间哈希缓存。
        /// </summary>
        public int alignmentNeighbors;

        public bool usedSpatialHash;

        /// <summary>
        /// 该个体是否参与了本帧计算。
        /// </summary>
        public bool wasProcessed;

        /// <summary>
        /// 创建默认快照。
        /// </summary>
        public static BoidsAgentDebugInfo CreateDefault(ulong objectId)
        {
            return new BoidsAgentDebugInfo
            {
                objectId = objectId,
                originalDirection = Vector3.zero,
                separationForce = Vector3.zero,
                cohesionForce = Vector3.zero,
                alignmentForce = Vector3.zero,
                finalDirection = Vector3.zero,
                separationNeighbors = 0,
                cohesionNeighbors = 0,
                alignmentNeighbors = 0,
                usedSpatialHash = false,
                wasProcessed = false
            };
        }
    }
}
