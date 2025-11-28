using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Core.Hex;
using Game.Units;
using Game.Battle.AI;
using Game.Battle.Abilities;
using Game.Grid;
using Game.Common; // 引用 HexHighlighter

namespace Game.Battle
{
    public class BattleIntentSystem : MonoBehaviour
    {
        public static BattleIntentSystem Instance { get; private set; }

        [Header("Visuals")]
        public GridOutlineManager outlineManager;
        public HexHighlighter highlighter; // ⭐ 必须引用这个才能让地板变色

        private List<EnemyBrain> _enemies = new List<EnemyBrain>();
        private BattleStateMachine _sm;

        // 缓存计算结果
        private HashSet<HexCoords> _dangerZone = new();
        private List<(Vector3, Vector3)> _arrowList = new();

        void Awake()
        {
            Instance = this;
            if (!outlineManager) outlineManager = FindFirstObjectByType<GridOutlineManager>();
            if (!highlighter) highlighter = FindFirstObjectByType<HexHighlighter>(); // 自动查找
            _sm = BattleStateMachine.Instance ?? FindFirstObjectByType<BattleStateMachine>();
        }

        void Start() { StartCoroutine(InitRoutine()); }
        IEnumerator InitRoutine() { yield return null; RefreshEnemyList(); if (_sm != null && _sm.CurrentTurn == TurnSide.Player) UpdateIntents(); }
        void OnEnable() { if (_sm == null) _sm = BattleStateMachine.Instance ?? FindFirstObjectByType<BattleStateMachine>(); if (_sm != null) _sm.OnTurnChanged += HandleTurnChanged; }
        void OnDisable() { if (_sm != null) _sm.OnTurnChanged -= HandleTurnChanged; }

        void HandleTurnChanged(TurnSide side)
        {
            if (side == TurnSide.Player)
            {
                RefreshEnemyList();
                UpdateIntents();
            }
            else
            {
                // 敌人回合开始，隐藏所有意图提示
                if (outlineManager) outlineManager.SetEnemyIntent(null, null);

                // ⭐ 清除地板高亮
                if (highlighter) highlighter.SetEnemyDanger(null);
            }
        }

        public void RefreshEnemyList()
        {
            _enemies.Clear();
            var all = FindObjectsByType<EnemyBrain>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var brain in all)
            {
                var bu = brain.GetComponent<BattleUnit>();
                if (bu != null && bu.Attributes.Core.HP > 0) _enemies.Add(brain);
            }
        }

        public void UpdateIntents()
        {
            if (_sm != null && _sm.CurrentTurn != TurnSide.Player) return;

            // 即使没有 outlineManager 也要跑计算，因为可能需要 highlighter
            // if (!outlineManager) return; 

            _dangerZone.Clear();
            _arrowList.Clear();

            var grid = FindFirstObjectByType<BattleHexGrid>();

            foreach (var enemy in _enemies)
            {
                if (enemy == null) continue;

                var plan = enemy.Think();
                if (plan.isValid)
                {
                    if (plan.ability != null)
                    {
                        var enemyUnit = enemy.GetComponent<Unit>();

                        // ⭐ 修正：使用计划中的移动终点 (moveDest) 作为施法原点
                        // 这样如果怪物是“移动后攻击”，扇形/直线会从新位置发出
                        HexCoords castOrigin = plan.moveDest;

                        var area = TargetingResolver.GetAOETiles(plan.targetCell, plan.ability, castOrigin);

                        _dangerZone.UnionWith(area);

                        if (!plan.targetCell.Equals(enemyUnit.Coords))
                        {
                            Vector3 start = enemy.transform.position;
                            Vector3 end = grid.GetTileWorldPosition(plan.targetCell);
                            _arrowList.Add((start, end));
                        }
                    }
                }
            }

            // 1. 更新红圈和箭头 (GridOutlineManager)
            if (outlineManager) outlineManager.SetEnemyIntent(_dangerZone, _arrowList);

            // 2. ⭐⭐⭐ 更新地板红色高亮 (HexHighlighter)
            // 这就是之前缺少的关键一步！
            if (highlighter) highlighter.SetEnemyDanger(_dangerZone);
        }
    }
}