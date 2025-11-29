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

            // ⭐ 新增：即时技能逻辑
            if (ability.triggerImmediately)
            {
                ExecuteImmediate(ability, _caster);
                return; // 直接返回，不进入瞄准状态
            }

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

        // ⭐ 执行即时技能
        private void ExecuteImmediate(Ability ability, BattleUnit caster)
        {
            if (!ability.CanUse(caster))
            {
                Debug.Log("AP/MP 不足，无法释放即时技能");
                return;
            }

            // 构造上下文：默认以自身为原点
            var ctx = new AbilityContext
            {
                Caster = caster,
                Origin = caster.UnitRef.Coords
            };

            // 如果目标类型是 Self，自动把这自己加进去 (方便 Effect 处理)
            if (ability.targetType == AbilityTargetType.Self)
            {
                ctx.TargetUnits.Add(caster);
                ctx.TargetTiles.Add(caster.UnitRef.Coords);
            }
            // 如果是 PBAoE (Point Blank AoE)，原点也是自己
            else
            {
                // 对于非 Self 的即时技能 (比如周身AOE)，我们只设 Origin，TargetUnits 由 AbilityEffect 自己去 Gather
                ctx.TargetTiles.Add(caster.UnitRef.Coords);
            }

            Debug.Log($"[Targeting] Immediate Fire: {ability.name}");

            // 入队执行
            var action = new AbilityAction(ability, ctx, abilityRunner);
            actionQueue.Enqueue(action);
            StartCoroutine(actionQueue.RunAll());

            // 不需要 CancelTargeting，因为压根没进去
        }

        private bool CheckContentValidity(HexCoords coords)
        {
            if (_currentAbility == null) return false;
            selectionManager.TryGetUnitAt(coords, out Unit targetUnit);
            var casterUnit = _caster.GetComponent<Unit>();

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
                    var aoeTiles = TargetingResolver.GetAOETiles(hoverC, _currentAbility, _caster.UnitRef.Coords);
                    Vector3 startPos = _caster.transform.position;
                    Vector3 endPos = grid.GetTileWorldPosition(hoverC);

                    bool isDirectionalAOE = _currentAbility.shape == TargetShape.Line || _currentAbility.shape == TargetShape.Cone;
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
                outlineManager.ClearPlayerIntent();

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
