using UnityEngine;
using System.Collections.Generic;
using Core.Hex;
using Game.Common;
using Game.Units;
using Game.Battle.Abilities;
using Game.Battle.Actions;
using Game.UI;
using UnityEngine.InputSystem;

namespace Game.Battle
{
    public class AbilityTargetingSystem : MonoBehaviour
    {
        [Header("Core References")]
        public BattleHexGrid grid;
        public SelectionManager selectionManager;
        public BattleHexInput input;
        public ActionQueue actionQueue;
        public AbilityRunner abilityRunner;
        public SkillBarController skillBarController;

        [Header("Visuals")]
        public HexHighlighter highlighter;
        public GridOutlineManager outlineManager;
        public BattleCursor gridCursor;

        [Header("Cursors")]
        public Texture2D cursorTarget;
        public Texture2D cursorInvalid;
        public Vector2 cursorHotspot = Vector2.zero;

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
            if (!gridCursor) gridCursor = FindFirstObjectByType<BattleCursor>();

            if (!outlineManager) outlineManager = FindFirstObjectByType<GridOutlineManager>();
        }

        void OnEnable()
        {
            if (skillBarController) skillBarController.OnAbilitySelected += EnterTargetingMode;
            if (input)
            {
                input.OnTileClicked += HandleTileClicked;
                input.OnHoverChanged += HandleHoverChanged;
            }
        }

        void OnDisable()
        {
            if (skillBarController) skillBarController.OnAbilitySelected -= EnterTargetingMode;
            if (input)
            {
                input.OnTileClicked -= HandleTileClicked;
                input.OnHoverChanged -= HandleHoverChanged;
            }
        }

        public void EnterTargetingMode(Ability ability)
        {
            var unit = selectionManager.SelectedUnit;
            if (unit == null) return;

            _caster = unit.GetComponent<BattleUnit>();
            _currentAbility = ability;
            _validTiles.Clear();

            Debug.Log($"[Targeting] 进入瞄准: {_currentAbility.name} ({_currentAbility.targetType})");

            var rangeTiles = TargetingResolver.TilesInRange(grid, unit.Coords, ability.minRange, ability.maxRange);
            foreach (var t in rangeTiles) _validTiles.Add(t);

            highlighter.ClearAll();

            if (outlineManager)
            {
                outlineManager.SetAbilityRange(_validTiles);
                outlineManager.SetState(OutlineState.AbilityTargeting);
            }

            if (gridCursor) gridCursor.Hide();

            // 初始状态设为 invalid，直到 hover 到有效目标
            Cursor.SetCursor(cursorInvalid, cursorHotspot, CursorMode.Auto);
        }

        // ⭐ 核心逻辑：检查格子内容是否符合技能要求
        private bool IsTargetContentValid(HexCoords coords)
        {
            if (_currentAbility == null) return false;

            // 1. 获取施法者自身坐标 (用于 Self 判断)
            var casterUnit = _caster.GetComponent<Unit>();
            if (casterUnit == null) return false;

            // ⭐ Self 特判：如果点击的是自己脚下的格子，无条件通过
            // (这解决了 0 距离释放技能时，射线打到地块而没打到单位导致验证失败的问题)
            if (_currentAbility.targetType == AbilityTargetType.Self)
            {
                if (coords.Equals(casterUnit.Coords)) return true;
            }

            // 2. 获取目标格子的单位
            selectionManager.TryGetUnitAt(coords, out Unit targetUnit);
            BattleUnit targetBattleUnit = targetUnit ? targetUnit.GetComponent<BattleUnit>() : null;

            // 3. 调用 Resolver 进行通用判断
            return TargetingResolver.IsTargetTypeValid(_caster, targetBattleUnit, _currentAbility.targetType);
        }

        void HandleHoverChanged(HexCoords? coords)
        {
            if (!IsTargeting) return;

            if (coords.HasValue)
            {
                // A. 范围检查
                bool inRange = _validTiles.Contains(coords.Value);

                // B. 内容类型检查 (是敌是友是空?)
                bool isContentValid = inRange && IsTargetContentValid(coords.Value);

                if (gridCursor)
                {
                    // 只有完全有效才显示绿色(true)，否则显示红色(false)表示不可用
                    gridCursor.Show(coords.Value, isContentValid);
                }

                // C. 光标样式
                // 需求：如果不符合 Target Type，使用 Invalid 光标
                var cursorTex = isContentValid ? cursorTarget : cursorInvalid;
                Cursor.SetCursor(cursorTex, cursorHotspot, CursorMode.Auto);
            }
            else
            {
                if (gridCursor) gridCursor.Hide();
                Cursor.SetCursor(cursorInvalid, cursorHotspot, CursorMode.Auto);
            }
        }

        void HandleTileClicked(HexCoords coords)
        {
            if (!IsTargeting) return;

            // 1. 范围检查
            if (!_validTiles.Contains(coords))
            {
                Debug.Log("[Targeting] 目标无效 (超出范围)");
                return;
            }

            // 2. 类型检查
            if (!IsTargetContentValid(coords))
            {
                Debug.Log($"[Targeting] 目标无效 (类型不匹配: 需要 {_currentAbility.targetType})");
                return;
            }

            // 3. 构建 Context
            selectionManager.TryGetUnitAt(coords, out Unit targetUnit);
            BattleUnit targetBattleUnit = targetUnit ? targetUnit.GetComponent<BattleUnit>() : null;
            var casterUnit = _caster.GetComponent<Unit>();

            // ⭐ 关键修复：如果是 Self 技能且点在自己脚下，强制把 caster 塞给 target
            if (_currentAbility.targetType == AbilityTargetType.Self &&
                coords.Equals(casterUnit.Coords))
            {
                targetBattleUnit = _caster;
            }

            var ctx = new AbilityContext
            {
                Caster = _caster,
                Origin = casterUnit.Coords
            };
            if (targetBattleUnit != null) ctx.TargetUnits.Add(targetBattleUnit);
            ctx.TargetTiles.Add(coords);

            // 4. 最终校验 (BasicAbility)
            if (!_currentAbility.IsValidTarget(_caster, ctx))
            {
                Debug.Log("[Targeting] 目标无效 (Ability.IsValidTarget 校验失败)");
                return;
            }

            Debug.Log($"[Targeting] 确认释放 -> {coords}");
            var action = new AbilityAction(_currentAbility, ctx, abilityRunner);
            actionQueue.Enqueue(action);
            StartCoroutine(actionQueue.RunAll());

            CancelTargeting();
        }

        public void CancelTargeting()
        {
            _currentAbility = null;
            _caster = null;
            _validTiles.Clear();

            if (gridCursor) gridCursor.Hide();
            highlighter.ClearAll();

            // 还原状态机状态
            if (outlineManager)
            {
                outlineManager.ClearAbilityRange();
                // 如果此时还选中了单位，回到移动模式；否则回到空闲模式
                if (selectionManager != null && selectionManager.SelectedUnit != null)
                    outlineManager.SetState(OutlineState.Movement);
                else
                    outlineManager.SetState(OutlineState.None);
            }

            // 还原光标 (交给 SelectionManager 接管)
            if (selectionManager != null)
                selectionManager.ApplyCursor(null);
            else
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);

            Debug.Log("[Targeting] 瞄准取消");
        }

        void Update()
        {
            if (IsTargeting)
            {
                if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
                {
                    CancelTargeting();
                }
            }
        }
    }
}