using System.Collections.Generic;
using System;
using UnityEngine;
using Core.Hex;
using Game.Common;
using Game.Units;
using Game.Grid;
using Game.UI; // 引用 Drawer 和 Cursor
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
        public HexHighlighter highlighter; // 仅用于 Selected (脚底蓝圈)
        public BattleHexGrid grid;
        [SerializeField] BattleStateMachine _battleSM;
        public AbilityTargetingSystem targetingSystem;
        public BattleRules battleRules;

        [Header("Visuals (Outlines & Cursors)")]
        public RangeOutlineDrawer moveFreeDrawer; // ⭐ 拖入负责画免费范围的物体 (绿框)
        public RangeOutlineDrawer moveCostDrawer; // ⭐ 拖入负责画付费范围的物体 (黄框)
        public BattleCursor gridCursor;           // ⭐ 拖入负责画格子的线框

        [Header("Range Settings")]
        public RangeMode rangeMode = RangeMode.None;
        public RangePivot rangePivot = RangePivot.Hover;
        [Min(0)] public int radius = 2;

        [Header("Mouse Icons")]
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

        void Reset()
        {
            if (!input) input = FindFirstObjectByType<BattleHexInput>(FindObjectsInactive.Exclude);
            if (!highlighter) highlighter = FindFirstObjectByType<HexHighlighter>(FindObjectsInactive.Exclude);
            if (!grid) grid = FindFirstObjectByType<BattleHexGrid>(FindObjectsInactive.Exclude);
        }

        void Awake()
        {
            if (_battleSM == null) _battleSM = FindFirstObjectByType<BattleStateMachine>();
            if (targetingSystem == null) targetingSystem = FindFirstObjectByType<AbilityTargetingSystem>();
            if (battleRules == null) battleRules = FindFirstObjectByType<BattleRules>();

            // 尝试自动查找 (建议手动拖拽以区分 Free 和 Cost)
            if (gridCursor == null) gridCursor = FindFirstObjectByType<BattleCursor>();
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
            if (!input) input = FindFirstObjectByType<BattleHexInput>(FindObjectsInactive.Exclude);
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

        [SerializeField] private Game.Grid.GridOccupancy occupancy;

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

            // A* 寻路
            var path = HexPathfinder.FindPath(unit.Coords, targetCoords, battleRules, unit);

            if (path == null || path.Count == 0)
            {
                Debug.Log("无法到达");
                return;
            }

            // 校验资源
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

            // 清理所有视觉
            highlighter.SetSelected(null);
            ClearVisuals();

            // 恢复默认光标 (BattleController 会在下一帧接管，或者显式设回默认)
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);

            SelectedUnit = null;

            if (_hoveredUnit != null && _hoveredUnit != SelectedUnit)
                GetHighlighter(_hoveredUnit)?.SetHover(true);
        }

        // Helper to clear lines and cursor
        void ClearVisuals()
        {
            if (moveFreeDrawer) moveFreeDrawer.Hide();
            if (moveCostDrawer) moveCostDrawer.Hide();
            if (gridCursor) gridCursor.Hide();
        }

        // ... Registration methods (unchanged) ...
        public void RegisterUnit(Unit u) { if (u == null) return; occupancy?.Register(u); RemoveUnitMapping(u); _units[u.Coords] = u; u.OnMoveFinished -= OnUnitMoveFinished; u.OnMoveFinished += OnUnitMoveFinished; _ = GetHighlighter(u); }
        public void UnregisterUnit(Unit u) { if (u == null) return; occupancy?.Unregister(u); u.OnMoveFinished -= OnUnitMoveFinished; var vis = GetHighlighter(u); vis?.SetHover(false); vis?.SetSelected(false); if (_hoveredUnit == u) _hoveredUnit = null; RemoveUnitMapping(u); if (SelectedUnit == u) { _selected = null; highlighter.SetSelected(null); SelectedUnit = null; } }
        public void SyncUnit(Unit u) { if (u == null) return; occupancy?.SyncUnit(u); RemoveUnitMapping(u); _units[u.Coords] = u; }
        void RemoveUnitMapping(Unit u) { HexCoords keyToRemove = default; bool found = false; foreach (var kv in _units) { if (kv.Value == u) { keyToRemove = kv.Key; found = true; break; } } if (found) _units.Remove(keyToRemove); }
        public bool IsOccupied(HexCoords c) => HasUnitAt(c);


        // —— Input Callbacks ——

        void OnHoverChanged(HexCoords? h)
        {
            // 1. 瞄准时完全退出，不干扰
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

            // 2. 处理移动光标 & 线框
            // 如果选中了自己人，且鼠标在地图上
            if (SelectedUnit != null && SelectedUnit.IsPlayerControlled && h.HasValue)
            {
                HexCoords pos = h.Value;
                bool isFree = _currentFreeSet.Contains(pos);
                bool isCost = _currentCostSet.Contains(pos);

                if (isFree || isCost)
                {
                    // 有效格子：显示线框
                    if (gridCursor) gridCursor.Show(pos, true);

                    // 切换鼠标图标
                    if (isFree) Cursor.SetCursor(cursorMoveFree, cursorHotspot, CursorMode.Auto);
                    else Cursor.SetCursor(cursorMoveCost, cursorHotspot, CursorMode.Auto);
                }
                else
                {
                    // 无效格子：隐藏线框 (或显示红色线框)，切换禁止光标
                    if (gridCursor) gridCursor.Hide(); // 或者 gridCursor.Show(pos, false) 显示红框
                    Cursor.SetCursor(cursorInvalid, cursorHotspot, CursorMode.Auto);
                }
            }
            else
            {
                // 没选中人时，清理
                if (gridCursor) gridCursor.Hide();
                // 恢复默认光标
                if (SelectedUnit == null) Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            }

            // 注意：我们移除了 highlighter.SetHover(h)，因为有了线框就不需要地面变黄了
            // 除非你想保留它作为辅助。如果想保留，取消下面这行的注释：
            // highlighter.SetHover(h);

            // ... Unit Highlighter logic (Keep unchanged) ...
            Unit newHover = null;
            if (h.HasValue) TryGetUnitAt(h.Value, out newHover);
            if (ReferenceEquals(newHover, _hoveredUnit))
            {
                if (SelectedUnit == null && debugRangeMode != RangeMode.None) RecalcRange();
                return;
            }
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
            if (SelectedUnit == null && debugRangeMode != RangeMode.None) RecalcRange();
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
            // 清理旧数据
            _currentFreeSet.Clear();
            _currentCostSet.Clear();
            ClearVisuals(); // 隐藏所有框

            if (targetingSystem != null && targetingSystem.IsTargeting) return;

            if (SelectedUnit != null)
            {
                if (SelectedUnit.IsPlayerControlled && SelectedUnit.TryGetComponent<UnitAttributes>(out var attrs))
                {
                    HexCoords center = SelectedUnit.Coords;
                    int stride = attrs.Core.CurrentStride;
                    int ap = attrs.Core.CurrentAP;

                    // 1. Free Set
                    if (stride > 0) foreach (var c in center.Disk(stride)) _currentFreeSet.Add(c);
                    else _currentFreeSet.Add(center);

                    // 2. Cost Set (排除 Free)
                    int totalRange = stride + ap;
                    if (ap > 0)
                    {
                        foreach (var c in center.Disk(totalRange))
                        {
                            if (!_currentFreeSet.Contains(c)) _currentCostSet.Add(c);
                        }
                    }

                    // ⭐ 3. 绘制两个轮廓
                    // 需要在 Inspector 里分配两个不同的 RangeOutlineDrawer 物体 (不同颜色)
                    if (moveFreeDrawer) moveFreeDrawer.Show(_currentFreeSet);
                    if (moveCostDrawer) moveCostDrawer.Show(_currentCostSet);

                    // 如果有鼠标位置，刷新一下光标状态
                    if (_hoverCache.HasValue) UpdateMovementCursor(_hoverCache);
                }
                return;
            }

            // Debug Logic (Optional)
            if (debugRangeMode != RangeMode.None && _hoverCache.HasValue)
            {
                var set = new HashSet<HexCoords>();
                if (debugRangeMode == RangeMode.Disk) foreach (var c in _hoverCache.Value.Disk(debugRadius)) set.Add(c);
                else foreach (var c in _hoverCache.Value.Ring(debugRadius)) set.Add(c);

                // Debug 时只用 Free 框
                if (moveFreeDrawer) moveFreeDrawer.Show(set);
            }
        }

        void UpdateMovementCursor(HexCoords? h)
        {
            // 复用 OnHoverChanged 里的逻辑
            OnHoverChanged(h);
        }
    }
}