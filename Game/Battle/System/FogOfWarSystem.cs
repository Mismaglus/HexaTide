using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Core.Hex;
using Game.Units;
using Game.Grid;
using Game.Common;

namespace Game.Battle
{
    /// <summary>
    /// 战争迷雾系统：负责视野计算、单位显隐和波纹感知。
    /// (已移除单位残影/Ghost逻辑，仅保留地形记忆和波纹)
    /// </summary>
    public class FogOfWarSystem : MonoBehaviour
    {
        public static FogOfWarSystem Instance { get; private set; }

        [Header("References")]
        public BattleHexGrid grid;
        public GridOccupancy occupancy;
        public HexHighlighter highlighter;

        [Header("Config")]
        public int senseRangeBonus = 2; // 感知范围 = 视野 + 2

        // --- Runtime Data ---
        private HashSet<HexCoords> _visibleTiles = new HashSet<HexCoords>();
        private HashSet<HexCoords> _visitedTiles = new HashSet<HexCoords>();

        private List<BattleUnit> _allUnitsCache = new List<BattleUnit>();

        void Awake()
        {
            Instance = this;
            if (!grid) grid = FindFirstObjectByType<BattleHexGrid>();
            if (!occupancy) occupancy = FindFirstObjectByType<GridOccupancy>();
            if (!highlighter) highlighter = FindFirstObjectByType<HexHighlighter>();
        }

        IEnumerator Start()
        {
            // 等待一帧，确保 Grid 生成完毕
            yield return null;

            var allUnits = FindObjectsByType<Unit>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var u in allUnits)
            {
                RegisterUnitEvents(u);
            }

            RefreshFog();
        }

        // API：查询格子是否可见 (用于交互限制，如禁止选中不可见单位)
        public bool IsTileVisible(HexCoords c)
        {
            return _visibleTiles.Contains(c);
        }

        // API：查询格子是否已探索 (用于 UI 过滤，如只显示已探索区域的攻击预警)
        public bool IsTileExplored(HexCoords c)
        {
            return _visibleTiles.Contains(c) || _visitedTiles.Contains(c);
        }

        void RegisterUnitEvents(Unit u)
        {
            u.OnMoveFinished -= OnUnitMoveStep;
            u.OnMoveFinished += OnUnitMoveStep;
        }

        void OnUnitMoveStep(Unit u, HexCoords from, HexCoords to)
        {
            var bu = u.GetComponent<BattleUnit>();
            if (bu == null) return;

            if (bu.isPlayer)
            {
                // 玩家移动：刷新全场视野
                RefreshFog();
            }
            else
            {
                // 敌人移动：只更新显隐和波纹
                UpdateEnemyVisibility(bu, from, to);
            }
        }

        // === 核心逻辑：刷新全场迷雾 ===
        public void RefreshFog()
        {
            if (grid == null) return;

            _visibleTiles.Clear();

            // 1. 收集单位
            var playerUnits = new List<BattleUnit>();
            var allObjs = FindObjectsByType<BattleUnit>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            _allUnitsCache.Clear();
            _allUnitsCache.AddRange(allObjs);

            foreach (var u in _allUnitsCache)
            {
                if (u.isPlayer) playerUnits.Add(u);
            }

            // 2. 计算玩家视野
            foreach (var pu in playerUnits)
            {
                int range = 6;
                if (pu.Attributes != null) range = pu.Attributes.Optional.SightRange;

                var visibleCells = ComputeVisibility(pu.UnitRef.Coords, range);
                _visibleTiles.UnionWith(visibleCells);
            }

            _visitedTiles.UnionWith(_visibleTiles);

            // 3. 更新地形迷雾状态 (Visible / Ghost / Unknown)
            // 地形依然保留 Ghost 状态(变灰)，代表"我去过这里，我知道这里是墙还是地"
            foreach (var tile in grid.EnumerateTiles())
            {
                var cell = tile.GetComponent<HexCell>();
                if (cell == null) continue;

                if (_visibleTiles.Contains(cell.Coords))
                {
                    cell.SetFogStatus(FogStatus.Visible);
                }
                else if (_visitedTiles.Contains(cell.Coords))
                {
                    cell.SetFogStatus(FogStatus.Ghost);
                }
                else
                {
                    cell.SetFogStatus(FogStatus.Unknown);
                }
            }

            // 4. 更新单位显隐
            foreach (var u in _allUnitsCache)
            {
                if (u.isPlayer)
                {
                    SetUnitVisible(u, true);
                }
                else
                {
                    // 简单粗暴：如果在视野里就显示，不在就隐藏
                    bool isVisibleNow = _visibleTiles.Contains(u.UnitRef.Coords);
                    SetUnitVisible(u, isVisibleNow);
                }
            }
        }

        // === 敌人移动时的特殊处理 (波纹) ===
        void UpdateEnemyVisibility(BattleUnit enemy, HexCoords from, HexCoords to)
        {
            bool toVisible = _visibleTiles.Contains(to);

            if (toVisible)
            {
                // 走进视野 -> 显形
                SetUnitVisible(enemy, true);
            }
            else
            {
                // 走进迷雾 -> 隐形
                SetUnitVisible(enemy, false);

                // 触发波纹：如果在视野外但在感知内
                if (IsSensed(to))
                {
                    if (highlighter) highlighter.TriggerRipple(to);
                }
            }
        }

        // 检查是否在感知范围内 (Visible range + Sense Bonus)
        bool IsSensed(HexCoords c)
        {
            foreach (var u in _allUnitsCache)
            {
                if (!u.isPlayer) continue;
                int range = u.Attributes.Optional.SightRange + senseRangeBonus;
                if (u.UnitRef.Coords.DistanceTo(c) <= range) return true;
            }
            return false;
        }

        // === 视野计算算法 (BFS) ===
        HashSet<HexCoords> ComputeVisibility(HexCoords center, int range)
        {
            var visible = new HashSet<HexCoords>();
            visible.Add(center);

            var queue = new Queue<HexCoords>();
            queue.Enqueue(center);

            var distances = new Dictionary<HexCoords, int>();
            distances[center] = 0;

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                int d = distances[current];

                if (d >= range) continue;

                // 暂时不计算地形阻挡 (BlocksSight)，如果以后有墙壁再加

                foreach (var neighbor in current.Neighbors())
                {
                    if (distances.ContainsKey(neighbor)) continue;

                    distances[neighbor] = d + 1;
                    visible.Add(neighbor);
                    queue.Enqueue(neighbor);
                }
            }

            return visible;
        }

        // === 单位显隐控制 ===
        void SetUnitVisible(BattleUnit unit, bool visible)
        {
            var renderers = unit.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers) r.enabled = visible;

            var canvases = unit.GetComponentsInChildren<Canvas>(true);
            foreach (var c in canvases) c.enabled = visible;
        }
    }
}