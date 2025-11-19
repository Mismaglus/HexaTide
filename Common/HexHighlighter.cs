using System.Collections.Generic;
using UnityEngine;
using Core.Hex;

namespace Game.Common
{
    /// <summary>
    /// 三类高亮（Hover / Selected / Range），用 MPB 染色；支持多材质（自动探测颜色属性）。
    /// 优先级：Hover > Selected > Range > None
    /// </summary>
    [DisallowMultipleComponent]
    public class HexHighlighter : MonoBehaviour
    {
        [Header("Refs")]
        public Game.Battle.BattleHexGrid grid;

        [Header("Colors")]
        public Color hoverColor = new Color(0.95f, 0.95f, 0.25f, 1f);
        public Color selectedColor = new Color(0.25f, 0.8f, 1.0f, 1f);
        public Color rangeColor = new Color(0.35f, 0.9f, 0.45f, 1f);
        [Range(0.1f, 4f)] public float hoverIntensity = 1.0f;
        [Range(0.1f, 4f)] public float selectedIntensity = 1.0f;
        [Range(0.1f, 4f)] public float rangeIntensity = 1.0f;

        [Header("Material Compatibility")]
        [Tooltip("按顺序尝试这些属性名；命中一个就用它进行染色")]
        public string[] colorPropertyNames = new[] { "_BaseColor", "_Color", "_Tint" };
        [Tooltip("若某材质没有可用颜色属性，是否打印一次警告（仅一次）")]
        public bool warnOnceIfNoColorProperty = true;

        // 每格缓存：Renderer + 命中的颜色属性ID（-1=未命中）
        struct Slot { public MeshRenderer mr; public int colorPropId; }
        readonly Dictionary<(int q, int r), Slot> _slots = new();

        // 三类状态
        HexCoords? _hover;
        HexCoords? _selected;
        readonly HashSet<HexCoords> _range = new();

        MaterialPropertyBlock _mpb; // 延迟创建
        uint _lastGridVersion;
        bool _warned;

        void Awake()
        {
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
        }

        void LateUpdate()
        {
            if (!grid) return;

            // 网格重建后：重建缓存并重刷当前状态
            if (_lastGridVersion != grid.Version)
            {
                _lastGridVersion = grid.Version;
                RebuildCache();
                ReapplyAll();
            }
        }

        // —— 外部调用 API ——

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
                    for (int i = 0; i < colorPropertyNames.Length; i++)
                    {
                        string name = colorPropertyNames[i];
                        if (string.IsNullOrEmpty(name)) continue;
                        int id = Shader.PropertyToID(name);
                        if (mat.HasProperty(id)) { pid = id; break; }
                    }
                }

                if (pid == -1 && warnOnceIfNoColorProperty && !_warned)
                {
                    _warned = true;
                    Debug.LogWarning(
                        $"[HexHighlighter] 材质 '{(mat ? mat.name : "null")}' 未找到可用的颜色属性。" +
                        $" 将回退尝试 _BaseColor/_Color/_Tint。可在 colorPropertyNames 里添加自定义属性名。"
                    );
                }

                _slots[(t.Coords.q, t.Coords.r)] = new Slot { mr = mr, colorPropId = pid };
            }
        }

        void revertPaintHelper(HexCoords? coords)
        {
            if (coords.HasValue)
            {
                if (HasAnyState(coords)) Repaint(coords);   // 退回到 Selected/Range 等
                else ClearPaint(coords);       // 没有任何状态了 → 直接清
            }
        }

        public void SetHover(HexCoords? coords)
        {
            var old = _hover;
            _hover = coords; // 先提交新状态

            revertPaintHelper(old);

            Repaint(_hover); // 新 hover 刷上去
        }


        // SetSelected
        public void SetSelected(HexCoords? coords)
        {
            var old = _selected;
            _selected = coords;

            revertPaintHelper(old);

            Repaint(_selected);
        }


        /// <summary>应用一组范围格；传 null 或空集合会清空范围。</summary>
        public void ApplyRange(IEnumerable<HexCoords> coords)
        {
            var old = new HashSet<HexCoords>(_range);

            // 先提交新集合
            _range.Clear();
            if (coords != null) foreach (var c in coords) _range.Add(c);

            // 旧里有、新里没有 → 可能需要清除或退回其它状态
            foreach (var c in old)
            {
                if (!_range.Contains(c))
                {
                    revertPaintHelper(c);
                }
            }

            // 新增进入范围的格 → 画出来
            foreach (var c in _range)
            {
                if (!old.Contains(c)) Repaint(c.q, c.r);
            }
        }


        public void ClearAll()
        {
            var toClear = new HashSet<HexCoords>();
            if (_hover.HasValue) toClear.Add(_hover.Value);
            if (_selected.HasValue) toClear.Add(_selected.Value);
            foreach (var v in _range) toClear.Add(v);

            _hover = null; _selected = null; _range.Clear(); // 先清状态

            foreach (var c in toClear) ClearPaint(c);        // 再显式清渲染
        }

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

            // 计算该格当前应显示的颜色（Hover > Selected > Range）
            bool isHover = _hover.HasValue && _hover.Value.q == q && _hover.Value.r == r;
            bool isSelected = _selected.HasValue && _selected.Value.q == q && _selected.Value.r == r;
            bool inRange = _range.Contains(new HexCoords(q, r));

            Color? outColor = null;
            if (isHover) outColor = hoverColor * hoverIntensity;
            else if (isSelected) outColor = selectedColor * selectedIntensity;
            else if (inRange) outColor = rangeColor * rangeIntensity;

            if (outColor.HasValue)
            {
                _mpb.Clear();
                if (slot.colorPropId != -1)
                {
                    _mpb.SetColor(slot.colorPropId, outColor.Value);
                }
                else
                {
                    // 回退：常见三种都设一次
                    _mpb.SetColor(Shader.PropertyToID("_BaseColor"), outColor.Value);
                    _mpb.SetColor(Shader.PropertyToID("_Color"), outColor.Value);
                    _mpb.SetColor(Shader.PropertyToID("_Tint"), outColor.Value);
                }
                slot.mr.SetPropertyBlock(_mpb);
            }
            else
            {
                // 无任何状态：恢复默认
                slot.mr.SetPropertyBlock(null);
            }
        }
        public void ClearPaint(HexCoords? c)
        {
            if (_slots.TryGetValue((c.Value.q, c.Value.r), out var slot) && slot.mr)
                slot.mr.SetPropertyBlock(null);
        }
        bool HasAnyState(HexCoords? c)
        {
            int q = c.Value.q;
            int r = c.Value.r;
            if (_hover is HexCoords h && h.q == q && h.r == r) return true;
            if (_selected is HexCoords s && s.q == q && s.r == r) return true;
            return _range.Contains(new HexCoords(q, r));
        }
    }
}
