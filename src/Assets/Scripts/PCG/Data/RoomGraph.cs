using System;
using System.Collections.Generic;
using UnityEngine;

namespace Matrix.PCG
{
    [Serializable]
    public sealed class RoomGraphNode
    {
        public int Id;
        public Vector2Int GridPosition;
        public RoomRole AssignedRole = RoomRole.Connector;

        public bool HasAssignedSideTask;
        public SideTaskType AssignedSideTask;
    }

    [Serializable]
    public sealed class RoomGraphEdge
    {
        public int NodeA;
        public int NodeB;
        public bool IsLoopEdge;

        public bool Contains(int nodeId)
        {
            return NodeA == nodeId || NodeB == nodeId;
        }

        public int Other(int nodeId)
        {
            if (NodeA == nodeId)
            {
                return NodeB;
            }

            return NodeB;
        }
    }

    [Serializable]
    public sealed class RoomGraph
    {
        private readonly List<RoomGraphNode> _nodes = new List<RoomGraphNode>();
        private readonly List<RoomGraphEdge> _edges = new List<RoomGraphEdge>();
        private readonly List<int> _primaryRingNodeIds = new List<int>();
        private readonly HashSet<int> _primaryRingLookup = new HashSet<int>();

        public IReadOnlyList<RoomGraphNode> Nodes => _nodes;
        public IReadOnlyList<RoomGraphEdge> Edges => _edges;
        public IReadOnlyList<int> PrimaryRingNodeIds => _primaryRingNodeIds;

        public int NodeCount => _nodes.Count;

        public int AddNode(Vector2Int gridPosition)
        {
            int id = _nodes.Count;
            _nodes.Add(new RoomGraphNode
            {
                Id = id,
                GridPosition = gridPosition,
                AssignedRole = RoomRole.Connector
            });

            return id;
        }

        public RoomGraphNode GetNode(int nodeId)
        {
            return _nodes[nodeId];
        }

        public bool AddEdge(int nodeA, int nodeB, bool isLoopEdge)
        {
            if (nodeA == nodeB || HasEdge(nodeA, nodeB))
            {
                return false;
            }

            _edges.Add(new RoomGraphEdge
            {
                NodeA = Mathf.Min(nodeA, nodeB),
                NodeB = Mathf.Max(nodeA, nodeB),
                IsLoopEdge = isLoopEdge
            });

            return true;
        }

        public bool HasEdge(int nodeA, int nodeB)
        {
            int low = Mathf.Min(nodeA, nodeB);
            int high = Mathf.Max(nodeA, nodeB);

            for (int i = 0; i < _edges.Count; i++)
            {
                RoomGraphEdge edge = _edges[i];
                if (edge.NodeA == low && edge.NodeB == high)
                {
                    return true;
                }
            }

            return false;
        }

        public int GetDegree(int nodeId)
        {
            int degree = 0;
            for (int i = 0; i < _edges.Count; i++)
            {
                if (_edges[i].Contains(nodeId))
                {
                    degree++;
                }
            }

            return degree;
        }

        public List<int> GetNeighborsSorted(int nodeId)
        {
            List<int> neighbors = new List<int>();

            for (int i = 0; i < _edges.Count; i++)
            {
                RoomGraphEdge edge = _edges[i];
                if (edge.NodeA == nodeId)
                {
                    neighbors.Add(edge.NodeB);
                }
                else if (edge.NodeB == nodeId)
                {
                    neighbors.Add(edge.NodeA);
                }
            }

            neighbors.Sort();
            return neighbors;
        }

        public void SetPrimaryRingNodes(IList<int> nodeIds)
        {
            _primaryRingNodeIds.Clear();
            _primaryRingLookup.Clear();

            if (nodeIds == null)
            {
                return;
            }

            for (int i = 0; i < nodeIds.Count; i++)
            {
                int nodeId = nodeIds[i];
                if (nodeId < 0 || nodeId >= _nodes.Count || _primaryRingLookup.Contains(nodeId))
                {
                    continue;
                }

                _primaryRingNodeIds.Add(nodeId);
                _primaryRingLookup.Add(nodeId);
            }
        }

        public bool IsPrimaryRingNode(int nodeId)
        {
            return _primaryRingLookup.Contains(nodeId);
        }
    }
}
