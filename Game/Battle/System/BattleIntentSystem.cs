using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Core.Hex;
using Game.Units;
using Game.Battle.AI;
using Game.Battle.Abilities;
using Game.Grid;
using Game.Common;

namespace Game.Battle
{
    public class BattleIntentSystem : MonoBehaviour
    {
        public static BattleIntentSystem Instance { get; private set; }

        [Header("Visuals")]
        public GridOutlineManager outlineManager;
        public HexHighlighter highlighter;

        private List<EnemyBrain> _enemies = new List<EnemyBrain>();
        private BattleStateMachine _sm;

        private HashSet<HexCoords> _dangerZone = new();
        private HashSet<HexCoords> _filteredDangerZone = new();
        private List<(Vector3, Vector3)> _arrowList = new();

        void Awake()
        {
            Instance = this;
            if (!outlineManager) outlineManager = FindFirstObjectByType<GridOutlineManager>();
            if (!highlighter) highlighter = FindFirstObjectByType<HexHighlighter>();
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
                if (outlineManager) outlineManager.SetEnemyIntent(null, null);
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
                        // AI 计划中的移动终点
                        HexCoords castOrigin = plan.moveDest;

                        var area = TargetingResolver.GetAOETiles(plan.targetCell, plan.ability, castOrigin);

                        _dangerZone.UnionWith(area);

                        if (!plan.targetCell.Equals(enemyUnit.Coords))
                        {
                            Vector3 start = grid.GetTileWorldPosition(plan.moveDest);
                            Vector3 end = grid.GetTileWorldPosition(plan.targetCell);
                            _arrowList.Add((start, end));
                        }
                    }
                }
            }

            // 过滤：只显示已探索区域的红框
            _filteredDangerZone.Clear();
            if (FogOfWarSystem.Instance != null)
            {
                foreach (var t in _dangerZone)
                {
                    // ⭐ 修复：使用正确的方法名 IsTileExplored
                    if (FogOfWarSystem.Instance.IsTileExplored(t))
                    {
                        _filteredDangerZone.Add(t);
                    }
                }
            }
            else
            {
                // 如果没有迷雾系统，全显
                _filteredDangerZone.UnionWith(_dangerZone);
            }

            // 更新 UI
            if (outlineManager) outlineManager.SetEnemyIntent(_filteredDangerZone, _arrowList);
            if (highlighter) highlighter.SetEnemyDanger(_filteredDangerZone);
        }
    }
}
