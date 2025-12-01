using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Core.Hex;
using Game.Units;
using Game.Grid;
using Game.Common;
using Game.Core;

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
        private HashSet<HexCoords> _sensedTilesThisTurn = new HashSet<HexCoords>(); // 本回合感知到的敌人位置
        private HashSet<int> _movedUnitsThisTurn = new HashSet<int>(); // 记录本回合移动过的单位ID

        private List<BattleUnit> _allUnitsCache = new List<BattleUnit>();
        private BattleStateMachine _sm;

        void CleanupUnitCache()
        {
            for (int i = _allUnitsCache.Count - 1; i >= 0; i--)
            {
                if (!IsUnitAlive(_allUnitsCache[i]))
                {
                    _allUnitsCache.RemoveAt(i);
                }
            }
        }

        bool IsUnitAlive(BattleUnit unit)
        {
            if (!unit) return false;
            var attributes = unit.Attributes;
            if (attributes == null) return false;
            return attributes.Core.HP > 0;
        }

        void Awake()
        {
            Instance = this;
            if (!grid) grid = FindFirstObjectByType<BattleHexGrid>();
            if (!occupancy) occupancy = FindFirstObjectByType<GridOccupancy>();
            if (!highlighter) highlighter = FindFirstObjectByType<HexHighlighter>();
            _sm = BattleStateMachine.Instance ?? FindFirstObjectByType<BattleStateMachine>();
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

            if (_sm != null) _sm.OnTurnChanged += HandleTurnChanged;
        }

        void OnDestroy()
        {
            if (_sm != null) _sm.OnTurnChanged -= HandleTurnChanged;
        }

        void HandleTurnChanged(TurnSide side)
        {
            if (side == TurnSide.Enemy)
            {
                // 敌人回合开始：
                // 不再清除感知标记，保留上一回合的侦测结果，直到敌人真正移动或状态改变
                // _sensedTilesThisTurn.Clear(); 
                // if (highlighter) highlighter.SetSensedTiles(null);

                _movedUnitsThisTurn.Clear();
            }
            else if (side == TurnSide.Player)
            {
                // 玩家回合开始 (即敌人回合结束结算)：

                // 1. 刷新迷雾，计算所有敌人的最新位置是否在感知范围内
                // 这会更新 _sensedTilesThisTurn 并通过 SetSensedTiles 设置持久高亮 (保留 Ripple Color)
                RefreshFog();

                // 2. 针对每个敌人进行波纹检测
                foreach (var enemy in _allUnitsCache)
                {
                    if (enemy == null || enemy.isPlayer) continue;

                    bool isSensed = _sensedTilesThisTurn.Contains(enemy.UnitRef.Coords);
                    bool hasMoved = _movedUnitsThisTurn.Contains(enemy.GetInstanceID());

                    if (isSensed)
                    {
                        if (!hasMoved)
                        {
                            // 如果他在sense range内 且 没有移动过：
                            // Trigger Ripple (闪烁一次) 且保留 Ripple Color (由上面的 RefreshFog 处理)
                            if (highlighter) highlighter.TriggerRipple(enemy.UnitRef.Coords);
                        }
                        else
                        {
                            // 如果移动过（也就是说ripple color已经绘制过了）：
                            // 单纯的保留 Ripple Color (由上面的 RefreshFog 处理，此处无需操作)
                        }
                    }
                }
            }
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

            _movedUnitsThisTurn.Add(bu.GetInstanceID()); // 记录移动过的单位

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
            _sensedTilesThisTurn.Clear(); // 重新计算感知列表

            // 1. 收集单位
            var playerUnits = new List<BattleUnit>();
            var allObjs = FindObjectsByType<BattleUnit>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            _allUnitsCache.Clear();
            foreach (var unit in allObjs)
            {
                if (IsUnitAlive(unit)) _allUnitsCache.Add(unit);
            }

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

                    // 如果不可见，检查是否在感知范围内
                    if (!isVisibleNow && IsSensed(u.UnitRef.Coords))
                    {
                        _sensedTilesThisTurn.Add(u.UnitRef.Coords);
                    }
                }
            }

            // 将感知到的位置传递给高亮器 (持久显示)
            if (highlighter) highlighter.SetSensedTiles(_sensedTilesThisTurn);
        }

        // === 敌人移动时的特殊处理 (波纹) ===
        void UpdateEnemyVisibility(BattleUnit enemy, HexCoords from, HexCoords to)
        {
            // 敌人开始移动（或移动了一步）：立即移除它原位置的感知高亮
            // 这样可以满足“只有在敌人移动的时候才消除掉ripple”的需求
            if (_sensedTilesThisTurn.Contains(from))
            {
                _sensedTilesThisTurn.Remove(from);
                if (highlighter) highlighter.SetSensedTiles(_sensedTilesThisTurn);
            }

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
            CleanupUnitCache();

            foreach (var u in _allUnitsCache)
            {
                if (!IsUnitAlive(u)) continue;

                var unitRef = u.UnitRef;
                if (unitRef == null) continue;

                var faction = unitRef.Faction;
                if (faction == null || faction.side != Side.Player) continue;

                int range = u.Attributes != null ? u.Attributes.Optional.SightRange + senseRangeBonus : senseRangeBonus;
                if (unitRef.Coords.DistanceTo(c) <= range) return true;
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
            if (!unit) return;

            var renderers = unit.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers) r.enabled = visible;

            var canvases = unit.GetComponentsInChildren<Canvas>(true);
            foreach (var c in canvases) c.enabled = visible;
        }

        public void OnUnitDied(BattleUnit unit)
        {
            if (unit == null) return;

            _allUnitsCache.Remove(unit);
            CleanupUnitCache();
            RefreshFog();
        }
    }
}
