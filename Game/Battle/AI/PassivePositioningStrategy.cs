// Scripts/Game/Battle/AI/PassivePositioningStrategy.cs
using System.Collections.Generic;
using UnityEngine;
using Core.Hex;

namespace Game.Battle.AI
{
    [CreateAssetMenu(menuName = "Battle/AI/Enemy Positioning/Passive (Guard)")]
    public class PassivePositioningStrategy : EnemyPositioningStrategy
    {
        public override string DisplayName => "Passive Guard";

        public override int GetPositionScore(HexCoords from, BattleUnit primaryTarget, IReadOnlyList<BattleUnit> allTargets)
        {
            // 消极 AI 不在乎离敌人多近，它只在乎是不是动了
            // 这里返回 0，意味着所有位置评分一样，但是...
            return 0;
        }

        // 移动惩罚极高，除非不得不动（例如为了施放技能）
        public override int MoveCostPenalty => 50;

        public override bool AllowMoveForAttack(HexCoords from, HexCoords to, BattleUnit primaryTarget, IReadOnlyList<BattleUnit> allTargets)
        {
            // 允许为了攻击而移动，但因为惩罚高，它会优先选近的
            return true;
        }
    }
}