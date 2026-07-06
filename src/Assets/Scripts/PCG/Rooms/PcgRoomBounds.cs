using UnityEngine;

namespace Matrix.PCG
{
    /// <summary>
    /// Explicit room bounds provider for physical stitch overlap tests.
    /// Attach this on room root and assign a BoxCollider that represents room footprint.
    /// </summary>
    public sealed class PcgRoomBounds : MonoBehaviour
    {
        [Tooltip("Axis-aligned source collider used for overlap checks and mission room-enter triggers.")]
        [SerializeField]
        private BoxCollider boundsCollider;

        [Tooltip("Optional shrink amount used in overlap checks to allow tiny seam contact.")]
        [Min(0f)]
        [SerializeField]
        private float overlapPadding = 0.15f;

        public BoxCollider BoundsCollider
        {
            get
            {
                if (boundsCollider == null)
                {
                    boundsCollider = GetComponent<BoxCollider>();
                }

                return boundsCollider;
            }
        }

        public float OverlapPadding => Mathf.Max(0f, overlapPadding);

        public bool TryGetBoundsCollider(out BoxCollider collider)
        {
            collider = BoundsCollider;
            return collider != null;
        }

        public bool TryGetWorldBounds(out Bounds bounds)
        {
            BoxCollider collider = BoundsCollider;

            if (collider == null)
            {
                bounds = default;
                return false;
            }

            // Collider.bounds may become empty when collider is disabled, so compute manually from local box data.
            bounds = ComputeWorldAabb(collider.transform.localToWorldMatrix, collider.center, collider.size);
            return true;
        }

        public bool TryGetWorldFootprintCorners(out Vector3 minXMinZ, out Vector3 maxXMinZ, out Vector3 maxXMaxZ, out Vector3 minXMaxZ)
        {
            BoxCollider collider = BoundsCollider;

            if (collider == null)
            {
                minXMinZ = default;
                maxXMinZ = default;
                maxXMaxZ = default;
                minXMaxZ = default;
                return false;
            }

            Vector3 center = collider.center;
            Vector3 half = collider.size * 0.5f;
            Matrix4x4 localToWorld = collider.transform.localToWorldMatrix;

            minXMinZ = localToWorld.MultiplyPoint3x4(center + new Vector3(-half.x, 0f, -half.z));
            maxXMinZ = localToWorld.MultiplyPoint3x4(center + new Vector3(half.x, 0f, -half.z));
            maxXMaxZ = localToWorld.MultiplyPoint3x4(center + new Vector3(half.x, 0f, half.z));
            minXMaxZ = localToWorld.MultiplyPoint3x4(center + new Vector3(-half.x, 0f, half.z));
            return true;
        }

        private static Bounds ComputeWorldAabb(Matrix4x4 localToWorld, Vector3 localCenter, Vector3 localSize)
        {
            Vector3 half = localSize * 0.5f;
            Vector3 worldCenter = localToWorld.MultiplyPoint3x4(localCenter);

            Vector3 axisX = localToWorld.MultiplyVector(new Vector3(half.x, 0f, 0f));
            Vector3 axisY = localToWorld.MultiplyVector(new Vector3(0f, half.y, 0f));
            Vector3 axisZ = localToWorld.MultiplyVector(new Vector3(0f, 0f, half.z));

            Vector3 extents = new Vector3(
                Mathf.Abs(axisX.x) + Mathf.Abs(axisY.x) + Mathf.Abs(axisZ.x),
                Mathf.Abs(axisX.y) + Mathf.Abs(axisY.y) + Mathf.Abs(axisZ.y),
                Mathf.Abs(axisX.z) + Mathf.Abs(axisY.z) + Mathf.Abs(axisZ.z));

            return new Bounds(worldCenter, extents * 2f);
        }
    }
}

