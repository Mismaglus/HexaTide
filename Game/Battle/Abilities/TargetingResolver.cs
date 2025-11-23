using System.Collections.Generic;
using Core.Hex;
using Game.Grid;
using Game.Units;
using UnityEngine;

namespace Game.Battle.Abilities
{
    public static class TargetingResolver
    {
        // === 范围计算 ===
        public static IEnumerable<HexCoords> TilesInRange(IHexGridProvider grid, HexCoords origin, int minRange, int maxRange)
        {
            if (grid == null) yield break;
            foreach (var c in origin.Disk(maxRange))
            {
                int d = origin.DistanceTo(c);
                if (d >= minRange && d <= maxRange)
                    yield return c;
            }
        }

        // === 有效性检查 ===
        public static bool IsTargetTypeValid(BattleUnit caster, BattleUnit target, AbilityTargetType type)
        {
            if (target == null)
                return type == AbilityTargetType.EmptyTile || type == AbilityTargetType.AnyTile;

            switch (type)
            {
                case AbilityTargetType.EnemyUnit: return caster.isPlayer != target.isPlayer;
                case AbilityTargetType.FriendlyUnit: return caster.isPlayer == target.isPlayer;
                case AbilityTargetType.Self: return caster.gameObject == target.gameObject;
                case AbilityTargetType.EmptyTile: return false;
                case AbilityTargetType.AnyTile: return true;
                default: return true;
            }
        }

        // === ⭐ 核心新增：AOE 目标搜寻 ===
        public static List<BattleUnit> GatherTargets(BattleUnit caster, HexCoords origin, Ability ability)
        {
            var results = new List<BattleUnit>();

            // 1. 找到地图数据源
            var occupancy = Object.FindFirstObjectByType<GridOccupancy>();

            // 允许兜底：如果占位表缺失或未注册，直接扫描场景里的 BattleUnit，按坐标建字典
            Dictionary<HexCoords, BattleUnit> unitsByCoords = null;
            void BuildFallbackCache()
            {
                if (unitsByCoords != null) return;
                unitsByCoords = new Dictionary<HexCoords, BattleUnit>();
                foreach (var bu in Object.FindObjectsByType<BattleUnit>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                {
                    if (bu == null || bu.UnitRef == null) continue;
                    unitsByCoords[bu.UnitRef.Coords] = bu;
                }
            }
            if (occupancy == null) BuildFallbackCache();

            // 2. 根据形状生成覆盖的格子
            IEnumerable<HexCoords> area = null;
            switch (ability.shape)
            {
                case TargetShape.Single:
                    // 单体只需要看原点
                    area = new List<HexCoords> { origin };
                    break;
                case TargetShape.Disk:
                    // 实心圆
                    area = origin.Disk(ability.aoeRadius);
                    break;
                case TargetShape.Ring:
                    // 空心环
                    area = origin.Ring(ability.aoeRadius);
                    break;
                // TODO: Cone, Line 等以后可以在这里扩展
                default:
                    area = new List<HexCoords> { origin };
                    break;
            }

            // 3. 遍历格子，抓取单位并筛选
            foreach (var c in area)
            {
                BattleUnit bu = null;

                bool found = false;
                if (occupancy != null && occupancy.TryGetUnitAt(c, out Unit unit))
                {
                    bu = unit.GetComponent<BattleUnit>();
                    found = bu != null;
                }
                if (!found)
                {
                    BuildFallbackCache();
                    unitsByCoords?.TryGetValue(c, out bu);
                }

                if (bu == null) continue;

                // --- 筛选逻辑 ---
                bool isSelf = (bu == caster);
                bool isAlly = (bu.isPlayer == caster.isPlayer);
                bool isEnemy = !isAlly;

                // 1. 自己
                if (isSelf)
                {
                    if (!ability.affectSelf) continue; // 技能不包含自己，跳过
                }
                // 2. 队友 (非自己)
                else if (isAlly)
                {
                    if (!ability.affectAllies) continue;
                }
                // 3. 敌人
                else if (isEnemy)
                {
                    if (!ability.affectEnemies) continue;
                }

                results.Add(bu);
            }

            return results;
        }
    }
}
