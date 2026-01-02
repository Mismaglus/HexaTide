using System.Collections.Generic;
using System;
using UnityEngine;
using Core.Hex;
using Game.Common;
using Game.Units;
using Game.Grid;
using Game.UI;
using Game.Battle.Status;
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
        [Tooltip("Prefab to spawn on the selected tile/unit (Ghost)")]
        public GameObject selectionGhostPrefab;
        private GameObject _selectionGhostInstance;

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

                if (_selectedUnit != null && _selectedUnit.TryGetComponent<BattleUnit>(out var oldBu))
                {
                    oldBu.OnResourcesChanged -= HandleUnitResourcesChanged;
                    if (oldBu.Status) oldBu.Status.OnStatusChanged -= HandleUnitStatusChanged;
                }

                _selectedUnit = value;

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
            OnSelectedUnitChanged += UpdateGhost;
        }
        void OnDisable()
        {
            if (input != null)
            {
                input.OnTileClicked -= OnTileClicked;
                input.OnHoverChanged -= OnHoverChanged;
            }
            OnSelectedUnitChanged -= UpdateGhost;
            if (_selectionGhostInstance) Destroy(_selectionGhostInstance);
        }

        void UpdateGhost(Unit u)
        {
            if (u != null)
            {
                if (!_selectionGhostInstance && selectionGhostPrefab)
                {
                    _selectionGhostInstance = Instantiate(selectionGhostPrefab);
                }
                if (_selectionGhostInstance)
                {
                    _selectionGhostInstance.SetActive(true);
                    if (grid) _selectionGhostInstance.transform.position = grid.GetTileWorldPosition(u.Coords);
                }
            }
            else
            {
                if (_selectionGhostInstance) _selectionGhostInstance.SetActive(false);
            }
        }

        void HandleUnitResourcesChanged() => RecalcRange();
        void HandleUnitStatusChanged() => RecalcRange();

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

        // ⭐ 核心修改：获取单位时检查迷雾
        public bool HasUnitAt(HexCoords c)
        {
            // 如果格子不可见，假装没有单位 (防止选中/悬停)
            if (FogOfWarSystem.Instance != null && !FogOfWarSystem.Instance.IsTileVisible(c))
            {
                return false;
            }

            if (_units.ContainsKey(c)) return true;
            return occupancy != null && occupancy.HasUnitAt(c);
        }

        public bool IsEmpty(HexCoords c) => !HasUnitAt(c);

        // ⭐ 核心修改：获取单位时检查迷雾
        public bool TryGetUnitAt(HexCoords c, out Unit u)
        {
            // 先获取真实单位
            bool hasUnit = false;
            u = null;

            if (_units.TryGetValue(c, out u)) hasUnit = true;
            else if (occupancy != null && occupancy.TryGetUnitAt(c, out var occ))
            {
                RemoveUnitMapping(occ);
                _units[c] = occ;
                u = occ;
                hasUnit = true;
            }

            // 迷雾过滤：如果单位存在但不可见，且不是玩家自己的单位，则视为获取失败
            if (hasUnit && u != null && FogOfWarSystem.Instance != null)
            {
                // 如果单位是敌方，且该格子不可见 -> 返回 False
                if (!u.IsPlayerControlled && !FogOfWarSystem.Instance.IsTileVisible(c))
                {
                    u = null;
                    return false;
                }
            }

            return hasUnit;
        }

        void HandleClickOnEmptyTile(HexCoords targetCoords)
        {
            if (_battleSM != null && _battleSM.CurrentTurn != TurnSide.Player) return;
            if (SelectedUnit == null || _selected == null) return;
            var unit = SelectedUnit;
            if (!unit.IsPlayerControlled) return;
            if (unit.Coords.Equals(targetCoords)) return;
            if (HasUnitAt(targetCoords)) return;

            if (!_currentFreeSet.Contains(targetCoords))
            {
                Debug.Log("Target out of range.");
                return;
            }

            if (!unit.TryGetComponent<UnitMover>(out var mover)) return;
            if (mover.IsMoving) return;
            if (battleRules == null) return;

            var battleUnit = unit.GetComponent<BattleUnit>();
            bool isSprinting = (battleUnit != null && battleUnit.Status != null && battleUnit.Status.HasSprintState);

            var path = HexPathfinder.FindPath(unit.Coords, targetCoords, battleRules, unit, isSprinting);
            if (path == null || path.Count == 0) { Debug.Log("无法到达"); return; }

            var calculator = new MovementCalculator(occupancy, battleRules);
            var oldProvider = mover.MovementCostProvider;

            mover.MovementCostProvider = (from, to) =>
            {
                return calculator.GetMoveCost(from, to, unit, isSprinting);
            };

            if (outlineManager) { outlineManager.ClearMovementRange(); outlineManager.ToggleEnemyIntent(false); }

            mover.FollowPath(path, onComplete: () =>
            {
                mover.MovementCostProvider = oldProvider;

                RemoveUnitMapping(unit);
                _units[unit.Coords] = unit;
                _selected = unit.Coords;
                highlighter.SetSelected(unit.Coords);

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
            PathPreviewController.ClearPreview(highlighter);
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
            if (targetingSystem != null && targetingSystem.IsTargeting)
            {
                if (_hoverCache.HasValue)
                {
                    _hoverCache = null;
                    highlighter.SetHover(null);
                    if (gridCursor) gridCursor.Hide();
                }
                PathPreviewController.ClearPreview(highlighter);
                return;
            }
            _hoverCache = h;
            Unit unitUnderMouse = null;

            // 获取单位时也会触发迷雾检查，如果不可见会返回 null
            if (h.HasValue) TryGetUnitAt(h.Value, out unitUnderMouse);

            if (unitUnderMouse != null)
            {
                if (gridCursor) gridCursor.Hide();
                ApplyCursor(cursorHoverSelectable);
                PathPreviewController.ClearPreview(highlighter);
            }
            else if (SelectedUnit != null && SelectedUnit.IsPlayerControlled && h.HasValue)
            {
                HexCoords pos = h.Value;
                bool isFree = _currentFreeSet.Contains(pos);

                if (gridCursor) gridCursor.Show(pos, isFree);
                if (isFree)
                {
                    ApplyCursor(cursorMoveFree);
                    if (!HasUnitAt(pos))
                    {
                        var bu = SelectedUnit.GetComponent<BattleUnit>();
                        bool isSprinting = (bu != null && bu.Status != null && bu.Status.HasSprintState);
                        if (!PathPreviewController.TryShowBattlePreview(highlighter, grid, SelectedUnit, battleRules, isSprinting, pos, out _))
                        {
                            PathPreviewController.ClearPreview(highlighter);
                        }
                    }
                    else
                    {
                        PathPreviewController.ClearPreview(highlighter);
                    }
                }
                else
                {
                    ApplyCursor(cursorInvalid);
                    PathPreviewController.ClearPreview(highlighter);
                }
            }
            else
            {
                if (gridCursor) gridCursor.Hide();
                ApplyCursor(cursorDefault);
                PathPreviewController.ClearPreview(highlighter);
            }

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
            if (SelectedUnit == u)
            {
                _selected = to;
                highlighter.SetSelected(to);
                RecalcRange();
                UpdateGhost(u);
            }
            if (BattleIntentSystem.Instance != null)
                BattleIntentSystem.Instance.UpdateIntents();
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
                    if (SelectedUnit.TryGetComponent<UnitMover>(out var mover) && mover.IsMoving)
                    {
                        if (outlineManager) outlineManager.ClearMovementRange();
                        return;
                    }

                    HexCoords center = SelectedUnit.Coords;
                    int stride = attrs.Core.CurrentStride;
                    int ap = attrs.Core.CurrentAP;

                    var bu = SelectedUnit.GetComponent<BattleUnit>();
                    bool isSprinting = (bu != null && bu.Status != null && bu.Status.HasSprintState);
                    int maxCost = isSprinting ? (stride + ap) : stride;

                    if (maxCost == 0)
                    {
                        if (outlineManager) outlineManager.ClearMovementRange();
                        return;
                    }

                    var reachable = HexPathfinder.GetReachableCells(center, maxCost, battleRules, SelectedUnit, isSprinting);

                    foreach (var kv in reachable)
                    {
                        HexCoords pos = kv.Key;
                        int cost = kv.Value;

                        _currentFreeSet.Add(center);
                        if (cost == 0) continue;

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
