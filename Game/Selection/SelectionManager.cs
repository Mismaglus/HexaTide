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
        public BattleHexInput input;       // 事件来源（Hover/Click）
        public HexHighlighter highlighter; // 视觉（Hover/Selected/Range）
        public BattleHexGrid grid;         // 网格（用于 Version 检测）
        [SerializeField] BattleStateMachine _battleSM; // assign in scene (or FindFirstObjectByType in Awake)

        [Header("Range")]
        public RangeMode rangeMode = RangeMode.None;
        public RangePivot rangePivot = RangePivot.Hover;
        [Min(0)] public int radius = 2;

        // —— 选择 & Hover —— //
        HexCoords? _selected;
        HexCoords? _hoverCache;

        // —— 单位与占位表 —— //
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

        // NEW: 追踪上一个 Hover 的单位与可视缓存
        Unit _hoveredUnit; // 上一个 hover 到的单位
        readonly Dictionary<Unit, UnitHighlighter> _HighlighterCache = new();

        void Reset()
        {
            if (!input) input = FindFirstObjectByType<BattleHexInput>(FindObjectsInactive.Exclude);
            if (!highlighter) highlighter = FindFirstObjectByType<HexHighlighter>(FindObjectsInactive.Exclude);
            if (!grid) grid = FindFirstObjectByType<BattleHexGrid>(FindObjectsInactive.Exclude);
        }
        void Update()
        {
            var controlSvc = PlayerControlService.Instance;
            if (controlSvc != null && !controlSvc.IsControlEnabled)
                return;

#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
                Deselect();

            var mouse = Mouse.current;
            if (mouse != null && mouse.rightButton.wasPressedThisFrame)
                Deselect();
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
    if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
        Deselect();
#endif
        }


        void Awake()
        {
            if (_battleSM == null)
                _battleSM = UnityEngine.Object.FindFirstObjectByType<BattleStateMachine>();
        }

        void OnEnable()
        {
            if (!input)
                input = FindFirstObjectByType<BattleHexInput>(FindObjectsInactive.Exclude);

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

        // —— 占位相关对外 API —— //
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
        // SelectionManager.cs
        void HandleClickOnEmptyTile(HexCoords c)
        {
            // 1) 仅玩家回合可下达命令
            if (_battleSM != null && _battleSM.CurrentTurn != TurnSide.Player)
            {
                Debug.Log("[Select] Denied: not player's turn.");
                return;
            }

            if (SelectedUnit == null || _selected == null)
            {
                Debug.Log("[Select] Denied: no selected unit.");
                return;
            }

            var unit = SelectedUnit;
            if (!unit.IsPlayerControlled)
            {
                Debug.Log("[Select] Denied: selected unit is not player-controlled.");
                return;
            }

            // 自己格子就不处理
            if (unit.Coords.Equals(c))
            {
                Debug.Log("[Select] Clicked on current tile; ignoring.");
                return;
            }

            // 2) 目标格不可被占用
            if (HasUnitAt(c))
            {
                Debug.Log("[Select] Denied: target tile is occupied.");
                return;
            }

            // 3) 必须是邻格（当前版本只支持一步移动）
            if (unit.Coords.DistanceTo(c) != 1)
            {
                Debug.Log("[Select] Denied: non-adjacent target. (Current design = 1-step only)");
                return;
            }

            // 4) 必须有 UnitMover 且不在移动
            if (!unit.TryGetComponent<UnitMover>(out var mover))
            {
                Debug.LogWarning("[Select] Denied: selected unit has no UnitMover component.");
                return;
            }
            if (mover.IsMoving)
            {
                Debug.Log("[Select] Denied: mover is currently moving.");
                return;
            }

            // 5) 尝试迈一步（会消耗 stride）
            bool ok = mover.TryStepTo(c, onDone: () =>
            {
                // 成功后同步占位与选择标记
                RemoveUnitMapping(unit);
                _units[c] = unit;

                _selected = c;
                highlighter.SetSelected(c);
                if (rangePivot == RangePivot.Selected) RecalcRange();
            });

            if (!ok)
            {
                Debug.Log("[Select] TryStepTo() returned false (likely no stride left or other guard failed).");
                // 这里也可以做个 UI 提示：比如闪红、提示音等
            }
        }


        // NEW: 小工具——拿到单位的可视控制组件（缓存）
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

            // 关掉选中色/描边（有就关）
            GetHighlighter(current)?.SetSelected(false);
            var ol = current.GetComponentInChildren<UnitHighlighter>(true);
            ol?.SetSelected(false);

            _selected = null;
            highlighter.SetSelected(null);
            SelectedUnit = null;

            // 若范围枢轴是 Selected，就清掉范围
            if (rangePivot == RangePivot.Selected) RecalcRange();

            // 如果此时有 hover 在别的单位上，恢复它的 hover 效果
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

            // NEW: 预热一次可视引用（可选）
            _ = GetHighlighter(u);
        }

        public void UnregisterUnit(Unit u)
        {
            if (u == null) return;

            occupancy?.Unregister(u);

            u.OnMoveFinished -= OnUnitMoveFinished;

            // NEW: 清理可视状态（避免残留高亮）
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

            // 可视缓存保留与否皆可；这里不清除以减少 GC
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
                if (kv.Value == u)
                {
                    keyToRemove = kv.Key;
                    found = true;
                    break;
                }
            }

            if (found)
                _units.Remove(keyToRemove);
        }

        public bool IsOccupied(HexCoords c) => HasUnitAt(c);

        // —— 输入回调 —— //

        void OnHoverChanged(HexCoords? h)
        {
            _hoverCache = h;

            // NEW: 计算新的 hover 单位
            Unit newHover = null;
            if (h.HasValue) TryGetUnitAt(h.Value, out newHover);

            // 若没有变化，照常处理范围即可
            if (ReferenceEquals(newHover, _hoveredUnit))
            {
                if (rangePivot == RangePivot.Hover) RecalcRange();
                return;
            }

            // 1) 关掉旧 Hover 的 hover 颜色；若它是选中单位，则恢复选中色
            if (_hoveredUnit != null)
            {
                var vOld = GetHighlighter(_hoveredUnit);
                if (_hoveredUnit == SelectedUnit)
                {
                    vOld?.SetHover(false);
                    vOld?.SetSelected(true);
                }
                else
                {
                    vOld?.SetHover(false);
                }
            }

            // 2) 打开新 Hover 的 hover 颜色；若它是选中单位，先选中再 hover（保证 hover 覆盖选中）
            if (newHover != null)
            {
                var vNew = GetHighlighter(newHover);
                if (newHover == SelectedUnit)
                {
                    vNew?.SetSelected(true);
                    vNew?.SetHover(true);
                }
                else
                {
                    vNew?.SetHover(true);
                }
            }

            _hoveredUnit = newHover;

            if (rangePivot == RangePivot.Hover)
                RecalcRange();
        }
        void OnTileClicked(HexCoords c)
        {
            // —— 点到单位 —— //
            if (TryGetUnitAt(c, out var unit))
            {
                // 点到当前选中的单位 → 直接取消选中（切换型 UX，常见）
                if (SelectedUnit == unit)
                {
                    Deselect();
                    return;
                }

                // 关旧开新（原逻辑）
                if (SelectedUnit != null)
                    GetHighlighter(SelectedUnit)?.SetSelected(false);

                SelectedUnit = unit;
                _selected = c;
                highlighter.SetSelected(c);

                var vNew = GetHighlighter(SelectedUnit);
                if (_hoveredUnit == SelectedUnit) { vNew?.SetSelected(true); vNew?.SetHover(true); }
                else vNew?.SetSelected(true);

                if (rangePivot == RangePivot.Selected) RecalcRange();
                return;
            }

            // —— 点到空地 —— //
            if (SelectedUnit != null && !SelectedUnit.IsMoving)
            {
                HandleClickOnEmptyTile(c);
                // var from = selectedUnit.Coords;
                // bool canIssueMove = from.DistanceTo(c) == 1 && !IsOccupied(c);

                // if (canIssueMove && selectedUnit.TryMoveTo(c))
                // {
                //     // 原“下命令移动”逻辑
                //     RemoveUnitMapping(selectedUnit);
                //     _units[c] = selectedUnit;

                //     _selected = c;
                //     highlighter.SetSelected(c);
                // }
                // else
                // {
                //     // 不是合法移动 → 视作“点击空白” → 取消选中
                //     Deselect();
                // }
            }
            else
            {
                // 本来就没选中，点空地什么都不做
            }
        }


        void OnUnitMoveFinished(Unit u, HexCoords from, HexCoords to)
        {
            // 确保占位表一致（防止被其它逻辑改动）
            RemoveUnitMapping(u);
            _units[to] = u;

            // 若它是当前选中单位，让选择标记站在新格（安全起见再设一次）
            if (SelectedUnit == u)
            {
                _selected = to;
                highlighter.SetSelected(to);
            }

            // 枢轴为 Selected 时，可在移动后重算范围
            if (rangePivot == RangePivot.Selected) RecalcRange();
        }

        // —— Range —— //

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
            if (rangeMode == RangeMode.Disk)
                foreach (var c in center.Value.Disk(radius)) set.Add(c);
            else
                foreach (var c in center.Value.Ring(radius)) set.Add(c);

            highlighter.ApplyRange(set);
        }

        Unit GetUnitAt(HexCoords c)
        {
            TryGetUnitAt(c, out var u);
            return u;
        }
    }
}
