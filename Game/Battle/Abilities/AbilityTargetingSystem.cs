// Scripts/Game/Battle/Abilities/AbilityTargetingSystem.cs
using UnityEngine;
using System.Collections.Generic;
using Core.Hex;
using Game.Common;
using Game.Units;
using Game.Battle.Abilities;
using Game.Battle.Actions;
using Game.UI;
using Game.Inventory;
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
        private InventoryItem _currentSourceItem;

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
            if (skillBarController) skillBarController.OnAbilitySelected += HandleSkillBarSelection;
            if (input)
            {
                input.OnTileClicked += HandleTileClicked;
                input.OnHoverChanged += HandleHoverChanged;
            }
        }

        void OnDisable()
        {
            if (skillBarController) skillBarController.OnAbilitySelected -= HandleSkillBarSelection;
            if (input)
            {
                input.OnTileClicked -= HandleTileClicked;
                input.OnHoverChanged -= HandleHoverChanged;
            }
        }

        // 技能栏点击（使用当前选中单位）
        void HandleSkillBarSelection(Ability ability)
        {
            EnterTargetingMode(ability, null, null);
        }

        // ⭐ 核心修改：支持传入 explicitCaster (用于物品使用，即使单位未被选中)
        public void EnterTargetingMode(Ability ability, InventoryItem sourceItem = null, BattleUnit explicitCaster = null)
        {
            // 1. 确定施法者：优先使用显式传入的，否则尝试获取当前选中的
            BattleUnit targetCaster = explicitCaster;
            if (targetCaster == null)
            {
                var selUnit = selectionManager.SelectedUnit;
                if (selUnit != null) targetCaster = selUnit.GetComponent<BattleUnit>();
            }

            if (targetCaster == null)
            {
                Debug.LogWarning("[AbilityTargetingSystem] Cannot enter targeting: No caster found (No selection & no explicit caster).");
                return;
            }

            _caster = targetCaster;
            _currentSourceItem = sourceItem;

            // 2. 检查即时触发
            if (ability.triggerImmediately)
            {
                ExecuteImmediate(ability, _caster, sourceItem);
                return;
            }

            _currentAbility = ability;
            _validTiles.Clear();

            Debug.Log($"[Targeting] Enter: {_currentAbility.name} (Caster: {_caster.name}, Source: {sourceItem?.name ?? "Skill"})");

            // 3. 计算范围
            var rangeTiles = TargetingResolver.TilesInRange(grid, _caster.UnitRef.Coords, ability.minRange, ability.maxRange);
            foreach (var t in rangeTiles) _validTiles.Add(t);

            // 4. 视觉反馈
            highlighter.ClearAll();
            if (outlineManager)
            {
                outlineManager.SetAbilityRange(_validTiles);
                outlineManager.SetState(OutlineState.AbilityTargeting);
            }

            if (gridCursor) gridCursor.Hide();
            Cursor.SetCursor(cursorInvalid, cursorHotspot, CursorMode.Auto);
        }

        private void ExecuteImmediate(Ability ability, BattleUnit caster, InventoryItem sourceItem)
        {
            if (!ability.CanUse(caster))
            {
                Debug.Log("AP/MP 不足，无法释放即时技能");
                return;
            }

            var ctx = new AbilityContext
            {
                Caster = caster,
                Origin = caster.UnitRef.Coords,
                SourceItem = sourceItem
            };

            if (ability.targetType == AbilityTargetType.Self)
            {
                ctx.TargetUnits.Add(caster);
                ctx.TargetTiles.Add(caster.UnitRef.Coords);
            }
            else
            {
                ctx.TargetTiles.Add(caster.UnitRef.Coords);
            }

            Debug.Log($"[Targeting] Immediate Fire: {ability.name}");
            var action = new AbilityAction(ability, ctx, abilityRunner);
            actionQueue.Enqueue(action);
            StartCoroutine(actionQueue.RunAll());
        }

        private bool CheckContentValidity(HexCoords coords)
        {
            if (_currentAbility == null || _caster == null) return false;

            selectionManager.TryGetUnitAt(coords, out Unit targetUnit);
            var casterUnit = _caster.GetComponent<Unit>();

            // 特殊处理 Self：允许点击自己所在的格子
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
                    // 只有有效时才绘制意图
                    var aoeTiles = TargetingResolver.GetAOETiles(hoverC, _currentAbility, _caster.UnitRef.Coords);
                    Vector3 startPos = _caster.transform.position;
                    Vector3 endPos = grid.GetTileWorldPosition(hoverC);

                    bool isDirectionalAOE = _currentAbility.shape == TargetShape.Line || _currentAbility.shape == TargetShape.Cone;
                    // 如果是自己点自己，不画箭头，只画格子
                    bool showArrow = !isDirectionalAOE && !hoverC.Equals(_caster.UnitRef.Coords);

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

            // 构造 Context
            selectionManager.TryGetUnitAt(coords, out Unit targetUnit);
            BattleUnit targetBattleUnit = targetUnit ? targetUnit.GetComponent<BattleUnit>() : null;
            var casterUnit = _caster.GetComponent<Unit>();

            // 如果是 Self 技能且点了自己，把 targetBattleUnit 修正为自己
            if (_currentAbility.targetType == AbilityTargetType.Self && coords.Equals(casterUnit.Coords))
                targetBattleUnit = _caster;

            var ctx = new AbilityContext
            {
                Caster = _caster,
                Origin = casterUnit.Coords,
                SourceItem = _currentSourceItem
            };
            if (targetBattleUnit != null) ctx.TargetUnits.Add(targetBattleUnit);
            ctx.TargetTiles.Add(coords);

            // 二次校验
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
            _currentSourceItem = null;
            _validTiles.Clear();

            if (gridCursor) gridCursor.Hide();
            highlighter.ClearAll();

            if (outlineManager)
            {
                outlineManager.ClearAbilityRange();
                outlineManager.ClearPlayerIntent();

                // 恢复 Outline 状态到 Movement (如果有选中单位)
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