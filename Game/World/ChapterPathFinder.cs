using System.Collections.Generic;
using UnityEngine;
using Core.Hex;
using Game.Grid;
using Game.Battle; // For BattleHexGrid reference only

namespace Game.World
{
    /// <summary>
    /// A lightweight A* pathfinder specifically for the Chapter Map.
    /// It ignores BattleRules, AP, and Factions.
    /// It respects Terrain Obstacles and the Tide Level.
    /// </summary>
    public static class ChapterPathfinder
    {
        public static List<HexCoords> FindPath(HexCoords start, HexCoords end, BattleHexGrid grid)
        {
            if (grid == null) return null;

            // 1. Validate Target
            if (!IsWalkable(end, grid)) return null;

            // 2. Setup A* Data
            var frontier = new PriorityQueue<HexCoords>();
            frontier.Enqueue(start, 0);

            var cameFrom = new Dictionary<HexCoords, HexCoords>();
            var costSoFar = new Dictionary<HexCoords, int>();

            cameFrom[start] = start;
            costSoFar[start] = 0;

            // 3. Search Loop
            while (frontier.Count > 0)
            {
                var current = frontier.Dequeue();

                if (current.Equals(end)) break;

                foreach (var next in current.Neighbors())
                {
                    if (!IsWalkable(next, grid)) continue;

                    // Standard movement cost is 1 for everything on the map 
                    // (unless you want Swamp/Roads later)
                    int newCost = costSoFar[current] + 1;

                    if (!costSoFar.ContainsKey(next) || newCost < costSoFar[next])
                    {
                        costSoFar[next] = newCost;
                        int priority = newCost + current.DistanceTo(end); // Heuristic
                        frontier.Enqueue(next, priority);
                        cameFrom[next] = current;
                    }
                }
            }

            // 4. Reconstruct Path
            if (!cameFrom.ContainsKey(end)) return null; // No path found

            var path = new List<HexCoords>();
            var curr = end;
            while (!curr.Equals(start))
            {
                path.Add(curr);
                curr = cameFrom[curr];
            }
            path.Reverse(); // Start -> End

            return path;
        }

        private static bool IsWalkable(HexCoords c, BattleHexGrid grid)
        {
            HexCell cell = null;
            if (grid != null) grid.TryGetCell(c, out cell);

            if (cell == null && ChapterMapManager.Instance != null)
            {
                var node = ChapterMapManager.Instance.GetNodeAt(c);
                if (node != null) cell = node.GetComponent<HexCell>();
            }

            if (cell == null) return false; // Gap/Hole in map

            // B. Logic Checks
            if (cell.IsFlooded) return false; // Blocked by Tide
            if (cell.terrainType == HexTerrainType.Obstacle || cell.terrainType == HexTerrainType.Wall) return false; // Blocked by Terrain

            return true;
        }
    }

    // Helper for A*
    public class PriorityQueue<T>
    {
        private List<KeyValuePair<T, int>> elements = new List<KeyValuePair<T, int>>();

        public int Count => elements.Count;

        public void Enqueue(T item, int priority)
        {
            elements.Add(new KeyValuePair<T, int>(item, priority));
        }

        public T Dequeue()
        {
            int bestIndex = 0;
            for (int i = 0; i < elements.Count; i++)
            {
                if (elements[i].Value < elements[bestIndex].Value) bestIndex = i;
            }

            T bestItem = elements[bestIndex].Key;
            elements.RemoveAt(bestIndex);
            return bestItem;
        }
    }
}
