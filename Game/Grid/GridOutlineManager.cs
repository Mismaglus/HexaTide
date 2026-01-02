using UnityEngine;
using System.Collections.Generic;
using Core.Hex;
using Game.Common;
using Game.UI;

namespace Game.Battle
{
    public enum OutlineState
    {
        None,
        Movement,
        AbilityTargeting
    }

    // 意图数据包
    public class IntentData
    {
        public HashSet<HexCoords> DangerTiles = new();
        public List<(Vector3 start, Vector3 end, Color color)> Arrows = new();
        public void Clear() { DangerTiles.Clear(); Arrows.Clear(); }
    }

    public class GridOutlineManager : MonoBehaviour
    {
        [Header("Drawers (Scene References)")]
        public RangeOutlineDrawer movementFreeDrawer;
        public RangeOutlineDrawer movementCostDrawer;
        public RangeOutlineDrawer abilityRangeDrawer;

        [Header("Player Intent")]
        public RangeOutlineDrawer impactDrawer;
        public PrefabArrow intentionArrow;
        public HexHighlighter highlighter;

        [Header("Player Colors")]
        // ⭐ 玩家 AOE 预瞄颜色 (建议青色/蓝色，与敌人的红色区分)
        public Color playerImpactColor = new Color(0.2f, 0.9f, 1.0f, 1f);

        [Header("Enemy Intent Drawers")]
        public RangeOutlineDrawer enemyIntentRedDrawer;    // 对应红色（本回合）
        public RangeOutlineDrawer enemyIntentYellowDrawer; // 对应黄色（未来/延迟）

        [Header("Enemy Colors")]
        // ⭐ 敌人即时威胁颜色 (红色)
        public Color enemyDangerColor = new Color(1f, 0.2f, 0.2f, 1f);
        // ⭐ 敌人未来威胁颜色 (黄色)
        public Color enemyFutureColor = new Color(1f, 0.8f, 0.2f, 1f);

        [Header("Enemy Arrow Pool")]
        public PrefabArrow arrowPrefab;
        public Transform arrowPoolRoot;

        // Cache Data
        private readonly HashSet<HexCoords> _moveFree = new();
        private readonly HashSet<HexCoords> _moveCost = new();
        private readonly HashSet<HexCoords> _abilityRange = new();
        private readonly HashSet<HexCoords> _impactArea = new();

        // 分离数据：即时威胁 vs 未来威胁
        private readonly IntentData _immediateIntent = new(); // Red
        private readonly IntentData _futureIntent = new();    // Yellow/Orange

        private readonly List<PrefabArrow> _spawnedArrows = new();
        private PrefabArrow _intentionArrowInstance;

        private OutlineState _currentState = OutlineState.None;
        private bool _showEnemyIntent = true;
        private bool _showFutureInfo = false;

        void Awake()
        {
            if (!highlighter) highlighter = FindFirstObjectByType<HexHighlighter>(FindObjectsInactive.Exclude);
            if (arrowPoolRoot == null) arrowPoolRoot = transform;
            ResolveIntentionArrowInstance();
        }

        // === 状态控制 ===

        public void SetState(OutlineState newState)
        {
            if (_currentState == newState) return;
            _currentState = newState;
            RefreshVisuals();

            if (newState != OutlineState.AbilityTargeting)
                ClearPlayerIntent();
        }

        public void ToggleEnemyIntent(bool isOn)
        {
            _showEnemyIntent = isOn;
            RefreshVisuals();
        }

        public void ToggleFutureVisibility(bool show)
        {
            if (_showFutureInfo == show) return;
            _showFutureInfo = show;
            RefreshVisuals();
        }

        // === 数据输入 ===

        public void SetMovementRange(IEnumerable<HexCoords> free, IEnumerable<HexCoords> cost)
        {
            _moveFree.Clear(); _moveCost.Clear();
            if (free != null) _moveFree.UnionWith(free);
            if (cost != null) _moveCost.UnionWith(cost);
            if (_currentState == OutlineState.Movement) RefreshVisuals();
        }

        public void ClearMovementRange()
        {
            _moveFree.Clear(); _moveCost.Clear();
            RefreshVisuals();
        }

        public void SetAbilityRange(IEnumerable<HexCoords> range)
        {
            _abilityRange.Clear();
            if (range != null) _abilityRange.UnionWith(range);
            if (_currentState == OutlineState.AbilityTargeting) RefreshVisuals();
        }

        public void ClearAbilityRange()
        {
            _abilityRange.Clear();
            RefreshVisuals();
        }

        // 接收即时意图 (Red)
        public void SetEnemyIntent(IEnumerable<HexCoords> dangerZone, List<(Vector3, Vector3)> arrows)
        {
            _immediateIntent.Clear();
            if (dangerZone != null) _immediateIntent.DangerTiles.UnionWith(dangerZone);
            if (arrows != null)
            {
                foreach (var a in arrows)
                    _immediateIntent.Arrows.Add((a.Item1, a.Item2, enemyDangerColor));
            }
            RefreshVisuals();
        }

        // 接收未来意图 (Yellow)
        public void SetFutureIntent(IEnumerable<HexCoords> dangerZone, List<(Vector3, Vector3)> arrows)
        {
            _futureIntent.Clear();
            if (dangerZone != null) _futureIntent.DangerTiles.UnionWith(dangerZone);
            if (arrows != null)
            {
                foreach (var a in arrows)
                    _futureIntent.Arrows.Add((a.Item1, a.Item2, enemyFutureColor));
            }
            RefreshVisuals();
        }

        // === 玩家意图 (单体/AOE) ===
        public void ShowPlayerIntent(Vector3 start, Vector3 end, IEnumerable<HexCoords> impact, bool arrow)
        {
            _impactArea.Clear();
            if (impact != null) _impactArea.UnionWith(impact);

            if (impactDrawer)
            {
                if (_impactArea.Count > 0)
                {
                    // ⭐ 强制设置颜色为玩家预瞄色 (青色)
                    impactDrawer.outlineColor = playerImpactColor;
                    impactDrawer.Show(_impactArea);
                }
                else impactDrawer.Hide();
            }

            // 高亮器也可能需要传颜色，目前 HexHighlighter 有自己的逻辑
            // 如果你想让单个格子的描边也变色，可以在 HexHighlighter 里加接口
            // 这里暂时只设置范围
            if (highlighter) highlighter.SetImpact(_impactArea);

            if (arrow && intentionArrow)
            {
                start.y += 0.8f; end.y += 0.2f;
                // ⭐ 箭头也用青色
                var arrowInstance = ResolveIntentionArrowInstance();
                if (arrowInstance)
                {
                    AdjustArrowEndpoints(ref start, ref end);
                    arrowInstance.SetPositions(start, end, playerImpactColor);
                }
            }
            else
            {
                var arrowInstance = ResolveIntentionArrowInstance();
                if (arrowInstance) arrowInstance.Hide();
            }
        }

        public void ClearPlayerIntent()
        {
            _impactArea.Clear();
            if (impactDrawer) impactDrawer.Hide();
            var arrowInstance = ResolveIntentionArrowInstance();
            if (arrowInstance) arrowInstance.Hide();
            if (highlighter) highlighter.SetImpact(null);
        }

        // === 内部刷新逻辑 ===

        private void RefreshVisuals()
        {
            // 1. 基础层 (移动)
            if (_currentState == OutlineState.Movement)
            {
                ShowDrawer(movementFreeDrawer, _moveFree);
                ShowDrawer(movementCostDrawer, _moveCost);
            }
            else
            {
                HideDrawer(movementFreeDrawer);
                HideDrawer(movementCostDrawer);
            }

            // 2. 技能层 (施法范围)
            if (_currentState == OutlineState.AbilityTargeting)
                ShowDrawer(abilityRangeDrawer, _abilityRange);
            else
                HideDrawer(abilityRangeDrawer);

            // 3. 敌方意图层
            bool showEnemy = _showEnemyIntent && (_currentState != OutlineState.AbilityTargeting);

            if (showEnemy)
            {
                // A. 即时威胁 (Red) - 总是显示
                if (enemyIntentRedDrawer) enemyIntentRedDrawer.outlineColor = enemyDangerColor;
                ShowDrawer(enemyIntentRedDrawer, _immediateIntent.DangerTiles);

                // B. 未来威胁 (Yellow) - 只有开关打开时显示
                if (_showFutureInfo)
                {
                    if (enemyIntentYellowDrawer) enemyIntentYellowDrawer.outlineColor = enemyFutureColor;
                    ShowDrawer(enemyIntentYellowDrawer, _futureIntent.DangerTiles);
                }
                else
                {
                    HideDrawer(enemyIntentYellowDrawer);
                }

                // C. 画所有箭头
                DrawCombinedArrows();
            }
            else
            {
                HideDrawer(enemyIntentRedDrawer);
                HideDrawer(enemyIntentYellowDrawer);
                HideEnemyArrows();
            }
        }

        // --- 箭头池管理 (混合绘制) ---
        void DrawCombinedArrows()
        {
            if (arrowPrefab == null) return;

            var allArrowsToDraw = new List<(Vector3, Vector3, Color)>();

            // 1. Red (Immediate)
            allArrowsToDraw.AddRange(_immediateIntent.Arrows);

            // 2. Yellow (Future)
            if (_showFutureInfo)
            {
                allArrowsToDraw.AddRange(_futureIntent.Arrows);
            }

            // 扩充池
            while (_spawnedArrows.Count < allArrowsToDraw.Count)
            {
                var go = Instantiate(arrowPrefab, arrowPoolRoot);
                _spawnedArrows.Add(go);
            }

            // 设置
            for (int i = 0; i < _spawnedArrows.Count; i++)
            {
                if (i < allArrowsToDraw.Count)
                {
                    var data = allArrowsToDraw[i];
                    Vector3 s = data.Item1;
                    Vector3 e = data.Item2;
                    // 稍微抬高
                    s.y += 0.8f; e.y += 0.2f;

                    var arrow = _spawnedArrows[i];
                    AdjustArrowEndpoints(ref s, ref e);
                    arrow.SetPositions(s, e, data.Item3);
                }
                else
                {
                    _spawnedArrows[i].Hide();
                }
            }
        }

        void HideEnemyArrows()
        {
            foreach (var a in _spawnedArrows) a.Hide();
        }

        void ShowDrawer(RangeOutlineDrawer d, HashSet<HexCoords> t) { if (d) { if (t != null && t.Count > 0) d.Show(t); else d.Hide(); } }
        void HideDrawer(RangeOutlineDrawer d) { if (d) d.Hide(); }

        PrefabArrow ResolveIntentionArrowInstance()
        {
            if (_intentionArrowInstance) return _intentionArrowInstance;
            if (!intentionArrow) return null;

            if (!intentionArrow.gameObject.scene.IsValid())
            {
                _intentionArrowInstance = Instantiate(intentionArrow, arrowPoolRoot);
                _intentionArrowInstance.Hide();
            }
            else
            {
                _intentionArrowInstance = intentionArrow;
            }

            return _intentionArrowInstance;
        }

        void AdjustArrowEndpoints(ref Vector3 start, ref Vector3 end)
        {
            // Compute trimming in grid-local space so grid rotation/scale doesn't skew which edge is chosen.
            // Then apply the trim back in world space.
            if (!highlighter || !highlighter.grid || !highlighter.grid.recipe)
            {
                // Fallback: old world-space trim.
                float edgeOffsetFallback = GetHexEdgeOffset(start, end);
                if (edgeOffsetFallback <= 0f) return;

                Vector3 flatDirFallback = end - start;
                flatDirFallback.y = 0f;
                float flatLenFallback = flatDirFallback.magnitude;
                if (flatLenFallback <= 0.001f) return;

                float maxTrimFallback = (flatLenFallback - 0.001f) * 0.5f;
                float trimFallback = Mathf.Min(edgeOffsetFallback, maxTrimFallback);
                if (trimFallback <= 0f) return;

                Vector3 dirFallback = flatDirFallback / flatLenFallback;
                start += dirFallback * trimFallback;
                end -= dirFallback * trimFallback;
                return;
            }

            var gridT = highlighter.grid.transform;
            float outerRadius = highlighter.grid.recipe.outerRadius;

            Vector3 startLocal = gridT.InverseTransformPoint(start);
            Vector3 endLocal = gridT.InverseTransformPoint(end);

            Vector3 deltaLocal = endLocal - startLocal;
            deltaLocal.y = 0f;
            float deltaLen = deltaLocal.magnitude;
            if (deltaLen <= 0.001f) return;

            float edgeOffset = ComputeHexEdgeDistance(deltaLocal, outerRadius);
            if (edgeOffset <= 0f) return;

            float maxTrim = (deltaLen - 0.001f) * 0.5f;
            float trim = Mathf.Min(edgeOffset, maxTrim);
            if (trim <= 0f) return;

            Vector3 dirLocal = deltaLocal / deltaLen;

            // Preserve the original local Y offsets (we only trim in the plane).
            startLocal += new Vector3(dirLocal.x, 0f, dirLocal.z) * trim;
            endLocal -= new Vector3(dirLocal.x, 0f, dirLocal.z) * trim;

            start = gridT.TransformPoint(startLocal);
            end = gridT.TransformPoint(endLocal);
        }

        float GetHexEdgeOffset(Vector3 start, Vector3 end)
        {
            if (!highlighter || !highlighter.grid || !highlighter.grid.recipe) return 0f;

            Vector3 dir = end - start;
            dir.y = 0f;
            if (dir.sqrMagnitude <= 0.0001f) return 0f;

            float outerRadius = highlighter.grid.recipe.outerRadius;
            return ComputeHexEdgeDistance(dir, outerRadius);
        }

        static float ComputeHexEdgeDistance(Vector3 direction, float outerRadius)
        {
            Vector2 dir = new Vector2(direction.x, direction.z);
            if (dir.sqrMagnitude <= 0.0001f) return 0f;
            dir.Normalize();

            float best = float.PositiveInfinity;
            for (int i = 0; i < 6; i++)
            {
                Vector2 p = HexMetrics.CORNER_DIRS[i] * outerRadius;
                Vector2 q = HexMetrics.CORNER_DIRS[(i + 1) % 6] * outerRadius;
                Vector2 s = q - p;
                float denom = Cross(dir, s);
                if (Mathf.Abs(denom) < 0.000001f) continue;

                float t = Cross(p, s) / denom;
                float u = Cross(p, dir) / denom;
                if (t >= 0f && u >= 0f && u <= 1f)
                {
                    if (t < best) best = t;
                }
            }

            if (float.IsInfinity(best)) return outerRadius;
            return best;
        }

        static float Cross(Vector2 a, Vector2 b) => (a.x * b.y) - (a.y * b.x);
    }
}
