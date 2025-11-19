// Script/Game/Battle/Abilities/BasicAbility.cs
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
            if (!base.IsValidTarget(caster, ctx)) return false;
            if (ctx.TargetUnits.Count != 1) return false;

            var target = ctx.TargetUnits[0];
            if (!TargetingResolver.IsFactionAllowed(caster, target, targetFaction)) return false;

            // get caster coords
            HexCoords casterC;
            if (!TryGetUnitCoords(caster, out casterC))
            {
                // fallback to context origin if provided by caller
                casterC = ctx.Origin;
            }

            // get target coords
            HexCoords targetC;
            if (!TryGetUnitCoords(target, out targetC))
            {
                // fallback: if caller填了目标格（例如从选中地块得到）
                if (ctx.TargetTiles.Count > 0) targetC = ctx.TargetTiles[0];
                else return false;
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
