using UnityEngine;

namespace Matrix.PCG
{
    public sealed class PcgConnectorMarker : MonoBehaviour
    {
        [SerializeField]
        private ConnectorKind connectorKind = ConnectorKind.Bidirectional;

        [SerializeField]
        private string connectorId = string.Empty;

        [Header("Socket")]
        [Tooltip("Socket tag for compatibility checks. Same tag can connect. Empty means wildcard.")]
        [SerializeField]
        private string socketTag = "Default";

        [Tooltip("Optional guide point used only to choose the outward side of the geometric socket plane.")]
        [SerializeField]
        private Transform socketFrame;

        [Tooltip("Optional snap offset from this socket along its geometric normal when connected.")]
        [SerializeField]
        private float socketOffset;

        [Tooltip("If enabled, reverse socket normal direction.")]
        [SerializeField]
        private bool invertSocketNormal;

        [Header("NavMesh Link Settings")]
        [Tooltip("此连接处创建的 NavMeshLink 宽度。")]
        [SerializeField]
        private float navLinkWidth = 2f;

        [Tooltip("此连接处采样到 NavMesh 的最大半径。")]
        [SerializeField]
        private float navLinkSampleRadius = 2f;

        [Tooltip("是否允许在此连接处创建 NavMeshLink。禁止时跳过此连接。")]
        [SerializeField]
        private bool allowNavLink = true;

        [SerializeField]
        private GameObject openDoorVisual;

        [SerializeField]
        private GameObject closedDoorVisual;

        public ConnectorKind Kind => connectorKind;

        public float NavLinkWidth => navLinkWidth;
        public float NavLinkSampleRadius => navLinkSampleRadius;
        public bool AllowNavLink => allowNavLink;

        public string ConnectorId
        {
            get
            {
                if (string.IsNullOrWhiteSpace(connectorId))
                {
                    return gameObject.name;
                }

                return connectorId;
            }
        }

        public string SocketTag => socketTag;
        public float SocketOffset => socketOffset;
        public bool HasExplicitSocketFrame => socketFrame != null;

        public bool SupportsIncoming => connectorKind == ConnectorKind.Entrance || connectorKind == ConnectorKind.Bidirectional;
        public bool SupportsOutgoing => connectorKind == ConnectorKind.Exit || connectorKind == ConnectorKind.Bidirectional;

        public bool IsSocketCompatible(PcgConnectorMarker other)
        {
            if (other == null)
            {
                return false;
            }

            string a = socketTag != null ? socketTag.Trim() : string.Empty;
            string b = other.socketTag != null ? other.socketTag.Trim() : string.Empty;

            if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
            {
                return true;
            }

            return string.Equals(a, b, System.StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Geometric socket normal, independent from Entry/Exit semantic usage.
        /// Priority: BoxCollider plane normal -> transform.forward fallback.
        /// socketFrame only selects the sign of the geometric normal; it never skews the normal axis.
        /// </summary>
        public Vector3 GetSocketNormal(bool _usedAsOutgoing)
        {
            Vector3 normal;
            BoxCollider box = GetComponent<BoxCollider>();
            if (box != null)
            {
                normal = ComputeDoorNormalFromCollider(box, socketFrame);
            }
            else
            {
                normal = FlattenHorizontal(transform.forward);
                normal = OrientNormalByGuidePoint(normal, transform.position, socketFrame);
            }

            return invertSocketNormal ? -normal : normal;
        }

        private static Vector3 ComputeDoorNormalFromCollider(BoxCollider box, Transform guidePoint)
        {
            // Find thinnest axis of the collider — this is perpendicular to the door plane.
            // Use Abs because Unity may report negative values for flipped colliders.
            Vector3 size = box.size;
            float absX = Mathf.Abs(size.x);
            float absY = Mathf.Abs(size.y);
            float absZ = Mathf.Abs(size.z);

            int thinAxis = 0;
            if (absY < absX) thinAxis = 1;
            if (absZ < Mathf.Abs(size[thinAxis])) thinAxis = 2;

            // Build local-space unit vector along thinnest axis.
            Vector3 localNormal = Vector3.zero;
            localNormal[thinAxis] = 1f;

            // Transform to world space, flatten to XZ.
            Vector3 worldNormal = box.transform.TransformDirection(localNormal);
            Vector3 normal = FlattenHorizontal(worldNormal);
            Vector3 planePoint = box.transform.TransformPoint(box.center);

            if (guidePoint != null)
            {
                return OrientNormalByGuidePoint(normal, planePoint, guidePoint);
            }

            // Determine outward direction: pick the face whose center is farther
            // from the room root position (point outside the door plane).
            var roomRoot = box.GetComponentInParent<PcgRoomRoot>();
            if (roomRoot != null)
            {
                Vector3 roomToPlane = planePoint - roomRoot.transform.position;
                if (Vector3.Dot(normal, roomToPlane) < 0f)
                    normal = -normal;
            }

            return normal;
        }

        public Vector3 GetSocketBaseWorldPoint()
        {
            return transform.position;
        }

        public Vector3 GetSocketWorldPoint(bool usedAsOutgoing)
        {
            return GetSocketBaseWorldPoint() + GetSocketNormal(usedAsOutgoing) * socketOffset;
        }

        public bool TryGetSocketColliderOutwardDistance(Vector3 outwardNormal, out float distance)
        {
            distance = 0f;

            BoxCollider box = GetComponent<BoxCollider>();
            if (box == null)
            {
                return false;
            }

            Vector3 normal = new Vector3(outwardNormal.x, 0f, outwardNormal.z);
            if (normal.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            normal.Normalize();

            Vector3 center = box.transform.TransformPoint(box.center);
            Vector3 half = box.size * 0.5f;
            Vector3 axisX = box.transform.TransformVector(new Vector3(half.x, 0f, 0f));
            Vector3 axisY = box.transform.TransformVector(new Vector3(0f, half.y, 0f));
            Vector3 axisZ = box.transform.TransformVector(new Vector3(0f, 0f, half.z));

            float projectedHalfExtent =
                Mathf.Abs(Vector3.Dot(axisX, normal)) +
                Mathf.Abs(Vector3.Dot(axisY, normal)) +
                Mathf.Abs(Vector3.Dot(axisZ, normal));

            float centerOffset = Vector3.Dot(center - GetSocketBaseWorldPoint(), normal);
            distance = Mathf.Max(0f, centerOffset + projectedHalfExtent);
            return distance > 0f;
        }

        public void SetPermanentlyClosed(bool closed)
        {
            if (openDoorVisual != null)
            {
                openDoorVisual.SetActive(!closed);
            }

            if (closedDoorVisual != null)
            {
                closedDoorVisual.SetActive(closed);
            }
        }

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(connectorId))
            {
                connectorId = gameObject.name;
            }
        }

        private static Vector3 FlattenHorizontal(Vector3 v)
        {
            Vector3 flat = new Vector3(v.x, 0f, v.z);
            if (flat.sqrMagnitude < 0.0001f)
            {
                return Vector3.forward;
            }

            return flat.normalized;
        }

        private static Vector3 OrientNormalByGuidePoint(Vector3 normal, Vector3 planePoint, Transform guidePoint)
        {
            if (guidePoint == null || normal.sqrMagnitude < 0.0001f)
            {
                return normal;
            }

            Vector3 guideDelta = guidePoint.position - planePoint;
            if (Vector3.Dot(normal, guideDelta) < 0f)
            {
                return -normal;
            }

            return normal;
        }
    }
}
