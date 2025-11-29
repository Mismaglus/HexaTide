using System.Collections.Generic;
using System;
using UnityEngine;
using Core.Hex;
using Game.Common;
using Game.Units;
using Game.Grid;
using Game.UI;
using Game.Battle.Status; // 引用 Status
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

        // 战术消耗常量
        private const int TACTICAL_AP_COST = 1;

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
                    if (oldBu.Status) oldBu.Status.OnStatusChanged -= HandleUnitStatusChanged;
                }

                _selectedUnit = value;

                // 2. 订阅新单位
                if (_selectedUnit != null && _selectedUnit.TryGetComponent<BattleUnit>(out var newBu))
                {
                    newBu.OnResourcesChanged += HandleUnitResourcesChanged;
                    if (newBu.Status) newBu.Status.OnStatusChanged += HandleUnitStatusChanged;
                }

                OnSelectedUnitChanged?.Invoke(_selectedUnit);
                CheckObserverCapability(_selectedUnit);

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
        void HandleUnitResourcesChanged() => RecalcRange();
        // 状态变化回调 (例如开启了疾跑)
        void HandleUnitStatusChanged() => RecalcRange();

        void CheckObserverCapability(Unit unit)
        {
            if (outlineManager == null) return;

            bool canSeeFuture = false;
            if (unit != null && unit.IsPlayerControlled)
            {
                if (unit.TryGetComponent<UnitAttributes>(out var attrs))
                {
                    canSeeFuture = attrs.Optional.CanSeeFutureIntents;
                }
            }

            outlineManager.ToggleFutureVisibility(canSeeFuture);
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

            // ⭐ 检查：目标是否在可达范围内 (Free Set)
            if (!_currentFreeSet.Contains(targetCoords))
            {
                Debug.Log("Target out of range.");
                return;
            }

            if (!unit.TryGetComponent<UnitMover>(out var mover)) return;
            if (mover.IsMoving) return;
            if (battleRules == null) return;

            // 1. 检查当前状态 (是否疾跑中)
            var battleUnit = unit.GetComponent<BattleUnit>();
            bool isSprinting = (battleUnit != null && battleUnit.Status != null && battleUnit.Status.HasSprintState);

            // 2. 寻路 (传入状态)
            var path = HexPathfinder.FindPath(unit.Coords, targetCoords, battleRules, unit, isSprinting);
            if (path == null || path.Count == 0) { Debug.Log("无法到达"); return; }

            // 3. 注入计算器给 UnitMover
            // 这样 UnitMover 在移动时也会应用无视惩罚的逻辑
            var calculator = new MovementCalculator(occupancy, battleRules);
            var oldProvider = mover.MovementCostProvider;

            mover.MovementCostProvider = (from, to) =>
            {
                return calculator.GetMoveCost(from, to, unit, isSprinting);
            };

            // 4. UI 清理
            if (outlineManager)
            {
                outlineManager.ClearMovementRange();
                outlineManager.ToggleEnemyIntent(false);
            }

            // 5. 执行移动
            mover.FollowPath(path, onComplete: () =>
            {
                // 还原 Provider
                mover.MovementCostProvider = oldProvider;

                RemoveUnitMapping(unit);
                _units[unit.Coords] = unit;
                _selected = unit.Coords;
                highlighter.SetSelected(unit.Coords);

                // 注意：我们不在这里移除 Sprint 状态，因为它通常是持续一回合的 Buff

                RecalcRange();
                if (outlineManager) outlineManager.ToggleEnemyIntent(true);
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

            ClearVisuals();
            if (outlineManager) outlineManager.SetState(OutlineState.None);

            ApplyCursor(cursorDefault);
            SelectedUnit = null;
            if (_hoveredUnit != null && _hoveredUnit != SelectedUnit) GetHighlighter(_hoveredUnit)?.SetHover(true);
        }

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
                HexCoords pos = h.Value;
                bool isFree = _currentFreeSet.Contains(pos);

                if (gridCursor) gridCursor.Show(pos, isFree);
                if (isFree) ApplyCursor(cursorMoveFree); else ApplyCursor(cursorInvalid);
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

        // ⭐⭐⭐ 核心修改：基于状态计算范围 ⭐⭐⭐
        void RecalcRange()
        {
            _currentFreeSet.Clear(); _currentCostSet.Clear();
            if (gridCursor) gridCursor.Hide();
            if (targetingSystem != null && targetingSystem.IsTargeting) return;

            if (SelectedUnit != null)
            {
                if (SelectedUnit.IsPlayerControlled && SelectedUnit.TryGetComponent<UnitAttributes>(out var attrs))
                {
                    if (SelectedUnit.TryGetComponent<UnitMover>(out var mover) && mover.IsMoving)
                    {
                        if (outlineManager) outlineManager.ClearMovementRange();
                        return;
                    }

                    HexCoords center = SelectedUnit.Coords;
                    int stride = attrs.Core.CurrentStride;
                    int ap = attrs.Core.CurrentAP;

                    var bu = SelectedUnit.GetComponent<BattleUnit>();

                    // 1. 检查状态
                    bool isSprinting = (bu != null && bu.Status != null && bu.Status.HasSprintState);

                    // 2. 预算计算：
                    // 如果疾跑中：预算 = Stride + AP (允许把 AP 当步数用，且无视地形/ZOC)
                    // 如果普通：预算 = Stride (只允许用现有步数，遵循地形/ZOC规则)
                    int maxCost = isSprinting ? (stride + ap) : stride;

                    if (maxCost == 0)
                    {
                        if (outlineManager) outlineManager.ClearMovementRange();
                        return;
                    }

                    // 3. 调用核心计算
                    var reachable = HexPathfinder.GetReachableCells(center, maxCost, battleRules, SelectedUnit, isSprinting);

                    foreach (var kv in reachable)
                    {
                        HexCoords pos = kv.Key;
                        int cost = kv.Value;

                        _currentFreeSet.Add(center);
                        if (cost == 0) continue;

                        // 全部放入 FreeSet (显示绿色)，因为只要能到达，我们就允许点击
                        _currentFreeSet.Add(pos);
                    }

                    if (outlineManager)
                    {
                        outlineManager.SetMovementRange(_currentFreeSet, null);
                        outlineManager.SetState(OutlineState.Movement);
                    }

                    if (_hoverCache.HasValue) OnHoverChanged(_hoverCache);
                }
                return;
            }

            // Debug 模式
            if (debugRangeMode != RangeMode.None && _hoverCache.HasValue && outlineManager)
            {
                var set = new HashSet<HexCoords>();
                if (debugRangeMode == RangeMode.Disk) foreach (var c in _hoverCache.Value.Disk(debugRadius)) set.Add(c); else foreach (var c in _hoverCache.Value.Ring(debugRadius)) set.Add(c);
                outlineManager.SetMovementRange(set, null); outlineManager.SetState(OutlineState.Movement);
            }
        }
    }
}