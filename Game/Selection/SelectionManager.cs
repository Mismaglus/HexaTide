using System.Collections.Generic;
using System;
using UnityEngine;
using Core.Hex;
using Game.Common;
using Game.Units;
using Game.Grid;
using Game.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Game.Battle
{
    public enum RangeMode { None, Disk, Ring }
    public enum RangePivot { Hover, Selected }

    [DisallowMultipleComponent]
    public class SelectionManager : MonoBehaviour
    {
        [Header("Refs")]
        public BattleHexInput input;
        public HexHighlighter highlighter;
        public BattleHexGrid grid;
        [SerializeField] BattleStateMachine _battleSM;
        public AbilityTargetingSystem targetingSystem;
        public BattleRules battleRules;

        [Header("Visuals Manager")]
        public GridOutlineManager outlineManager;
        public BattleCursor gridCursor;

        [Header("Range Settings")]
        public RangeMode rangeMode = RangeMode.None;
        public RangePivot rangePivot = RangePivot.Hover;
        [Min(0)] public int radius = 2;

        [Header("Mouse Icons")]
        public Texture2D cursorDefault;
        public Texture2D cursorHoverSelectable;
        public Texture2D cursorMoveFree;
        public Texture2D cursorMoveCost;
        public Texture2D cursorInvalid;
        public Vector2 cursorHotspot = Vector2.zero;

        [Header("Debug")]
        public RangeMode debugRangeMode = RangeMode.None;
        public int debugRadius = 2;

        // Cache
        HexCoords? _selected;
        HexCoords? _hoverCache;

        HashSet<HexCoords> _currentFreeSet = new HashSet<HexCoords>();
        HashSet<HexCoords> _currentCostSet = new HashSet<HexCoords>();

        readonly Dictionary<HexCoords, Unit> _units = new();

        [Header("Data Source")]
        [SerializeField] private GridOccupancy occupancy;

        Unit _selectedUnit;

        public Unit SelectedUnit
        {
            get => _selectedUnit;
            private set
            {
                if (_selectedUnit == value) return;

                // 1. 取消订阅旧单位
                if (_selectedUnit != null && _selectedUnit.TryGetComponent<BattleUnit>(out var oldBu))
                {
                    oldBu.OnResourcesChanged -= HandleUnitResourcesChanged;
                }

                _selectedUnit = value;

                // 2. 订阅新单位
                if (_selectedUnit != null && _selectedUnit.TryGetComponent<BattleUnit>(out var newBu))
                {
                    newBu.OnResourcesChanged += HandleUnitResourcesChanged;
                }

                OnSelectedUnitChanged?.Invoke(_selectedUnit);
                RecalcRange();
            }
        }

        public event Action<Unit> OnSelectedUnitChanged;
        Unit _hoveredUnit;
        readonly Dictionary<Unit, UnitHighlighter> _HighlighterCache = new();

        void Reset()
        {
            if (!input) input = FindFirstObjectByType<BattleHexInput>(FindObjectsInactive.Exclude);
            if (!highlighter) highlighter = FindFirstObjectByType<HexHighlighter>(FindObjectsInactive.Exclude);
            if (!grid) grid = FindFirstObjectByType<BattleHexGrid>(FindObjectsInactive.Exclude);
            if (!outlineManager) outlineManager = FindFirstObjectByType<GridOutlineManager>(FindObjectsInactive.Exclude);
            if (!occupancy) occupancy = FindFirstObjectByType<GridOccupancy>(FindObjectsInactive.Exclude);
        }

        void Awake()
        {
            if (_battleSM == null) _battleSM = FindFirstObjectByType<BattleStateMachine>();
            if (targetingSystem == null) targetingSystem = FindFirstObjectByType<AbilityTargetingSystem>();
            if (battleRules == null) battleRules = FindFirstObjectByType<BattleRules>();

            if (gridCursor == null) gridCursor = FindFirstObjectByType<BattleCursor>();
            if (outlineManager == null) outlineManager = FindFirstObjectByType<GridOutlineManager>();

            if (occupancy == null)
            {
                occupancy = FindFirstObjectByType<GridOccupancy>();
                if (occupancy == null)
                {
                    var go = new GameObject("GridOccupancy_Auto");
                    occupancy = go.AddComponent<GridOccupancy>();
                }
            }
        }

        void Start()
        {
            if (cursorDefault == null)
            {
                var bc = FindFirstObjectByType<BattleController>();
                if (bc != null) cursorDefault = bc.defaultCursor;
            }
            ApplyCursor(cursorDefault);
        }

        public void ApplyCursor(Texture2D tex)
        {
            var target = tex ? tex : cursorDefault;
            if (target != null) Cursor.SetCursor(target, cursorHotspot, CursorMode.Auto);
            else Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        }

        void Update()
        {
            var controlSvc = PlayerControlService.Instance;
            if (controlSvc != null && !controlSvc.IsControlEnabled) return;

#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) Deselect();
            var mouse = Mouse.current;
            if (mouse != null && mouse.rightButton.wasPressedThisFrame) Deselect();
#endif
        }

        void OnEnable()
        {
            if (input != null)
            {
                input.OnTileClicked += OnTileClicked;
                input.OnHoverChanged += OnHoverChanged;
            }
        }
        void OnDisable()
        {
            if (input != null)
            {
                input.OnTileClicked -= OnTileClicked;
                input.OnHoverChanged -= OnHoverChanged;
            }
        }

        // 资源变化回调
        void HandleUnitResourcesChanged()
        {
            if (SelectedUnit != null) RecalcRange();
        }

        public bool HasUnitAt(HexCoords c)
        {
            if (_units.ContainsKey(c)) return true;
            return occupancy != null && occupancy.HasUnitAt(c);
        }
        public bool IsEmpty(HexCoords c) => !HasUnitAt(c);

        public bool TryGetUnitAt(HexCoords c, out Unit u)
        {
            if (_units.TryGetValue(c, out u)) return true;
            if (occupancy != null && occupancy.TryGetUnitAt(c, out var occ))
            {
                RemoveUnitMapping(occ);
                _units[c] = occ;
                u = occ;
                return true;
            }
            u = null;
            return false;
        }

        void HandleClickOnEmptyTile(HexCoords targetCoords)
        {
            if (_battleSM != null && _battleSM.CurrentTurn != TurnSide.Player) return;
            if (SelectedUnit == null || _selected == null) return;
            var unit = SelectedUnit;
            if (!unit.IsPlayerControlled) return;
            if (unit.Coords.Equals(targetCoords)) return;
            if (HasUnitAt(targetCoords)) return;
            if (!unit.TryGetComponent<UnitMover>(out var mover)) return;
            if (mover.IsMoving) return;
            if (battleRules == null) return;
            var path = HexPathfinder.FindPath(unit.Coords, targetCoords, battleRules, unit);
            if (path == null || path.Count == 0) { Debug.Log("无法到达"); return; }
            if (!_currentFreeSet.Contains(targetCoords) && !_currentCostSet.Contains(targetCoords)) { Debug.Log("目标太远"); return; }

            // 移动开始，清除范围显示
            if (outlineManager) outlineManager.ClearMovementRange();

            mover.FollowPath(path, onComplete: () =>
            {
                RemoveUnitMapping(unit);
                _units[unit.Coords] = unit;
                _selected = unit.Coords;
                highlighter.SetSelected(unit.Coords);
                RecalcRange();
            });
        }

        UnitHighlighter GetHighlighter(Unit u)
        {
            if (u == null) return null;
            if (_HighlighterCache.TryGetValue(u, out var v) && v != null) return v;
            v = u.GetComponentInChildren<UnitHighlighter>(true);
            _HighlighterCache[u] = v;
            return v;
        }

        void Deselect()
        {
            var current = SelectedUnit;
            if (current == null) return;
            GetHighlighter(current)?.SetSelected(false);
            _selected = null;
            highlighter.SetSelected(null);

            // 清理
            ClearVisuals();
            if (outlineManager) outlineManager.SetState(OutlineState.None);

            ApplyCursor(cursorDefault);
            SelectedUnit = null;
            if (_hoveredUnit != null && _hoveredUnit != SelectedUnit) GetHighlighter(_hoveredUnit)?.SetHover(true);
        }

        // ⭐ 修复：移除了不存在的 drawer 引用，改为调用 Manager
        void ClearVisuals()
        {
            if (outlineManager) outlineManager.ClearMovementRange();
            if (gridCursor) gridCursor.Hide();
        }

        public void RegisterUnit(Unit u)
        {
            if (u == null) return;
            if (occupancy != null) occupancy.Register(u);
            RemoveUnitMapping(u);
            _units[u.Coords] = u;
            u.OnMoveFinished -= OnUnitMoveFinished;
            u.OnMoveFinished += OnUnitMoveFinished;
            _ = GetHighlighter(u);
        }

        public void UnregisterUnit(Unit u)
        {
            if (u == null) return;
            if (occupancy != null) occupancy.Unregister(u);
            u.OnMoveFinished -= OnUnitMoveFinished;
            var vis = GetHighlighter(u);
            vis?.SetHover(false);
            vis?.SetSelected(false);
            if (_hoveredUnit == u) _hoveredUnit = null;
            RemoveUnitMapping(u);
            if (SelectedUnit == u) { _selected = null; highlighter.SetSelected(null); SelectedUnit = null; }
        }

        public void SyncUnit(Unit u)
        {
            if (u == null) return;
            if (occupancy != null) occupancy.SyncUnit(u);
            RemoveUnitMapping(u);
            _units[u.Coords] = u;
        }

        void RemoveUnitMapping(Unit u)
        {
            HexCoords keyToRemove = default;
            bool found = false;
            foreach (var kv in _units) { if (kv.Value == u) { keyToRemove = kv.Key; found = true; break; } }
            if (found) _units.Remove(keyToRemove);
        }

        void OnHoverChanged(HexCoords? h)
        {
            if (targetingSystem != null && targetingSystem.IsTargeting) { if (_hoverCache.HasValue) { _hoverCache = null; highlighter.SetHover(null); if (gridCursor) gridCursor.Hide(); } return; }
            _hoverCache = h;
            Unit unitUnderMouse = null;
            if (h.HasValue) TryGetUnitAt(h.Value, out unitUnderMouse);

            if (unitUnderMouse != null) { if (gridCursor) gridCursor.Hide(); ApplyCursor(cursorHoverSelectable); }
            else if (SelectedUnit != null && SelectedUnit.IsPlayerControlled && h.HasValue)
            {
                HexCoords pos = h.Value; bool isFree = _currentFreeSet.Contains(pos); bool isCost = _currentCostSet.Contains(pos); bool isValid = isFree || isCost;
                if (gridCursor) gridCursor.Show(pos, isValid);
                if (isFree) ApplyCursor(cursorMoveFree); else if (isCost) ApplyCursor(cursorMoveCost); else ApplyCursor(cursorInvalid);
            }
            else { if (gridCursor) gridCursor.Hide(); ApplyCursor(cursorDefault); }

            Unit newHover = unitUnderMouse;
            if (ReferenceEquals(newHover, _hoveredUnit)) return;
            if (_hoveredUnit != null) { var vOld = GetHighlighter(_hoveredUnit); if (_hoveredUnit == SelectedUnit) { vOld?.SetHover(false); vOld?.SetSelected(true); } else { vOld?.SetHover(false); } }
            if (newHover != null) { var vNew = GetHighlighter(newHover); if (newHover == SelectedUnit) { vNew?.SetSelected(true); vNew?.SetHover(true); } else { vNew?.SetHover(true); } }
            _hoveredUnit = newHover;
        }

        void OnTileClicked(HexCoords c)
        {
            if (targetingSystem != null && targetingSystem.IsTargeting) return;
            if (TryGetUnitAt(c, out var unit))
            {
                if (SelectedUnit == unit) { Deselect(); return; }
                if (SelectedUnit != null) GetHighlighter(SelectedUnit)?.SetSelected(false);
                SelectedUnit = unit; _selected = c; highlighter.SetSelected(c);
                var vNew = GetHighlighter(SelectedUnit); if (_hoveredUnit == SelectedUnit) { vNew?.SetSelected(true); vNew?.SetHover(true); } else vNew?.SetSelected(true);
                return;
            }
            if (SelectedUnit != null && !SelectedUnit.IsMoving) HandleClickOnEmptyTile(c);
        }

        void OnUnitMoveFinished(Unit u, HexCoords from, HexCoords to)
        {
            RemoveUnitMapping(u); _units[to] = u;
            if (SelectedUnit == u) { _selected = to; highlighter.SetSelected(to); RecalcRange(); }
        }

        void RecalcRange()
        {
            _currentFreeSet.Clear(); _currentCostSet.Clear();
            if (gridCursor) gridCursor.Hide();
            if (targetingSystem != null && targetingSystem.IsTargeting) return;

            if (SelectedUnit != null)
            {
                if (SelectedUnit.IsPlayerControlled && SelectedUnit.TryGetComponent<UnitAttributes>(out var attrs))
                {

                    // 如果在移动中，清空并不画
                    if (SelectedUnit.TryGetComponent<UnitMover>(out var mover) && mover.IsMoving)
                    {
                        if (outlineManager) outlineManager.ClearMovementRange();
                        return;
                    }

                    HexCoords center = SelectedUnit.Coords; int stride = attrs.Core.CurrentStride; int ap = attrs.Core.CurrentAP;

                    // 这里的逻辑可以根据需求微调：比如 AP=0 Stride=0 时不画
                    if (stride == 0 && ap == 0)
                    {
                        if (outlineManager) outlineManager.ClearMovementRange();
                        return;
                    }

                    if (stride > 0) foreach (var c in center.Disk(stride)) _currentFreeSet.Add(c); else _currentFreeSet.Add(center);
                    int totalRange = stride + ap;
                    if (ap > 0) foreach (var c in center.Disk(totalRange)) if (!_currentFreeSet.Contains(c)) _currentCostSet.Add(c);
                    if (outlineManager) { outlineManager.SetMovementRange(_currentFreeSet, _currentCostSet); outlineManager.SetState(OutlineState.Movement); }
                    if (_hoverCache.HasValue) OnHoverChanged(_hoverCache);
                }
                return;
            }
            if (debugRangeMode != RangeMode.None && _hoverCache.HasValue && outlineManager)
            {
                var set = new HashSet<HexCoords>();
                if (debugRangeMode == RangeMode.Disk) foreach (var c in _hoverCache.Value.Disk(debugRadius)) set.Add(c); else foreach (var c in _hoverCache.Value.Ring(debugRadius)) set.Add(c);
                outlineManager.SetMovementRange(set, null); outlineManager.SetState(OutlineState.Movement);
            }
        }
    }
}