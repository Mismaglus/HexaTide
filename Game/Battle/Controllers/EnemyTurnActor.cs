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

        public GridOutlineManager outlineManager;

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
            if (outlineManager == null) outlineManager = FindFirstObjectByType<GridOutlineManager>();
        }

        public void OnTurnStart() { _unit?.ResetTurnResources(); }
        public bool HasPendingAction => false;

        public IEnumerator TakeTurn()
        {
            if (_brain == null) yield break;

            AIPlan plan = _brain.Think();

            if (plan.isValid)
            {
                // 1. 清除旧的预警
                if (outlineManager) outlineManager.ClearPlayerIntent();

                // 2. 执行移动
                if (!plan.moveDest.Equals(_unit.UnitRef.Coords))
                {
                    var path = HexPathfinder.FindPath(_unit.UnitRef.Coords, plan.moveDest, rules, _unit.UnitRef);
                    if (path != null && path.Count > 0)
                    {
                        queue.Enqueue(new PathMoveAction(_mover, path));
                    }
                }

                // 3. 执行技能 (如果有)
                if (plan.ability != null)
                {
                    var ctx = new AbilityContext { Caster = _unit, Origin = plan.moveDest };
                    if (plan.target != null) ctx.TargetUnits.Add(plan.target);
                    ctx.TargetTiles.Add(plan.targetCell);

                    queue.Enqueue(new AbilityAction(plan.ability, ctx, runner));
                }
                else
                {
                    // 只有移动，没有攻击
                    Debug.Log($"[EnemyAI] {name} 仅移动 (追击)");
                }

                yield return queue.RunAll();
            }
            else
            {
                Debug.Log($"[EnemyAI] {name} 发呆");
                yield return new WaitForSeconds(0.2f);
            }
        }
    }
}