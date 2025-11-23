using System.Collections.Generic;
using Core.Hex;
using Game.Grid;

namespace Game.Battle.Abilities
{
    public static class TargetingResolver
    {
        // 获取范围内的格子
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

        // ⭐ 修复：使用 AbilityTargetType 替代旧的 TargetFaction
        public static bool IsTargetTypeValid(BattleUnit caster, BattleUnit target, AbilityTargetType type)
        {
            // 1. 如果目标位置没有单位 (target == null)
            if (target == null)
            {
                // 只有 "空地" 或 "任意" 类型允许无单位
                return type == AbilityTargetType.EmptyTile || type == AbilityTargetType.AnyTile;
            }

            // 2. 如果目标位置有单位，根据类型判断
            switch (type)
            {
                case AbilityTargetType.EnemyUnit:
                    // 必须是敌人 (阵营不同)
                    return caster.isPlayer != target.isPlayer;

                case AbilityTargetType.FriendlyUnit:
                    // 必须是友军 (阵营相同)
                    return caster.isPlayer == target.isPlayer;

                case AbilityTargetType.EmptyTile:
                    // 既然 target != null，说明这里有单位，所以不是空地 -> 无效
                    return false;

                case AbilityTargetType.AnyTile:
                    // 任意类型，有单位也行
                    return true;

                default:
                    return true;
            }
        }
    }
}