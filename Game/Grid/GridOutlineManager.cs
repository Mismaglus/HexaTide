using UnityEngine;
using System.Collections.Generic;
using Core.Hex;
using Game.UI; // 引用 RangeOutlineDrawer

namespace Game.Battle
{
    public enum OutlineState
    {
        None,           // 无状态
        Movement,       // 选中单位，规划移动 (显示移动范围 + 敌方意图)
        AbilityTargeting // 释放技能中 (只显示技能范围)
    }

    public class GridOutlineManager : MonoBehaviour
    {
        [Header("Drawers (Scene References)")]
        public RangeOutlineDrawer movementFreeDrawer;
        public RangeOutlineDrawer movementCostDrawer;
        public RangeOutlineDrawer abilityRangeDrawer;
        [Tooltip("将来用于显示敌方AOE或警戒范围")]
        public RangeOutlineDrawer enemyIntentDrawer;

        // 缓存各层的数据
        private readonly HashSet<HexCoords> _moveFree = new();
        private readonly HashSet<HexCoords> _moveCost = new();
        private readonly HashSet<HexCoords> _abilityRange = new();
        private readonly HashSet<HexCoords> _enemyIntent = new();

        private OutlineState _currentState = OutlineState.None;

        // === 数据输入接口 ===

        public void SetMovementRange(IEnumerable<HexCoords> free, IEnumerable<HexCoords> cost)
        {
            _moveFree.Clear();
            _moveCost.Clear();
            if (free != null) _moveFree.UnionWith(free);
            if (cost != null) _moveCost.UnionWith(cost);
            RefreshVisuals();
        }

        public void ClearMovementRange()
        {
            _moveFree.Clear();
            _moveCost.Clear();
            RefreshVisuals();
        }

        public void SetAbilityRange(IEnumerable<HexCoords> range)
        {
            _abilityRange.Clear();
            if (range != null) _abilityRange.UnionWith(range);
            RefreshVisuals();
        }

        public void ClearAbilityRange()
        {
            _abilityRange.Clear();
            RefreshVisuals();
        }

        // 预留给未来的敌方意图系统调用
        public void SetEnemyIntent(IEnumerable<HexCoords> dangerZone)
        {
            _enemyIntent.Clear();
            if (dangerZone != null) _enemyIntent.UnionWith(dangerZone);
            RefreshVisuals();
        }

        // === 状态控制接口 ===

        public void SetState(OutlineState newState)
        {
            if (_currentState == newState) return;
            _currentState = newState;
            RefreshVisuals();
        }

        // === 核心显示逻辑 ===

        private void RefreshVisuals()
        {
            switch (_currentState)
            {
                case OutlineState.None:
                    HideAll();
                    break;

                case OutlineState.Movement:
                    // 移动模式：显示移动范围
                    ShowDrawer(movementFreeDrawer, _moveFree);
                    ShowDrawer(movementCostDrawer, _moveCost);
                    // 移动模式：允许显示敌方意图 (方便躲避)
                    ShowDrawer(enemyIntentDrawer, _enemyIntent);
                    // 隐藏技能范围
                    HideDrawer(abilityRangeDrawer);
                    break;

                case OutlineState.AbilityTargeting:
                    // 瞄准模式：只显示技能范围
                    ShowDrawer(abilityRangeDrawer, _abilityRange);

                    // 瞄准模式：强制隐藏移动范围 (根据你的需求)
                    HideDrawer(movementFreeDrawer);
                    HideDrawer(movementCostDrawer);

                    // 瞄准模式：默认隐藏敌方意图 (根据你的需求，让视野清晰)
                    HideDrawer(enemyIntentDrawer);
                    break;
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

        void HideAll()
        {
            HideDrawer(movementFreeDrawer);
            HideDrawer(movementCostDrawer);
            HideDrawer(abilityRangeDrawer);
            HideDrawer(enemyIntentDrawer);
        }
    }
}