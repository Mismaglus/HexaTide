using UnityEngine;
using Core.Hex;
using Game.Units;
using Game.Grid;

namespace Game.Battle
{
    [DisallowMultipleComponent]
    public class UnitSpawner : MonoBehaviour
    {
        public BattleHexGrid grid;
        public SelectionManager selection;
        public BattleUnit unitPrefab;

        [Header("Spawn Coords")]
        public int startQ = 2;
        public int startR = 2;

        void Reset()
        {
            if (!grid) grid = FindFirstObjectByType<BattleHexGrid>(FindObjectsInactive.Exclude);
            if (!selection) selection = FindFirstObjectByType<SelectionManager>(FindObjectsInactive.Exclude);
        }

        [ContextMenu("Spawn Now")]
        public void SpawnNow()
        {
            if (!grid || !unitPrefab)
            {
                Debug.LogWarning("Missing grid or prefab.");
                return;
            }

            // 1) 实例化
            var go = Instantiate(unitPrefab.gameObject, Vector3.zero, Quaternion.identity, transform);

            // 2) 用通用 Unit 初始化到指定格
            var unit = go.GetComponent<Unit>();
            if (!unit)
            {
                Debug.LogError("Spawned prefab lacks Unit component.");
                return;
            }
            var c = new HexCoords(startQ, startR);
            unit.Initialize(grid, c);

            // 3) 注册占位
            var sel = selection ? selection : Object.FindFirstObjectByType<SelectionManager>();
            sel?.RegisterUnit(unit);

            // 4) ? 修复：初始化资源 (AP/MP/Stride)
            // 使用 BattleUnit.ResetTurnResources 来代替旧的 mover.ResetStride
            var battleUnit = go.GetComponent<BattleUnit>();
            if (battleUnit)
            {
                battleUnit.ResetTurnResources();
            }
            else
            {
                // 如果没有 BattleUnit (纯 Unit)，尝试手动重置 Attributes
                var attrs = go.GetComponent<UnitAttributes>();
                if (attrs) attrs.Core.CurrentStride = attrs.Core.Stride;
            }
        }
    }
}