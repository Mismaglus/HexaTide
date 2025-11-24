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
        Movement,        // 移动规划
        AbilityTargeting // 技能瞄准
    }

    public class GridOutlineManager : MonoBehaviour
    {
        [Header("Drawers (Scene References)")]
        public RangeOutlineDrawer movementFreeDrawer;
        public RangeOutlineDrawer movementCostDrawer;
        public RangeOutlineDrawer abilityRangeDrawer;

        [Header("Player Intent")]
        public RangeOutlineDrawer impactDrawer;
        public BattleArrow intentionArrow;
        public HexHighlighter highlighter;
        public Color impactLineColor = new Color(1f, 0.2f, 0.2f, 1f);

        [Header("Enemy Intent")]
        public RangeOutlineDrawer enemyIntentDrawer; // 危险区域红圈

        [Header("Enemy Arrow Pool")]
        public BattleArrow arrowPrefab;              // ⭐ 必须拖拽 BattleArrow 的 Prefab
        public Transform arrowPoolRoot;              // 可选：箭头生成的父节点

        // Cache Data
        private readonly HashSet<HexCoords> _moveFree = new();
        private readonly HashSet<HexCoords> _moveCost = new();
        private readonly HashSet<HexCoords> _abilityRange = new();
        private readonly HashSet<HexCoords> _impactArea = new();
        private readonly HashSet<HexCoords> _enemyIntentTiles = new();

        // 敌方箭头数据
        private readonly List<(Vector3 s, Vector3 e)> _enemyArrowData = new();
        private readonly List<BattleArrow> _spawnedArrows = new();

        private OutlineState _currentState = OutlineState.None;
        private bool _showEnemyIntent = true;

        void Awake()
        {
            if (!highlighter) highlighter = FindFirstObjectByType<HexHighlighter>(FindObjectsInactive.Exclude);
            if (arrowPoolRoot == null) arrowPoolRoot = transform;
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

        // ⭐⭐⭐ 核心：接收敌方意图数据 (区域 + 箭头列表)
        public void SetEnemyIntent(IEnumerable<HexCoords> dangerZone, List<(Vector3, Vector3)> arrows)
        {
            _enemyIntentTiles.Clear();
            if (dangerZone != null) _enemyIntentTiles.UnionWith(dangerZone);

            _enemyArrowData.Clear();
            if (arrows != null) _enemyArrowData.AddRange(arrows);

            RefreshVisuals();
        }

        // === 玩家意图 (单体) ===
        public void ShowPlayerIntent(Vector3 start, Vector3 end, IEnumerable<HexCoords> impact, bool arrow)
        {
            _impactArea.Clear();
            if (impact != null) _impactArea.UnionWith(impact);

            if (impactDrawer)
            {
                if (_impactArea.Count > 0) { impactDrawer.outlineColor = impactLineColor; impactDrawer.Show(_impactArea); }
                else impactDrawer.Hide();
            }
            if (highlighter) highlighter.SetImpact(_impactArea);

            if (arrow && intentionArrow) { start.y += 0.8f; end.y += 0.2f; intentionArrow.SetPositions(start, end); }
            else if (intentionArrow) intentionArrow.Hide();
        }

        public void ClearPlayerIntent()
        {
            _impactArea.Clear();
            if (impactDrawer) impactDrawer.Hide();
            if (intentionArrow) intentionArrow.Hide();
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

            // 2. 技能层
            if (_currentState == OutlineState.AbilityTargeting)
                ShowDrawer(abilityRangeDrawer, _abilityRange);
            else
                HideDrawer(abilityRangeDrawer);

            // 3. 敌方意图层 (关键逻辑)
            // 只要不是在“瞄准技能”，就显示敌人意图
            bool showEnemy = _showEnemyIntent && (_currentState != OutlineState.AbilityTargeting);

            if (showEnemy)
            {
                ShowDrawer(enemyIntentDrawer, _enemyIntentTiles);
                DrawEnemyArrows(); // 画箭头
            }
            else
            {
                HideDrawer(enemyIntentDrawer);
                HideEnemyArrows(); // 藏箭头
            }
        }

        // --- 箭头池管理 ---
        void DrawEnemyArrows()
        {
            if (arrowPrefab == null) return;

            // 1. 确保池子够大
            while (_spawnedArrows.Count < _enemyArrowData.Count)
            {
                var go = Instantiate(arrowPrefab, arrowPoolRoot);
                _spawnedArrows.Add(go);
            }

            // 2. 激活并设置位置
            for (int i = 0; i < _spawnedArrows.Count; i++)
            {
                if (i < _enemyArrowData.Count)
                {
                    var (s, e) = _enemyArrowData[i];
                    // 稍微抬高，防止Z-fighting
                    s.y += 0.8f; e.y += 0.2f;
                    _spawnedArrows[i].SetPositions(s, e);
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
    }
}