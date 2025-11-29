using UnityEngine;
using System.Collections.Generic;
using Core.Hex;
using Game.Units;
using Game.Grid;
using Game.Battle.Units;
using Game.Common;

namespace Game.Battle
{
    /// <summary>
    /// 战争迷雾系统：负责视野计算、单位显隐、残影和波纹感知。
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
        [Tooltip("Ghost Prefab: 一个简单的带SpriteRenderer的物体，用于显示敌人残影")]
        public GameObject ghostPrefab;

        // --- Runtime Data ---
        private HashSet<HexCoords> _visibleTiles = new HashSet<HexCoords>();
        private HashSet<HexCoords> _visitedTiles = new HashSet<HexCoords>();

        // 残影字典: Key=UnitID (InstanceID), Value=GhostInstance
        private Dictionary<int, GameObject> _activeGhosts = new Dictionary<int, GameObject>();

        private List<BattleUnit> _allUnitsCache = new List<BattleUnit>();
        private bool _dirty = false;

        void Awake()
        {
            Instance = this;
            if (!grid) grid = FindFirstObjectByType<BattleHexGrid>();
            if (!occupancy) occupancy = FindFirstObjectByType<GridOccupancy>();
            if (!highlighter) highlighter = FindFirstObjectByType<HexHighlighter>();
        }

        void Start()
        {
            // 监听单位移动
            var allUnits = FindObjectsByType<Unit>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var u in allUnits)
            {
                RegisterUnitEvents(u);
            }

            // 初始计算
            RefreshFog();
        }

        void RegisterUnitEvents(Unit u)
        {
            u.OnMoveFinished -= OnUnitMoveStep;
            u.OnMoveFinished += OnUnitMoveStep;
        }

        // 单位每走一步都会触发
        void OnUnitMoveStep(Unit u, HexCoords from, HexCoords to)
        {
            var bu = u.GetComponent<BattleUnit>();
            if (bu == null) return;

            if (bu.isPlayer)
            {
                // 玩家移动：刷新视野
                RefreshFog();
            }
            else
            {
                // 敌人移动：检查是否触发波纹或显隐
                UpdateEnemyVisibility(bu, from, to);
            }
        }

        // === 核心逻辑：刷新全场迷雾 ===
        public void RefreshFog()
        {
            if (grid == null) return;

            _visibleTiles.Clear();

            // 1. 收集所有我方单位
            var playerUnits = new List<BattleUnit>();
            var allObjs = FindObjectsByType<BattleUnit>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            _allUnitsCache.Clear();
            _allUnitsCache.AddRange(allObjs);

            foreach (var u in _allUnitsCache)
            {
                if (u.isPlayer) playerUnits.Add(u);
            }

            // 2. 计算视野 (Flood Fill)
            foreach (var pu in playerUnits)
            {
                int range = 6; // 默认
                if (pu.Attributes != null) range = pu.Attributes.Optional.SightRange;

                // 计算该单位的视野并合并到总视野
                var visibleCells = ComputeVisibility(pu.UnitRef.Coords, range);
                _visibleTiles.UnionWith(visibleCells);
            }

            // 3. 记录已探索
            _visitedTiles.UnionWith(_visibleTiles);

            // 4. 更新所有格子的状态
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

            // 5. 更新所有单位的显隐状态
            foreach (var u in _allUnitsCache)
            {
                if (u.isPlayer)
                {
                    SetUnitVisible(u, true);
                }
                else
                {
                    bool isVisible = _visibleTiles.Contains(u.UnitRef.Coords);

                    if (isVisible)
                    {
                        SetUnitVisible(u, true);
                        RemoveGhost(u); // 看见本体了，删掉残影
                    }
                    else
                    {
                        // 如果刚刚从可见变为不可见（在UpdateEnemyVisibility里处理了移动），这里主要处理静态
                        SetUnitVisible(u, false);
                    }
                }
            }
        }

        // === 敌人移动时的特殊处理 (波纹 & 残影) ===
        void UpdateEnemyVisibility(BattleUnit enemy, HexCoords from, HexCoords to)
        {
            bool toVisible = _visibleTiles.Contains(to);
            bool fromVisible = _visibleTiles.Contains(from);

            if (toVisible)
            {
                // 走进视野：显示，删残影
                SetUnitVisible(enemy, true);
                RemoveGhost(enemy);
            }
            else
            {
                // 在迷雾中
                SetUnitVisible(enemy, false);

                // 1. 处理残影：如果从可见区域离开，在起点留下残影
                if (fromVisible)
                {
                    CreateGhost(enemy, from);
                }

                // 2. 处理波纹：如果不可见，但处于感知范围内
                if (IsSensed(to))
                {
                    if (highlighter) highlighter.TriggerRipple(to);
                }
            }
        }

        // 检查是否在感知范围内 (Visible range + Sense Bonus)
        bool IsSensed(HexCoords c)
        {
            // 简单优化：只检查它是否在大一点的范围内
            // 严谨做法是重新跑一遍大范围的 BFS，这里为了性能，简单判断是否邻接可见区域
            // 或者遍历所有玩家计算距离
            foreach (var u in _allUnitsCache)
            {
                if (!u.isPlayer) continue;
                int range = u.Attributes.Optional.SightRange + senseRangeBonus;
                if (u.UnitRef.Coords.DistanceTo(c) <= range) return true;
            }
            return false;
        }

        // === 视野计算算法 (BFS with Obstacles) ===
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

                // 检查当前格子是否阻挡视线 (墙壁/树林)
                // 注意：如果 center 是障碍物(比如站在树林里)，它还是能看出去的
                // 规则：视线在遇到障碍物时停止，但障碍物本身是可见的。
                bool currentBlocks = IsBlocking(current) && !current.Equals(center);
                if (currentBlocks) continue; // 视线被挡住，不再扩散

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

        bool IsBlocking(HexCoords c)
        {
            // 需要获取 Cell 数据
            // 这里假设 GridOccupancy 不能用(那是单位)，要用 Grid 找 Cell
            // 为了性能，最好缓存 Cell Map。这里先动态找。
            // 优化：BattleHexGrid 应该提供 GetCell
            // 这里临时用 TileTag 查找，或者直接在 BattleHexGrid 里缓存了 HexCell
            // 简单起见，假设所有格子都在 Grid 的子物体里
            // *实际项目中建议在 BattleHexGrid 建立 Dictionary<HexCoords, HexCell>*
            return false; // 暂时默认不阻挡，稍后请在 BattleHexGrid 增加 GetCell 方法
        }

        // === 单位显隐与残影 ===

        void SetUnitVisible(BattleUnit unit, bool visible)
        {
            var visual = unit.GetComponent<UnitVisual2D>();
            if (visual) visual.SetVisible(visible);
        }

        void CreateGhost(BattleUnit unit, HexCoords pos)
        {
            if (ghostPrefab == null) return;

            int id = unit.GetInstanceID();
            // 如果已有残影，移动它
            if (_activeGhosts.TryGetValue(id, out var go))
            {
                if (go != null)
                {
                    UpdateGhostPos(go, pos);
                    return;
                }
            }

            // 新建残影
            var newGhost = Instantiate(ghostPrefab, transform);
            newGhost.name = $"Ghost_{unit.name}";

            // 设置 Sprite
            var srcSprite = unit.GetComponentInChildren<SpriteRenderer>();
            var dstSprite = newGhost.GetComponentInChildren<SpriteRenderer>();
            if (srcSprite && dstSprite)
            {
                dstSprite.sprite = srcSprite.sprite;
                dstSprite.color = new Color(0.5f, 0.5f, 0.5f, 0.7f); // 灰色半透明
            }

            UpdateGhostPos(newGhost, pos);
            _activeGhosts[id] = newGhost;
        }

        void RemoveGhost(BattleUnit unit)
        {
            int id = unit.GetInstanceID();
            if (_activeGhosts.TryGetValue(id, out var go))
            {
                if (go != null) Destroy(go);
                _activeGhosts.Remove(id);
            }
        }

        void UpdateGhostPos(GameObject ghost, HexCoords c)
        {
            if (grid != null)
            {
                ghost.transform.position = grid.GetTileWorldPosition(c);
            }
        }
    }
}