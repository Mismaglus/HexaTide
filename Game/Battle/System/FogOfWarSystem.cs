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
    /// </summary>
    public class FogOfWarSystem : MonoBehaviour
    {
        public static FogOfWarSystem Instance { get; private set; }

        [Header("References")]
        public BattleHexGrid grid;
        public GridOccupancy occupancy;
        public HexHighlighter highlighter;

        // 引用状态机以监听回合切换
        private BattleStateMachine _battleSM;

        [Header("Config")]
        public bool enableFog = true; // If false, all tiles are visible
        // [Tooltip("感知范围加成：基础视野 + 此数值 = 波纹侦测范围")]
        // public int senseRangeBonus = 2; // 已移除，改用 UnitAttributes

        // --- Runtime Data ---
        private HashSet<HexCoords> _visibleTiles = new HashSet<HexCoords>();
        private HashSet<HexCoords> _visitedTiles = new HashSet<HexCoords>();
        private HashSet<BattleUnit> _movedEnemies = new HashSet<BattleUnit>();

        // 缓存所有战斗单位
        private List<BattleUnit> _allUnitsCache = new List<BattleUnit>();

        void Awake()
        {
            Instance = this;

            if (!grid) grid = FindFirstObjectByType<BattleHexGrid>();
            if (!occupancy) occupancy = FindFirstObjectByType<GridOccupancy>();
            if (!highlighter) highlighter = FindFirstObjectByType<HexHighlighter>();

            _battleSM = FindFirstObjectByType<BattleStateMachine>();
        }

        IEnumerator Start()
        {
            // 等待一帧，确保 Grid 生成完毕
            yield return null;

            if (_battleSM != null)
            {
                _battleSM.OnTurnChanged += HandleTurnChanged;
            }

            RefreshUnitCache();
            RefreshFog();
        }

        void OnDestroy()
        {
            if (_battleSM != null) _battleSM.OnTurnChanged -= HandleTurnChanged;

            foreach (var u in _allUnitsCache)
            {
                if (u != null && u.UnitRef != null)
                    u.UnitRef.OnMoveFinished -= OnUnitMoveStep;
            }
        }

        /// <summary>
        /// 刷新单位缓存并绑定移动事件
        /// </summary>
        public void RefreshUnitCache()
        {
            _allUnitsCache.Clear();
            var allObjs = FindObjectsByType<BattleUnit>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            foreach (var bu in allObjs)
            {
                _allUnitsCache.Add(bu);
                if (bu.UnitRef != null)
                {
                    bu.UnitRef.OnMoveFinished -= OnUnitMoveStep;
                    bu.UnitRef.OnMoveFinished += OnUnitMoveStep;
                }
            }
        }

        // ⭐⭐⭐ 修复核心：添加 OnUnitDied 方法供 BattleUnit 调用 ⭐⭐⭐
        public void OnUnitDied(BattleUnit unit)
        {
            if (unit == null) return;

            // 1. 从缓存移除
            if (_allUnitsCache.Contains(unit))
            {
                if (unit.UnitRef != null)
                    unit.UnitRef.OnMoveFinished -= OnUnitMoveStep;

                _allUnitsCache.Remove(unit);
            }

            // 2. 如果是玩家单位死了，视野会变小，立即刷新
            // 如果是敌人死了，不需要立即刷新(尸体不会动)，等下一次Refresh即可
            if (unit.isPlayer)
            {
                RefreshFog();
            }
        }

        // =========================================================
        // 事件回调
        // =========================================================

        void HandleTurnChanged(TurnSide side)
        {
            if (side == TurnSide.Enemy)
            {
                _movedEnemies.Clear();
                // 敌人回合开始时，不要清除持久化波纹，因为需要保留上回合移动后的位置提示
                // if (highlighter) highlighter.ClearPersistentRipples();
            }
            else if (side == TurnSide.Player)
            {
                PulseStaticEnemies();
            }
        }

        void OnUnitMoveStep(Unit u, HexCoords from, HexCoords to)
        {
            var bu = u.GetComponent<BattleUnit>();
            if (bu == null) return;

            if (bu.isPlayer)
            {
                RefreshFog();
            }
            else
            {
                _movedEnemies.Add(bu);
                UpdateEnemyVisibility(bu, from, to);
            }
        }

        // =========================================================
        // 核心逻辑
        // =========================================================

        public void RefreshFog()
        {
            if (grid == null) return;

            if (!enableFog)
            {
                // If fog is disabled, make everything visible
                foreach (var tile in grid.EnumerateTiles())
                {
                    var cell = tile.GetComponent<HexCell>();
                    if (cell) cell.SetFogStatus(FogStatus.Visible);
                }
                // Also update unit visibility so they are all shown
                foreach (var u in _allUnitsCache)
                {
                    if (u != null && u.UnitRef != null)
                    {
                        var vis = u.GetComponentInChildren<UnitHighlighter>(true);
                        if (vis) vis.SetVisible(true);
                    }
                }
                return;
            }

            _visibleTiles.Clear();

            var playerUnits = new List<BattleUnit>();
            // 清理无效引用
            for (int i = _allUnitsCache.Count - 1; i >= 0; i--)
            {
                if (_allUnitsCache[i] == null) _allUnitsCache.RemoveAt(i);
                else if (_allUnitsCache[i].isPlayer) playerUnits.Add(_allUnitsCache[i]);
            }

            foreach (var pu in playerUnits)
            {
                int range = 6;
                if (pu.Attributes != null) range = pu.Attributes.Optional.SightRange;

                var visibleCells = ComputeVisibility(pu.UnitRef.Coords, range);
                _visibleTiles.UnionWith(visibleCells);
            }

            _visitedTiles.UnionWith(_visibleTiles);

            foreach (var tile in grid.EnumerateTiles())
            {
                var cell = tile.GetComponent<HexCell>();
                if (cell == null) continue;

                if (_visibleTiles.Contains(cell.Coords))
                {
                    cell.SetFogStatus(FogStatus.Visible);
                }
                // else if (IsSensed(cell.Coords))
                // {
                //     cell.SetFogStatus(FogStatus.Sensed);
                // }
                else if (_visitedTiles.Contains(cell.Coords))
                {
                    cell.SetFogStatus(FogStatus.Ghost);
                }
                else
                {
                    cell.SetFogStatus(FogStatus.Unknown);
                }
            }

            foreach (var u in _allUnitsCache)
            {
                if (u == null) continue;

                if (u.isPlayer)
                {
                    SetUnitVisible(u, true);
                }
                else
                {
                    bool isVisibleNow = _visibleTiles.Contains(u.UnitRef.Coords);
                    SetUnitVisible(u, isVisibleNow);
                }
            }

            // 刷新完迷雾后，HexCell 的颜色被重置了，需要通知 Highlighter 重新应用波纹等效果
            if (highlighter) highlighter.ReapplyAll();
        }

        void UpdateEnemyVisibility(BattleUnit enemy, HexCoords from, HexCoords to)
        {
            bool toVisible = _visibleTiles.Contains(to);

            if (toVisible)
            {
                SetUnitVisible(enemy, true);
                if (highlighter)
                {
                    highlighter.SetPersistentRipple(to, false);
                    highlighter.SetPersistentRipple(from, false);
                }
            }
            else
            {
                SetUnitVisible(enemy, false);
                if (highlighter) highlighter.SetPersistentRipple(from, false);

                if (IsSensed(to))
                {
                    if (highlighter)
                    {
                        // 移动后的新位置，闪烁一次以提示玩家，然后转为持久化
                        // 动态调整波纹持续时间以匹配单位移动速度
                        float duration = 0.5f;
                        if (enemy.UnitRef != null) duration = enemy.UnitRef.secondsPerTile;

                        highlighter.TriggerRipple(to, duration, true);
                    }
                }
            }
        }

        void PulseStaticEnemies()
        {
            foreach (var u in _allUnitsCache)
            {
                if (u == null || u.isPlayer) continue;

                HexCoords pos = u.UnitRef.Coords;
                if (!_visibleTiles.Contains(pos) && IsSensed(pos))
                {
                    if (highlighter)
                    {
                        if (_movedEnemies.Contains(u))
                        {
                            // 如果移动过，确保是持久化波纹（静态）
                            highlighter.SetPersistentRipple(pos, true);
                        }
                        else
                        {
                            // 如果没移动过，强制闪烁一次然后转为持久化
                            highlighter.TriggerRipple(pos, 0.5f, true);
                        }
                    }
                }
            }
        }

        bool IsSensed(HexCoords c)
        {
            foreach (var u in _allUnitsCache)
            {
                if (u == null || !u.isPlayer) continue;

                int range = 6;
                int bonus = 0;
                if (u.Attributes != null)
                {
                    range = u.Attributes.Optional.SightRange;
                    bonus = u.Attributes.Optional.SenseRangeBonus;
                }
                int senseDist = range + bonus;

                if (u.UnitRef.Coords.DistanceTo(c) <= senseDist) return true;
            }
            return false;
        }

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

        void SetUnitVisible(BattleUnit unit, bool visible)
        {
            var renderers = unit.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers) r.enabled = visible;

            var canvases = unit.GetComponentsInChildren<Canvas>(true);
            foreach (var c in canvases) c.enabled = visible;
        }

        public bool IsTileVisible(HexCoords c)
        {
            if (!enableFog) return true;
            return _visibleTiles.Contains(c);
        }
        public bool IsTileExplored(HexCoords c) => _visibleTiles.Contains(c) || _visitedTiles.Contains(c);
    }
}