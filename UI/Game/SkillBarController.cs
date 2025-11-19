using UnityEngine;
using Game.Battle; // 引用 SelectionManager, BattleUnit
using Game.Units;  // 引用 Unit

namespace Game.UI
{
    public class SkillBarController : MonoBehaviour
    {
        [Header("UI References")]
        public SkillBarPopulator populator; // 负责生成图标的那个脚本

        [Header("System References")]
        public SelectionManager selectionManager;

        // 缓存当前选中的单位，避免重复刷新
        private Unit _currentUnit;

        void Awake()
        {
            // 自动查找系统
            if (selectionManager == null)
                selectionManager = FindFirstObjectByType<SelectionManager>(FindObjectsInactive.Exclude);

            // 自动查找同一物体或子物体上的 Populator
            if (populator == null)
                populator = GetComponentInChildren<SkillBarPopulator>();
        }

        void OnEnable()
        {
            if (selectionManager != null)
            {
                selectionManager.OnSelectedUnitChanged += HandleSelectionChanged;
                // 初始化时刷新一次
                HandleSelectionChanged(selectionManager.SelectedUnit);
            }
        }

        void OnDisable()
        {
            if (selectionManager != null)
                selectionManager.OnSelectedUnitChanged -= HandleSelectionChanged;
        }

        void HandleSelectionChanged(Unit unit)
        {
            _currentUnit = unit;

            // 1. 没选中单位，或者单位没有战斗组件 -> 清空技能栏
            if (unit == null || !unit.TryGetComponent<BattleUnit>(out var battleUnit))
            {
                ClearSkillBar();
                return;
            }

            // 2. 选中了 -> 读取 BattleUnit 里的技能列表
            // 注意：BattleUnit.abilities 是我们在上一步刚刚加进去的 List<Ability>
            if (populator != null)
            {
                // 把单位的技能列表复制给 UI
                populator.abilities.Clear();
                if (battleUnit.abilities != null)
                {
                    populator.abilities.AddRange(battleUnit.abilities);
                }

                // 让 UI 重绘
                populator.Populate();
            }
        }

        void ClearSkillBar()
        {
            if (populator != null)
            {
                populator.abilities.Clear();
                populator.Populate(); // 空列表 Populate = 清空图标
            }
        }

        // 可选：如果你想以后做“快捷键按下”，可以在 Update 里监听 Input 
        // 然后调用 UseAbility(index) ...
    }
}