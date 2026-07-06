using System;
using System.Collections.Generic;
using Matrix.PCG.Instances;
using UnityEngine;

namespace Matrix.PCG
{
    public sealed class PcgRoomRoot : MonoBehaviour
    {
        [SerializeField]
        private RoomRole defaultRole = RoomRole.Connector;

        [SerializeField]
        private List<PcgResourcePointMarker> resourcePoints = new List<PcgResourcePointMarker>();

        [SerializeField]
        private List<PcgSpawnPointMarker> spawnPoints = new List<PcgSpawnPointMarker>();

        [SerializeField]
        private List<PcgBossSpawnPointMarker> bossSpawnPoints = new List<PcgBossSpawnPointMarker>();

        [SerializeField]
        private List<PcgDefenseObjectivePointMarker> defenseObjectivePoints = new List<PcgDefenseObjectivePointMarker>();

        [SerializeField]
        private List<PcgConnectorMarker> connectors = new List<PcgConnectorMarker>();

        [SerializeField]
        private PcgRoomBounds roomBounds;

        [Header("Minimap")]
        [Tooltip("Pre-baked top-down thumbnail of this room variant, used for minimap stitching.")]
        [SerializeField]
        private Texture2D minimapThumbnail;

        public RoomRole DefaultRole => defaultRole;
        public IReadOnlyList<PcgResourcePointMarker> ResourcePoints => resourcePoints;
        public IReadOnlyList<PcgSpawnPointMarker> SpawnPoints => spawnPoints;
        public IReadOnlyList<PcgBossSpawnPointMarker> BossSpawnPoints => bossSpawnPoints;
        public IReadOnlyList<PcgDefenseObjectivePointMarker> DefenseObjectivePoints => defenseObjectivePoints;
        public IReadOnlyList<PcgConnectorMarker> Connectors => connectors;
        public PcgRoomBounds RoomBounds => roomBounds;
        public Texture2D MinimapThumbnail => minimapThumbnail;

        public int ConnectorCount => connectors != null ? connectors.Count : 0;

        public bool HasBossPoint => bossSpawnPoints != null && bossSpawnPoints.Count > 0;
        public bool HasDefenseObjectivePoint => defenseObjectivePoints != null && defenseObjectivePoints.Count > 0;

        [ContextMenu("Refresh PCG Node Cache")]
        public void RefreshNodeCache()
        {
            EnsureLists();
            resourcePoints.Clear();
            spawnPoints.Clear();
            bossSpawnPoints.Clear();
            defenseObjectivePoints.Clear();
            connectors.Clear();

            GetComponentsInChildren(true, resourcePoints);
            GetComponentsInChildren(true, spawnPoints);
            GetComponentsInChildren(true, bossSpawnPoints);
            GetComponentsInChildren(true, defenseObjectivePoints);
            GetComponentsInChildren(true, connectors);
            roomBounds = GetComponentInChildren<PcgRoomBounds>(true);

            SortByHierarchyPath(resourcePoints);
            SortByHierarchyPath(spawnPoints);
            SortByHierarchyPath(bossSpawnPoints);
            SortByHierarchyPath(defenseObjectivePoints);
            SortByHierarchyPath(connectors);
        }

        public bool TryGetWorldBounds(out Bounds bounds, out float padding)
        {
            padding = 0.05f;

            if (roomBounds != null && roomBounds.TryGetWorldBounds(out bounds))
            {
                padding = roomBounds.OverlapPadding;
                return true;
            }

            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider col = colliders[i];
                if (col == null)
                {
                    continue;
                }

                bounds = col.bounds;
                for (int j = i + 1; j < colliders.Length; j++)
                {
                    Collider next = colliders[j];
                    if (next != null)
                    {
                        bounds.Encapsulate(next.bounds);
                    }
                }

                return true;
            }

            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null)
                {
                    continue;
                }

                bounds = r.bounds;
                for (int j = i + 1; j < renderers.Length; j++)
                {
                    Renderer next = renderers[j];
                    if (next != null)
                    {
                        bounds.Encapsulate(next.bounds);
                    }
                }

                return true;
            }

            bounds = default;
            return false;
        }

        public List<PcgConnectorMarker> GetAvailableConnectors(bool outgoing, HashSet<PcgConnectorMarker> used)
        {
            List<PcgConnectorMarker> result = new List<PcgConnectorMarker>();
            if (connectors == null)
            {
                return result;
            }

            for (int i = 0; i < connectors.Count; i++)
            {
                PcgConnectorMarker marker = connectors[i];
                if (marker == null)
                {
                    continue;
                }

                if (used != null && used.Contains(marker))
                {
                    continue;
                }

                if (outgoing && !marker.SupportsOutgoing)
                {
                    continue;
                }

                if (!outgoing && !marker.SupportsIncoming)
                {
                    continue;
                }

                result.Add(marker);
            }

            return result;
        }

        private void OnValidate()
        {
            RefreshNodeCache();
        }

        private void EnsureLists()
        {
            if (resourcePoints == null)
            {
                resourcePoints = new List<PcgResourcePointMarker>();
            }

            if (spawnPoints == null)
            {
                spawnPoints = new List<PcgSpawnPointMarker>();
            }

            if (bossSpawnPoints == null)
            {
                bossSpawnPoints = new List<PcgBossSpawnPointMarker>();
            }

            if (defenseObjectivePoints == null)
            {
                defenseObjectivePoints = new List<PcgDefenseObjectivePointMarker>();
            }

            if (connectors == null)
            {
                connectors = new List<PcgConnectorMarker>();
            }
        }

        private static void SortByHierarchyPath<T>(List<T> list) where T : Component
        {
            if (list == null)
            {
                return;
            }

            list.Sort((a, b) =>
            {
                string pathA = BuildPath(a != null ? a.transform : null);
                string pathB = BuildPath(b != null ? b.transform : null);
                return string.CompareOrdinal(pathA, pathB);
            });
        }

        private static string BuildPath(Transform t)
        {
            if (t == null)
            {
                return string.Empty;
            }

            Stack<string> stack = new Stack<string>();
            Transform current = t;
            while (current != null)
            {
                stack.Push(current.name);
                current = current.parent;
            }

            return string.Join("/", stack.ToArray());
        }
    }
}
