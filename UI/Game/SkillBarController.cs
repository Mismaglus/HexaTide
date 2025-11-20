using UnityEngine;
using Game.Battle; // 引用 SelectionManager, BattleUnit, BattleController
using Game.Units;  // 引用 Unit
using Game.Battle.Abilities;

namespace Game.UI
{
    public class SkillBarController : MonoBehaviour
    {
        [Header("UI References")]
        public SkillBarPopulator populator; // 负责生成图标的那个脚本

        // 私有变量，等待 Initialize 注入
        private SelectionManager _selectionManager;
        public event System.Action<Ability> OnAbilitySelected;
        // 缓存当前选中的单位
        private Unit _currentUnit;

        void Awake()
        {
            // 只保留自身组件的查找，不找外部依赖
            if (populator == null)
                populator = GetComponentInChildren<SkillBarPopulator>();
        }

        // ⭐ 这就是报错缺少的那个方法！
        public void Initialize(BattleController battle)
        {
            if (battle == null) return;

            // 1. 尝试查找 SelectionManager
            // 先试着从 BattleController 身上找
            _selectionManager = battle.GetComponent<SelectionManager>();

            // 如果没找到，再去场景全局找 (双重保险)
            if (_selectionManager == null)
                _selectionManager = FindFirstObjectByType<SelectionManager>();

            if (_selectionManager != null)
            {
                // 2. 订阅事件 (先减后加，防止重复)
                _selectionManager.OnSelectedUnitChanged -= HandleSelectionChanged;
                _selectionManager.OnSelectedUnitChanged += HandleSelectionChanged;

                // 3. 立即刷新一次 (以防已经选中了单位)
                HandleSelectionChanged(_selectionManager.SelectedUnit);

                Debug.Log("[SkillBarController] 初始化成功");
            }
            else
            {
                Debug.LogError("[SkillBarController] 找不到 SelectionManager，技能栏无法工作！");
            }

            if (populator != null)
            {
                populator.OnSkillClicked -= HandleSkillClicked;
                populator.OnSkillClicked += HandleSkillClicked;
            }
        }

        void OnDestroy()
        {
            if (_selectionManager != null)
                _selectionManager.OnSelectedUnitChanged -= HandleSelectionChanged;
        }

        // === 下面的逻辑保持不变 ===

        // SkillBarController.cs

        void HandleSelectionChanged(Unit unit)
        {
            _currentUnit = unit;

            // 1. 基础检查：没选中、没组件 -> 依然清空
            if (unit == null || !unit.TryGetComponent<BattleUnit>(out var battleUnit))
            {
                ClearSkillBar();
                return;
            }

            // ⭐ 修改逻辑：不再 return，而是设置状态
            bool isEnemy = !battleUnit.IsPlayerControlled;

            if (populator != null)
            {
                // 2. 告诉 UI：如果是敌人，就锁定 (变灰)
                populator.SetLockedState(isEnemy);

                // 3. 无论敌我，都填入数据！
                // (这样玩家就能看到敌人的技能图标了)
                populator.abilities.Clear();
                if (battleUnit.abilities != null)
                {
                    populator.abilities.AddRange(battleUnit.abilities);
                }

                populator.Populate();
            }
        }
        void HandleSkillClicked(int index)
        {
            // 如果选中的是敌对单位，或者索引无效
            if (_currentUnit == null || !_currentUnit.IsPlayerControlled)
            {
                // 🔇 这里可以播放一个“Error”音效
                Debug.Log("Cannot use enemy skills!");
                return;
            }

            // 从 BattleUnit 获取对应索引的技能
            var battleUnit = _currentUnit.GetComponent<BattleUnit>();
            if (battleUnit != null && index < battleUnit.abilities.Count)
            {
                var ability = battleUnit.abilities[index];
                Debug.Log($"选择了技能: {ability.name}");

                // 广播事件：有人想用这个技能！
                OnAbilitySelected?.Invoke(ability);
            }
        }

        void ClearSkillBar()
        {
            if (populator != null)
            {
                populator.SetLockedState(false); // 恢复默认
                populator.abilities.Clear();
                populator.Populate();
            }
        }
    }
}