using System.Collections.Generic;
using UnityEngine;
using Core.Hex;

namespace Game.Battle.AI
{
    public enum KeepDistanceScope
    {
        PrimaryTargetOnly,
        AllOpponentsTotalDistance
    }

    [CreateAssetMenu(menuName = "Battle/AI/Enemy Positioning/Keep Distance (Flee)")]
    public class KeepDistancePositioningStrategy : EnemyPositioningStrategy
    {
        [Tooltip("PrimaryTargetOnly: kite away from current focus. AllOpponentsTotalDistance: maximize summed distance to all opposing units.")]
        public KeepDistanceScope scope = KeepDistanceScope.PrimaryTargetOnly;

        public override string DisplayName => "Keep Distance";
        public override int GetPositionScore(HexCoords from, BattleUnit primaryTarget, IReadOnlyList<BattleUnit> allTargets)
        {
            if (scope == KeepDistanceScope.AllOpponentsTotalDistance && allTargets != null)
            {
                int sum = 0;
                foreach (var t in allTargets)
                {
                    if (t == null) continue;
                    sum += from.DistanceTo(t.UnitRef.Coords);
                }
                return sum;
            }

            if (primaryTarget == null) return 0;
            return from.DistanceTo(primaryTarget.UnitRef.Coords);
        }

        public override bool AllowMoveForAttack(HexCoords from, HexCoords to, BattleUnit primaryTarget, IReadOnlyList<BattleUnit> allTargets)
        {
            // Do not allow moves that reduce preferred distance.
            return GetPositionScore(to, primaryTarget, allTargets) >= GetPositionScore(from, primaryTarget, allTargets);
        }

        public override int MoveCostPenalty => 0;

        public override bool TryPickFallbackDestination(
            HexCoords myPos,
            BattleUnit target,
            IEnumerable<HexCoords> candidates,
            IReadOnlyList<BattleUnit> allTargets,
            int preferredMinRange,
            int preferredMaxRange,
            out HexCoords destination)
        {
            destination = myPos;
            if (target == null) return false;

            var currentScore = GetPositionScore(myPos, target, allTargets);
            var bestScore = currentScore;
            var found = false;

            foreach (var c in candidates)
            {
                // Keep within attack window so we don't kite out of range.
                int distToPrimary = target.UnitRef.Coords.DistanceTo(c);
                if (distToPrimary < preferredMinRange || distToPrimary > preferredMaxRange) continue;

                var score = GetPositionScore(c, target, allTargets);
                if (score > bestScore)
                {
                    bestScore = score;
                    destination = c;
                    found = true;
                }
            }

            return found;
        }
    }
}
