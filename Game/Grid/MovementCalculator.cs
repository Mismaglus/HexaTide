using System.Collections.Generic;
using Core.Hex;
using Game.Battle;
using Game.Units;
using UnityEngine;

namespace Game.Grid
{
    public class MovementCalculator
    {
        private readonly GridOccupancy _occupancy;
        private readonly BattleRules _rules;
        private readonly Dictionary<HexCoords, HexCell> _cellCache;

        // ZOC 惩罚值：如果在敌人旁边移动，额外消耗多少 AP/Stride
        private const int ZOC_PENALTY = 1;

        public MovementCalculator(GridOccupancy occupancy, BattleRules rules)
        {
            _occupancy = occupancy;
            _rules = rules;
            _cellCache = new Dictionary<HexCoords, HexCell>();

            // 建立 Cell 缓存
            var allCells = Object.FindObjectsByType<HexCell>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var cell in allCells)
            {
                if (cell != null) _cellCache[cell.Coords] = cell;
            }
        }

        public int GetMoveCost(HexCoords from, HexCoords to, Unit mover)
        {
            // 1. 基础地形消耗
            if (!_cellCache.TryGetValue(to, out var targetCell)) return 999; // 无效格子
            if (!targetCell.IsTerrainWalkable) return 999; // 地形不可走

            int cost = targetCell.GetBaseMoveCost();

            // 2. 检查是否有单位阻挡 (友军可穿过但不能停留，敌军不可穿过)
            // 注意：A* 算法中，如果是中间节点，通常允许穿过友军。
            // 这里我们简化：如果有单位且是敌人，则不可通行 (Cost无限)
            if (_occupancy != null && _occupancy.TryGetUnitAt(to, out var blocker))
            {
                if (_rules.IsEnemy(mover, blocker)) return 999;
            }

            // 3. ZOC (Zone of Control) 判定
            // 规则：如果“出发点(from)”在敌人的 ZOC 内，且“目标点(to)”也在敌人的 ZOC 内，或者试图脱离 ZOC
            // 简单来说：只要出发时身边有敌人，移动就变慢。
            if (IsInZoneOfControl(from, mover))
            {
                cost += ZOC_PENALTY;
            }

            return cost;
        }

        private bool IsInZoneOfControl(HexCoords center, Unit friendlyUnit)
        {
            if (_occupancy == null || _rules == null) return false;

            foreach (var neighbor in center.Neighbors())
            {
                if (_occupancy.TryGetUnitAt(neighbor, out var unit))
                {
                    // 如果旁边站着敌人，则处于 ZOC
                    if (_rules.IsEnemy(friendlyUnit, unit))
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}