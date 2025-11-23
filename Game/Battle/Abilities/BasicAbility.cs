using UnityEngine;
using Core.Hex;
using Game.Units;                 // <-- for UnitMover

namespace Game.Battle.Abilities
{
    [CreateAssetMenu(menuName = "Battle/Ability/Basic")]
    public class BasicAbility : Ability
    {
        public override bool IsValidTarget(BattleUnit caster, AbilityContext ctx)
        {
            // 1. 基础检查 (基类检查 Ability.cs 里的通用逻辑)
            if (!base.IsValidTarget(caster, ctx)) return false;

            // BasicAbility 默认逻辑是“必须有一个目标单位”
            // 如果是空地技能，TargetUnits 可能为空，这里需要根据 targetType 灵活处理
            // 但为了修复当前报错，我们先假设 BasicAbility 主要是针对单位的技能
            if (ctx.TargetUnits.Count > 0)
            {
                var target = ctx.TargetUnits[0];

                // ⭐ 修复核心：使用新的 IsTargetTypeValid 和 targetType
                if (!TargetingResolver.IsTargetTypeValid(caster, target, targetType))
                    return false;
            }
            else
            {
                // 如果没有目标单位，但技能类型是“针对单位”的，则无效
                if (targetType == AbilityTargetType.EnemyUnit || targetType == AbilityTargetType.FriendlyUnit)
                    return false;
            }

            // --- 距离检查 ---

            // get caster coords
            HexCoords casterC;
            if (!TryGetUnitCoords(caster, out casterC))
            {
                // fallback to context origin if provided by caller
                casterC = ctx.Origin;
            }

            // get target coords
            HexCoords targetC;
            if (ctx.TargetUnits.Count > 0 && TryGetUnitCoords(ctx.TargetUnits[0], out targetC))
            {
                // 优先使用目标单位的坐标
            }
            else if (ctx.TargetTiles.Count > 0)
            {
                // 如果没有单位，或者取不到坐标，使用选中的地块坐标
                targetC = ctx.TargetTiles[0];
            }
            else
            {
                return false;
            }

            int d = casterC.DistanceTo(targetC);
            return d >= minRange && d <= maxRange; // e.g. 1..1 for melee
        }

        private static bool TryGetUnitCoords(BattleUnit u, out HexCoords c)
        {
            c = default;
            if (u == null) return false;
            var mover = u.GetComponent<UnitMover>();
            if (mover == null) return false;
            c = mover._mCoords;
            return true;
        }
    }
}