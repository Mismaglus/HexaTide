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
                    // ⭐ 修复点 1: 只有当 plan 有技能时，才计算 AOE 危险区
                    // 如果是 Chase (追击) 模式，ability 为 null，就不画红圈
                    if (plan.ability != null)
                    {
                        var area = TargetingResolver.GetAOETiles(plan.targetCell, plan.ability);
                        _dangerZone.UnionWith(area);
                    }

                    // 2. 收集箭头
                    // 无论是攻击还是移动，只要目标点不是自己，就画箭头
                    // 注意：如果是 Chase 模式，targetCell 是它想去的格子；如果是攻击，是攻击目标格
                    if (!plan.targetCell.Equals(enemy.GetComponent<Unit>().Coords))
                    {
                        Vector3 start = enemy.transform.position;
                        Vector3 end = grid.GetTileWorldPosition(plan.targetCell);
                        _arrowList.Add((start, end));
                    }
                }
            }

            outlineManager.SetEnemyIntent(_dangerZone, _arrowList);
        }
    }
}