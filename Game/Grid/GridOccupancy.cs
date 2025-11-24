using System.Collections.Generic;
using UnityEngine;
using Core.Hex;
using Game.Units;

namespace Game.Grid
{
    public class GridOccupancy : MonoBehaviour
    {
        private readonly Dictionary<HexCoords, Unit> _map = new();

        void Start()
        {
            // 启动时自动搜集场景中的单位
            var allUnits = FindObjectsByType<Unit>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            int count = 0;
            foreach (var u in allUnits)
            {
                if (u != null) { Register(u); count++; }
            }
            Debug.Log($"[GridOccupancy] Auto-registered {count} units.");
        }

        public bool HasUnitAt(HexCoords c) => _map.ContainsKey(c);
        public bool IsEmpty(HexCoords c) => !_map.ContainsKey(c);
        public bool TryGetUnitAt(HexCoords c, out Unit u) => _map.TryGetValue(c, out u);

        public void Register(Unit u)
        {
            if (u == null) return;

            // 注册前先清理旧记录，防止重复
            RemoveByValue(u);

            _map[u.Coords] = u;

            u.OnMoveFinished -= OnUnitMoved;
            u.OnMoveFinished += OnUnitMoved;
        }

        public void Unregister(Unit u)
        {
            if (u == null) return;
            u.OnMoveFinished -= OnUnitMoved;
            RemoveByValue(u);
        }

        public void SyncUnit(Unit u)
        {
            if (u == null) return;
            Register(u); // Register 内部包含了清理逻辑
        }

        public void ClearAll() => _map.Clear();

        private void OnUnitMoved(Unit u, HexCoords from, HexCoords to)
        {
            // 1. 尝试正常移除
            bool removed = false;
            if (_map.TryGetValue(from, out var who) && who == u)
            {
                _map.Remove(from);
                removed = true;
            }

            // ? 2. 安全网：如果正常移除失败（比如 from 参数传错，或者不同步），
            // 必须全表扫描确保该单位没有残留在旧位置，否则会产生“分身”
            if (!removed)
            {
                // Debug.LogWarning($"[GridOccupancy] Unit {u.name} moved but wasn't found at 'from' {from}. Cleaning up safely.");
                RemoveByValue(u);
            }

            // 3. 写入新位置
            _map[to] = u;
        }

        // 辅助：通过 Value 移除 Key（略慢，但安全）
        private void RemoveByValue(Unit u)
        {
            HexCoords keyToRemove = default;
            bool found = false;

            foreach (var kv in _map)
            {
                if (kv.Value == u)
                {
                    keyToRemove = kv.Key;
                    found = true;
                    break; // 假设一个单位只能占一个格子
                }
            }

            if (found) _map.Remove(keyToRemove);
        }
    }
}