using UnityEngine;

namespace Framework.LogicLayer.Module.SpawnSystem
{
    /// <summary>
    /// Runtime wrapper for a PCG spawn point.
    /// </summary>
    public sealed class SpawnPointData
    {
        public int RegionId { get; }
        public Vector3 WorldPosition { get; }
        public Transform Transform { get; }

        public SpawnPointData(int regionId, Transform transform)
        {
            RegionId = regionId;
            Transform = transform;
            WorldPosition = transform != null ? transform.position : Vector3.zero;
        }
    }
}
