using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Core.Hex;
using Game.Units;
using Game.Battle;          // 引用 BattleUnit, BattleRules
using Game.Battle.Abilities; // 引用 Ability
using Game.Grid;            // 引用 IHexGridProvider 等

namespace Game.Battle.AI
{
    // 定义 AI 的决策结果
    public struct AIPlan
    {
        public Ability ability;       // 打算用的技能
        public HexCoords moveDest;    // 打算走到哪里
        public BattleUnit target;     // 打算打哪个单位
        public HexCoords targetCell;  // 打算打哪个格子
        public int score;             // 评分
        public bool isValid;          // 是否有可行方案
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
            // 查找全局战斗规则
            _rules = FindFirstObjectByType<BattleRules>(FindObjectsInactive.Exclude);
        }

        /// <summary>
        /// 核心思考函数：返回当前局势下的最佳行动
        /// </summary>
        public AIPlan Think()
        {
            var bestPlan = new AIPlan { isValid = false, score = -9999 };

            if (_unit == null || _mover == null) return bestPlan;

            // 1. 获取所有活着的玩家单位
            var allPlayers = FindObjectsByType<BattleUnit>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(u => u.isPlayer && u.Attributes.Core.HP > 0)
                .ToList();

            if (allPlayers.Count == 0) return bestPlan;

            // 2. 遍历所有可用技能
            foreach (var ability in _unit.abilities)
            {
                if (!ability.CanUse(_unit)) continue;

                // 3. 模拟不移动直接打
                EvaluateAbility(ability, _unit.UnitRef.Coords, allPlayers, ref bestPlan, false);

                // 4. 模拟移动后打
                if (_mover.strideLeft > 0)
                {
                    var moveCandidates = GetMoveCandidates();
                    foreach (var movePos in moveCandidates)
                    {
                        EvaluateAbility(ability, movePos, allPlayers, ref bestPlan, true);
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
                // 简单范围搜索，实际可配合 Pathfinder
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
            // 简化版逻辑：遍历所有玩家，看谁在射程内且收益最高
            foreach (var target in targets)
            {
                // 检查射程
                int dist = castPos.DistanceTo(target.UnitRef.Coords);
                if (dist < ability.minRange || dist > ability.maxRange) continue;

                // === 简单评分逻辑 ===
                int score = 100;

                // 优先击杀残血 (<50 HP)
                if (target.Attributes.Core.HP < 50) score += 50;

                // 距离惩罚 (稍微倾向于打近的，避免风筝)
                score -= dist;

                // 移动惩罚 (稍微倾向于原地输出，省步数)
                if (requiresMove) score -= 10;

                // 如果找到了更高分的方案，记录下来
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