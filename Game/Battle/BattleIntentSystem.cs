using UnityEngine;
using Game.Units;
using Game.Battle.AI;
using System.Collections;
using System.Collections.Generic; // 确保引用 List

namespace Game.Battle
{
    /// <summary>
    /// 负责在玩家回合实时显示敌人的意图（预警）。
    /// 监听 Unit.OnMoveFinished 事件。
    /// </summary>
    public class BattleIntentSystem : MonoBehaviour
    {
        public GridOutlineManager outlineManager;

        // 缓存所有敌人
        private List<EnemyBrain> _enemies = new List<EnemyBrain>();

        void Awake()
        {
            if (!outlineManager) outlineManager = FindFirstObjectByType<GridOutlineManager>();
        }

        void Start()
        {
            RefreshEnemyList();
            // 初始计算一次
            UpdateIntents();
        }

        void OnEnable()
        {
            // 这是一个静态事件或者是全局广播，你需要确保 Unit.cs 里有这个事件
            // 假设你在 Unit.cs 或者 SelectionManager 里有类似 OnAnyUnitMoved 的事件
            // 这里我们暂时假设通过 GridOccupancy 或者手动订阅
            // 为了简单，我们用 InvokeRepeating 低频检测，或者你可以去 Unit.cs 加个全局静态事件
            // Unit.OnAnyMoveFinished += OnUnitMoved; 
        }

        // 为了方便集成，我们暂时用轮询状态或者手动调用
        // 最佳实践是在 UnitMover 完成移动后调用 BattleIntentSystem.Instance.UpdateIntents();

        // 单例方便调用
        public static BattleIntentSystem Instance { get; private set; }
        void AwakeSingleton() { Instance = this; }

        void Update()
        {
            // 实际上不需要每帧 Update。
            // 这里留空。
        }

        public void RefreshEnemyList()
        {
            _enemies.Clear();
            var all = FindObjectsByType<EnemyBrain>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            _enemies.AddRange(all);
        }

        [ContextMenu("Force Update Intents")]
        public void UpdateIntents()
        {
            if (!outlineManager) return;

            // 先清除旧的意图 (目前 GridOutlineManager 只有一套 EnemyIntentDrawer，
            // 如果要显示多个敌人的意图，你需要 Manager 支持多个 Drawer 或者合并区域)
            // 这里我们简化：只显示“针对当前选中单位”的意图，或者“所有危险区域合并”。

            // 假设我们把所有敌人的攻击范围合并成一个 Danger Zone
            HashSet<Core.Hex.HexCoords> dangerZone = new HashSet<Core.Hex.HexCoords>();

            foreach (var enemy in _enemies)
            {
                var plan = enemy.Think();
                if (plan.isValid)
                {
                    // 这里的逻辑是：如果敌人决定打某个位置，那个位置就是危险的
                    // 我们可以调用 TargetingResolver.GetAOETiles 来获取具体的危险格
                    var area = Abilities.TargetingResolver.GetAOETiles(plan.targetCell, plan.ability);
                    dangerZone.UnionWith(area);

                    // 如果你想画箭头，需要 Manager 支持画多条箭头 (目前只支持一条)
                    // 暂时我们只画红圈
                }
            }

            // 提交给 Manager
            outlineManager.SetEnemyIntent(dangerZone);
        }
    }
}