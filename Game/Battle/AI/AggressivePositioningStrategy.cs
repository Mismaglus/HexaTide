using System.Collections.Generic;
using UnityEngine;
using Core.Hex;

namespace Game.Battle.AI
{
    [CreateAssetMenu(menuName = "Battle/AI/Enemy Positioning/Aggressive (Chase)")]
    public class AggressivePositioningStrategy : EnemyPositioningStrategy
    {
        public override string DisplayName => "Aggressive";
        public override int GetPositionScore(HexCoords from, BattleUnit primaryTarget, IReadOnlyList<BattleUnit> allTargets)
        {
            if (primaryTarget == null) return 0;
            return -from.DistanceTo(primaryTarget.UnitRef.Coords);
        }

        public override int MoveCostPenalty => 2;
    }
}
