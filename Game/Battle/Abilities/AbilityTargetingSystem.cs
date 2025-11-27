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

            Debug.Log($"[Targeting] Enter: {_currentAbility.name}");

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

        private bool CheckContentValidity(HexCoords coords)
        {
            if (_currentAbility == null) return false;
            selectionManager.TryGetUnitAt(coords, out Unit targetUnit);
            var casterUnit = _caster.GetComponent<Unit>();

            // Self 特判
            if (_currentAbility.targetType == AbilityTargetType.Self)
            {
                if (casterUnit != null && coords.Equals(casterUnit.Coords)) return true;
            }

            BattleUnit targetBattleUnit = targetUnit ? targetUnit.GetComponent<BattleUnit>() : null;
            return TargetingResolver.IsTargetTypeValid(_caster, targetBattleUnit, _currentAbility.targetType);
        }
        void HandleHoverChanged(HexCoords? coords)
        {
            if (!IsTargeting) return;

            if (coords.HasValue)
            {
                HexCoords hoverC = coords.Value;
                bool inRange = _validTiles.Contains(hoverC);
                bool isContentValid = inRange && CheckContentValidity(hoverC);

                if (gridCursor) gridCursor.Show(hoverC, isContentValid);

                var cursorTex = isContentValid ? cursorTarget : cursorInvalid;
                Cursor.SetCursor(cursorTex, cursorHotspot, CursorMode.Auto);

                if (isContentValid && outlineManager)
                {
                    // 1. 计算 AOE 区域 (使用新的 ThickLine / Variable Cone 算法)
                    var aoeTiles = TargetingResolver.GetAOETiles(hoverC, _currentAbility, _caster.UnitRef.Coords);

                    // 2. 计算世界坐标
                    Vector3 startPos = _caster.transform.position;
                    Vector3 endPos = grid.GetTileWorldPosition(hoverC);

                    // 3. ⭐ 决定是否显示箭头
                    // 逻辑：如果是 Line 或 Cone，不显示箭头 (因为地面已经有 Tint 了)
                    // 如果是 Single/Disk/Ring，且不是对自己释放，则显示箭头
                    bool isDirectionalAOE = _currentAbility.shape == TargetShape.Line || _currentAbility.shape == TargetShape.Cone;
                    bool showArrow = !isDirectionalAOE && !hoverC.Equals(_caster.UnitRef.Coords);

                    // 4. 显示意图
                    outlineManager.ShowPlayerIntent(startPos, endPos, aoeTiles, showArrow);
                }
                else if (outlineManager)
                {
                    outlineManager.ClearPlayerIntent();
                }
            }
            else
            {
                if (gridCursor) gridCursor.Hide();
                if (outlineManager) outlineManager.ClearPlayerIntent();
                Cursor.SetCursor(cursorInvalid, cursorHotspot, CursorMode.Auto);
            }
        }
        void HandleTileClicked(HexCoords coords)
        {
            if (!IsTargeting) return;

            if (!_validTiles.Contains(coords)) return;
            if (!CheckContentValidity(coords)) return;

            selectionManager.TryGetUnitAt(coords, out Unit targetUnit);
            BattleUnit targetBattleUnit = targetUnit ? targetUnit.GetComponent<BattleUnit>() : null;
            var casterUnit = _caster.GetComponent<Unit>();

            if (_currentAbility.targetType == AbilityTargetType.Self && coords.Equals(casterUnit.Coords))
                targetBattleUnit = _caster;

            var ctx = new AbilityContext
            {
                Caster = _caster,
                Origin = casterUnit.Coords
            };
            if (targetBattleUnit != null) ctx.TargetUnits.Add(targetBattleUnit);
            ctx.TargetTiles.Add(coords);

            if (!_currentAbility.IsValidTarget(_caster, ctx)) return;

            Debug.Log($"[Targeting] Fire -> {coords}");
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
                outlineManager.ClearPlayerIntent(); // ⭐ 记得清理意图

                if (selectionManager != null && selectionManager.SelectedUnit != null)
                    outlineManager.SetState(OutlineState.Movement);
                else
                    outlineManager.SetState(OutlineState.None);
            }

            if (selectionManager != null) selectionManager.ApplyCursor(null);
            else Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        }

        void Update()
        {
            if (IsTargeting)
            {
                if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
                    CancelTargeting();
            }
        }
    }
}