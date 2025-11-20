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

        [Header("Colors")]
        public Color hoverColor = new Color(0.95f, 0.95f, 0.25f, 0.5f);    // 黄 (移动模式悬停)
        public Color selectedColor = new Color(0.25f, 0.8f, 1.0f, 0.5f);   // 蓝 (选中单位)
        public Color rangeColor = new Color(0.0f, 1.0f, 0.4f, 0.3f);     // ? 绿 (移动范围底色)

        [Header("Intensity")]
        [Range(0.1f, 4f)] public float hoverIntensity = 1.0f;
        [Range(0.1f, 4f)] public float selectedIntensity = 1.0f;
        [Range(0.1f, 4f)] public float rangeIntensity = 1.0f;

        [Header("Material Compatibility")]
        public string[] colorPropertyNames = new[] { "_BaseColor", "_Color", "_Tint" };

        struct Slot { public MeshRenderer mr; public int colorPropId; }
        readonly Dictionary<(int q, int r), Slot> _slots = new();

        // States
        HexCoords? _hover;
        HexCoords? _selected;
        readonly HashSet<HexCoords> _range = new(); // 用于存储移动范围

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

        // === API Methods (供 SelectionManager 调用) ===

        public void SetHover(HexCoords? c)
        {
            var o = _hover; _hover = c; revertPaintHelper(o); Repaint(_hover);
        }

        public void SetSelected(HexCoords? c)
        {
            var o = _selected; _selected = c; revertPaintHelper(o); Repaint(_selected);
        }

        // ? 恢复了这个方法，SelectionManager 不会再报错了
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
            foreach (var x in _range) t.Add(x);

            _hover = null; _selected = null; _range.Clear();
            foreach (var x in t) ClearPaint(x);
        }

        // === Painting Logic ===

        void revertPaintHelper(HexCoords? c) { if (c.HasValue) Repaint(c.Value.q, c.Value.r); }
        void Repaint(HexCoords? c) { if (c.HasValue) Repaint(c.Value.q, c.Value.r); }

        void ReapplyAll()
        {
            if (_hover.HasValue) Repaint(_hover.Value.q, _hover.Value.r);
            if (_selected.HasValue) Repaint(_selected.Value.q, _selected.Value.r);
            foreach (var v in _range) Repaint(v.q, v.r);
        }

        void Repaint(int q, int r)
        {
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            if (!_slots.TryGetValue((q, r), out var slot) || !slot.mr) return;

            var coord = new HexCoords(q, r);
            bool isHover = _hover.HasValue && _hover.Value.Equals(coord);
            bool isSelected = _selected.HasValue && _selected.Value.Equals(coord);
            bool inRange = _range.Contains(coord);

            Color finalColor = Color.clear;
            bool shouldPaint = false;

            // 简单优先级：Hover > Selected > Range
            // 注意：这套逻辑现在只服务于“移动模式”
            // “技能模式”的高亮由 BattleCursor 和 RangeOutlineDrawer 负责，不走这里

            if (isHover)
            {
                finalColor = hoverColor * hoverIntensity;
                shouldPaint = true;
            }
            else if (isSelected)
            {
                finalColor = selectedColor * selectedIntensity;
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