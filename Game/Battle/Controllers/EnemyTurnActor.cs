using System.Collections;
using UnityEngine;
using Core.Hex;
using Game.Units;
using Game.Battle.Actions;
using Game.Battle.Abilities;
using Game.Battle.AI;
using Game.Grid;

namespace Game.Battle
{
    [RequireComponent(typeof(EnemyBrain))]
    public class EnemyTurnActor : MonoBehaviour, ITurnActor
    {
        [Header("Systems")]
        public ActionQueue queue;
        public AbilityRunner runner;
        public BattleRules rules;

        // 视觉引用删掉了，因为不需要了

        private EnemyBrain _brain;
        private UnitMover _mover;
        private BattleUnit _unit;

        void Awake()
        {
            _brain = GetComponent<EnemyBrain>();
            _mover = GetComponent<UnitMover>();
            _unit = GetComponent<BattleUnit>();
            if (queue == null) queue = FindFirstObjectByType<ActionQueue>();
            if (runner == null) runner = FindFirstObjectByType<AbilityRunner>();
            if (rules == null) rules = FindFirstObjectByType<BattleRules>();
        }

        public void OnTurnStart() { _unit?.ResetTurnResources(); }
        public bool HasPendingAction => false;

        public IEnumerator TakeTurn()
        {
            if (_brain == null) yield break;

            // 1. 思考
            AIPlan plan = _brain.Think();

            if (plan.isValid)
            {
                Debug.Log($"[EnemyAI] 行动: 移动到 {plan.moveDest} -> 攻击 {plan.target?.name ?? "Ground"}");

                // 2. 直接执行移动
                if (!plan.moveDest.Equals(_unit.UnitRef.Coords))
                {
                    var path = HexPathfinder.FindPath(_unit.UnitRef.Coords, plan.moveDest, rules, _unit.UnitRef);
                    if (path != null && path.Count > 0)
                    {
                        queue.Enqueue(new PathMoveAction(_mover, path));
                    }
                }

                // 3. 直接执行技能
                var ctx = new AbilityContext { Caster = _unit, Origin = plan.moveDest };
                if (plan.target != null) ctx.TargetUnits.Add(plan.target);
                ctx.TargetTiles.Add(plan.targetCell);

                queue.Enqueue(new AbilityAction(plan.ability, ctx, runner));

                yield return queue.RunAll();
            }
            else
            {
                yield return new WaitForSeconds(0.2f); // 稍微发呆一下表示“我在思考但放弃了”
            }
        }
    }
}