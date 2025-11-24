using UnityEngine;
using System.Collections.Generic;
using Core.Hex;
using Game.Common; // 引用 HexHighlighter
using Game.UI;     // 引用 RangeOutlineDrawer, BattleArrow

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

        [Header("Intent Visuals")]
        public RangeOutlineDrawer impactDrawer;       // 负责画 AOE 的边框
        public BattleArrow intentionArrow;            // 负责画箭头
        public HexHighlighter highlighter;            // 负责画 AOE 的底色

        [Header("Visual Settings")]
        public Color impactLineColor = new Color(1f, 0.2f, 0.2f, 1f); // 强制指定 Impact 线框颜色(红)

        [Header("Enemy Intent")]
        public RangeOutlineDrawer enemyIntentDrawer;

        // Cache Data
        private readonly HashSet<HexCoords> _moveFree = new();
        private readonly HashSet<HexCoords> _moveCost = new();
        private readonly HashSet<HexCoords> _abilityRange = new();
        private readonly HashSet<HexCoords> _impactArea = new();
        private readonly HashSet<HexCoords> _enemyIntent = new();

        private OutlineState _currentState = OutlineState.None;

        // ⭐ 开关：是否允许显示敌方意图 (用于 UI 按钮手动切换)
        private bool _showEnemyIntent = true;

        void Awake()
        {
            if (!highlighter) highlighter = FindFirstObjectByType<HexHighlighter>(FindObjectsInactive.Exclude);
        }

        // === 状态控制 ===

        public void SetState(OutlineState newState)
        {
            if (_currentState == newState) return;
            _currentState = newState;
            RefreshVisuals();

            if (newState != OutlineState.AbilityTargeting)
                ClearIntent();
        }

        // ⭐ 新增：手动切换意图显示的接口
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

        public void SetEnemyIntent(IEnumerable<HexCoords> dangerZone)
        {
            _enemyIntent.Clear();
            if (dangerZone != null) _enemyIntent.UnionWith(dangerZone);
            RefreshVisuals();
        }

        // === 核心功能：显示意图 ===
        public void ShowIntent(Vector3 startWorldPos, Vector3 endWorldPos, IEnumerable<HexCoords> impactTiles, bool showArrow)
        {
            // 1. 更新缓存
            _impactArea.Clear();
            if (impactTiles != null) _impactArea.UnionWith(impactTiles);

            // 2. 显示线框 (Outline)
            if (impactDrawer != null)
            {
                if (_impactArea.Count > 0)
                {
                    impactDrawer.outlineColor = impactLineColor;
                    impactDrawer.Show(_impactArea);
                }
                else
                {
                    impactDrawer.Hide();
                }
            }

            // 3. 显示底色 (Highlight)
            if (highlighter != null)
            {
                highlighter.SetImpact(_impactArea);
            }

            // 4. 显示箭头 (Arrow)
            if (showArrow && intentionArrow != null)
            {
                // 稍微抬高防止穿模
                startWorldPos.y += 0.8f;
                endWorldPos.y += 0.2f;
                intentionArrow.SetPositions(startWorldPos, endWorldPos);
            }
            else if (intentionArrow != null)
            {
                intentionArrow.Hide();
            }
        }

        public void ClearIntent()
        {
            _impactArea.Clear();
            if (impactDrawer) impactDrawer.Hide();
            if (intentionArrow) intentionArrow.Hide();
            if (highlighter) highlighter.SetImpact(null);
        }

        // === 内部刷新逻辑 (核心修改) ===

        private void RefreshVisuals()
        {
            // 1. 移动范围层 (Base Drawers)
            switch (_currentState)
            {
                case OutlineState.Movement:
                    ShowDrawer(movementFreeDrawer, _moveFree);
                    ShowDrawer(movementCostDrawer, _moveCost);
                    break;

                default:
                    // 在 None 或 Targeting 状态下，隐藏移动范围
                    HideDrawer(movementFreeDrawer);
                    HideDrawer(movementCostDrawer);
                    break;
            }

            // 2. 技能范围层 (Range)
            if (_currentState == OutlineState.AbilityTargeting)
            {
                ShowDrawer(abilityRangeDrawer, _abilityRange);
            }
            else
            {
                HideDrawer(abilityRangeDrawer);
            }

            // 3. ⭐ 敌方意图层 (Enemy Intent)
            // 逻辑：只要开关是开的，且不在“释放技能”状态，就一直显示
            // (None 状态下现在也会显示了，这就是“常驻”)
            bool shouldShowIntent = _showEnemyIntent && (_currentState != OutlineState.AbilityTargeting);

            if (shouldShowIntent)
            {
                ShowDrawer(enemyIntentDrawer, _enemyIntent);
            }
            else
            {
                HideDrawer(enemyIntentDrawer);
            }
        }

        void ShowDrawer(RangeOutlineDrawer drawer, HashSet<HexCoords> tiles)
        {
            if (drawer == null) return;
            if (tiles != null && tiles.Count > 0) drawer.Show(tiles);
            else drawer.Hide();
        }

        void HideDrawer(RangeOutlineDrawer drawer)
        {
            if (drawer != null) drawer.Hide();
        }
    }
}