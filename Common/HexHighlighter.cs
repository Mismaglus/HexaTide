using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Core.Hex;
using Game.Grid;

namespace Game.Common
{
    [DisallowMultipleComponent]
    public class HexHighlighter : MonoBehaviour
    {
        [Header("References")]
        public Game.Battle.BattleHexGrid grid;

        [Header("Standard Colors")]
        // Fix: Use high alpha (1.0) and light colors for tinting to avoid making the mesh transparent
        public Color hoverColor = new Color(1.0f, 1.0f, 0.8f, 1.0f); // Light Yellow Tint
        public Color selectedColor = new Color(0.6f, 1.0f, 0.6f, 1.0f); // Light Green Tint (Requested)

        [Header("Combat Colors")]
        public Color rangeColor = new Color(0.0f, 1.0f, 0.4f, 0.3f);
        public Color playerImpactColor = new Color(0f, 1f, 1f, 0.8f);
        public Color enemyDangerColor = new Color(1.0f, 0.2f, 0.2f, 0.6f);
        public Color invalidColor = new Color(1.0f, 0.1f, 0.1f, 0.5f);

        [Header("Movement Colors")]
        public Color moveFreeColor = new Color(0.0f, 1.0f, 0.4f, 0.15f);
        public Color moveCostColor = new Color(1.0f, 0.8f, 0.0f, 0.15f);

        [Header("FX Colors")]
        public Color rippleColor = new Color(1f, 0.2f, 0.2f, 1f); // 波纹红 (建议 Alpha=1)

        [Header("Fog Colors")]
        public bool ignoreFog = false;
        public Color fogUnknownColor = Color.black;
        public Color fogGhostColor = new Color(0.5f, 0.5f, 0.6f, 1f);

        [Header("Path Visuals")]
        public GameObject targetCursorPrefab;
        public GameObject pathNodePrefab;
        public GameObject costLabelPrefab;

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
            public HexCell cell;
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

        // 波纹字典: Value > 0 为倒计时， Value == -1 为持久化
        struct RippleState
        {
            public float remaining;
            public float totalDuration;
        }
        private Dictionary<HexCoords, RippleState> _activeRipples = new Dictionary<HexCoords, RippleState>();
        private HashSet<HexCoords> _ripplesToPersist = new HashSet<HexCoords>();

        MaterialPropertyBlock _mpb;
        uint _lastGridVersion;
        bool _warned;

        // Visuals State
        private GameObject _currentCursor;
        private GameObject _currentLabel;
        private List<GameObject> _activePathNodes = new List<GameObject>();
        private Queue<GameObject> _pathNodePool = new Queue<GameObject>();

        void Awake() { if (_mpb == null) _mpb = new MaterialPropertyBlock(); }

        void Update()
        {
            if (_activeRipples.Count > 0)
            {
                var keys = new List<HexCoords>(_activeRipples.Keys);
                foreach (var k in keys)
                {
                    RippleState state = _activeRipples[k];

                    // 如果是持久化波纹 (-1)，不倒计时，但依然重绘以保持闪烁动画
                    if (state.remaining < 0)
                    {
                        Repaint(k.q, k.r);
                        continue;
                    }

                    // 倒计时逻辑
                    state.remaining -= Time.deltaTime;
                    if (state.remaining <= 0)
                    {
                        if (_ripplesToPersist.Contains(k))
                        {
                            _activeRipples[k] = new RippleState { remaining = -1f, totalDuration = 0f }; // 转为持久化
                            _ripplesToPersist.Remove(k);
                            Repaint(k.q, k.r);
                        }
                        else
                        {
                            _activeRipples.Remove(k);
                            Repaint(k.q, k.r); // 结束时重绘以清除颜色
                        }
                    }
                    else
                    {
                        _activeRipples[k] = state;
                        Repaint(k.q, k.r);
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

                var cell = t.GetComponent<HexCell>();
                _slots[(t.Coords.q, t.Coords.r)] = new Slot { mr = mr, colorPropId = pid, cell = cell };
            }
        }

        // === API Methods ===

        // 触发短暂波纹 (用于静态敌人或非终点移动)
        public void TriggerRipple(HexCoords c, float duration = 0.5f, bool persistAfter = false)
        {
            if (!_slots.ContainsKey((c.q, c.r))) return;

            // 强制重置状态，确保即使是持久化波纹也能重新闪烁
            if (_activeRipples.ContainsKey(c)) _activeRipples.Remove(c);

            _activeRipples[c] = new RippleState { remaining = duration, totalDuration = duration };
            if (persistAfter) _ripplesToPersist.Add(c);
            else _ripplesToPersist.Remove(c);

            Repaint(c.q, c.r);
        }

        // 设置/取消持久化波纹 (用于移动终点)
        public void SetPersistentRipple(HexCoords c, bool active)
        {
            if (!_slots.ContainsKey((c.q, c.r))) return;

            if (active)
            {
                _activeRipples[c] = new RippleState { remaining = -1f, totalDuration = 0f }; // -1 标记为持久
                _ripplesToPersist.Remove(c);
                Repaint(c.q, c.r);
            }
            else
            {
                if (_activeRipples.ContainsKey(c))
                {
                    _activeRipples.Remove(c);
                    _ripplesToPersist.Remove(c);
                    Repaint(c.q, c.r);
                }
            }
        }

        // 清除所有持久化波纹 (用于回合开始)
        public void ClearPersistentRipples()
        {
            var keys = new List<HexCoords>(_activeRipples.Keys);
            foreach (var k in keys)
            {
                if (_activeRipples[k].remaining < 0)
                {
                    _activeRipples.Remove(k);
                    _ripplesToPersist.Remove(k);
                    Repaint(k.q, k.r);
                }
            }
        }

        // 查询是否已有持久波纹
        public bool HasPersistentRipple(HexCoords c)
        {
            return _activeRipples.TryGetValue(c, out RippleState val) && val.remaining < 0;
        }

        public void SetHover(HexCoords? c) { var o = _hover; _hover = c; revertPaintHelper(o); Repaint(_hover); }
        public void SetSelected(HexCoords? c) { var o = _selected; _selected = c; revertPaintHelper(o); Repaint(_selected); }

        public void ApplyRange(IEnumerable<HexCoords> c)
        {
            var old = new HashSet<HexCoords>(_range); _range.Clear(); if (c != null) foreach (var x in c) _range.Add(x);
            foreach (var x in old) if (!_range.Contains(x)) revertPaintHelper(x); foreach (var x in _range) if (!old.Contains(x)) Repaint(x.q, x.r);
        }

        public void ApplyMoveRange(IEnumerable<HexCoords> free, IEnumerable<HexCoords> cost)
        {
            var dirty = new HashSet<HexCoords>(_moveFree); dirty.UnionWith(_moveCost);
            _moveFree.Clear(); _moveCost.Clear();
            if (free != null) foreach (var x in free) _moveFree.Add(x); if (cost != null) foreach (var x in cost) _moveCost.Add(x);
            dirty.UnionWith(_moveFree); dirty.UnionWith(_moveCost);
            foreach (var t in dirty) Repaint(t.q, t.r);
        }

        public void SetImpact(IEnumerable<HexCoords> c)
        {
            var old = new HashSet<HexCoords>(_impact); _impact.Clear(); if (c != null) foreach (var x in c) _impact.Add(x);
            foreach (var x in old) if (!_impact.Contains(x)) Repaint(x.q, x.r); foreach (var x in _impact) if (!old.Contains(x)) Repaint(x.q, x.r);
        }

        public void SetEnemyDanger(IEnumerable<HexCoords> c)
        {
            var old = new HashSet<HexCoords>(_enemyDanger); _enemyDanger.Clear(); if (c != null) foreach (var x in c) _enemyDanger.Add(x);
            foreach (var x in old) if (!_enemyDanger.Contains(x)) Repaint(x.q, x.r); foreach (var x in _enemyDanger) if (!old.Contains(x)) Repaint(x.q, x.r);
        }

        public void ClearAll()
        {
            var t = new HashSet<HexCoords>();
            if (_hover.HasValue) t.Add(_hover.Value); if (_selected.HasValue) t.Add(_selected.Value);
            foreach (var x in _range) t.Add(x); foreach (var x in _impact) t.Add(x); foreach (var x in _enemyDanger) t.Add(x);
            foreach (var x in _moveFree) t.Add(x); foreach (var x in _moveCost) t.Add(x);
            _hover = null; _selected = null; _range.Clear(); _impact.Clear(); _enemyDanger.Clear(); _moveFree.Clear(); _moveCost.Clear(); _activeRipples.Clear();
            foreach (var x in t) ClearPaint(x);

            ClearVisuals(); // Also clear new visuals
        }

        void revertPaintHelper(HexCoords? c) { if (c.HasValue) Repaint(c.Value.q, c.Value.r); }
        void Repaint(HexCoords? c) { if (c.HasValue) Repaint(c.Value.q, c.Value.r); }

        public void ReapplyAll()
        {
            if (_hover.HasValue) Repaint(_hover.Value.q, _hover.Value.r);
            if (_selected.HasValue) Repaint(_selected.Value.q, _selected.Value.r);
            foreach (var v in _range) Repaint(v.q, v.r); foreach (var v in _impact) Repaint(v.q, v.r);
            foreach (var v in _enemyDanger) Repaint(v.q, v.r); foreach (var v in _moveFree) Repaint(v.q, v.r);
            foreach (var v in _moveCost) Repaint(v.q, v.r); foreach (var v in _activeRipples.Keys) Repaint(v.q, v.r);
        }

        void Repaint(int q, int r)
        {
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            if (!_slots.TryGetValue((q, r), out var slot) || !slot.mr) return;

            var coord = new HexCoords(q, r);

            FogStatus fog = FogStatus.Visible;
            if (slot.cell != null) fog = slot.cell.fogStatus;

            if (ignoreFog) fog = FogStatus.Visible;

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

            // 优先级: Ripple > Impact > Unknown Fog > Others

            if (isRipple)
            {
                RippleState state = _activeRipples[coord];

                // 确定底色：优先从 HexCell 获取当前 Fog 对应的颜色
                Color baseCol = fogUnknownColor;
                if (slot.cell != null)
                {
                    switch (fog)
                    {
                        case FogStatus.Ghost: baseCol = slot.cell.FogColorGhost; break;
                        case FogStatus.Sensed: baseCol = slot.cell.FogColorSensed; break;
                        case FogStatus.Visible: baseCol = slot.cell.FogColorVisible; break;
                        case FogStatus.Unknown: baseCol = slot.cell.FogColorUnknown; break;
                    }
                }
                else
                {
                    // Fallback
                    baseCol = (fog == FogStatus.Unknown) ? fogUnknownColor :
                              (fog == FogStatus.Ghost ? fogGhostColor : Color.clear);
                }

                if (state.remaining < 0) // Persistent: Static Color
                {
                    finalColor = rippleColor;
                }
                else // Temporary: Flashing
                {
                    // 呼吸效果: 根据剩余时间计算进度，确保在 duration 内完成一次完整的呼吸 (0 -> 1 -> 0)
                    // progress: 0 (start) -> 1 (end)
                    float progress = 1f - (state.remaining / state.totalDuration);

                    // 使用 Sin 曲线: Sin(0) = 0, Sin(PI/2) = 1, Sin(PI) = 0
                    float t = Mathf.Sin(progress * Mathf.PI);

                    if (fog == FogStatus.Visible)
                    {
                        // 可见区域：在透明和红色之间闪烁
                        finalColor = rippleColor;
                        finalColor.a = t * 0.8f;
                    }
                    else
                    {
                        // 迷雾区域：在 底色 和 高亮色 之间插值
                        // 为了让闪烁更明显，我们可以让它稍微亮一点
                        Color flashColor = rippleColor * 1.2f;
                        flashColor.a = 1f;

                        finalColor = Color.Lerp(baseCol, flashColor, t);
                        finalColor.a = 1f;
                    }
                }
                shouldPaint = true;
            }
            else if (inImpact)
            {
                finalColor = playerImpactColor * impactIntensity;
                shouldPaint = true;
            }
            else if (fog == FogStatus.Unknown)
            {
                // 如果 HexCell 已经是 Unknown，则使用 HexCell 的颜色（通常是半透明黑）
                // 否则使用 Highlighter 的默认 Unknown 颜色（通常是纯黑）
                // 这里为了避免覆盖 HexCell 的半透明效果，优先使用 HexCell 的颜色
                if (slot.cell != null) finalColor = slot.cell.FogColorUnknown;
                else finalColor = fogUnknownColor;

                shouldPaint = true;
            }
            else
            {
                if (isSelected) { finalColor = selectedColor * selectedIntensity; shouldPaint = true; }
                else if (inDanger) { finalColor = enemyDangerColor * dangerIntensity; shouldPaint = true; }
                else if (isHover)
                {
                    if (_range.Count > 0) { finalColor = invalidColor; shouldPaint = true; }
                    else { finalColor = hoverColor * hoverIntensity; shouldPaint = true; }
                }
                else if (inCost) { finalColor = moveCostColor * rangeIntensity; shouldPaint = true; }
                else if (inFree) { finalColor = moveFreeColor * rangeIntensity; shouldPaint = true; }
                else if (inRange) { finalColor = rangeColor * rangeIntensity; shouldPaint = true; }

                if (!shouldPaint)
                {
                    // 兜底逻辑：如果 Highlighter 没有特殊显示，则还原 HexCell 的迷雾颜色
                    // 避免 SetPropertyBlock(null) 导致 HexCell 的颜色被清除
                    if (slot.cell != null)
                    {
                        switch (fog)
                        {
                            case FogStatus.Ghost:
                                finalColor = slot.cell.FogColorGhost;
                                shouldPaint = true;
                                break;
                            case FogStatus.Sensed:
                                finalColor = slot.cell.FogColorSensed;
                                shouldPaint = true;
                                break;
                            case FogStatus.Visible:
                                finalColor = slot.cell.FogColorVisible;
                                shouldPaint = true;
                                break;
                                // Unknown 已经在上面处理过了
                        }
                    }
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

        public void ClearPaint(HexCoords c)
        {
            if (_slots.ContainsKey((c.q, c.r))) Repaint(c.q, c.r);
        }

        // === New Visual Feedback API ===

        public void ShowDestCursor(HexCoords c, string labelText)
        {
            if (!grid) return;
            Vector3 pos = grid.GetTileWorldPosition(c);
            pos.y += 0.1f; // Slight offset

            // 1. Cursor
            if (targetCursorPrefab)
            {
                if (!_currentCursor) _currentCursor = Instantiate(targetCursorPrefab, transform);
                _currentCursor.transform.position = pos;
                _currentCursor.SetActive(true);
            }

            // 2. Label
            if (costLabelPrefab)
            {
                if (!_currentLabel) _currentLabel = Instantiate(costLabelPrefab, transform);
                _currentLabel.transform.position = pos + Vector3.up * 1.5f; // Float above
                _currentLabel.SetActive(true);

                // Try setting text on various components
                var tmpro = _currentLabel.GetComponentInChildren<TMPro.TMP_Text>();
                if (tmpro) tmpro.text = labelText;
                else
                {
                    var uiText = _currentLabel.GetComponentInChildren<UnityEngine.UI.Text>();
                    if (uiText) uiText.text = labelText;
                }
            }
        }

        public void ShowPath(IList<HexCoords> path)
        {
            if (!grid || path == null || path.Count == 0) return;

            // Recycle active nodes
            foreach (var node in _activePathNodes)
            {
                node.SetActive(false);
                _pathNodePool.Enqueue(node);
            }
            _activePathNodes.Clear();

            if (!pathNodePrefab) return;

            // Iterate path, skipping the last one (covered by cursor)
            for (int i = 0; i < path.Count - 1; i++)
            {
                HexCoords current = path[i];
                HexCoords next = path[i + 1];

                Vector3 pos = grid.GetTileWorldPosition(current);
                pos.y += 0.05f;

                GameObject node = GetPathNode();
                node.transform.position = pos;

                // Rotate to face next tile (XZ plane only)
                Vector3 nextPos = grid.GetTileWorldPosition(next);
                nextPos.y = pos.y;

                Vector3 dir = nextPos - pos;
                dir.y = 0f;

                if (dir.sqrMagnitude > 1e-6f)
                {
                    node.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
                }

                node.SetActive(true);
                _activePathNodes.Add(node);
            }
        }

        public void ClearVisuals()
        {
            if (_currentCursor) _currentCursor.SetActive(false);
            if (_currentLabel) _currentLabel.SetActive(false);

            foreach (var node in _activePathNodes)
            {
                node.SetActive(false);
                _pathNodePool.Enqueue(node);
            }
            _activePathNodes.Clear();
        }

        private GameObject GetPathNode()
        {
            if (_pathNodePool.Count > 0)
            {
                return _pathNodePool.Dequeue();
            }
            return Instantiate(pathNodePrefab, transform);
        }
    }
}
