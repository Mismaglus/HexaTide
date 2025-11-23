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
        // ⭐ 替换了原来的 RangeOutlineDrawer 引用
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
        Unit _selectedUnit;

        public Unit SelectedUnit
        {
            get => _selectedUnit;
            private set
            {
                if (_selectedUnit == value) return;
                _selectedUnit = value;
                OnSelectedUnitChanged?.Invoke(_selectedUnit);
                RecalcRange();
            }
        }

        public event Action<Unit> OnSelectedUnitChanged;
        Unit _hoveredUnit;
        readonly Dictionary<Unit, UnitHighlighter> _HighlighterCache = new();

        [Header("Grid/Occupancy")]
        [SerializeField] private Game.Grid.GridOccupancy occupancy;

        void Reset()
        {
            if (!input) input = FindFirstObjectByType<BattleHexInput>(FindObjectsInactive.Exclude);
            if (!highlighter) highlighter = FindFirstObjectByType<HexHighlighter>(FindObjectsInactive.Exclude);
            if (!grid) grid = FindFirstObjectByType<BattleHexGrid>(FindObjectsInactive.Exclude);
            if (!outlineManager) outlineManager = FindFirstObjectByType<GridOutlineManager>(FindObjectsInactive.Exclude);
        }

        void Awake()
        {
            if (_battleSM == null) _battleSM = FindFirstObjectByType<BattleStateMachine>();
            if (targetingSystem == null) targetingSystem = FindFirstObjectByType<AbilityTargetingSystem>();
            if (battleRules == null) battleRules = FindFirstObjectByType<BattleRules>();
            if (gridCursor == null) gridCursor = FindFirstObjectByType<BattleCursor>();

            // 自动查找 OutlineManager
            if (outlineManager == null) outlineManager = FindFirstObjectByType<GridOutlineManager>();
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

            if (path == null || path.Count == 0)
            {
                Debug.Log("无法到达");
                return;
            }

            if (!_currentFreeSet.Contains(targetCoords) && !_currentCostSet.Contains(targetCoords))
            {
                Debug.Log("目标太远，资源不足");
                return;
            }

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

            // ⭐ 只是通知 Manager 清除，并切回无状态
            if (outlineManager)
            {
                outlineManager.ClearMovementRange();
                outlineManager.SetState(OutlineState.None);
            }
            if (gridCursor) gridCursor.Hide();

            ApplyCursor(cursorDefault);
            SelectedUnit = null;

            if (_hoveredUnit != null && _hoveredUnit != SelectedUnit)
                GetHighlighter(_hoveredUnit)?.SetHover(true);
        }

        public void RegisterUnit(Unit u) { if (u == null) return; occupancy?.Register(u); RemoveUnitMapping(u); _units[u.Coords] = u; u.OnMoveFinished -= OnUnitMoveFinished; u.OnMoveFinished += OnUnitMoveFinished; _ = GetHighlighter(u); }
        public void UnregisterUnit(Unit u) { if (u == null) return; occupancy?.Unregister(u); u.OnMoveFinished -= OnUnitMoveFinished; var vis = GetHighlighter(u); vis?.SetHover(false); vis?.SetSelected(false); if (_hoveredUnit == u) _hoveredUnit = null; RemoveUnitMapping(u); if (SelectedUnit == u) { _selected = null; highlighter.SetSelected(null); SelectedUnit = null; } }
        public void SyncUnit(Unit u) { if (u == null) return; occupancy?.SyncUnit(u); RemoveUnitMapping(u); _units[u.Coords] = u; }
        void RemoveUnitMapping(Unit u) { HexCoords keyToRemove = default; bool found = false; foreach (var kv in _units) { if (kv.Value == u) { keyToRemove = kv.Key; found = true; break; } } if (found) _units.Remove(keyToRemove); }
        public bool IsOccupied(HexCoords c) => HasUnitAt(c);

        void OnHoverChanged(HexCoords? h)
        {
            // 如果 TargetingSystem 正在工作，它会接管状态，这里只负责更新 HoverCache
            if (targetingSystem != null && targetingSystem.IsTargeting)
            {
                if (_hoverCache.HasValue)
                {
                    _hoverCache = null;
                    highlighter.SetHover(null);
                    if (gridCursor) gridCursor.Hide();
                }
                return;
            }

            _hoverCache = h;
            Unit unitUnderMouse = null;
            if (h.HasValue) TryGetUnitAt(h.Value, out unitUnderMouse);

            // === 光标逻辑 ===
            if (unitUnderMouse != null)
            {
                if (gridCursor) gridCursor.Hide();
                ApplyCursor(cursorHoverSelectable);
            }
            else if (SelectedUnit != null && SelectedUnit.IsPlayerControlled && h.HasValue)
            {
                HexCoords pos = h.Value;
                bool isFree = _currentFreeSet.Contains(pos);
                bool isCost = _currentCostSet.Contains(pos);
                bool isValid = isFree || isCost;

                if (gridCursor) gridCursor.Show(pos, isValid);

                if (isFree) ApplyCursor(cursorMoveFree);
                else if (isCost) ApplyCursor(cursorMoveCost);
                else ApplyCursor(cursorInvalid);
            }
            else
            {
                if (gridCursor) gridCursor.Hide();
                ApplyCursor(cursorDefault);
            }

            // ... Highlighter ...
            Unit newHover = unitUnderMouse;
            if (ReferenceEquals(newHover, _hoveredUnit)) return;
            if (_hoveredUnit != null)
            {
                var vOld = GetHighlighter(_hoveredUnit);
                if (_hoveredUnit == SelectedUnit) { vOld?.SetHover(false); vOld?.SetSelected(true); }
                else { vOld?.SetHover(false); }
            }
            if (newHover != null)
            {
                var vNew = GetHighlighter(newHover);
                if (newHover == SelectedUnit) { vNew?.SetSelected(true); vNew?.SetHover(true); }
                else { vNew?.SetHover(true); }
            }
            _hoveredUnit = newHover;
        }

        void OnTileClicked(HexCoords c)
        {
            if (targetingSystem != null && targetingSystem.IsTargeting) return;

            if (TryGetUnitAt(c, out var unit))
            {
                if (SelectedUnit == unit) { Deselect(); return; }
                if (SelectedUnit != null) GetHighlighter(SelectedUnit)?.SetSelected(false);

                SelectedUnit = unit;
                _selected = c;
                highlighter.SetSelected(c);

                var vNew = GetHighlighter(SelectedUnit);
                if (_hoveredUnit == SelectedUnit) { vNew?.SetSelected(true); vNew?.SetHover(true); }
                else vNew?.SetSelected(true);

                return;
            }

            if (SelectedUnit != null && !SelectedUnit.IsMoving)
            {
                HandleClickOnEmptyTile(c);
            }
        }

        void OnUnitMoveFinished(Unit u, HexCoords from, HexCoords to)
        {
            RemoveUnitMapping(u);
            _units[to] = u;
            if (SelectedUnit == u)
            {
                _selected = to;
                highlighter.SetSelected(to);
                RecalcRange();
            }
        }

        void RecalcRange()
        {
            _currentFreeSet.Clear();
            _currentCostSet.Clear();
            if (gridCursor) gridCursor.Hide();

            if (targetingSystem != null && targetingSystem.IsTargeting) return;

            if (SelectedUnit != null)
            {
                if (SelectedUnit.IsPlayerControlled && SelectedUnit.TryGetComponent<UnitAttributes>(out var attrs))
                {
                    HexCoords center = SelectedUnit.Coords;
                    int stride = attrs.Core.CurrentStride;
                    int ap = attrs.Core.CurrentAP;

                    if (stride > 0) foreach (var c in center.Disk(stride)) _currentFreeSet.Add(c);
                    else _currentFreeSet.Add(center);

                    int totalRange = stride + ap;
                    if (ap > 0)
                    {
                        foreach (var c in center.Disk(totalRange))
                        {
                            if (!_currentFreeSet.Contains(c)) _currentCostSet.Add(c);
                        }
                    }

                    // ⭐ 更新 Manager 数据并请求移动状态
                    if (outlineManager)
                    {
                        outlineManager.SetMovementRange(_currentFreeSet, _currentCostSet);
                        outlineManager.SetState(OutlineState.Movement);
                    }

                    if (_hoverCache.HasValue) OnHoverChanged(_hoverCache);
                }
                return;
            }

            if (debugRangeMode != RangeMode.None && _hoverCache.HasValue && outlineManager)
            {
                var set = new HashSet<HexCoords>();
                if (debugRangeMode == RangeMode.Disk) foreach (var c in _hoverCache.Value.Disk(debugRadius)) set.Add(c);
                else foreach (var c in _hoverCache.Value.Ring(debugRadius)) set.Add(c);

                // Debug 模式暂用 FreeDrawer 显示
                outlineManager.SetMovementRange(set, null);
                outlineManager.SetState(OutlineState.Movement);
            }
        }
    }
}
