using UnityEngine;
using System.Collections.Generic;
using Core.Hex;
using Game.Units;
using Game.Grid;
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

        // ⭐ 新增：记录上一帧哪些单位是可见的，用于判断“刚刚丢失视野”
        private HashSet<int> _visibleUnitIDs = new HashSet<int>();

        private List<BattleUnit> _allUnitsCache = new List<BattleUnit>();

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

            // 1. 准备数据
            _visibleTiles.Clear();
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
                int range = 6;
                if (pu.Attributes != null) range = pu.Attributes.Optional.SightRange;

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

            // 5. 更新所有单位的显隐状态 & 处理视野丢失时的残影
            // ⭐ 新增：准备新的可见列表
            var currentVisibleIDs = new HashSet<int>();

            foreach (var u in _allUnitsCache)
            {
                int uid = u.GetInstanceID();

                if (u.isPlayer)
                {
                    SetUnitVisible(u, true);
                    currentVisibleIDs.Add(uid);
                }
                else
                {
                    bool isVisibleNow = _visibleTiles.Contains(u.UnitRef.Coords);

                    if (isVisibleNow)
                    {
                        // 现在可见
                        SetUnitVisible(u, true);
                        RemoveGhost(u);
                        currentVisibleIDs.Add(uid);
                    }
                    else
                    {
                        // 现在不可见
                        SetUnitVisible(u, false);

                        // ⭐ 关键修复：如果我们上一帧还能看见它 (在 _visibleUnitIDs 里)，说明是刚刚走远丢失视野
                        // 此时应该在它当前位置生成残影
                        if (_visibleUnitIDs.Contains(uid))
                        {
                            CreateGhost(u, u.UnitRef.Coords);
                        }
                    }
                }
            }

            // ⭐ 更新缓存供下一帧对比
            _visibleUnitIDs = currentVisibleIDs;
        }

        // === 敌人移动时的特殊处理 (波纹 & 残影) ===
        void UpdateEnemyVisibility(BattleUnit enemy, HexCoords from, HexCoords to)
        {
            bool toVisible = _visibleTiles.Contains(to);
            bool fromVisible = _visibleTiles.Contains(from);

            int uid = enemy.GetInstanceID();

            if (toVisible)
            {
                // 走进视野
                SetUnitVisible(enemy, true);
                RemoveGhost(enemy);
                _visibleUnitIDs.Add(uid); // 标记为可见
            }
            else
            {
                // 在迷雾中
                SetUnitVisible(enemy, false);
                _visibleUnitIDs.Remove(uid); // 标记为不可见

                // 1. 处理残影：如果从可见区域离开，在起点留下残影
                if (fromVisible)
                {
                    CreateGhost(enemy, from);
                }

                // 2. 处理波纹
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

                // 简单起见，暂时不判断阻挡
                // bool currentBlocks = IsBlocking(current);
                // if (currentBlocks && !current.Equals(center)) continue;

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

        // === 单位显隐与残影 ===

        void SetUnitVisible(BattleUnit unit, bool visible)
        {
            // 遍历所有 Renderer 和 Canvas
            var renderers = unit.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers) r.enabled = visible;

            var canvases = unit.GetComponentsInChildren<Canvas>(true);
            foreach (var c in canvases) c.enabled = visible;
        }

        void CreateGhost(BattleUnit unit, HexCoords pos)
        {
            if (ghostPrefab == null) return;

            int id = unit.GetInstanceID();

            // 如果已有残影，更新位置即可
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

            // 复制 Sprite
            var srcSprite = unit.GetComponentInChildren<SpriteRenderer>();
            var dstSprite = newGhost.GetComponentInChildren<SpriteRenderer>();

            // 如果 Ghost Prefab 自身没有 SpriteRenderer，尝试在子物体找
            if (dstSprite == null) dstSprite = newGhost.GetComponentInChildren<SpriteRenderer>(true);

            if (srcSprite && dstSprite)
            {
                dstSprite.sprite = srcSprite.sprite;
                dstSprite.flipX = srcSprite.flipX; // 保持朝向

                // 设为灰色半透明
                var c = srcSprite.color;
                dstSprite.color = new Color(c.r * 0.5f, c.g * 0.5f, c.b * 0.5f, 0.6f);
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