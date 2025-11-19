using System.Collections.Generic;
using UnityEngine;
using Core.Hex;          // HexCoords
using Game.Units;        // Unit

namespace Game.Grid
{
    /// 维护“哪个格子被哪个单位占据”的唯一真相
    public class GridOccupancy : MonoBehaviour
    {
        private readonly Dictionary<HexCoords, Unit> _map = new();

        // 查询
        public bool HasUnitAt(HexCoords c) => _map.ContainsKey(c);
        public bool IsEmpty(HexCoords c) => !_map.ContainsKey(c);
        public bool TryGetUnitAt(HexCoords c, out Unit u) => _map.TryGetValue(c, out u);

        // 注册/反注册
        public void Register(Unit u)
        {
            if (u == null) return;

            // 清理旧记录
            HexCoords oldKey = default;
            bool found = false;
            foreach (var kv in _map)
            {
                if (kv.Value == u) { oldKey = kv.Key; found = true; break; }
            }
            if (found) _map.Remove(oldKey);

            _map[u.Coords] = u;

            u.OnMoveFinished -= OnUnitMoved; // 防重复订阅
            u.OnMoveFinished += OnUnitMoved;
        }

        public void Unregister(Unit u)
        {
            if (u == null) return;
            u.OnMoveFinished -= OnUnitMoved;

            HexCoords key = default;
            bool found = false;
            foreach (var kv in _map)
            {
                if (kv.Value == u) { key = kv.Key; found = true; break; }
            }
            if (found) _map.Remove(key);
        }

        // 读档/传送/初始化后手动同步
        public void SyncUnit(Unit u)
        {
            if (u == null) return;
            Unregister(u);
            _map[u.Coords] = u;
            u.OnMoveFinished -= OnUnitMoved;
            u.OnMoveFinished += OnUnitMoved;
        }

        public void ClearAll() => _map.Clear();

        private void OnUnitMoved(Unit u, HexCoords from, HexCoords to)
        {
            if (_map.TryGetValue(from, out var who) && who == u) _map.Remove(from);
            _map[to] = u;
        }
    }
}
