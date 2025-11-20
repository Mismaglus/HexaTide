using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Core.Hex;
using Game.Common;
using Game.Units;
using Game.Battle.Abilities;
using Game.Battle.Actions;
using Game.UI;
using UnityEngine.InputSystem; // ⭐ 必须加上这个引用

namespace Game.Battle
{
    public class AbilityTargetingSystem : MonoBehaviour
    {
        [Header("Core References")]
        public BattleHexGrid grid;
        public HexHighlighter highlighter;
        public SelectionManager selectionManager;
        public BattleHexInput input;
        public ActionQueue actionQueue;
        public AbilityRunner abilityRunner;
        public SkillBarController skillBarController;

        // 运行时状态
        private Ability _currentAbility;
        private BattleUnit _caster;
        private HashSet<HexCoords> _validTiles = new HashSet<HexCoords>();

        public bool IsTargeting => _currentAbility != null;

        void Awake()
        {
            if (!grid) grid = FindFirstObjectByType<BattleHexGrid>();
            if (!highlighter) highlighter = FindFirstObjectByType<HexHighlighter>();
            if (!selectionManager) selectionManager = FindFirstObjectByType<SelectionManager>();
            if (!input) input = FindFirstObjectByType<BattleHexInput>();
            if (!actionQueue) actionQueue = FindFirstObjectByType<ActionQueue>();
            if (!abilityRunner) abilityRunner = FindFirstObjectByType<AbilityRunner>();
            if (!skillBarController) skillBarController = FindFirstObjectByType<SkillBarController>();
        }

        void OnEnable()
        {
            if (skillBarController) skillBarController.OnAbilitySelected += EnterTargetingMode;
            if (input) input.OnTileClicked += HandleTileClicked;
        }

        void OnDisable()
        {
            if (skillBarController) skillBarController.OnAbilitySelected -= EnterTargetingMode;
            if (input) input.OnTileClicked -= HandleTileClicked;
        }

        // 1. 进入瞄准模式
        public void EnterTargetingMode(Ability ability)
        {
            var unit = selectionManager.SelectedUnit;
            if (unit == null) return;

            _caster = unit.GetComponent<BattleUnit>();
            _currentAbility = ability;
            _validTiles.Clear();

            Debug.Log($"[Targeting] 开始瞄准: {_currentAbility.name}");

            var rangeTiles = TargetingResolver.TilesInRange(grid, unit.Coords, ability.minRange, ability.maxRange);

            foreach (var t in rangeTiles) _validTiles.Add(t);

            highlighter.ApplyRange(_validTiles);
        }

        // 2. 处理点击 (左键逻辑已经在 BattleHexInput -> HandleTileClicked 处理了，这里只负责逻辑判断)
        void HandleTileClicked(HexCoords coords)
        {
            if (!IsTargeting) return;

            // 1. 校验射程
            if (!_validTiles.Contains(coords))
            {
                Debug.Log("[Targeting] 超出射程或无效区域，取消瞄准。");
                CancelTargeting();
                return;
            }

            // 2. 校验目标
            selectionManager.TryGetUnitAt(coords, out Unit targetUnit);
            BattleUnit targetBattleUnit = targetUnit ? targetUnit.GetComponent<BattleUnit>() : null;

            var ctx = new AbilityContext
            {
                Caster = _caster,
                Origin = _caster.GetComponent<Unit>().Coords
            };

            if (targetBattleUnit != null) ctx.TargetUnits.Add(targetBattleUnit);
            ctx.TargetTiles.Add(coords);

            if (!_currentAbility.IsValidTarget(_caster, ctx))
            {
                Debug.Log("[Targeting] 目标无效。");
                return;
            }

            // 3. 执行
            Debug.Log($"[Targeting] 释放技能 -> {coords}");
            var action = new AbilityAction(_currentAbility, ctx, abilityRunner);
            actionQueue.Enqueue(action);
            StartCoroutine(actionQueue.RunAll());

            CancelTargeting();
        }

        // 3. 退出瞄准
        public void CancelTargeting()
        {
            _currentAbility = null;
            _caster = null;
            _validTiles.Clear();
            highlighter.ClearAll();
            Debug.Log("[Targeting] 瞄准结束");
        }

        // ⭐ 修复了这里的报错：使用新输入系统检测右键
        void Update()
        {
            if (IsTargeting)
            {
                // 检查鼠标是否存在，且右键是否按下
                if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
                {
                    CancelTargeting();
                }
            }
        }
    }
}