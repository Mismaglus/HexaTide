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

        [Header("Debug / Editor Settings")]
        [Tooltip("仅在未选中单位时生效的调试模式")]
        public RangeMode debugRangeMode = RangeMode.None;
        public int debugRadius = 2;

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
            if (_battleSM == null)
                _battleSM = UnityEngine.Object.FindFirstObjectByType<BattleStateMachine>();

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

            if (unit.Coords.DistanceTo(c) > 1)
            {
                // 这里简化处理，你可以根据需要加寻路
            }

            if (!unit.TryGetComponent<UnitMover>(out var mover)) return;
            if (mover.IsMoving) return;

            bool ok = mover.TryStepTo(c, onDone: () =>
            {
                RemoveUnitMapping(unit);
                _units[c] = unit;
                _selected = c;
                highlighter.SetSelected(c);
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
            SelectedUnit = null;
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

        void OnHoverChanged(HexCoords? h)
        {
            if (targetingSystem != null && targetingSystem.IsTargeting)
            {
                if (_hoverCache.HasValue)
                {
                    _hoverCache = null;
                    highlighter.SetHover(null);
                }
                return;
            }

            _hoverCache = h;

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

        // ⭐⭐⭐ 核心逻辑：范围计算 ⭐⭐⭐
        void RecalcRange()
        {
            // 1. 如果在瞄准技能，清空移动显示
            if (targetingSystem != null && targetingSystem.IsTargeting)
            {
                highlighter.ApplyMoveRange(null, null);
                return;
            }

            // 2. 如果选中了单位
            if (SelectedUnit != null)
            {
                // ⭐ 关键修改：只有玩家控制的单位才显示
                // 敌人 (IsPlayerControlled = false) 会直接走 else 分支，清空高亮
                if (SelectedUnit.IsPlayerControlled && SelectedUnit.TryGetComponent<UnitAttributes>(out var attrs))
                {
                    HexCoords center = SelectedUnit.Coords;
                    int stride = attrs.Core.CurrentStride;
                    int ap = attrs.Core.CurrentAP;

                    // 免费区
                    var freeSet = new HashSet<HexCoords>();
                    if (stride > 0)
                    {
                        foreach (var c in center.Disk(stride)) freeSet.Add(c);
                    }
                    else
                    {
                        freeSet.Add(center);
                    }

                    // 付费区
                    var costSet = new HashSet<HexCoords>();
                    int totalRange = stride + ap;
                    if (ap > 0)
                    {
                        foreach (var c in center.Disk(totalRange))
                        {
                            if (!freeSet.Contains(c)) costSet.Add(c);
                        }
                    }

                    highlighter.ApplyMoveRange(freeSet, costSet);
                }
                else
                {
                    // 选中了敌人，清空所有移动高亮
                    highlighter.ApplyMoveRange(null, null);
                }
                return;
            }

            // 3. 未选中单位 (Debug 模式)
            if (debugRangeMode != RangeMode.None && _hoverCache.HasValue)
            {
                var set = new HashSet<HexCoords>();
                if (debugRangeMode == RangeMode.Disk)
                    foreach (var c in _hoverCache.Value.Disk(debugRadius)) set.Add(c);
                else
                    foreach (var c in _hoverCache.Value.Ring(debugRadius)) set.Add(c);

                highlighter.ApplyMoveRange(set, null);
                return;
            }

            // 默认清空
            highlighter.ApplyMoveRange(null, null);
        }
    }
}