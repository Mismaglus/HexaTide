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
        public Color hoverColor = new Color(0.95f, 0.95f, 0.25f, 1f);    // 黄 (普通模式用)
        public Color selectedColor = new Color(0.25f, 0.8f, 1.0f, 1f);   // 蓝 (选中单位)
        public Color rangeColor = new Color(0.0f, 1.0f, 0.4f, 0.3f);     // ? 绿 (技能范围底色)

        [Header("Intensity")]
        [Range(0.1f, 4f)] public float hoverIntensity = 1.0f;
        [Range(0.1f, 4f)] public float selectedIntensity = 1.0f;
        [Range(0.1f, 4f)] public float rangeIntensity = 1.0f;

        [Header("Material Compatibility")]
        public string[] colorPropertyNames = new[] { "_BaseColor", "_Color", "_Tint" };

        struct Slot { public MeshRenderer mr; public int colorPropId; }
        readonly Dictionary<(int q, int r), Slot> _slots = new();

        HexCoords? _hover;
        HexCoords? _selected;
        readonly HashSet<HexCoords> _range = new();

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

        // API
        public void SetHover(HexCoords? c) { var o = _hover; _hover = c; revertPaintHelper(o); Repaint(_hover); }
        public void SetSelected(HexCoords? c) { var o = _selected; _selected = c; revertPaintHelper(o); Repaint(_selected); }
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

            bool isHover = _hover.HasValue && _hover.Value.q == q && _hover.Value.r == r;
            bool isSelected = _selected.HasValue && _selected.Value.q == q && _selected.Value.r == r;
            bool inRange = _range.Contains(new HexCoords(q, r));

            // 判断是否在瞄准模式 (Range不为空)
            bool isTargeting = _range.Count > 0;

            Color finalColor = Color.clear;
            bool shouldPaint = false;

            if (isTargeting)
            {
                // === 瞄准模式：只显示底色 ===
                // 鼠标悬停不显示黄色，而是完全交给线框光标 (BattleCursor) 去处理
                // 这样底下的绿格子就不会因为鼠标移上去而变色
                if (inRange)
                {
                    finalColor = rangeColor * rangeIntensity;
                    shouldPaint = true;
                }
            }
            else
            {
                // === 普通模式：显示黄色悬停 ===
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

        public void ClearPaint(HexCoords c) { /* ... */ if (_slots.TryGetValue((c.q, c.r), out var slot) && slot.mr) slot.mr.SetPropertyBlock(null); }
    }
}