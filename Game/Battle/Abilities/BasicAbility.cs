using UnityEngine;
using Core.Hex;
using Game.Units;

namespace Game.Battle.Abilities
{
    [CreateAssetMenu(menuName = "Battle/Ability/Basic")]
    public class BasicAbility : Ability
    {
        public override bool IsValidTarget(BattleUnit caster, AbilityContext ctx)
        {
            // 1. 基础检查
            if (!base.IsValidTarget(caster, ctx)) return false;

            // 2. 目标类型检查
            // 如果 Context 里有目标单位，检查单位类型是否匹配
            if (ctx.TargetUnits.Count > 0)
            {
                var target = ctx.TargetUnits[0];

                // ⭐ 修复：调用新的 API，传入 targetType
                if (!TargetingResolver.IsTargetTypeValid(caster, target, targetType))
                    return false;
            }
            else
            {
                // 如果 Context 里没单位（点的是空地），但技能要求必须有单位 -> 无效
                if (targetType == AbilityTargetType.EnemyUnit ||
                    targetType == AbilityTargetType.FriendlyUnit ||
                    targetType == AbilityTargetType.Self)
                {
                    return false;
                }
            }

            // 3. 距离检查
            HexCoords casterC;
            if (!TryGetUnitCoords(caster, out casterC))
                casterC = ctx.Origin; // fallback

            HexCoords targetC;
            // 优先取单位坐标，没有则取地块坐标
            if (ctx.TargetUnits.Count > 0 && TryGetUnitCoords(ctx.TargetUnits[0], out targetC))
            {
            }
            else if (ctx.TargetTiles.Count > 0)
            {
                targetC = ctx.TargetTiles[0];
            }
            else
            {
                return false;
            }

            int d = casterC.DistanceTo(targetC);
            return d >= minRange && d <= maxRange;
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