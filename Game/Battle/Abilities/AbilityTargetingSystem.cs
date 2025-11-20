using UnityEngine;
using Game.Common;
using Game.Units;
using Game.Battle.Abilities;
using Game.UI; // 引用 SkillBarController
using Core.Hex;

namespace Game.Battle
{
    public class AbilityTargetingSystem : MonoBehaviour
    {
        [Header("Refs")]
        public BattleHexGrid grid;
        public HexHighlighter highlighter;
        public SelectionManager selectionManager;
        public SkillBarController skillBarController; // 需要监听它

        private Ability _currentAbility;
        private BattleUnit _caster;

        public bool IsTargeting => _currentAbility != null;

        void Awake()
        {
            if (!grid) grid = FindFirstObjectByType<BattleHexGrid>();
            if (!highlighter) highlighter = FindFirstObjectByType<HexHighlighter>();
            if (!selectionManager) selectionManager = FindFirstObjectByType<SelectionManager>();

            // SkillBarController 可能在 UI 场景，稍微难找一点，建议由 BattleController 注入
            if (!skillBarController) skillBarController = FindFirstObjectByType<SkillBarController>();
        }

        void OnEnable()
        {
            if (skillBarController)
                skillBarController.OnAbilitySelected += EnterTargetingMode;
        }

        void OnDisable()
        {
            if (skillBarController)
                skillBarController.OnAbilitySelected -= EnterTargetingMode;
        }

        public void EnterTargetingMode(Ability ability)
        {
            var unit = selectionManager.SelectedUnit;
            if (unit == null) return;

            _caster = unit.GetComponent<BattleUnit>();
            _currentAbility = ability;

            Debug.Log($"进入瞄准模式: {_currentAbility.name}");

            // 1. 计算范围 (复用 TargetingResolver)
            var rangeTiles = TargetingResolver.TilesInRange(grid, unit.Coords, ability.minRange, ability.maxRange);

            // 2. 高亮显示 (复用 HexHighlighter)
            highlighter.ApplyRange(rangeTiles);
        }

        // 退出瞄准 (比如右键取消时调用)
        public void CancelTargeting()
        {
            _currentAbility = null;
            _caster = null;
            highlighter.ClearAll(); // 清除高亮
        }

        // TODO: 在 Update 里检测鼠标点击，如果点在了 rangeTiles 里，就释放技能
    }
}