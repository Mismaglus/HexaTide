// Scripts/Game/Battle/AI/AggressivePositioningStrategy.cs
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
            // 距离越小，负值越大，分数越高
            return -from.DistanceTo(primaryTarget.UnitRef.Coords);
        }

        public override int MoveCostPenalty => 2; // 稍微惩罚移动，避免在同样距离的格子上反复横跳
    }
}