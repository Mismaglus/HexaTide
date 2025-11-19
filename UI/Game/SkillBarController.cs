using UnityEngine;
using Game.Battle; // 引用 SelectionManager, BattleUnit, BattleController
using Game.Units;  // 引用 Unit

namespace Game.UI
{
    public class SkillBarController : MonoBehaviour
    {
        [Header("UI References")]
        public SkillBarPopulator populator; // 负责生成图标的那个脚本

        // 私有变量，等待 Initialize 注入
        private SelectionManager _selectionManager;

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
        }

        void OnDestroy()
        {
            if (_selectionManager != null)
                _selectionManager.OnSelectedUnitChanged -= HandleSelectionChanged;
        }

        // === 下面的逻辑保持不变 ===

        void HandleSelectionChanged(Unit unit)
        {
            _currentUnit = unit;

            // 1. 基础检查：没选中、没组件、或者是敌人 -> 清空
            if (unit == null || !unit.TryGetComponent<BattleUnit>(out var battleUnit))
            {
                ClearSkillBar();
                return;
            }

            // ⭐ 新增判断：如果不是玩家可控单位，也清空技能栏
            // (这样选中敌人时，技能栏会变空，避免误导玩家)
            if (!battleUnit.IsPlayerControlled)
            {
                ClearSkillBar();
                return;
            }

            // 2. 是自己人 -> 显示技能
            if (populator != null)
            {
                populator.abilities.Clear();
                if (battleUnit.abilities != null)
                {
                    populator.abilities.AddRange(battleUnit.abilities);
                }
                populator.Populate();
            }
        }

        void ClearSkillBar()
        {
            if (populator != null)
            {
                populator.abilities.Clear();
                populator.Populate();
            }
        }
    }
}