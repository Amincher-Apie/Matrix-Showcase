using System;
using UnityEngine;
using UnityEngine.AI;

namespace Matrix.PCG.Navigation
{
    /// <summary>
    /// 挂在每个 RoomPrefab 根节点上，保存该 Prefab 对应的预烘焙 NavMeshData。
    ///
    /// 职责：
    /// 1. 保存预烘焙的 NavMeshData 和 Agent Type 配置
    /// 2. 运行时负责把 NavMeshData 加入全局 NavMesh（AddNavMeshData）
    /// 3. 提供清理接口（RemoveFromWorld）
    ///
    /// 注意：
    /// - 不运行时 BuildNavMesh，只 AddNavMeshData
    /// - 不接入 PathService
    /// </summary>
    [RequireComponent(typeof(PcgRoomRoot))]
    public class RoomPrebakedNavMeshAsset : MonoBehaviour
    {
        [Header("NavMesh Data")]
        [Tooltip("预烘焙的 NavMeshData 资源。")]
        [SerializeField]
        private NavMeshData navMeshData;

        [Tooltip("关联的 Agent Type ID。必须与 PcgNavMeshAssembler 的 expectedAgentTypeId 一致。")]
        [SerializeField]
        private int agentTypeId;

        [Header("Transform Offset")]
        [Tooltip("NavMeshData 相对于 RoomPrefab 根节点的局部位置偏移。预烘焙时 NavMesh 坐标系原点为 (0,0,0)。")]
        [SerializeField]
        private Vector3 localPositionOffset = Vector3.zero;

        [Tooltip("NavMeshData 相对于 RoomPrefab 根节点的局部欧拉角偏移。")]
        [SerializeField]
        private Vector3 localEulerOffset = Vector3.zero;

        [Header("Validation")]
        [Tooltip("预烘焙时使用的地面高度基准（Y 值）。用于运行时校验。")]
        [SerializeField]
        private float bakedFloorY;

        public bool HasValidData => navMeshData != null;

        public int AgentTypeId => agentTypeId;

        public NavMeshData NavMeshData
        {
            get => navMeshData;
            set => navMeshData = value;
        }

        public Vector3 LocalPositionOffset
        {
            get => localPositionOffset;
            set => localPositionOffset = value;
        }

        public Vector3 LocalEulerOffset
        {
            get => localEulerOffset;
            set => localEulerOffset = value;
        }

        private NavMeshDataInstance? _instance;

        /// <summary>
        /// 将预烘焙的 NavMeshData 添加到全局 NavMesh。
        /// </summary>
        /// <param name="roomTransform">房间实例的 Transform。</param>
        /// <returns>创建的 NavMeshDataInstance，用于后续清理。</returns>
        public NavMeshDataInstance AddToWorld(Transform roomTransform)
        {
            if (!HasValidData)
            {
                Debug.LogError($"[RoomPrebakedNavMeshAsset] {name} 没有有效的 NavMeshData！", this);
                return default;
            }

            Vector3 worldPos = roomTransform.TransformPoint(localPositionOffset);
            Quaternion worldRot = roomTransform.rotation * Quaternion.Euler(localEulerOffset);

            NavMeshDataInstance instance = NavMesh.AddNavMeshData(navMeshData, worldPos, worldRot);
            _instance = instance;

            return instance;
        }

        /// <summary>
        /// 将此房间的 NavMeshData 从全局 NavMesh 移除。
        /// </summary>
        public void RemoveFromWorld()
        {
            if (_instance.HasValue && _instance.Value.valid)
            {
                NavMesh.RemoveNavMeshData(_instance.Value);
            }

            _instance = null;
        }

        /// <summary>
        /// 运行时校验：检查房间 Transform 是否符合规范。
        /// </summary>
        /// <returns>校验结果和错误信息。</returns>
        public (bool isValid, string error) ValidateRuntimeTransform()
        {
            if (!Mathf.Approximately(transform.lossyScale.x, 1f) ||
                !Mathf.Approximately(transform.lossyScale.y, 1f) ||
                !Mathf.Approximately(transform.lossyScale.z, 1f))
            {
                return (false, $"房间 {name} 的缩放不为 (1,1,1)，当前为 {transform.lossyScale}。禁止运行时非等比缩放！");
            }

            if (!Mathf.Approximately(transform.rotation.x, 0f) &&
                !Mathf.Approximately(transform.rotation.x, 1f) &&
                !Mathf.Approximately(transform.rotation.x, -1f) &&
                Mathf.Abs(transform.eulerAngles.x) > 0.01f)
            {
                return (false, $"房间 {name} 有 X 轴旋转 {transform.eulerAngles.x}，禁止非 Y 轴旋转！");
            }

            if (!Mathf.Approximately(transform.rotation.z, 0f) &&
                !Mathf.Approximately(transform.rotation.z, 1f) &&
                !Mathf.Approximately(transform.rotation.z, -1f) &&
                Mathf.Abs(transform.eulerAngles.z) > 0.01f)
            {
                return (false, $"房间 {name} 有 Z 轴旋转 {transform.eulerAngles.z}，禁止非 Y 轴旋转！");
            }

            return (true, string.Empty);
        }

        private void OnDestroy()
        {
            RemoveFromWorld();
        }
    }
}
