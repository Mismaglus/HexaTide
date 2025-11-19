using UnityEngine;
using Core.Hex;
using Game.Units;   // 新增：拿通用 Unit / UnitMover
using Game.Grid;    // 若需要用到 IHexGridProvider，可保留

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

            // 2) 用通用 Unit 初始化到指定格（注意：不是 BattleUnit）
            var unit = go.GetComponent<Unit>();
            if (!unit)
            {
                Debug.LogError("Spawned prefab lacks Unit component.");
                return;
            }
            var c = new HexCoords(startQ, startR);
            unit.Initialize(grid, c);   // BattleHexGrid 实现了 IHexGridProvider，可直接传

            // 3) 在这里加入“注册占位”的代码（关键两行）
            var sel = selection ? selection : Object.FindFirstObjectByType<SelectionManager>();
            sel?.RegisterUnit(unit);

            // 4) 可选：刷新步点，便于立刻可动
            var mover = go.GetComponent<UnitMover>();
            mover?.ResetStride();
        }
    }
}
