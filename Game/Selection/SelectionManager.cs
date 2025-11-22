using System.Collections.Generic;
using System;
using UnityEngine;
using Core.Hex;
using Game.Common;
using Game.Units;
using Game.Grid;
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

        [Header("Range")]
        public RangeMode rangeMode = RangeMode.None;
        public RangePivot rangePivot = RangePivot.Hover;
        [Min(0)] public int radius = 2;

        [Header("Cursors (Movement)")] // ⭐ 新增光标配置
        public Texture2D cursorMoveFree; // 绿色/靴子 (Stride足)
        public Texture2D cursorMoveCost; // 黄色/AP (消耗AP)
        public Texture2D cursorInvalid;  // 红色/禁止 (不可达)
        public Vector2 cursorHotspot = Vector2.zero;

        [Header("Debug")]
        public RangeMode debugRangeMode = RangeMode.None;
        public int debugRadius = 2;

        // Cache States
        HexCoords? _selected;
        HexCoords? _hoverCache;

        // 缓存当前的移动范围，用于判断光标样式
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

            // 寻路校验
            if (battleRules == null) return;
            var path = HexPathfinder.FindPath(unit.Coords, targetCoords, battleRules, unit);

            // 这里的路径检查要配合 UnitMover 的资源检查 (CanStepTo)
            // 简单起见，我们让 Mover 自己走，走不动它会停
            if (path == null || path.Count == 0)
            {
                Debug.Log("无法到达");
                return;
            }

            // 最终校验一下目标点是不是在我们的 Cost 或 Free 集合里 (防止 A* 算出一条极其绕远的路导致资源不够)
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

            // 清除高亮和光标
            highlighter.SetSelected(null);
            highlighter.ApplyMoveRange(null, null);
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);

            SelectedUnit = null;

            if (_hoveredUnit != null && _hoveredUnit != SelectedUnit)
                GetHighlighter(_hoveredUnit)?.SetHover(true);
        }

        // ... Unit Registration (Keep unchanged) ...
        public void RegisterUnit(Unit u) { if (u == null) return; occupancy?.Register(u); RemoveUnitMapping(u); _units[u.Coords] = u; u.OnMoveFinished -= OnUnitMoveFinished; u.OnMoveFinished += OnUnitMoveFinished; _ = GetHighlighter(u); }
        public void UnregisterUnit(Unit u) { if (u == null) return; occupancy?.Unregister(u); u.OnMoveFinished -= OnUnitMoveFinished; var vis = GetHighlighter(u); vis?.SetHover(false); vis?.SetSelected(false); if (_hoveredUnit == u) _hoveredUnit = null; RemoveUnitMapping(u); if (SelectedUnit == u) { _selected = null; highlighter.SetSelected(null); SelectedUnit = null; } }
        public void SyncUnit(Unit u) { if (u == null) return; occupancy?.SyncUnit(u); RemoveUnitMapping(u); _units[u.Coords] = u; }
        void RemoveUnitMapping(Unit u) { HexCoords keyToRemove = default; bool found = false; foreach (var kv in _units) { if (kv.Value == u) { keyToRemove = kv.Key; found = true; break; } } if (found) _units.Remove(keyToRemove); }
        public bool IsOccupied(HexCoords c) => HasUnitAt(c);

        // —— Input Callbacks ——

        void OnHoverChanged(HexCoords? h)
        {
            // 1. 技能瞄准模式下，SelectionManager 彻底退出，不干扰高亮
            if (targetingSystem != null && targetingSystem.IsTargeting)
            {
                if (_hoverCache.HasValue)
                {
                    _hoverCache = null;
                    highlighter.SetHover(null);
                    Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto); // 交还光标控制权
                }
                return;
            }

            _hoverCache = h;

            // 2. ⭐ 修复：通知 Highlighter 绘制悬停框 (黄色)
            // 这样在移动模式下，你也能清楚看到鼠标指哪儿
            highlighter.SetHover(h);

            // 3. ⭐ 光标逻辑：根据位置设置光标样式
            UpdateMovementCursor(h);

            // ... (Unit Hover Logic) ...
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

        // ⭐ 新增：光标更新方法
        void UpdateMovementCursor(HexCoords? h)
        {
            // 如果没选中自己人，或者是敌人，或者是空，恢复默认
            if (SelectedUnit == null || !SelectedUnit.IsPlayerControlled || !h.HasValue)
            {
                // 只有当之前修改过光标时才重置，避免闪烁
                // 但为了保险，在非 Target 模式下我们设为 null (System Default)
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
                return;
            }

            HexCoords pos = h.Value;

            // 检查该格子在哪个集合里
            if (_currentFreeSet.Contains(pos))
            {
                // 免费区 -> 绿色/靴子
                Cursor.SetCursor(cursorMoveFree, cursorHotspot, CursorMode.Auto);
            }
            else if (_currentCostSet.Contains(pos))
            {
                // 付费区 -> 黄色/AP
                Cursor.SetCursor(cursorMoveCost, cursorHotspot, CursorMode.Auto);
            }
            else
            {
                // 不在移动范围内 -> 禁止
                Cursor.SetCursor(cursorInvalid, cursorHotspot, CursorMode.Auto);
            }
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
            // 清理缓存
            _currentFreeSet.Clear();
            _currentCostSet.Clear();

            if (targetingSystem != null && targetingSystem.IsTargeting)
            {
                highlighter.ApplyMoveRange(null, null);
                return;
            }

            if (SelectedUnit != null)
            {
                if (SelectedUnit.IsPlayerControlled && SelectedUnit.TryGetComponent<UnitAttributes>(out var attrs))
                {
                    HexCoords center = SelectedUnit.Coords;
                    int stride = attrs.Core.CurrentStride;
                    int ap = attrs.Core.CurrentAP;

                    // 计算免费集合
                    if (stride > 0)
                        foreach (var c in center.Disk(stride)) _currentFreeSet.Add(c);
                    else
                        _currentFreeSet.Add(center);

                    // 计算付费集合
                    int totalRange = stride + ap;
                    if (ap > 0)
                    {
                        foreach (var c in center.Disk(totalRange))
                        {
                            if (!_currentFreeSet.Contains(c)) _currentCostSet.Add(c);
                        }
                    }

                    // 应用高亮
                    highlighter.ApplyMoveRange(_currentFreeSet, _currentCostSet);

                    // 立即刷新一次光标 (防止鼠标不动时光标不更新)
                    UpdateMovementCursor(_hoverCache);
                }
                else
                {
                    highlighter.ApplyMoveRange(null, null);
                }
                return;
            }

            // Debug Logic
            if (debugRangeMode != RangeMode.None && _hoverCache.HasValue)
            {
                // 简单的 Debug 显示，不进缓存集合
                var set = new HashSet<HexCoords>();
                if (debugRangeMode == RangeMode.Disk) foreach (var c in _hoverCache.Value.Disk(debugRadius)) set.Add(c);
                else foreach (var c in _hoverCache.Value.Ring(debugRadius)) set.Add(c);
                highlighter.ApplyMoveRange(set, null);
                return;
            }

            highlighter.ApplyMoveRange(null, null);
        }
    }
}