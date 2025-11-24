using System.Collections;
using UnityEngine;
using Core.Hex;
using Game.Units;
using Game.Battle.Actions;
using Game.Battle.Abilities; // 引用 TargetingResolver
using Game.Battle.AI;        // 引用 EnemyBrain
using Game.Grid;             // 引用 HexPathfinder

namespace Game.Battle
{
    [RequireComponent(typeof(EnemyBrain))]
    public class EnemyTurnActor : MonoBehaviour, ITurnActor
    {
        [Header("Systems")]
        public ActionQueue queue;
        public AbilityRunner runner;
        public BattleRules rules;

        [Header("Visuals")]
        public GridOutlineManager outlineManager; // 用于在攻击前展示意图

        private EnemyBrain _brain;
        private UnitMover _mover;
        private BattleUnit _unit;

        void Awake()
        {
            _brain = GetComponent<EnemyBrain>();
            _mover = GetComponent<UnitMover>();
            _unit = GetComponent<BattleUnit>();

            // 自动查找依赖
            if (queue == null) queue = FindFirstObjectByType<ActionQueue>();
            if (runner == null) runner = FindFirstObjectByType<AbilityRunner>();
            if (rules == null) rules = FindFirstObjectByType<BattleRules>();
            if (outlineManager == null) outlineManager = FindFirstObjectByType<GridOutlineManager>();
        }

        public void OnTurnStart()
        {
            _unit?.ResetTurnResources();
        }

        public bool HasPendingAction => false;

        public IEnumerator TakeTurn()
        {
            if (_brain == null) yield break;

            // 1. 思考最佳方案
            AIPlan plan = _brain.Think();

            if (plan.isValid)
            {
                Debug.Log($"[EnemyAI] {name} 决定: 移动到 {plan.moveDest}, 对 {plan.target.name} 使用 {plan.ability.name}");

                // 2. 展示意图 (Show Off)
                // 让玩家看到这一回合敌人想干嘛：画箭头和红圈
                if (outlineManager)
                {
                    // 获取 AOE 范围
                    var aoe = TargetingResolver.GetAOETiles(plan.targetCell, plan.ability);

                    Vector3 startPos = transform.position;
                    // 需要获取目标格子的世界坐标，这里简单从 Unit 上取，或者通过 Grid 计算
                    // 假设 Grid 就在场景里
                    var gridComp = FindFirstObjectByType<BattleHexGrid>();
                    Vector3 endPos = gridComp.GetTileWorldPosition(plan.targetCell);

                    // 只有当真的要打的时候才画箭头
                    bool showArrow = !plan.targetCell.Equals(_unit.UnitRef.Coords);

                    outlineManager.ShowIntent(startPos, endPos, aoe, showArrow);
                }

                // 3. 停顿 1 秒让玩家看清楚
                yield return new WaitForSeconds(1.0f);

                // 4. 清除意图显示，开始行动
                if (outlineManager) outlineManager.ClearIntent();

                // 5. 执行移动 (如果需要)
                if (!plan.moveDest.Equals(_unit.UnitRef.Coords))
                {
                    var path = HexPathfinder.FindPath(_unit.UnitRef.Coords, plan.moveDest, rules, _unit.UnitRef);
                    if (path != null && path.Count > 0)
                    {
                        queue.Enqueue(new PathMoveAction(_mover, path));
                    }
                }

                // 6. 执行技能
                var ctx = new AbilityContext
                {
                    Caster = _unit,
                    Origin = plan.moveDest // 关键：技能原点应该是移动后的位置
                };

                // 根据技能类型填入 Target
                if (plan.target != null) ctx.TargetUnits.Add(plan.target);
                ctx.TargetTiles.Add(plan.targetCell);

                queue.Enqueue(new AbilityAction(plan.ability, ctx, runner));

                // 7. 跑！
                yield return queue.RunAll();
            }
            else
            {
                Debug.Log($"[EnemyAI] {name} 发呆 (没有好的行动方案)");
                yield return new WaitForSeconds(0.5f);
            }
        }
    }
}