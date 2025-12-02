// Scripts/Game/Battle/AI/KeepDistancePositioningStrategy.cs
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
            // 分数 = 距离。距离越远，分数越高 -> 逃跑
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
            // 胆小鬼不应该为了攻击而缩短距离（除非必须？）
            // 这里允许它为了攻击移动，只要最终位置评分更高（即更远，或同样远）
            // 但通常为了攻击需要靠近，所以这会阻止它去送死
            int scoreFrom = GetPositionScore(from, primaryTarget, allTargets);
            int scoreTo = GetPositionScore(to, primaryTarget, allTargets);

            return scoreTo >= scoreFrom;
        }

        public override int MoveCostPenalty => 0;

        // 核心逃跑逻辑：在移动候选点中，找一个离敌人最远的
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
                // 对于纯逃跑（Flee），我们可能不关心 AttackRangeEnvelope
                // 但如果是 Archer Kite，我们希望保持在 MaxRange 边缘
                // 如果 preferredMaxRange == 0 或很大，说明没技能可用，那就是纯逃跑

                // 简单处理：如果没技能(Max=int.max)，无视Range限制，只管跑
                // 如果有技能，尽量不要跑出射程（Kiting），除非被迫

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