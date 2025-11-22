using System.Collections.Generic;
using UnityEngine;
using Core.Hex;

namespace Game.Common
{
    [DisallowMultipleComponent]
    public class HexHighlighter : MonoBehaviour
    {
        [Header("Refs")]
        public Game.Battle.BattleHexGrid grid;

        [Header("Standard Colors")]
        public Color hoverColor = new Color(0.95f, 0.95f, 0.25f, 0.5f);
        public Color selectedColor = new Color(0.25f, 0.8f, 1.0f, 0.5f);

        [Header("Movement Colors (Adjusted for Subtlety)")]
        // ? 调整：Alpha 0.3 -> 0.15 (更加柔和)
        public Color moveFreeColor = new Color(0.0f, 1.0f, 0.4f, 0.15f);   // ? 免费区域 (淡绿)
        public Color moveCostColor = new Color(1.0f, 0.8f, 0.0f, 0.15f);   // ? AP付费区域 (淡黄)

        [Header("Combat Colors")]
        public Color impactColor = new Color(1.0f, 0.4f, 0.0f, 0.7f);
        public Color invalidColor = new Color(1.0f, 0.0f, 0.0f, 0.5f);

        // 注意：这个 RangeColor 仅用于 HexHighlighter 内部的兼容逻辑
        // 你的技能范围现在是由 RangeOutlineDrawer 画的，所以这个颜色基本用不到
        public Color rangeColor = new Color(0.0f, 1.0f, 0.4f, 0.15f);

        [Header("Settings")]
        public string[] colorPropertyNames = new[] { "_BaseColor", "_Color", "_Tint" };
        [Range(0.1f, 4f)] public float hoverIntensity = 1.0f;
        [Range(0.1f, 4f)] public float selectedIntensity = 1.0f;
        [Range(0.1f, 4f)] public float rangeIntensity = 1.0f;

        // Cache
        struct Slot { public MeshRenderer mr; public int colorPropId; }
        readonly Dictionary<(int q, int r), Slot> _slots = new();

        // States
        HexCoords? _hover;
        HexCoords? _selected;
        readonly HashSet<HexCoords> _range = new();
        readonly HashSet<HexCoords> _impact = new();
        readonly HashSet<HexCoords> _moveFree = new();
        readonly HashSet<HexCoords> _moveCost = new();

        MaterialPropertyBlock _mpb;
        uint _lastGridVersion;

        void Awake() { if (_mpb == null) _mpb = new MaterialPropertyBlock(); }

        void LateUpdate()
        {
            if (grid && grid.Version != _lastGridVersion)
            {
                _lastGridVersion = grid.Version;
                RebuildCache();
                ReapplyAll();
            }
        }

        public void RebuildCache()
        {
            _slots.Clear();
            if (!grid) return;
            var tags = grid.GetComponentsInChildren<TileTag>(true);
            foreach (var t in tags)
            {
                var mr = t.GetComponent<MeshRenderer>();
                if (!mr) continue;
                int pid = -1;
                var mat = mr.sharedMaterial;
                if (mat)
                {
                    foreach (var n in colorPropertyNames)
                    {
                        int id = Shader.PropertyToID(n);
                        if (mat.HasProperty(id)) { pid = id; break; }
                    }
                }
                _slots[(t.Coords.q, t.Coords.r)] = new Slot { mr = mr, colorPropId = pid };
            }
        }

        // === API ===
        public void SetHover(HexCoords? c) { var o = _hover; _hover = c; revertPaintHelper(o); Repaint(_hover); }
        public void SetSelected(HexCoords? c) { var o = _selected; _selected = c; revertPaintHelper(o); Repaint(_selected); }

        public void SetImpact(IEnumerable<HexCoords> c)
        {
            var old = new HashSet<HexCoords>(_impact);
            _impact.Clear();
            if (c != null) foreach (var x in c) _impact.Add(x);
            foreach (var x in old) if (!_impact.Contains(x)) Repaint(x.q, x.r);
            foreach (var x in _impact) if (!old.Contains(x)) Repaint(x.q, x.r);
        }

        public void ApplyMoveRange(IEnumerable<HexCoords> free, IEnumerable<HexCoords> cost)
        {
            var old = new HashSet<HexCoords>(_moveFree);
            old.UnionWith(_moveCost);

            _moveFree.Clear();
            _moveCost.Clear();

            if (free != null) foreach (var x in free) _moveFree.Add(x);
            if (cost != null) foreach (var x in cost) _moveCost.Add(x);

            var current = new HashSet<HexCoords>(_moveFree);
            current.UnionWith(_moveCost);

            foreach (var x in old) if (!current.Contains(x)) Repaint(x.q, x.r);
            foreach (var x in current) if (!old.Contains(x)) Repaint(x.q, x.r);
        }

        public void ApplyRange(IEnumerable<HexCoords> c)
        {
            var old = new HashSet<HexCoords>(_range);
            _range.Clear();
            if (c != null) foreach (var x in c) _range.Add(x);
            foreach (var x in old) if (!_range.Contains(x)) revertPaintHelper(x);
            foreach (var x in _range) if (!old.Contains(x)) Repaint(x.q, x.r);
        }

        public void ClearAll()
        {
            var t = new HashSet<HexCoords>();
            if (_hover.HasValue) t.Add(_hover.Value);
            if (_selected.HasValue) t.Add(_selected.Value);
            foreach (var x in _impact) t.Add(x);
            foreach (var x in _moveFree) t.Add(x);
            foreach (var x in _moveCost) t.Add(x);
            foreach (var x in _range) t.Add(x);

            _hover = null; _selected = null;
            _impact.Clear(); _moveFree.Clear(); _moveCost.Clear(); _range.Clear();

            foreach (var x in t) ClearPaint(x);
        }

        void revertPaintHelper(HexCoords? c) { if (c.HasValue) Repaint(c.Value.q, c.Value.r); }
        void Repaint(HexCoords? c) { if (c.HasValue) Repaint(c.Value.q, c.Value.r); }

        void ReapplyAll()
        {
            if (_hover.HasValue) Repaint(_hover.Value.q, _hover.Value.r);
            if (_selected.HasValue) Repaint(_selected.Value.q, _selected.Value.r);
            foreach (var v in _moveFree) Repaint(v.q, v.r);
            foreach (var v in _moveCost) Repaint(v.q, v.r);
            foreach (var v in _impact) Repaint(v.q, v.r);
            foreach (var v in _range) Repaint(v.q, v.r);
        }

        void Repaint(int q, int r)
        {
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            if (!_slots.TryGetValue((q, r), out var slot) || !slot.mr) return;

            var coord = new HexCoords(q, r);
            bool isHover = _hover.HasValue && _hover.Value.Equals(coord);
            bool isSelected = _selected.HasValue && _selected.Value.Equals(coord);
            bool inImpact = _impact.Contains(coord);
            bool inFree = _moveFree.Contains(coord);
            bool inCost = _moveCost.Contains(coord);
            bool inRange = _range.Contains(coord);

            Color finalColor = Color.clear;
            bool shouldPaint = false;

            // 优先级：Impact > Selected > Hover > MoveCost > MoveFree > Range
            if (inImpact)
            {
                finalColor = impactColor;
                shouldPaint = true;
            }
            else if (isSelected)
            {
                finalColor = selectedColor * selectedIntensity;
                shouldPaint = true;
            }
            else if (isHover)
            {
                finalColor = hoverColor * hoverIntensity;
                shouldPaint = true;
            }
            else if (inCost)
            {
                finalColor = moveCostColor * rangeIntensity;
                shouldPaint = true;
            }
            else if (inFree)
            {
                finalColor = moveFreeColor * rangeIntensity;
                shouldPaint = true;
            }
            else if (inRange)
            {
                finalColor = rangeColor * rangeIntensity;
                shouldPaint = true;
            }

            if (shouldPaint)
            {
                _mpb.Clear();
                if (slot.colorPropId != -1) _mpb.SetColor(slot.colorPropId, finalColor);
                else
                {
                    _mpb.SetColor(Shader.PropertyToID("_BaseColor"), finalColor);
                    _mpb.SetColor(Shader.PropertyToID("_Color"), finalColor);
                    _mpb.SetColor(Shader.PropertyToID("_Tint"), finalColor);
                }
                slot.mr.SetPropertyBlock(_mpb);
            }
            else
            {
                slot.mr.SetPropertyBlock(null);
            }
        }

        public void ClearPaint(HexCoords c)
        {
            if (_slots.TryGetValue((c.q, c.r), out var slot) && slot.mr)
                slot.mr.SetPropertyBlock(null);
        }
    }
}