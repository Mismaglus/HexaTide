// Script/Game/Battle/Abilities/TargetingResolver.cs
using System.Collections.Generic;
using Core.Hex;
using Game.Grid;

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

        public static bool IsFactionAllowed(BattleUnit caster, BattleUnit target, TargetFaction rule)
        {
            if (rule == TargetFaction.Any) return true;
            if (rule == TargetFaction.SelfOnly) return ReferenceEquals(caster, target);
            bool ally = caster.isPlayer == target.isPlayer;
            return rule == TargetFaction.Ally ? ally : !ally;
        }
    }
}
