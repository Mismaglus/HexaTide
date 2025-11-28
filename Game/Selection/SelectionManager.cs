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

        // ⭐ 战术动作状态：是否激活了“疾跑/无视惩罚”模式
        private bool _isTacticalMoveActive = false;
        public bool IsTacticalMoveActive => _isTacticalMoveActive;
        public event Action<bool> OnTacticalStateChanged;

        // 战术动作消耗
        private const int TACTICAL_AP_COST = 1;

        Unit _selectedUnit;

        public Unit SelectedUnit
        {
            get => _selectedUnit;
            private set
            {
                if (_selectedUnit == value) return;

                if (_selectedUnit != null && _selectedUnit.TryGetComponent<BattleUnit>(out var oldBu))
                {
                    oldBu.OnResourcesChanged -= HandleUnitResourcesChanged;
                }

                _selectedUnit = value;

                // 切换单位时，重置战术状态
                _isTacticalMoveActive = false;
                OnTacticalStateChanged?.Invoke(false);

                if (_selectedUnit != null && _selectedUnit.TryGetComponent<BattleUnit>(out var newBu))
                {
                    newBu.OnResourcesChanged += HandleUnitResourcesChanged;
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

        // === ⭐ API: 切换战术动作 (UI 绑定用) ===
        public void ToggleTacticalAction()
        {
            if (SelectedUnit == null) return;

            // 检查是否有足够资源开启
            var battleUnit = SelectedUnit.GetComponent<BattleUnit>();
            if (battleUnit != null && battleUnit.CurAP < TACTICAL_AP_COST && !_isTacticalMoveActive)
            {
                Debug.Log("Not enough AP for Tactical Sprint!");
                return;
            }

            _isTacticalMoveActive = !_isTacticalMoveActive;
            OnTacticalStateChanged?.Invoke(_isTacticalMoveActive);
            RecalcRange();
        }

        public void SetTacticalAction(bool active)
        {
            if (_isTacticalMoveActive == active) return;
            _isTacticalMoveActive = active;
            OnTacticalStateChanged?.Invoke(_isTacticalMoveActive);
            RecalcRange();
        }

        void HandleUnitResourcesChanged()
        {
            if (SelectedUnit != null) RecalcRange();
        }

        void CheckObserverCapability(Unit unit)
        {
            if (outlineManager == null) return;
            bool canSeeFuture = false;
            if (unit != null && unit.IsPlayerControlled)
            {
                if (unit.TryGetComponent<UnitAttributes>(out var attrs))
                    canSeeFuture = attrs.Optional.CanSeeFutureIntents;
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

            // 1. 寻路 (传入 _isTacticalMoveActive)
            var path = HexPathfinder.FindPath(unit.Coords, targetCoords, battleRules, unit, _isTacticalMoveActive);
            if (path == null || path.Count == 0) { Debug.Log("无法到达"); return; }

            // 2. 战术动作扣费 (1 AP)
            // 这里的扣费是作为“开启状态”的代价，而不是步数消耗
            if (_isTacticalMoveActive)
            {
                var bu = unit.GetComponent<BattleUnit>();
                if (bu != null) bu.TrySpendAP(TACTICAL_AP_COST);
            }

            // 3. ⭐⭐⭐ 关键修改：注入 MovementCostProvider
            // 这样 UnitMover 在移动时也会应用无视惩罚的逻辑，从而正确扣除 Stride (1格1步)
            var calculator = new MovementCalculator(occupancy, battleRules);
            var oldProvider = mover.MovementCostProvider;

            mover.MovementCostProvider = (from, to) =>
            {
                return calculator.GetMoveCost(from, to, unit, _isTacticalMoveActive);
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

                // 移动结束后，自动关闭战术状态
                SetTacticalAction(false);

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
                // 只有 FreeSet 是有效的 (CostSet 现在不使用，或者仅作为备用)
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

        // ⭐⭐⭐ 核心修改：使用真实的 Dijkstra 计算范围 ⭐⭐⭐
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
                    // 因为不允许 AP 移动，所以预算始终是 Stride
                    int maxCost = stride;

                    if (maxCost == 0)
                    {
                        if (outlineManager) outlineManager.ClearMovementRange();
                        return;
                    }

                    // ⭐ 传入战术状态：如果是 True，则计算消耗时无视惩罚 (1步1格)，范围自然变大
                    var reachable = HexPathfinder.GetReachableCells(center, maxCost, battleRules, SelectedUnit, _isTacticalMoveActive);

                    foreach (var kv in reachable)
                    {
                        HexCoords pos = kv.Key;
                        int cost = kv.Value;

                        _currentFreeSet.Add(center);
                        if (cost == 0) continue;

                        // 因为 maxCost == stride，且我们只搜索到 maxCost，所以所有结果都是 Free
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

            if (debugRangeMode != RangeMode.None && _hoverCache.HasValue && outlineManager)
            {
                var set = new HashSet<HexCoords>();
                if (debugRangeMode == RangeMode.Disk) foreach (var c in _hoverCache.Value.Disk(debugRadius)) set.Add(c); else foreach (var c in _hoverCache.Value.Ring(debugRadius)) set.Add(c);
                outlineManager.SetMovementRange(set, null); outlineManager.SetState(OutlineState.Movement);
            }
        }
    }
}