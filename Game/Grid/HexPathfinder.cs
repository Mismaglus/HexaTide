using System.Collections.Generic;
using UnityEngine;
using Core.Hex;
using Game.Battle;
using Game.Units;

namespace Game.Grid
{
    public static class HexPathfinder
    {
        // ⭐ 增加 ignorePenalties 参数
        public static List<HexCoords> FindPath(HexCoords start, HexCoords end, BattleRules rules, Unit unit, bool ignorePenalties = false)
        {
            var occupancy = Object.FindFirstObjectByType<GridOccupancy>();
            var calculator = new MovementCalculator(occupancy, rules);

            // 检查终点是否合法
            if (calculator.GetMoveCost(start, end, unit, ignorePenalties) >= 999)
            {
                Debug.LogWarning($"[Pathfinder] Target {end} is blocked or unreachable terrain.");
                return null;
            }

            var frontier = new PriorityQueue<HexCoords>();
            frontier.Enqueue(start, 0);

            var cameFrom = new Dictionary<HexCoords, HexCoords>();
            var costSoFar = new Dictionary<HexCoords, int>();

            cameFrom[start] = start;
            costSoFar[start] = 0;

            int safetyCounter = 0;

            while (frontier.Count > 0)
            {
                if (safetyCounter++ > 2000) break;

                var current = frontier.Dequeue();

                if (current.Equals(end)) break;

                foreach (var next in current.Neighbors())
                {
                    // 传递参数
                    int moveCost = calculator.GetMoveCost(current, next, unit, ignorePenalties);
                    if (moveCost >= 999) continue;

                    int newCost = costSoFar[current] + moveCost;

                    if (!costSoFar.ContainsKey(next) || newCost < costSoFar[next])
                    {
                        costSoFar[next] = newCost;
                        int priority = newCost + current.DistanceTo(end);
                        frontier.Enqueue(next, priority);
                        cameFrom[next] = current;
                    }
                }
            }

            if (!cameFrom.ContainsKey(end)) return null;

            var path = new List<HexCoords>();
            var curr = end;
            while (!curr.Equals(start))
            {
                path.Add(curr);
                curr = cameFrom[curr];
            }
            path.Reverse();
            return path;
        }

        // ⭐ 增加 ignorePenalties 参数
        public static Dictionary<HexCoords, int> GetReachableCells(HexCoords start, int maxCost, BattleRules rules, Unit unit, bool ignorePenalties = false)
        {
            var results = new Dictionary<HexCoords, int>();
            var occupancy = Object.FindFirstObjectByType<GridOccupancy>();
            var calculator = new MovementCalculator(occupancy, rules);

            var frontier = new PriorityQueue<HexCoords>();
            frontier.Enqueue(start, 0);

            var costSoFar = new Dictionary<HexCoords, int>();
            costSoFar[start] = 0;
            results[start] = 0;

            int safetyCounter = 0;

            while (frontier.Count > 0)
            {
                if (safetyCounter++ > 5000) break;

                var current = frontier.Dequeue();
                int currentCost = costSoFar[current];

                if (currentCost >= maxCost) continue;

                foreach (var next in current.Neighbors())
                {
                    // 传递参数
                    int moveCost = calculator.GetMoveCost(current, next, unit, ignorePenalties);

                    if (moveCost >= 999) continue;

                    int newCost = currentCost + moveCost;

                    if (newCost <= maxCost)
                    {
                        if (!costSoFar.ContainsKey(next) || newCost < costSoFar[next])
                        {
                            costSoFar[next] = newCost;
                            frontier.Enqueue(next, newCost);
                            results[next] = newCost;
                        }
                    }
                }
            }

            return results;
        }

        class PriorityQueue<T>
        {
            private List<KeyValuePair<T, int>> elements = new List<KeyValuePair<T, int>>();
            public int Count => elements.Count;
            public void Enqueue(T item, int priority) => elements.Add(new KeyValuePair<T, int>(item, priority));
            public T Dequeue()
            {
                int bestIndex = 0;
                for (int i = 0; i < elements.Count; i++)
                    if (elements[i].Value < elements[bestIndex].Value) bestIndex = i;
                T bestItem = elements[bestIndex].Key;
                elements.RemoveAt(bestIndex);
                return bestItem;
            }
        }
    }
}