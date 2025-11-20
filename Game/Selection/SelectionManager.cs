using System.Collections.Generic;
using System;
using UnityEngine;
using Core.Hex;
using Game.Common;
using Game.Units;
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

        [Header("Range")]
        public RangeMode rangeMode = RangeMode.None;
        public RangePivot rangePivot = RangePivot.Hover;
        [Min(0)] public int radius = 2;

        HexCoords? _selected;
        HexCoords? _hoverCache;

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
            }
        }

        [Obsolete("Use SelectedUnit instead.")]
        public Unit selectedUnit => SelectedUnit;

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
            if (_battleSM == null)
                _battleSM = UnityEngine.Object.FindFirstObjectByType<BattleStateMachine>();
        }

        void Start()
        {
            if (targetingSystem == null)
                targetingSystem = UnityEngine.Object.FindFirstObjectByType<AbilityTargetingSystem>();
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

        void HandleClickOnEmptyTile(HexCoords c)
        {
            if (_battleSM != null && _battleSM.CurrentTurn != TurnSide.Player) return;
            if (SelectedUnit == null || _selected == null) return;

            var unit = SelectedUnit;
            if (!unit.IsPlayerControlled) return;
            if (unit.Coords.Equals(c)) return;

            if (HasUnitAt(c)) return;
            if (unit.Coords.DistanceTo(c) != 1) return;

            if (!unit.TryGetComponent<UnitMover>(out var mover)) return;
            if (mover.IsMoving) return;

            bool ok = mover.TryStepTo(c, onDone: () =>
            {
                RemoveUnitMapping(unit);
                _units[c] = unit;
                _selected = c;
                highlighter.SetSelected(c);
                if (rangePivot == RangePivot.Selected) RecalcRange();
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
            SelectedUnit = null;
            if (rangePivot == RangePivot.Selected) RecalcRange();
            if (_hoveredUnit != null && _hoveredUnit != SelectedUnit)
                GetHighlighter(_hoveredUnit)?.SetHover(true);
        }

        public void RegisterUnit(Unit u)
        {
            if (u == null) return;
            occupancy?.Register(u);
            RemoveUnitMapping(u);
            _units[u.Coords] = u;
            u.OnMoveFinished -= OnUnitMoveFinished;
            u.OnMoveFinished += OnUnitMoveFinished;
            _ = GetHighlighter(u);
        }

        public void UnregisterUnit(Unit u)
        {
            if (u == null) return;
            occupancy?.Unregister(u);
            u.OnMoveFinished -= OnUnitMoveFinished;
            var vis = GetHighlighter(u);
            vis?.SetHover(false);
            vis?.SetSelected(false);
            if (_hoveredUnit == u) _hoveredUnit = null;
            RemoveUnitMapping(u);
            if (SelectedUnit == u)
            {
                _selected = null;
                highlighter.SetSelected(null);
                SelectedUnit = null;
            }
        }

        public void SyncUnit(Unit u)
        {
            if (u == null) return;
            occupancy?.SyncUnit(u);
            RemoveUnitMapping(u);
            _units[u.Coords] = u;
        }

        void RemoveUnitMapping(Unit u)
        {
            HexCoords keyToRemove = default;
            bool found = false;
            foreach (var kv in _units)
            {
                if (kv.Value == u) { keyToRemove = kv.Key; found = true; break; }
            }
            if (found) _units.Remove(keyToRemove);
        }

        public bool IsOccupied(HexCoords c) => HasUnitAt(c);

        // —— 输入回调 (核心修改) —— //

        void OnHoverChanged(HexCoords? h)
        {
            if (targetingSystem == null) targetingSystem = UnityEngine.Object.FindFirstObjectByType<AbilityTargetingSystem>();

            // ⭐ 关键修复：如果处于瞄准模式，强制 SelectionManager 停止高亮
            if (targetingSystem != null && targetingSystem.IsTargeting)
            {
                // 必须清理掉 SelectionManager 可能遗留的黄色高亮
                // 否则它会一直卡在最后一帧的状态
                if (_hoverCache.HasValue)
                {
                    highlighter.SetHover(null);
                    _hoverCache = null;
                }
                return; // 直接退出，不执行下面的逻辑
            }

            // === 以下逻辑保持不变 ===
            _hoverCache = h;
            highlighter.SetHover(h);

            Unit newHover = null;
            if (h.HasValue) TryGetUnitAt(h.Value, out newHover);

            if (ReferenceEquals(newHover, _hoveredUnit))
            {
                if (rangePivot == RangePivot.Hover) RecalcRange();
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
            if (rangePivot == RangePivot.Hover) RecalcRange();
        }

        void OnTileClicked(HexCoords c)
        {
            // ⭐ 关键修复：如果处于瞄准模式，禁止 SelectionManager 处理点击（移动/选人）
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

                if (rangePivot == RangePivot.Selected) RecalcRange();
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
            }
            if (rangePivot == RangePivot.Selected) RecalcRange();
        }

        public void SetRangeMode(RangeMode mode) { rangeMode = mode; RecalcRange(); }
        public void SetRadius(int r) { radius = Mathf.Max(0, r); RecalcRange(); }

        void RecalcRange()
        {
            if (rangeMode == RangeMode.None || radius <= 0)
            {
                highlighter.ApplyRange(null);
                return;
            }

            HexCoords? center = (rangePivot == RangePivot.Hover) ? _hoverCache : _selected;
            if (!center.HasValue) { highlighter.ApplyRange(null); return; }

            var set = new HashSet<HexCoords>();
            if (rangeMode == RangeMode.Disk) foreach (var c in center.Value.Disk(radius)) set.Add(c);
            else foreach (var c in center.Value.Ring(radius)) set.Add(c);

            highlighter.ApplyRange(set);
        }
    }
}