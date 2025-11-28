using System.Collections.Generic;
using UnityEngine;
using Core.Hex;
using Game.Battle;
using Game.Units;

namespace Game.Grid
{
    public static class HexPathfinder
    {
        /// <summary>
        /// 寻找从 start 到 end 的最短路径，考虑地形消耗和 ZOC。
        /// </summary>
        public static List<HexCoords> FindPath(HexCoords start, HexCoords end, BattleRules rules, Unit unit)
        {
            // 1. 初始化计算器
            var occupancy = Object.FindFirstObjectByType<GridOccupancy>();
            var calculator = new MovementCalculator(occupancy, rules);

            // 2. 检查目标点是否被完全阻挡 (例如是墙壁或被敌人占据)
            // 注意：GetMoveCost 会返回 999 表示不可通行
            if (calculator.GetMoveCost(start, end, unit) >= 999)
            {
                // 特殊情况：如果目标点虽然有单位，但是我们只是想寻路到它旁边攻击，
                // A* 应该寻路到 Adjacent。但这里是 Move 寻路，所以如果终点不可走，就是不可达。
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
                if (safetyCounter++ > 1000) break;

                var current = frontier.Dequeue();

                if (current.Equals(end)) break;

                foreach (var next in current.Neighbors())
                {
                    // 计算从 current -> next 的实际消耗 (含 Terrain + ZOC)
                    int moveCost = calculator.GetMoveCost(current, next, unit);

                    // 如果不可通行
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