using System.Collections.Generic;
using Matrix.PCG;

namespace Framework.LogicLayer.Module.SpawnSystem
{
    /// <summary>
    /// BFS shortest-path extensions for RoomGraph.
    /// </summary>
    public static class RoomGraphExtensions
    {
        /// <summary>
        /// Returns the shortest graph distance (in edges) from node A to node B.
        /// Returns int.MaxValue if no path exists.
        /// </summary>
        public static int GetShortestDistance(this RoomGraph graph, int from, int to)
        {
            if (graph == null) return int.MaxValue;
            if (from < 0 || to < 0 || from >= graph.NodeCount || to >= graph.NodeCount)
                return int.MaxValue;
            if (from == to) return 0;

            var visited = new HashSet<int>();
            var queue = new Queue<(int NodeId, int Distance)>();

            queue.Enqueue((from, 0));
            visited.Add(from);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                int nextDistance = current.Distance + 1;
                var neighbors = graph.GetNeighborsSorted(current.NodeId);

                foreach (int neighbor in neighbors)
                {
                    if (visited.Contains(neighbor)) continue;
                    visited.Add(neighbor);

                    if (neighbor == to)
                        return nextDistance;

                    queue.Enqueue((neighbor, nextDistance));
                }
            }

            return int.MaxValue;
        }
    }
}
