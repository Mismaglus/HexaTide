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

        // ZOC 惩罚值
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

        // ⭐ 新增参数：ignorePenalties
        public int GetMoveCost(HexCoords from, HexCoords to, Unit mover, bool ignorePenalties = false)
        {
            // 1. 基础地形消耗
            if (!_cellCache.TryGetValue(to, out var targetCell)) return 999; // 无效格子

            // 即使无视地形消耗，如果是完全不可走的墙壁，通常依然不可走
            // (除非是“飞行”单位，那是另一个逻辑)
            if (!targetCell.IsTerrainWalkable) return 999;

            // 如果 ignorePenalties = true，基础消耗强制为 1，否则查表
            int cost = ignorePenalties ? 1 : targetCell.GetBaseMoveCost();

            // 2. 检查是否有单位阻挡
            if (_occupancy != null && _occupancy.TryGetUnitAt(to, out var blocker))
            {
                if (_rules.IsEnemy(mover, blocker)) return 999;
            }

            // 3. ZOC (Zone of Control) 判定
            // 如果 ignorePenalties = true，跳过 ZOC 计算
            if (!ignorePenalties && IsInZoneOfControl(from, mover))
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