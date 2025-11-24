using System.Collections.Generic;
using UnityEngine;
using Core.Hex;
using Game.Units;

namespace Game.Battle.AI
{
    /// <summary>
    /// Strategy object that decides how an enemy prefers to position itself relative to a target.
    /// Extend this ScriptableObject to add more movement tendencies.
    /// </summary>
    public abstract class EnemyPositioningStrategy : ScriptableObject
    {
        public abstract string DisplayName { get; }

        /// <summary>
        /// Higher score means the position is more desirable for this strategy.
        /// primaryTarget may be null when no clear target exists.
        /// allTargets are the currently relevant opposing units (usually all players).
        /// </summary>
        public abstract int GetPositionScore(HexCoords from, BattleUnit primaryTarget, IReadOnlyList<BattleUnit> allTargets);

        /// <summary>
        /// Whether moving from 'from' to 'to' for an attack is acceptable for this tendency.
        /// Default: always true.
        /// </summary>
        public virtual bool AllowMoveForAttack(HexCoords from, HexCoords to, BattleUnit primaryTarget, IReadOnlyList<BattleUnit> allTargets)
        {
            return true;
        }

        /// <summary>
        /// Optional penalty applied when a plan requires moving to cast.
        /// Default 0 to let positioning score drive the decision.
        /// </summary>
        public virtual int MoveCostPenalty => 0;

        /// <summary>
        /// Pick a target unit the AI should react to. Default: nearest player.
        /// </summary>
        public virtual BattleUnit PickReferenceTarget(List<BattleUnit> candidates, HexCoords myPos)
        {
            BattleUnit best = null;
            var bestDistance = int.MaxValue;

            foreach (var c in candidates)
            {
                var d = myPos.DistanceTo(c.UnitRef.Coords);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = c;
                }
            }

            return best;
        }

        /// <summary>
        /// Choose the best move destination among walkable candidates.
        /// Returns true only if a position strictly improves upon the current score.
        /// preferredMinRange / preferredMaxRange can be used by strategies that care about staying in weapon range.
        /// </summary>
        public virtual bool TryPickFallbackDestination(
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
