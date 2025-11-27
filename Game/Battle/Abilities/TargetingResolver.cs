using System.Collections.Generic;
using Core.Hex;
using Game.Grid;
using Game.Units;
using UnityEngine;

namespace Game.Battle.Abilities
{
    public static class TargetingResolver
    {
        private const int INFINITE_RANGE = 60;

        public static IEnumerable<HexCoords> TilesInRange(IHexGridProvider grid, HexCoords origin, int minRange, int maxRange)
        {
            if (grid == null) yield break;
            int r = (maxRange == -1) ? INFINITE_RANGE : maxRange;

            foreach (var c in origin.Disk(r))
            {
                int d = origin.DistanceTo(c);
                if (d >= minRange && d <= r)
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

        // === 核心 AOE 计算 ===
        public static IEnumerable<HexCoords> GetAOETiles(HexCoords origin, Ability ability, HexCoords? casterPos = null)
        {
            int effectiveRadius = (ability.aoeRadius == -1) ? INFINITE_RANGE : ability.aoeRadius;

            switch (ability.shape)
            {
                case TargetShape.Single:
                case TargetShape.Self:
                    return new List<HexCoords> { origin };

                case TargetShape.Disk:
                    return origin.Disk(effectiveRadius);

                case TargetShape.Ring:
                    return origin.Ring(effectiveRadius);

                case TargetShape.Cone:
                    // 需要施法者位置来计算方向
                    if (casterPos == null) return new List<HexCoords> { origin };
                    // ⭐ 使用配置的角度
                    return GetConeTiles(casterPos.Value, origin, effectiveRadius, ability.coneAngle);

                case TargetShape.Line:
                    // 需要施法者位置来计算方向
                    if (casterPos == null) return new List<HexCoords> { origin };
                    // ⭐ 使用配置的宽度
                    return GetThickLineTiles(casterPos.Value, origin, effectiveRadius, ability.lineWidth);

                default:
                    return new List<HexCoords> { origin };
            }
        }

        // === 扇形算法 (角度可配) ===
        private static IEnumerable<HexCoords> GetConeTiles(HexCoords start, HexCoords target, int range, float angleWidth)
        {
            var results = new List<HexCoords>();

            Vector3 startPos = start.ToWorld(1f, true);
            Vector3 targetPos = target.ToWorld(1f, true);
            Vector3 castDirection = (targetPos - startPos).normalized;

            if (castDirection == Vector3.zero) castDirection = Vector3.forward;

            float halfAngle = angleWidth / 2f;

            // 遍历扇形半径范围内的所有格子
            foreach (var c in start.Disk(range))
            {
                if (c.Equals(start)) continue;

                Vector3 tilePos = c.ToWorld(1f, true);
                Vector3 directionToTile = (tilePos - startPos).normalized;

                // 角度检查 (+1f 容差)
                if (Vector3.Angle(castDirection, directionToTile) <= halfAngle + 1f)
                {
                    results.Add(c);
                }
            }
            return results;
        }

        // === ⭐ 新增：宽直线算法 (Thick Line) ===
        // 逻辑：遍历射程内的所有格子，计算格子中心到“施法射线”的垂直距离。
        // 如果距离 <= width / 2，则判定命中。
        private static IEnumerable<HexCoords> GetThickLineTiles(HexCoords start, HexCoords target, int range, float width)
        {
            var results = new List<HexCoords>();

            Vector3 P0 = start.ToWorld(1f, true); // 射线起点
            Vector3 targetPos = target.ToWorld(1f, true);
            Vector3 dir = (targetPos - P0).normalized; // 射线方向 (单位向量)

            if (dir == Vector3.zero) dir = Vector3.forward;

            float halfWidth = width * 0.5f;

            // 优化：只遍历 range 范围内的格子 (Disk)，而不是全图
            foreach (var c in start.Disk(range))
            {
                if (c.Equals(start)) continue; // 排除自己

                Vector3 P = c.ToWorld(1f, true); // 格子中心点
                Vector3 V = P - P0; // 起点到格子的向量

                // 1. 投影长度 check (防止打到背后的格子)
                float dot = Vector3.Dot(V, dir);
                if (dot < 0) continue; // 在背后

                // 2. 垂直距离 check
                // 垂直距离公式 (3D): magnitude of cross product / magnitude of direction
                // 因为 dir 是 normalized，分母为1，直接算 Cross 的模长即可
                // 但我们是在 2D 平面 (XZ)，简化为：除去投影分量后的残余向量长度
                Vector3 projection = dir * dot;
                Vector3 rejection = V - projection; // 垂线向量
                float distSq = rejection.sqrMagnitude;

                if (distSq <= halfWidth * halfWidth)
                {
                    results.Add(c);
                }
            }

            return results;
        }

        // === 获取目标单位 ===
        public static List<BattleUnit> GatherTargets(BattleUnit caster, HexCoords origin, Ability ability)
        {
            var results = new List<BattleUnit>();
            var occupancy = Object.FindFirstObjectByType<GridOccupancy>();

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

            HexCoords? casterPos = caster != null ? caster.UnitRef.Coords : (HexCoords?)null;

            var area = GetAOETiles(origin, ability, casterPos);

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