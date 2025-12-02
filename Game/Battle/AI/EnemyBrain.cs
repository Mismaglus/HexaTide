// Scripts/Game/Battle/AI/EnemyBrain.cs
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
    public enum AIPersonality
    {
        Aggressive, // 激进：感知到就追击
        Passive,    // 消极/守卫：只有看见了才打，否则保持巡逻
        Cowardly    // 胆小：感知到就跑 (盲跑限速)
    }

    public enum IdleBehaviorType
    {
        Stationary, // 原地不动
        Patrol      // 巡逻
    }

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
        [Header("Personality & Strategy")]
        public AIPersonality personality = AIPersonality.Aggressive;

        [Tooltip("Controls how this enemy prefers to position relative to targets (aggressive/keep distance/etc).")]
        public EnemyPositioningStrategy positioningStrategy;

        [Header("Targeting")]
        public TargetSelectionRule targetRule = TargetSelectionRule.Nearest;

        [Header("Idle / Patrol")]
        public IdleBehaviorType idleType = IdleBehaviorType.Stationary;
        [Tooltip("Add Hex Coordinates here relative to grid (q, r). If empty, uses current pos.")]
        public List<Vector2Int> patrolWaypoints = new List<Vector2Int>();

        private int _currentPatrolIndex = 0;

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
            AIPlan bestPlan = new AIPlan { isValid = false, score = -9999 };
            if (_unit == null || _mover == null) return bestPlan;

            // 1. 感知阶段：获取所有玩家，并区分“可见”和“感知”
            var allPlayers = FindObjectsByType<BattleUnit>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(u => u.isPlayer && u.Attributes.Core.HP > 0)
                .ToList();

            if (allPlayers.Count == 0) return bestPlan;

            int sightRange = 6;
            int senseRange = 8;
            if (_unit.Attributes != null)
            {
                sightRange = _unit.Attributes.Optional.SightRange;
                senseRange = sightRange + _unit.Attributes.Optional.SenseRangeBonus;
            }

            var visibleTargets = new List<BattleUnit>();
            var sensedTargets = new List<BattleUnit>(); // 包含可见 + 仅感知的

            foreach (var p in allPlayers)
            {
                int dist = _unit.UnitRef.Coords.DistanceTo(p.UnitRef.Coords);
                if (dist <= sightRange) visibleTargets.Add(p);
                if (dist <= senseRange) sensedTargets.Add(p);
            }

            // 2. 决策阶段：根据性格决定是否进入战斗状态
            bool enterCombat = false;
            List<BattleUnit> activeTargets = null;

            switch (personality)
            {
                case AIPersonality.Aggressive:
                    // 只要感知到，就进入战斗
                    if (sensedTargets.Count > 0)
                    {
                        enterCombat = true;
                        activeTargets = sensedTargets;
                    }
                    break;

                case AIPersonality.Passive:
                    // 只有看见了，才进入战斗；否则忽略感知，继续巡逻
                    if (visibleTargets.Count > 0)
                    {
                        enterCombat = true;
                        activeTargets = visibleTargets;
                    }
                    break;

                case AIPersonality.Cowardly:
                    // 感知到就触发(逃跑)
                    if (sensedTargets.Count > 0)
                    {
                        enterCombat = true;
                        activeTargets = sensedTargets;
                    }
                    break;
            }

            // 3. 执行阶段
            if (enterCombat)
            {
                // === 战斗逻辑 ===
                var strategy = GetPositioningStrategy();

                // 选目标 (优先从可见的选，如果没可见的，就从感知的选)
                var candidates = (visibleTargets.Count > 0) ? visibleTargets : activeTargets;
                var primaryTarget = SelectPrimaryTarget(candidates, _unit.UnitRef.Coords);

                // 如果是胆小鬼，且没有可见目标（被动感知到了），强制限制移动力
                bool isCowardlyBlindFlee = (personality == AIPersonality.Cowardly && visibleTargets.Count == 0);

                // 临时保存原始移动力
                int originalStride = _mover.strideLeft;
                if (isCowardlyBlindFlee)
                {
                    // 强制锁定只移动1格
                    // 注意：这里只是逻辑上的限制，实际 _mover 数据未改，我们在生成 Path 时截断
                    Debug.Log($"[AI] {name} senses threat but can't see. Panic creeping (1 step).");
                }

                // Phase A: 尝试攻击 (必须有视野 visibleTargets)
                // 胆小鬼通常不攻击，除非它是“且战且退”类型。这里假设胆小鬼如果有技能也能放。
                // 关键约束：技能只能对 visibleTargets 释放
                if (visibleTargets.Count > 0)
                {
                    var targetList = (primaryTarget != null && visibleTargets.Contains(primaryTarget))
                        ? new List<BattleUnit> { primaryTarget }
                        : visibleTargets;

                    foreach (var ability in _unit.abilities)
                    {
                        if (!ability.CanUse(_unit)) continue;

                        // 1. 原地打
                        EvaluateAbility(ability, _unit.UnitRef.Coords, targetList, activeTargets, strategy, ref bestPlan, false);

                        // 2. 走几步打 (如果不是盲目逃跑状态)
                        if (_mover.strideLeft > 0 && !isCowardlyBlindFlee)
                        {
                            var moveCandidates = GetMoveCandidates(originalStride);
                            foreach (var movePos in moveCandidates)
                            {
                                EvaluateAbility(ability, movePos, targetList, activeTargets, strategy, ref bestPlan, true);
                            }
                        }
                    }
                }

                // Phase B: 移动 (追击 或 逃跑)
                // 如果没攻击计划，或者只是为了单纯移动
                if (!bestPlan.isValid && _mover.strideLeft > 0)
                {
                    HexCoords myPos = _unit.UnitRef.Coords;
                    var referenceTarget = primaryTarget ?? strategy.PickReferenceTarget(activeTargets, myPos);

                    if (referenceTarget != null)
                    {
                        // 计算移动范围：如果是盲跑，半径只有1
                        int effectiveStride = isCowardlyBlindFlee ? 1 : originalStride;
                        var moveCandidates = GetMoveCandidates(effectiveStride);

                        // 确定理想距离区间
                        var (prefMin, prefMax) = DetermineAttackRangeEnvelope();

                        // 对于胆小鬼(KeepDistance)，它会倾向于选更远的点
                        if (strategy.TryPickFallbackDestination(myPos, referenceTarget, moveCandidates, activeTargets, prefMin, prefMax, out var bestMove))
                        {
                            bestPlan = new AIPlan
                            {
                                isValid = true,
                                moveDest = bestMove,
                                ability = null,
                                target = null,
                                targetCell = bestMove,
                                score = 0
                            };
                        }
                    }
                }
            }
            else
            {
                // === 巡逻/挂机逻辑 ===
                PlanIdleMovement(ref bestPlan);
            }

            return bestPlan;
        }

        // --- 辅助逻辑 ---

        void PlanIdleMovement(ref AIPlan plan)
        {
            if (idleType == IdleBehaviorType.Stationary)
            {
                Debug.Log($"[AI] {name} Idle (Stationary).");
                return; // do nothing
            }

            if (idleType == IdleBehaviorType.Patrol && patrolWaypoints.Count > 0)
            {
                // 获取当前目标点
                Vector2Int pt = patrolWaypoints[_currentPatrolIndex];
                HexCoords targetPatrol = new HexCoords(pt.x, pt.y);

                // 如果已经到了，切到下一个点
                if (_unit.UnitRef.Coords.Equals(targetPatrol))
                {
                    _currentPatrolIndex = (_currentPatrolIndex + 1) % patrolWaypoints.Count;
                    pt = patrolWaypoints[_currentPatrolIndex];
                    targetPatrol = new HexCoords(pt.x, pt.y);
                }

                // 寻路去目标点
                // 这里简单使用 HexPathfinder
                var path = HexPathfinder.FindPath(_unit.UnitRef.Coords, targetPatrol, _rules, _unit.UnitRef);

                if (path != null && path.Count > 0)
                {
                    // 截取当前回合能走的路径
                    int steps = Mathf.Min(path.Count, _mover.strideLeft);
                    HexCoords dest = path[steps - 1];

                    plan.isValid = true;
                    plan.moveDest = dest;
                    plan.ability = null;
                    plan.target = null;
                    plan.targetCell = dest;
                    plan.score = 0;

                    Debug.Log($"[AI] {name} Patrolling to {targetPatrol} (Step to {dest})");
                }
            }
        }

        HashSet<HexCoords> GetMoveCandidates(int range)
        {
            var candidates = new HashSet<HexCoords>();
            var start = _unit.UnitRef.Coords;

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

                if (requiresMove && !strategy.AllowMoveForAttack(currentPos, castPos, target, allOpponents)) continue;

                int score = 100;
                // 优先打残血
                if (target.Attributes.Core.HP < target.Attributes.Core.HPMax * 0.5f) score += 50;

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

            switch (targetRule)
            {
                case TargetSelectionRule.Player:
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

        // 方便在编辑器里添加巡逻点
        [ContextMenu("Add Current Pos to Patrol")]
        void AddCurrentToPatrol()
        {
            var u = GetComponent<Unit>();
            if (u) patrolWaypoints.Add(new Vector2Int(u.Coords.q, u.Coords.r));
        }
    }
}