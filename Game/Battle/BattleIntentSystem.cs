using UnityEngine;
using System.Collections.Generic;
using Core.Hex;
using Game.Units;
using Game.Battle.AI;       // 引用 EnemyBrain
using Game.Battle.Abilities;// 引用 TargetingResolver

namespace Game.Battle
{
    /// <summary>
    /// 负责在玩家回合实时显示敌人的意图（预警）。
    /// </summary>
    public class BattleIntentSystem : MonoBehaviour
    {
        public static BattleIntentSystem Instance { get; private set; }

        [Header("Visuals")]
        public GridOutlineManager outlineManager;

        // 缓存所有敌人大脑
        private List<EnemyBrain> _enemies = new List<EnemyBrain>();

        void Awake()
        {
            Instance = this;
            if (!outlineManager) outlineManager = FindFirstObjectByType<GridOutlineManager>();
        }

        void Start()
        {
            RefreshEnemyList();
        }

        public void RefreshEnemyList()
        {
            _enemies.Clear();
            var all = FindObjectsByType<EnemyBrain>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            _enemies.AddRange(all);
        }

        /// <summary>
        /// 由 UnitMover 在移动结束时调用
        /// </summary>
        public void UpdateIntents()
        {
            // 只有在玩家回合才显示预警（或者是任何时候你想要显示）
            var sm = BattleStateMachine.Instance;
            if (sm != null && sm.CurrentTurn != TurnSide.Player)
            {
                // 如果不是玩家回合（比如轮到敌人动了），可以清除预警，或者交给 EnemyTurnActor 自己去画
                // 这里我们选择清理掉，避免和敌人行动时的 Intent 混淆
                if (outlineManager) outlineManager.SetEnemyIntent(null);
                return;
            }

            if (!outlineManager) return;

            HashSet<HexCoords> dangerZone = new HashSet<HexCoords>();

            foreach (var enemy in _enemies)
            {
                if (enemy == null) continue;

                // 让敌人思考一下
                var plan = enemy.Think();

                if (plan.isValid)
                {
                    // 获取该技能的打击范围
                    // 注意：这里的 targetCell 是敌人思考后的最佳目标点
                    var area = TargetingResolver.GetAOETiles(plan.targetCell, plan.ability);
                    dangerZone.UnionWith(area);
                }
            }

            // 提交给 Manager 统一绘制红色警戒区
            outlineManager.SetEnemyIntent(dangerZone);
        }
    }
}