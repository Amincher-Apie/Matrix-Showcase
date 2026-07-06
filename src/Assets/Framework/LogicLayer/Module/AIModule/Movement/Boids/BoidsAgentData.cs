using UnityEngine;

namespace Framework.LogicLayer.Module.AIModule.Movement.Boids
{
    /// <summary>
    /// Boids 系统每帧更新时，用于在中央控制器和各个体之间传递数据的结构体。
    /// 采用 struct 是为了避免 GC 开销，控制器预先分配并复用这些实例。
    /// </summary>
    public struct BoidsAgentData
    {
        /// <summary>
        /// 对应敌人的网络对象 ID，用于唯一标识和调试输出。
        /// </summary>
        public ulong objectId;

        /// <summary>
        /// 该个体当前的世界坐标。
        /// </summary>
        public Vector3 position;

        /// <summary>
        /// 该个体当前的移动速度向量（非归一化）。
        /// 用于对齐行为计算邻居的平均方向。
        /// </summary>
        public Vector3 velocity;

        /// <summary>
        /// 该个体当前的速度归一化方向（即朝向）。
        /// 用于对齐行为计算邻居的平均朝向。
        /// </summary>
        public Vector3 forward;

        /// <summary>
        /// 该个体所在的区域 ID。
        /// -1 表示未分配区域或区域服务不可用，此时 Boids 不做区域过滤。
        /// </summary>
        public int regionId;

        /// <summary>
        /// 该个体是否处于活跃状态。
        /// 非活跃个体（脱战、已死亡）不参与群体行为计算。
        /// </summary>
        public bool isActive;

        /// <summary>
        /// 该个体当前是否处于战斗状态。
        /// 战斗状态下的 Boids 参数可能与待机状态有所区别（如更大的感知半径）。
        /// </summary>
        public bool isInCombat;

        /// <summary>
        /// 计算得到的分离力向量（未归一化）。
        /// </summary>
        public Vector3 separationForce;

        /// <summary>
        /// 计算得到的聚集力向量（未归一化）。
        /// </summary>
        public Vector3 cohesionForce;

        /// <summary>
        /// 计算得到的对齐力向量（未归一化）。
        /// </summary>
        public Vector3 alignmentForce;

        /// <summary>
        /// 分离力命中的有效邻居数量。
        /// </summary>
        public int separationNeighborCount;

        /// <summary>
        /// 聚集力命中的有效邻居数量。
        /// </summary>
        public int cohesionNeighborCount;

        /// <summary>
        /// 对齐力命中的有效邻居数量。
        /// </summary>
        public int alignmentNeighborCount;

        /// <summary>
        /// 最终 Boids 合力（归一化后）。
        /// 由中央控制器统一计算后写入，供 SteeringSystem 读取。
        /// </summary>
        public Vector3 finalBoidsDirection;

        /// <summary>
        /// 最终 Boids 合力叠加前的原始基础方向。
        /// </summary>
        public Vector3 originalBaseDirection;

        /// <summary>
        /// 本次计算是否命中了空间哈希缓存。
        /// 用于调试和性能分析。
        /// </summary>
        public bool usedSpatialHashCache;

        /// <summary>
        /// 本次计算的总邻居查询次数。
        /// 用于调试和性能分析。
        /// </summary>
        public int totalNeighborQueries;

        /// <summary>
        /// 重置所有计算相关字段，便于控制器复用该实例。
        /// </summary>
        public void Reset()
        {
            separationForce = Vector3.zero;
            cohesionForce = Vector3.zero;
            alignmentForce = Vector3.zero;
            separationNeighborCount = 0;
            cohesionNeighborCount = 0;
            alignmentNeighborCount = 0;
            finalBoidsDirection = Vector3.zero;
            originalBaseDirection = Vector3.zero;
            usedSpatialHashCache = false;
            totalNeighborQueries = 0;
            isActive = false;
            isInCombat = false;
        }
    }
}
