using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Core.Hex;
using Game.Units;
using Game.Battle.Abilities;
using Game.Grid; // for Pathfinder

namespace Game.Battle.AI
{
    // 定义一个结构来描述“想干什么”
    public struct AIPlan
    {
        public Ability ability;       // 用哪个技能
        public HexCoords moveDest;    // 走到哪里
        public BattleUnit target;     // 打谁 (如果是单位目标)
        public HexCoords targetCell;  // 打哪里 (如果是地面目标)
        public int score;             // 这个计划多少分
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

        /// <summary>
        /// 核心思考函数：返回当前局势下的最佳行动
        /// </summary>
        public AIPlan Think()
        {
            var bestPlan = new AIPlan { isValid = false, score = -9999 };

            if (_unit == null || _mover == null) return bestPlan;

            // 1. 获取所有潜在目标（玩家单位）
            var allPlayers = FindObjectsByType<BattleUnit>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(u => u.isPlayer && u.Attributes.Core.HP > 0)
                .ToList();

            if (allPlayers.Count == 0) return bestPlan;

            // 2. 遍历所有可用技能
            foreach (var ability in _unit.abilities)
            {
                if (!ability.CanUse(_unit)) continue;

                // 3. 模拟：如果不移动直接打
                EvaluateAbility(ability, _unit.UnitRef.Coords, allPlayers, ref bestPlan, false);

                // 4. 模拟：如果需要移动 (简化的移动逻辑：尝试靠近目标)
                // 注意：为了性能，这里我们只找“能打到人的位置”，而不是遍历全图
                if (_mover.strideLeft > 0)
                {
                    // 找到所有玩家周围的“射程内空位”作为候选移动点
                    // (这里为了演示简化逻辑，实际可以遍历 Unit 可达范围)
                    var moveCandidates = GetMoveCandidates();
                    foreach (var movePos in moveCandidates)
                    {
                        EvaluateAbility(ability, movePos, allPlayers, ref bestPlan, true);
                    }
                }
            }

            return bestPlan;
        }

        // 获取当前步数能走到的所有格子 (简单的 BFS 或者是已有的 GetStepCandidates)
        HashSet<HexCoords> GetMoveCandidates()
        {
            // 这里简单直接调用 Mover 的逻辑，或者用 Pathfinder 算出的范围
            // 为了演示，我们假设 UnitMover 知道自己能走到哪 (或者暂时只搜寻周围几圈)
            var candidates = new HashSet<HexCoords>();
            var start = _unit.UnitRef.Coords;
            int range = _mover.strideLeft;

            // 简单粗暴：获取移动范围内所有可走格子
            // 实际项目中应该缓存这个结果，避免重复计算
            if (_rules != null)
            {
                // 使用简单的 Disk 搜索，然后用 Rules 过滤
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
                // 检查射程
                int dist = castPos.DistanceTo(target.UnitRef.Coords);
                if (dist < ability.minRange || dist > ability.maxRange) continue;

                // 简单评分：伤害量
                // (注：这里没真正跑 Effect，只是预估。如果想要精确，需要模拟 CombatCalculator)
                int score = 100;

                // 优先击杀残血
                if (target.Attributes.Core.HP < 50) score += 50;

                // 距离惩罚 (稍微倾向于打近的，或者远的看职业)
                score -= dist;

                // 移动惩罚 (稍微倾向于站桩输出，省 AP/Stride)
                if (requiresMove) score -= 10;

                // 刷新最高分
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