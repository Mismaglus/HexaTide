using System.Collections.Generic;
using UnityEngine;
using Core.Hex;
using Game.Battle;
using Game.Units;

namespace Game.Grid
{
    /// <summary>
    /// A* 寻路算法工具类
    /// </summary>
    public static class HexPathfinder
    {
        /// <summary>
        /// 寻找从 start 到 end 的最短路径。
        /// </summary>
        /// <param name="rules">用于判断格子是否可行走</param>
        /// <param name="unit">移动的单位（可选，用于处理飞行等特殊逻辑）</param>
        public static List<HexCoords> FindPath(HexCoords start, HexCoords end, BattleRules rules, Unit unit)
        {
            // 1. 基础检查
            // 如果目标点不可行走 (例如是障碍物)，直接返回 null
            // 注意：如果目标点是单位，IsTileWalkable 会返回 false。
            // 这里的逻辑假设必须点击空地移动。如果要攻击移动，逻辑会不同。
            if (!rules.IsTileWalkable(end))
            {
                Debug.LogWarning($"[Pathfinder] Target {end} is not walkable.");
                return null;
            }

            // A* 核心数据结构
            var frontier = new PriorityQueue<HexCoords>();
            frontier.Enqueue(start, 0);

            var cameFrom = new Dictionary<HexCoords, HexCoords>();
            var costSoFar = new Dictionary<HexCoords, int>();

            cameFrom[start] = start;
            costSoFar[start] = 0;

            while (frontier.Count > 0)
            {
                var current = frontier.Dequeue();

                if (current.Equals(end)) break;

                foreach (var next in current.Neighbors())
                {
                    // 询问规则：这一格能走吗？
                    if (!rules.IsTileWalkable(next)) continue;

                    // 计算代价 (默认为 1)
                    // 未来可以在这里接入 MovementCostProvider
                    int newCost = costSoFar[current] + 1;

                    if (!costSoFar.ContainsKey(next) || newCost < costSoFar[next])
                    {
                        costSoFar[next] = newCost;
                        // 启发式函数：距离目标的距离
                        int priority = newCost + current.DistanceTo(end);
                        frontier.Enqueue(next, priority);
                        cameFrom[next] = current;
                    }
                }
            }

            // 构建路径
            if (!cameFrom.ContainsKey(end)) return null; // 没路

            var path = new List<HexCoords>();
            var curr = end;
            while (!curr.Equals(start))
            {
                path.Add(curr);
                curr = cameFrom[curr];
            }
            path.Reverse(); // 翻转列表 (从 Start -> End)
            return path;
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