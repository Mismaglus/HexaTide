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
        public GridOutlineManager outlineManager; // 假设你已经按照上一步重构了
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
            Cursor.SetCursor(cursorInvalid, cursorHotspot, CursorMode.Auto);
        }

        // ⭐ 核心新增：检查目标格的内容是否符合 AbilityTargetType
        private bool IsTargetContentValid(HexCoords coords)
        {
            if (_currentAbility == null) return false;

            // 1. 先获取该格子上是否有单位
            selectionManager.TryGetUnitAt(coords, out Unit targetUnit);

            // 2. 根据类型判断
            switch (_currentAbility.targetType)
            {
                case AbilityTargetType.EmptyTile:
                    // 必须为空
                    return targetUnit == null;

                case AbilityTargetType.EnemyUnit:
                    // 必须有单位 且 不是我方控制
                    return targetUnit != null && !targetUnit.IsPlayerControlled;

                case AbilityTargetType.FriendlyUnit:
                    // 必须有单位 且 是我方控制
                    return targetUnit != null && targetUnit.IsPlayerControlled;

                case AbilityTargetType.AnyTile:
                    // 既然已经在 _validTiles (范围) 检查过了，这里只要在格子上就行，有无单位均可
                    return true;

                default:
                    return false;
            }
        }

        void HandleHoverChanged(HexCoords? coords)
        {
            if (!IsTargeting) return;

            if (coords.HasValue)
            {
                // 1. 检查是否在距离范围内
                bool inRange = _validTiles.Contains(coords.Value);

                // 2. 检查格子内容是否符合技能要求 (敌/我/空)
                bool isContentValid = inRange && IsTargetContentValid(coords.Value);

                if (gridCursor)
                {
                    // 格子框依然可以显示，但颜色可以区分，或者简单处理：有效才显示绿框/黄框
                    gridCursor.Show(coords.Value, isContentValid);
                }

                // 3. 只有 范围+内容 都有效，才显示 Target 光标，否则显示 Invalid
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

            // 2. ⭐ 内容检查 (防止点击无效目标触发技能)
            if (!IsTargetContentValid(coords))
            {
                Debug.Log($"[Targeting] 目标无效 (类型不匹配: 需要 {_currentAbility.targetType})");
                return;
            }

            selectionManager.TryGetUnitAt(coords, out Unit targetUnit);
            BattleUnit targetBattleUnit = targetUnit ? targetUnit.GetComponent<BattleUnit>() : null;

            var ctx = new AbilityContext
            {
                Caster = _caster,
                Origin = _caster.GetComponent<Unit>().Coords
            };
            if (targetBattleUnit != null) ctx.TargetUnits.Add(targetBattleUnit);
            ctx.TargetTiles.Add(coords);

            // (BasicAbility 内部可能还有额外的 IsValidTarget 检查，保留双重验证)
            if (!_currentAbility.IsValidTarget(_caster, ctx))
            {
                Debug.Log("[Targeting] 目标无效 (Ability.IsValidTarget 返回 false)");
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

            if (outlineManager)
            {
                outlineManager.ClearAbilityRange();
                if (selectionManager != null && selectionManager.SelectedUnit != null)
                    outlineManager.SetState(OutlineState.Movement);
                else
                    outlineManager.SetState(OutlineState.None);
            }

            if (selectionManager != null) selectionManager.ApplyCursor(null);
            else Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);

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