using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Core.Hex;
using Game.Units;
using Game.Battle.AI;
using Game.Battle.Abilities;

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

        private List<EnemyBrain> _enemies = new List<EnemyBrain>();
        private BattleStateMachine _sm;

        void Awake()
        {
            Instance = this;
            if (!outlineManager) outlineManager = FindFirstObjectByType<GridOutlineManager>();
            _sm = BattleStateMachine.Instance ?? FindFirstObjectByType<BattleStateMachine>();
        }

        void Start()
        {
            // 延迟一帧初始化，确保所有敌人的 Start() 都跑完了，属性都初始化好了
            StartCoroutine(InitRoutine());
        }

        IEnumerator InitRoutine()
        {
            yield return null;
            RefreshEnemyList();

            // 如果游戏刚开始就是玩家回合，计算一次
            if (_sm != null && _sm.CurrentTurn == TurnSide.Player)
            {
                UpdateIntents();
            }
        }

        void OnEnable()
        {
            if (_sm == null) _sm = BattleStateMachine.Instance ?? FindFirstObjectByType<BattleStateMachine>();
            if (_sm != null) _sm.OnTurnChanged += HandleTurnChanged;
        }

        void OnDisable()
        {
            if (_sm != null) _sm.OnTurnChanged -= HandleTurnChanged;
        }

        // ⭐ 核心修复：监听回合切换
        void HandleTurnChanged(TurnSide side)
        {
            if (side == TurnSide.Player)
            {
                // 轮到玩家了：刷新列表（可能有新敌人或死掉的），然后计算意图
                RefreshEnemyList();
                UpdateIntents();
                Debug.Log("[BattleIntentSystem] Player Turn Started -> Updated Enemy Intents");
            }
            else
            {
                // 轮到敌人了：清除地上的红圈，交给 EnemyTurnActor 自己去画它的行动
                if (outlineManager) outlineManager.SetEnemyIntent(null);
            }
        }

        public void RefreshEnemyList()
        {
            _enemies.Clear();
            // 重新查找所有活着的敌人大脑
            var all = FindObjectsByType<EnemyBrain>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var brain in all)
            {
                // 简单的存活检查
                var bu = brain.GetComponent<BattleUnit>();
                if (bu != null && bu.Attributes.Core.HP > 0)
                {
                    _enemies.Add(brain);
                }
            }
        }

        public void UpdateIntents()
        {
            // 双重检查：如果当前不是玩家回合，不要画预警（除非你希望任何时候都看得到）
            if (_sm != null && _sm.CurrentTurn != TurnSide.Player) return;
            if (!outlineManager) return;

            HashSet<HexCoords> dangerZone = new HashSet<HexCoords>();

            foreach (var enemy in _enemies)
            {
                if (enemy == null) continue;

                // 让敌人思考当前局势下的最佳方案
                var plan = enemy.Think();

                if (plan.isValid)
                {
                    // 获取该技能的打击范围
                    var area = TargetingResolver.GetAOETiles(plan.targetCell, plan.ability);
                    dangerZone.UnionWith(area);
                }
            }

            // 提交给 Manager
            outlineManager.SetEnemyIntent(dangerZone);
        }
    }
}