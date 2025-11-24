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
        [Header("Positioning")]
        [Tooltip("Controls how this enemy prefers to position relative to targets (aggressive/keep distance/etc).")]
        public EnemyPositioningStrategy positioningStrategy;

        [Header("Targeting")]
        public TargetSelectionRule targetRule = TargetSelectionRule.Nearest;

        private BattleUnit _unit;
        private UnitMover _mover;
        private BattleRules _rules;
        private EnemyPositioningStrategy _runtimeDefaultStrategy;

        void Awake()
        {
            _unit = GetComponent<BattleUnit>();
            _mover = GetComponent<UnitMover>();
            _rules = FindFirstObjectByType<BattleRules>(FindObjectsInactive.Exclude);
        }

        public AIPlan Think()
        {
            var strategy = GetPositioningStrategy();
            var bestPlan = new AIPlan { isValid = false, score = -9999 };

            if (_unit == null || _mover == null) return bestPlan;

            var allPlayers = FindObjectsByType<BattleUnit>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(u => u.isPlayer && u.Attributes.Core.HP > 0)
                .ToList();

            if (allPlayers.Count == 0) return bestPlan;

            // Pick primary target according to the configured rule
            var primaryTarget = SelectPrimaryTarget(allPlayers, _unit.UnitRef.Coords);
            var targetList = primaryTarget != null ? new List<BattleUnit> { primaryTarget } : allPlayers;

            // --- Phase A: 尝试攻击 ---
            foreach (var ability in _unit.abilities)
            {
                if (!ability.CanUse(_unit)) continue;

                // 1. 原地打
                EvaluateAbility(ability, _unit.UnitRef.Coords, targetList, allPlayers, strategy, ref bestPlan, false);

                // 2. 走几步打
                if (_mover.strideLeft > 0)
                {
                    var moveCandidates = GetMoveCandidates();
                    foreach (var movePos in moveCandidates)
                    {
                        EvaluateAbility(ability, movePos, targetList, allPlayers, strategy, ref bestPlan, true);
                    }
                }
            }

            // --- Phase B: 追击 (如果无法攻击，就靠近) ---
            // ⭐⭐⭐ 新增：如果 bestPlan 无效，寻找最近的敌人并移动
            if (!bestPlan.isValid && _mover.strideLeft > 0)
            {
                HexCoords myPos = _unit.UnitRef.Coords;
                var referenceTarget = primaryTarget ?? strategy.PickReferenceTarget(allPlayers, myPos);

                if (referenceTarget != null)
                {
                    var moveCandidates = GetMoveCandidates();
                    var (prefMinRange, prefMaxRange) = DetermineAttackRangeEnvelope();
                    if (strategy.TryPickFallbackDestination(myPos, referenceTarget, moveCandidates, allPlayers, prefMinRange, prefMaxRange, out var bestMove))
                    {
                        bestPlan = new AIPlan
                        {
                            isValid = true,
                            moveDest = bestMove,
                            ability = null, // 没技能可用，只移动
                            target = null,
                            targetCell = bestMove,
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

        EnemyPositioningStrategy GetPositioningStrategy()
        {
            if (positioningStrategy != null) return positioningStrategy;
            if (_runtimeDefaultStrategy == null) _runtimeDefaultStrategy = ScriptableObject.CreateInstance<AggressivePositioningStrategy>();
            return _runtimeDefaultStrategy;
        }

        void EvaluateAbility(Ability ability, HexCoords castPos, List<BattleUnit> targets, List<BattleUnit> allOpponents, EnemyPositioningStrategy strategy, ref AIPlan bestPlan, bool requiresMove)
        {
            var currentPos = _unit.UnitRef.Coords;

            foreach (var target in targets)
            {
                int dist = castPos.DistanceTo(target.UnitRef.Coords);
                if (dist < ability.minRange || dist > ability.maxRange) continue;

                // For cautious behaviors, skip moves that violate the desired distance.
                if (requiresMove && !strategy.AllowMoveForAttack(currentPos, castPos, target, allOpponents)) continue;

                int score = 100;
                if (target.Attributes.Core.HP < 50) score += 50;
                score += strategy.GetPositionScore(castPos, target, allOpponents);
                if (requiresMove) score -= strategy.MoveCostPenalty;

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

        BattleUnit SelectPrimaryTarget(List<BattleUnit> candidates, HexCoords myPos)
        {
            if (candidates == null || candidates.Count == 0) return null;

            BattleUnit best = null;
            int bestMetric = targetRule == TargetSelectionRule.Farthest ? int.MinValue : int.MaxValue;

            switch (targetRule)
            {
                case TargetSelectionRule.Player:
                    // Prefer player-controlled; if multiple, pick nearest among them, else fall back to nearest overall
                    var players = candidates.Where(c => c.isPlayer).ToList();
                    if (players.Count > 0) return PickByDistance(players, myPos, pickFarthest: false);
                    return PickByDistance(candidates, myPos, pickFarthest: false);

                case TargetSelectionRule.Farthest:
                    return PickByDistance(candidates, myPos, pickFarthest: true);

                case TargetSelectionRule.Nearest:
                default:
                    return PickByDistance(candidates, myPos, pickFarthest: false);
            }
        }

        BattleUnit PickByDistance(IEnumerable<BattleUnit> candidates, HexCoords myPos, bool pickFarthest)
        {
            BattleUnit best = null;
            int bestDist = pickFarthest ? int.MinValue : int.MaxValue;

            foreach (var c in candidates)
            {
                if (c == null) continue;
                int d = myPos.DistanceTo(c.UnitRef.Coords);
                if (pickFarthest)
                {
                    if (d > bestDist) { bestDist = d; best = c; }
                }
                else
                {
                    if (d < bestDist) { bestDist = d; best = c; }
                }
            }

            return best;
        }

        (int minRange, int maxRange) DetermineAttackRangeEnvelope()
        {
            int minRange = int.MaxValue;
            int maxRange = 0;

            foreach (var ability in _unit.abilities)
            {
                if (ability == null || !ability.CanUse(_unit)) continue;
                if (ability.minRange < minRange) minRange = ability.minRange;
                if (ability.maxRange > maxRange) maxRange = ability.maxRange;
            }

            if (minRange == int.MaxValue) minRange = 0;
            if (maxRange == 0) maxRange = int.MaxValue;
            return (minRange, maxRange);
        }
    }
}
