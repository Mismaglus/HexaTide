using UnityEngine;
using Game.Battle; // 引用 SelectionManager, BattleUnit, BattleController
using Game.Units;  // 引用 Unit
using Game.Battle.Abilities;

namespace Game.UI
{
    public class SkillBarController : MonoBehaviour
    {
        [Header("UI References")]
        public SkillBarPopulator populator; // 负责生成图标的脚本

        // 私有变量
        private SelectionManager _selectionManager;
        public event System.Action<Ability> OnAbilitySelected;

        // 缓存当前选中的单位
        private Unit _currentUnit;

        void Awake()
        {
            // 自动查找子组件中的 Populator
            if (populator == null)
                populator = GetComponentInChildren<SkillBarPopulator>();
        }

        // 由 BattleUIRoot 调用进行初始化
        public void Initialize(BattleController battle)
        {
            if (battle == null) return;

            // 1. 尝试查找 SelectionManager
            _selectionManager = battle.GetComponent<SelectionManager>();

            // 双重保险：如果 BattleController 上没有，去场景找
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

            // 订阅 Populator 的点击事件
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

        // 处理单位选中逻辑
        void HandleSelectionChanged(Unit unit)
        {
            _currentUnit = unit;

            // 基础检查：没选中单位，或者选中的单位没有 BattleUnit 组件 -> 清空技能栏
            if (unit == null || !unit.TryGetComponent<BattleUnit>(out var battleUnit))
            {
                ClearSkillBar();
                return;
            }

            // 判断是否为敌方单位
            bool isEnemy = !battleUnit.IsPlayerControlled;

            if (populator != null)
            {
                // ⭐⭐ 关键修改：将 BattleUnit 传递给 Populator 以便 Tooltip 使用 ⭐⭐
                populator.currentOwner = battleUnit;

                // 设置锁定状态（如果是敌人则变灰）
                populator.SetLockedState(isEnemy);

                // 填入数据
                populator.abilities.Clear();
                if (battleUnit.abilities != null)
                {
                    populator.abilities.AddRange(battleUnit.abilities);
                }

                // 执行生成
                populator.Populate();
            }
        }

        // 处理技能点击
        void HandleSkillClicked(int index)
        {
            // 如果选中的是敌对单位，或者索引无效，不响应点击
            if (_currentUnit == null || !_currentUnit.IsPlayerControlled)
            {
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
                populator.currentOwner = null; // 清理引用
                populator.SetLockedState(false); // 恢复默认
                populator.abilities.Clear();
                populator.Populate();
            }
        }
    }
}