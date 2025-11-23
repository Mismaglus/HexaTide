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
        public HexHighlighter highlighter;            // ⭐ 新增：负责画 AOE 的底色

        [Header("Visual Settings")]
        public Color impactLineColor = new Color(1f, 0.2f, 0.2f, 1f); // ⭐ 新增：强制指定 Impact 线框颜色(红)

        [Header("Enemy Intent (Future)")]
        public RangeOutlineDrawer enemyIntentDrawer;

        // Cache Data
        private readonly HashSet<HexCoords> _moveFree = new();
        private readonly HashSet<HexCoords> _moveCost = new();
        private readonly HashSet<HexCoords> _abilityRange = new();
        private readonly HashSet<HexCoords> _impactArea = new(); // 缓存当前的打击范围
        private readonly HashSet<HexCoords> _enemyIntent = new();

        private OutlineState _currentState = OutlineState.None;

        void Awake()
        {
            // 自动查找，防止漏配
            if (!highlighter) highlighter = FindFirstObjectByType<HexHighlighter>(FindObjectsInactive.Exclude);
        }

        // === 状态控制 ===

        public void SetState(OutlineState newState)
        {
            if (_currentState == newState) return;
            _currentState = newState;
            RefreshVisuals();

            // 如果退出了瞄准模式，清理掉临时的意图显示
            if (newState != OutlineState.AbilityTargeting)
                ClearIntent();
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

        // === ⭐ 核心升级：显示意图 (箭头 + 线框 + 底色) ===
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
                    // ⭐ 强制设置为醒目的颜色（如红色），覆盖 Drawer原本的配置
                    impactDrawer.outlineColor = impactLineColor;
                    impactDrawer.Show(_impactArea);
                }
                else
                {
                    impactDrawer.Hide();
                }
            }

            // 3. ⭐ 显示底色 (Highlight) - 这会让范围非常直观！
            // HexHighlighter 会根据 SetImpact 传入的格子，将其渲染为 impactColor (通常是橙色/红色半透明)
            if (highlighter != null)
            {
                highlighter.SetImpact(_impactArea);
            }

            // 4. 显示箭头 (Arrow)
            if (showArrow && intentionArrow != null)
            {
                // 稍微抬高一点起点和终点，防止被模型遮挡
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

            // 隐藏线框
            if (impactDrawer) impactDrawer.Hide();

            // 隐藏箭头
            if (intentionArrow) intentionArrow.Hide();

            // ⭐ 清除底色高亮
            if (highlighter) highlighter.SetImpact(null);
        }

        // === 内部刷新逻辑 ===

        private void RefreshVisuals()
        {
            switch (_currentState)
            {
                case OutlineState.None:
                    HideBaseDrawers();
                    break;

                case OutlineState.Movement:
                    ShowDrawer(movementFreeDrawer, _moveFree);
                    ShowDrawer(movementCostDrawer, _moveCost);
                    ShowDrawer(enemyIntentDrawer, _enemyIntent); // 移动时可以看到敌人的意图
                    HideDrawer(abilityRangeDrawer);
                    break;

                case OutlineState.AbilityTargeting:
                    HideDrawer(movementFreeDrawer);
                    HideDrawer(movementCostDrawer);
                    HideDrawer(enemyIntentDrawer); // 瞄准时隐藏敌人意图，避免视觉杂乱
                    ShowDrawer(abilityRangeDrawer, _abilityRange);
                    break;
            }
        }

        void HideBaseDrawers()
        {
            HideDrawer(movementFreeDrawer);
            HideDrawer(movementCostDrawer);
            HideDrawer(abilityRangeDrawer);
            HideDrawer(enemyIntentDrawer);
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