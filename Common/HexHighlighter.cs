using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Core.Hex;
using Game.Grid; // 引用 HexCell

namespace Game.Common
{
    /// <summary>
    /// 增强版高亮器：支持瞄准模式、波纹特效以及迷雾遮挡。
    /// </summary>
    [DisallowMultipleComponent]
    public class HexHighlighter : MonoBehaviour
    {
        [Header("Refs")]
        public Game.Battle.BattleHexGrid grid;

        [Header("Standard Colors")]
        public Color hoverColor = new Color(0.95f, 0.95f, 0.25f, 0.5f);
        public Color selectedColor = new Color(0.25f, 0.8f, 1.0f, 0.5f);

        [Header("Combat Colors")]
        public Color rangeColor = new Color(0.0f, 1.0f, 0.4f, 0.3f);
        public Color playerImpactColor = new Color(0f, 1f, 1f, 0.8f);
        public Color enemyDangerColor = new Color(1.0f, 0.2f, 0.2f, 0.6f);
        public Color invalidColor = new Color(1.0f, 0.1f, 0.1f, 0.5f);

        [Header("Movement Colors")]
        public Color moveFreeColor = new Color(0.0f, 1.0f, 0.4f, 0.15f);
        public Color moveCostColor = new Color(1.0f, 0.8f, 0.0f, 0.15f);

        [Header("FX Colors")]
        public Color rippleColor = new Color(1f, 0.0f, 0.0f, 0.8f); // 波纹颜色 (红)

        [Header("Intensity")]
        [Range(0.1f, 4f)] public float hoverIntensity = 1.0f;
        [Range(0.1f, 4f)] public float selectedIntensity = 1.0f;
        [Range(0.1f, 4f)] public float rangeIntensity = 1.0f;
        [Range(0.1f, 4f)] public float impactIntensity = 1.0f;
        [Range(0.1f, 4f)] public float dangerIntensity = 1.0f;

        [Header("Material Compatibility")]
        public string[] colorPropertyNames = new[] { "_BaseColor", "_Color", "_Tint" };
        public bool warnOnceIfNoColorProperty = true;

        // Cache
        struct Slot
        {
            public MeshRenderer mr;
            public int colorPropId;
            public HexCell cell; // 缓存 Cell 引用以便快速检查 FogStatus
        }
        readonly Dictionary<(int q, int r), Slot> _slots = new();

        // States
        HexCoords? _hover;
        HexCoords? _selected;

        readonly HashSet<HexCoords> _range = new();
        readonly HashSet<HexCoords> _impact = new();
        readonly HashSet<HexCoords> _enemyDanger = new();
        readonly HashSet<HexCoords> _moveFree = new();
        readonly HashSet<HexCoords> _moveCost = new();

        // 波纹特效状态: Key=Coord, Value=剩余时间
        private Dictionary<HexCoords, float> _activeRipples = new Dictionary<HexCoords, float>();

        MaterialPropertyBlock _mpb;
        uint _lastGridVersion;
        bool _warned;

        void Awake() { if (_mpb == null) _mpb = new MaterialPropertyBlock(); }

        void Update()
        {
            // 更新波纹动画
            if (_activeRipples.Count > 0)
            {
                var keys = new List<HexCoords>(_activeRipples.Keys);
                foreach (var k in keys)
                {
                    _activeRipples[k] -= Time.deltaTime;
                    if (_activeRipples[k] <= 0)
                    {
                        _activeRipples.Remove(k);
                        Repaint(k.q, k.r); // 结束时重绘
                    }
                    else
                    {
                        Repaint(k.q, k.r); // 动画中每帧重绘 (如果需要淡出效果)
                    }
                }
            }
        }

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
                    Debug.LogWarning($"[HexHighlighter] Material '{mat.name}' has no matching color property.");
                }

                var cell = t.GetComponent<HexCell>(); // 获取 HexCell
                _slots[(t.Coords.q, t.Coords.r)] = new Slot { mr = mr, colorPropId = pid, cell = cell };
            }
        }

        // === API Methods ===

        public void TriggerRipple(HexCoords c, float duration = 0.5f)
        {
            if (!_slots.ContainsKey((c.q, c.r))) return;

            _activeRipples[c] = duration;
            Repaint(c.q, c.r);
        }

        public void SetHover(HexCoords? c)
        {
            var o = _hover;
            _hover = c;
            revertPaintHelper(o);
            Repaint(_hover);
        }

        public void SetSelected(HexCoords? c)
        {
            var o = _selected;
            _selected = c;
            revertPaintHelper(o);
            Repaint(_selected);
        }

        public void ApplyRange(IEnumerable<HexCoords> c)
        {
            var old = new HashSet<HexCoords>(_range);
            _range.Clear();
            if (c != null) foreach (var x in c) _range.Add(x);

            foreach (var x in old) if (!_range.Contains(x)) revertPaintHelper(x);
            foreach (var x in _range) if (!old.Contains(x)) Repaint(x.q, x.r);
        }

        public void ApplyMoveRange(IEnumerable<HexCoords> free, IEnumerable<HexCoords> cost)
        {
            var dirtyTiles = new HashSet<HexCoords>(_moveFree);
            dirtyTiles.UnionWith(_moveCost);

            _moveFree.Clear();
            _moveCost.Clear();
            if (free != null) foreach (var x in free) _moveFree.Add(x);
            if (cost != null) foreach (var x in cost) _moveCost.Add(x);

            dirtyTiles.UnionWith(_moveFree);
            dirtyTiles.UnionWith(_moveCost);

            foreach (var t in dirtyTiles) Repaint(t.q, t.r);
        }

        public void SetImpact(IEnumerable<HexCoords> c)
        {
            var old = new HashSet<HexCoords>(_impact);
            _impact.Clear();
            if (c != null) foreach (var x in c) _impact.Add(x);

            foreach (var x in old) if (!_impact.Contains(x)) Repaint(x.q, x.r);
            foreach (var x in _impact) if (!old.Contains(x)) Repaint(x.q, x.r);
        }

        public void SetEnemyDanger(IEnumerable<HexCoords> c)
        {
            var old = new HashSet<HexCoords>(_enemyDanger);
            _enemyDanger.Clear();
            if (c != null) foreach (var x in c) _enemyDanger.Add(x);

            foreach (var x in old) if (!_enemyDanger.Contains(x)) Repaint(x.q, x.r);
            foreach (var x in _enemyDanger) if (!old.Contains(x)) Repaint(x.q, x.r);
        }

        public void ClearAll()
        {
            var t = new HashSet<HexCoords>();
            if (_hover.HasValue) t.Add(_hover.Value);
            if (_selected.HasValue) t.Add(_selected.Value);
            foreach (var x in _range) t.Add(x);
            foreach (var x in _impact) t.Add(x);
            foreach (var x in _enemyDanger) t.Add(x);
            foreach (var x in _moveFree) t.Add(x);
            foreach (var x in _moveCost) t.Add(x);

            _hover = null; _selected = null;
            _range.Clear(); _impact.Clear(); _enemyDanger.Clear(); _moveFree.Clear(); _moveCost.Clear();
            _activeRipples.Clear();

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
            foreach (var v in _impact) Repaint(v.q, v.r);
            foreach (var v in _enemyDanger) Repaint(v.q, v.r);
            foreach (var v in _moveFree) Repaint(v.q, v.r);
            foreach (var v in _moveCost) Repaint(v.q, v.r);
            foreach (var v in _activeRipples.Keys) Repaint(v.q, v.r);
        }

        void Repaint(int q, int r)
        {
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            if (!_slots.TryGetValue((q, r), out var slot) || !slot.mr) return;

            // ?? 迷雾检查：如果格子是 Unknown (黑雾)，则完全不显示高亮 ??
            // 如果是 Ghost (迷雾中)，可以显示高亮，但可能需要变暗？这里暂定允许显示
            if (slot.cell != null && slot.cell.fogStatus == FogStatus.Unknown)
            {
                slot.mr.SetPropertyBlock(null); // 清除任何高亮
                return;
            }

            var coord = new HexCoords(q, r);
            bool isRipple = _activeRipples.ContainsKey(coord);
            bool inImpact = _impact.Contains(coord);
            bool isSelected = _selected.HasValue && _selected.Value.Equals(coord);
            bool inDanger = _enemyDanger.Contains(coord);
            bool isHover = _hover.HasValue && _hover.Value.Equals(coord);
            bool inFree = _moveFree.Contains(coord);
            bool inCost = _moveCost.Contains(coord);
            bool inRange = _range.Contains(coord);

            Color finalColor = Color.clear;
            bool shouldPaint = false;

            // 优先级：波纹 > Impact > Selected > EnemyDanger > Hover > Move > Range
            if (isRipple)
            {
                // 波纹闪烁
                float t = _activeRipples[coord];
                // 简单闪烁：根据时间调整 Alpha
                float alphaMult = Mathf.PingPong(Time.time * 10f, 1f);
                finalColor = rippleColor;
                finalColor.a *= alphaMult;
                shouldPaint = true;
            }
            else if (inImpact)
            {
                finalColor = playerImpactColor * impactIntensity;
                shouldPaint = true;
            }
            else if (isSelected)
            {
                finalColor = selectedColor * selectedIntensity;
                shouldPaint = true;
            }
            else if (inDanger)
            {
                finalColor = enemyDangerColor * dangerIntensity;
                shouldPaint = true;
            }
            else if (isHover)
            {
                if (_range.Count > 0)
                {
                    finalColor = invalidColor;
                    shouldPaint = true;
                }
                else
                {
                    finalColor = hoverColor * hoverIntensity;
                    shouldPaint = true;
                }
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