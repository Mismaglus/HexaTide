using System.Collections.Generic;
using UnityEngine;
using Core.Hex;
using Game.Battle;
using Game.Units;

namespace Game.Grid
{
    /// <summary>
    /// A* 寻路与 Dijkstra 范围计算工具类
    /// </summary>
    public static class HexPathfinder
    {
        /// <summary>
        /// 寻找从 start 到 end 的最短路径。
        /// </summary>
        public static List<HexCoords> FindPath(HexCoords start, HexCoords end, BattleRules rules, Unit unit, bool ignorePenalties = false)
        {
            // 1. 初始化计算器
            var occupancy = Object.FindFirstObjectByType<GridOccupancy>();
            var calculator = new MovementCalculator(occupancy, rules);

            // 2. 检查目标点是否被完全阻挡
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

            // 防止搜索过多 (Max Steps)
            int safetyCounter = 0;

            while (frontier.Count > 0)
            {
                if (safetyCounter++ > 2000) break;

                var current = frontier.Dequeue();

                if (current.Equals(end)) break;

                foreach (var next in current.Neighbors())
                {
                    int moveCost = calculator.GetMoveCost(current, next, unit, ignorePenalties);
                    if (moveCost >= 999) continue;

                    int newCost = costSoFar[current] + moveCost;

                    if (!costSoFar.ContainsKey(next) || newCost < costSoFar[next])
                    {
                        costSoFar[next] = newCost;
                        int priority = newCost + current.DistanceTo(end); // 启发式
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

        /// <summary>
        /// 获取所有可到达的格子及其消耗 (Dijkstra Flood Fill)
        /// </summary>
        public static Dictionary<HexCoords, int> GetReachableCells(HexCoords start, int maxCost, BattleRules rules, Unit unit, bool ignorePenalties = false)
        {
            var results = new Dictionary<HexCoords, int>();

            // 1. 初始化
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

                // 如果当前消耗已经很大，虽然还能往外看，但通常没必要过度搜索
                if (currentCost >= maxCost) continue;

                foreach (var next in current.Neighbors())
                {
                    // 计算单步消耗 (含地形 + ZOC)
                    int moveCost = calculator.GetMoveCost(current, next, unit, ignorePenalties);

                    // 999 代表阻挡/不可通行
                    if (moveCost >= 999) continue;

                    int newCost = currentCost + moveCost;

                    // 只有在总消耗 <= maxCost 时才加入结果
                    if (newCost <= maxCost)
                    {
                        if (!costSoFar.ContainsKey(next) || newCost < costSoFar[next])
                        {
                            costSoFar[next] = newCost;
                            frontier.Enqueue(next, newCost);

                            // 记录结果
                            results[next] = newCost;
                        }
                    }
                }
            }

            return results;
        }

        // 简单的优先队列 (最小堆) 实现
        class PriorityQueue<T>
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
}