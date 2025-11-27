using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Core.Hex;
using Game.Units;
using Game.Battle.AI;
using Game.Battle.Abilities;
using Game.Grid;

namespace Game.Battle
{
    public class BattleIntentSystem : MonoBehaviour
    {
        public static BattleIntentSystem Instance { get; private set; }

        [Header("Visuals")]
        public GridOutlineManager outlineManager;

        private List<EnemyBrain> _enemies = new List<EnemyBrain>();
        private BattleStateMachine _sm;

        // 缓存计算结果
        private HashSet<HexCoords> _dangerZone = new();
        private List<(Vector3, Vector3)> _arrowList = new();

        void Awake()
        {
            Instance = this;
            if (!outlineManager) outlineManager = FindFirstObjectByType<GridOutlineManager>();
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
            if (!outlineManager) return;

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
                        // ⭐ 修改：获取敌人 Unit 组件并传入坐标
                        var enemyUnit = enemy.GetComponent<Unit>();

                        // 传入 enemyUnit.Coords，这样敌人的扇形/直线也能正确显示红色危险区
                        var area = TargetingResolver.GetAOETiles(plan.targetCell, plan.ability, enemyUnit.Coords);

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

            outlineManager.SetEnemyIntent(_dangerZone, _arrowList);
        }
    }
}
