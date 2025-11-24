using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Core.Hex;
using Game.Units;
using Game.Battle;
using Game.Battle.Abilities;
using Game.Grid;

namespace Game.Battle.AI
{
    public struct AIPlan
    {
        public Ability ability;
        public HexCoords moveDest;
        public BattleUnit target;
        public HexCoords targetCell;
        public int score;
        public bool isValid;
    }

    [RequireComponent(typeof(BattleUnit))]
    public class EnemyBrain : MonoBehaviour
    {
        private BattleUnit _unit;
        private UnitMover _mover;
        private BattleRules _rules;

        void Awake()
        {
            _unit = GetComponent<BattleUnit>();
            _mover = GetComponent<UnitMover>();
            _rules = FindFirstObjectByType<BattleRules>(FindObjectsInactive.Exclude);
        }

        public AIPlan Think()
        {
            var bestPlan = new AIPlan { isValid = false, score = -9999 };

            if (_unit == null || _mover == null) return bestPlan;

            var allPlayers = FindObjectsByType<BattleUnit>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(u => u.isPlayer && u.Attributes.Core.HP > 0)
                .ToList();

            if (allPlayers.Count == 0) return bestPlan;

            // --- Phase A: 尝试攻击 ---
            foreach (var ability in _unit.abilities)
            {
                if (!ability.CanUse(_unit)) continue;

                // 1. 原地打
                EvaluateAbility(ability, _unit.UnitRef.Coords, allPlayers, ref bestPlan, false);

                // 2. 走几步打
                if (_mover.strideLeft > 0)
                {
                    var moveCandidates = GetMoveCandidates();
                    foreach (var movePos in moveCandidates)
                    {
                        EvaluateAbility(ability, movePos, allPlayers, ref bestPlan, true);
                    }
                }
            }

            // --- Phase B: 追击 (如果无法攻击，就靠近) ---
            // ⭐⭐⭐ 新增：如果 bestPlan 无效，寻找最近的敌人并移动
            if (!bestPlan.isValid && _mover.strideLeft > 0)
            {
                BattleUnit closestTarget = null;
                int minDis = int.MaxValue;
                HexCoords myPos = _unit.UnitRef.Coords;

                // 1. 找最近的玩家
                foreach (var p in allPlayers)
                {
                    int d = myPos.DistanceTo(p.UnitRef.Coords);
                    if (d < minDis) { minDis = d; closestTarget = p; }
                }

                if (closestTarget != null)
                {
                    // 2. 找一个离他最近的、我能走到的格子
                    var moveCandidates = GetMoveCandidates();
                    HexCoords bestMove = myPos;
                    int bestDistToTarget = minDis;

                    foreach (var c in moveCandidates)
                    {
                        int d = c.DistanceTo(closestTarget.UnitRef.Coords);
                        // 找到一个能更靠近目标的格子
                        if (d < bestDistToTarget)
                        {
                            bestDistToTarget = d;
                            bestMove = c;
                        }
                    }

                    // 如果找到了更好的位置，且不是原地
                    if (!bestMove.Equals(myPos))
                    {
                        bestPlan = new AIPlan
                        {
                            isValid = true,
                            moveDest = bestMove,
                            ability = null, // 没技能可用，只移动
                            target = null,
                            score = 0
                        };
                    }
                }
            }

            return bestPlan;
        }

        HashSet<HexCoords> GetMoveCandidates()
        {
            var candidates = new HashSet<HexCoords>();
            var start = _unit.UnitRef.Coords;
            int range = _mover.strideLeft;

            if (_rules != null)
            {
                foreach (var c in start.Disk(range))
                {
                    if (_rules.IsTileWalkable(c) || c.Equals(start))
                        candidates.Add(c);
                }
            }
            return candidates;
        }

        void EvaluateAbility(Ability ability, HexCoords castPos, List<BattleUnit> targets, ref AIPlan bestPlan, bool requiresMove)
        {
            foreach (var target in targets)
            {
                int dist = castPos.DistanceTo(target.UnitRef.Coords);
                if (dist < ability.minRange || dist > ability.maxRange) continue;

                int score = 100;
                if (target.Attributes.Core.HP < 50) score += 50;
                score -= dist;
                if (requiresMove) score -= 10;

                if (score > bestPlan.score)
                {
                    bestPlan = new AIPlan
                    {
                        ability = ability,
                        moveDest = castPos,
                        target = target,
                        targetCell = target.UnitRef.Coords,
                        score = score,
                        isValid = true
                    };
                }
            }
        }
    }
}