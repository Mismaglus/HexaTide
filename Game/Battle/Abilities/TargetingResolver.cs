using System.Collections.Generic;
using Core.Hex;
using Game.Grid;
using Game.Units;
using UnityEngine;

namespace Game.Battle.Abilities
{
    public static class TargetingResolver
    {
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

        // === ⭐ 新增：纯粹获取 AOE 覆盖的格子 (不关心上面有没有人) ===
        public static IEnumerable<HexCoords> GetAOETiles(HexCoords origin, Ability ability)
        {
            switch (ability.shape)
            {
                case TargetShape.Single:
                case TargetShape.Self:
                    return new List<HexCoords> { origin };

                case TargetShape.Disk:
                    return origin.Disk(ability.aoeRadius);

                case TargetShape.Ring:
                    return origin.Ring(ability.aoeRadius);

                // TODO: Cone (需要施法者位置来计算方向)
                default:
                    return new List<HexCoords> { origin };
            }
        }

        // === GatherTargets 现在调用上面的方法 ===
        public static List<BattleUnit> GatherTargets(BattleUnit caster, HexCoords origin, Ability ability)
        {
            var results = new List<BattleUnit>();
            var occupancy = Object.FindFirstObjectByType<GridOccupancy>();

            // 兜底缓存逻辑 (略，保持原样或根据上一版代码)
            Dictionary<HexCoords, BattleUnit> unitsByCoords = null;
            void BuildFallbackCache()
            {
                if (unitsByCoords != null) return;
                unitsByCoords = new Dictionary<HexCoords, BattleUnit>();
                foreach (var bu in Object.FindObjectsByType<BattleUnit>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                {
                    if (bu && bu.UnitRef) unitsByCoords[bu.UnitRef.Coords] = bu;
                }
            }
            if (occupancy == null) BuildFallbackCache();

            // ⭐ 使用拆分出来的逻辑获取范围
            var area = GetAOETiles(origin, ability);

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

                bool isSelf = (bu == caster);
                bool isAlly = (bu.isPlayer == caster.isPlayer);
                bool isEnemy = !isAlly;

                if (isSelf) { if (!ability.affectSelf) continue; }
                else if (isAlly) { if (!ability.affectAllies) continue; }
                else if (isEnemy) { if (!ability.affectEnemies) continue; }

                results.Add(bu);
            }

            return results;
        }
    }
}