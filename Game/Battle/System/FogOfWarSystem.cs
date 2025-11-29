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

        // 残影字典
        private Dictionary<int, GameObject> _activeGhosts = new Dictionary<int, GameObject>();
        // 可见单位缓存
        private HashSet<int> _visibleUnitIDs = new HashSet<int>();

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
            yield return null;

            var allUnits = FindObjectsByType<Unit>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var u in allUnits)
            {
                RegisterUnitEvents(u);
            }

            RefreshFog();
        }

        // ⭐⭐ 新增 API：查询格子是否可见 ⭐⭐
        public bool IsTileVisible(HexCoords c)
        {
            return _visibleTiles.Contains(c);
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
                RefreshFog();
            }
            else
            {
                UpdateEnemyVisibility(bu, from, to);
            }
        }

        public void RefreshFog()
        {
            if (grid == null) return;

            _visibleTiles.Clear();

            var playerUnits = new List<BattleUnit>();
            var allObjs = FindObjectsByType<BattleUnit>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            _allUnitsCache.Clear();
            _allUnitsCache.AddRange(allObjs);

            foreach (var u in _allUnitsCache)
            {
                if (u.isPlayer) playerUnits.Add(u);
            }

            foreach (var pu in playerUnits)
            {
                int range = 6; // 默认
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
                else if (_visitedTiles.Contains(cell.Coords))
                {
                    cell.SetFogStatus(FogStatus.Ghost);
                }
                else
                {
                    cell.SetFogStatus(FogStatus.Unknown);
                }
            }

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
                        SetUnitVisible(u, true);
                        RemoveGhost(u);
                        currentVisibleIDs.Add(uid);
                    }
                    else
                    {
                        SetUnitVisible(u, false);
                        // 如果刚丢失视野 -> 生成残影
                        if (_visibleUnitIDs.Contains(uid))
                        {
                            CreateGhost(u, u.UnitRef.Coords);
                        }
                    }
                }
            }
            _visibleUnitIDs = currentVisibleIDs;
        }

        void UpdateEnemyVisibility(BattleUnit enemy, HexCoords from, HexCoords to)
        {
            bool toVisible = _visibleTiles.Contains(to);
            bool fromVisible = _visibleTiles.Contains(from);
            int uid = enemy.GetInstanceID();

            if (toVisible)
            {
                SetUnitVisible(enemy, true);
                RemoveGhost(enemy);
                _visibleUnitIDs.Add(uid);
            }
            else
            {
                SetUnitVisible(enemy, false);
                _visibleUnitIDs.Remove(uid);

                if (fromVisible)
                {
                    CreateGhost(enemy, from);
                }

                if (IsSensed(to))
                {
                    if (highlighter) highlighter.TriggerRipple(to);
                }
            }
        }

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

        void CreateGhost(BattleUnit unit, HexCoords pos)
        {
            if (ghostPrefab == null) return;

            int id = unit.GetInstanceID();
            if (_activeGhosts.TryGetValue(id, out var go))
            {
                if (go != null)
                {
                    UpdateGhostPos(go, pos);
                    return;
                }
            }

            var newGhost = Instantiate(ghostPrefab, transform);
            newGhost.name = $"Ghost_{unit.name}";

            var srcSprite = unit.GetComponentInChildren<SpriteRenderer>();
            var dstSprite = newGhost.GetComponentInChildren<SpriteRenderer>();
            if (dstSprite == null) dstSprite = newGhost.GetComponentInChildren<SpriteRenderer>(true);

            if (srcSprite && dstSprite)
            {
                dstSprite.sprite = srcSprite.sprite;
                dstSprite.flipX = srcSprite.flipX;

                var c = srcSprite.color;
                // 设置为灰色，表示“记忆”
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