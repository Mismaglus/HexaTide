using System.Collections.Generic;
using UnityEngine;
using Core.Hex;

namespace Game.Common
{
    /// <summary>
    /// 增强版高亮器：支持瞄准模式下的智能颜色混合。
    /// 优先级：Player Impact (Cyan) > Selected > Enemy Danger (Red) > Hover > Move/Range
    /// </summary>
    [DisallowMultipleComponent]
    public class HexHighlighter : MonoBehaviour
    {
        [Header("Refs")]
        public Game.Battle.BattleHexGrid grid;

        [Header("Standard Colors")]
        public Color hoverColor = new Color(0.95f, 0.95f, 0.25f, 0.5f);    // 平时悬停（黄）
        public Color selectedColor = new Color(0.25f, 0.8f, 1.0f, 0.5f);   // 选中单位（蓝）

        [Header("Combat Colors")]
        // 技能射程范围 (底色)
        public Color rangeColor = new Color(0.0f, 1.0f, 0.4f, 0.3f);

        // ? 玩家预瞄 (Player Impact) - 既然你想要 Cyan，这里默认设为青色
        public Color playerImpactColor = new Color(0f, 1f, 1f, 0.8f);

        // ? 敌人威胁 (Enemy Danger) - 红色
        public Color enemyDangerColor = new Color(1.0f, 0.2f, 0.2f, 0.6f);

        // 无效/错误 (Invalid)
        public Color invalidColor = new Color(1.0f, 0.1f, 0.1f, 0.5f);

        [Header("Movement Colors")]
        public Color moveFreeColor = new Color(0.0f, 1.0f, 0.4f, 0.15f);
        public Color moveCostColor = new Color(1.0f, 0.8f, 0.0f, 0.15f);

        [Header("Intensity")]
        [Range(0.1f, 4f)] public float hoverIntensity = 1.0f;
        [Range(0.1f, 4f)] public float selectedIntensity = 1.0f;
        [Range(0.1f, 4f)] public float rangeIntensity = 1.0f;
        [Range(0.1f, 4f)] public float impactIntensity = 1.0f; // ? 新增 Impact 强度控制
        [Range(0.1f, 4f)] public float dangerIntensity = 1.0f; // ? 新增 Danger 强度控制

        [Header("Material Compatibility")]
        public string[] colorPropertyNames = new[] { "_BaseColor", "_Color", "_Tint" };
        public bool warnOnceIfNoColorProperty = true;

        // Cache
        struct Slot { public MeshRenderer mr; public int colorPropId; }
        readonly Dictionary<(int q, int r), Slot> _slots = new();

        // States
        HexCoords? _hover;
        HexCoords? _selected;

        readonly HashSet<HexCoords> _range = new();        // 玩家技能范围
        readonly HashSet<HexCoords> _impact = new();       // 玩家AOE预瞄
        readonly HashSet<HexCoords> _enemyDanger = new();  // ? 敌人AOE范围
        readonly HashSet<HexCoords> _moveFree = new();     // 免费移动
        readonly HashSet<HexCoords> _moveCost = new();     // 付费移动

        MaterialPropertyBlock _mpb;
        uint _lastGridVersion;
        bool _warned;

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

                _slots[(t.Coords.q, t.Coords.r)] = new Slot { mr = mr, colorPropId = pid };
            }
        }

        // === API Methods ===

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

        // 玩家预瞄 (AOE)
        public void SetImpact(IEnumerable<HexCoords> c)
        {
            var old = new HashSet<HexCoords>(_impact);
            _impact.Clear();
            if (c != null) foreach (var x in c) _impact.Add(x);

            foreach (var x in old) if (!_impact.Contains(x)) Repaint(x.q, x.r);
            foreach (var x in _impact) if (!old.Contains(x)) Repaint(x.q, x.r);
        }

        // ? 新增：设置敌人危险区域
        public void SetEnemyDanger(IEnumerable<HexCoords> c)
        {
            var old = new HashSet<HexCoords>(_enemyDanger);
            _enemyDanger.Clear();
            if (c != null) foreach (var x in c) _enemyDanger.Add(x);

            // 刷新旧的和新的区域
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
            foreach (var x in _enemyDanger) t.Add(x); // Add Danger
            foreach (var x in _moveFree) t.Add(x);
            foreach (var x in _moveCost) t.Add(x);

            _hover = null; _selected = null;
            _range.Clear(); _impact.Clear(); _enemyDanger.Clear(); _moveFree.Clear(); _moveCost.Clear();

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
            foreach (var v in _enemyDanger) Repaint(v.q, v.r); // Repaint Danger
            foreach (var v in _moveFree) Repaint(v.q, v.r);
            foreach (var v in _moveCost) Repaint(v.q, v.r);
        }

        void Repaint(int q, int r)
        {
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            if (!_slots.TryGetValue((q, r), out var slot) || !slot.mr) return;

            var coord = new HexCoords(q, r);
            bool isHover = _hover.HasValue && _hover.Value.Equals(coord);
            bool isSelected = _selected.HasValue && _selected.Value.Equals(coord);
            bool inRange = _range.Contains(coord);
            bool inImpact = _impact.Contains(coord);
            bool inDanger = _enemyDanger.Contains(coord); // ?
            bool inFree = _moveFree.Contains(coord);
            bool inCost = _moveCost.Contains(coord);

            Color finalColor = Color.clear;
            bool shouldPaint = false;

            // 优先级逻辑：
            // 1. Impact (玩家瞄准)：最高优先级，我正在瞄准，我要看清楚覆盖了什么
            // 2. Selected (玩家选中)：知道自己在哪
            // 3. EnemyDanger (敌人威胁)：重要的警示信息
            // 4. Hover (鼠标悬停)：交互反馈
            // 5. Move/Range：底色信息

            if (inImpact)
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
                // ? 敌人威胁 (红色)
                finalColor = enemyDangerColor * dangerIntensity;
                shouldPaint = true;
            }
            else if (isHover)
            {
                // 如果在射程内但不在Impact内，显示普通Hover，否则如果是在瞄准状态但指歪了显示Invalid
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