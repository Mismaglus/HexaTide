using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Core.Hex;
using Game.Grid;
using Game.Units;

namespace Game.Battle
{
    public class BattleRules : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("任何实现了 IHexGridProvider 的网格组件（如 BattleHexGrid）")]
        [SerializeField] private MonoBehaviour gridComponent;

        [SerializeField] private SelectionManager selection;
        [SerializeField] private Game.Grid.GridOccupancy occupancy;

        private IHexGridProvider grid;

        void Awake()
        {
            grid = gridComponent as IHexGridProvider;
            if (selection == null)
                selection = FindFirstObjectByType<SelectionManager>(FindObjectsInactive.Exclude);
            if (grid == null)
                grid = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                        .OfType<IHexGridProvider>().FirstOrDefault();
        }

        // —— 阵营相关 —— //

        public bool IsPlayer(ITurnActor actor)
        {
            if (actor is MonoBehaviour mb &&
                mb.TryGetComponent(out BattleUnit bu))
                return bu.isPlayer;
            return false;
        }

        public bool IsEnemy(ITurnActor actor) => !IsPlayer(actor);

        // ? 新增：比较两个单位是否敌对
        public bool IsEnemy(Unit a, Unit b)
        {
            if (a == null || b == null) return false;
            var buA = a.GetComponent<BattleUnit>();
            var buB = b.GetComponent<BattleUnit>();
            if (buA == null || buB == null) return false; // 非战斗单位默认中立
            return buA.isPlayer != buB.isPlayer;
        }

        public bool CanSelect(Unit unit)
        {
            if (unit == null) return false;
            return unit.TryGetComponent(out BattleUnit bu) && bu.isPlayer;
        }

        // —— 格子/行走判定 —— //

        public bool Contains(HexCoords c)
        {
            if (grid == null) return false;
            return grid.EnumerateTiles().Any(t => t != null && t.Coords.Equals(c));
        }

        public bool IsOccupied(HexCoords c)
        {
            return occupancy != null && occupancy.HasUnitAt(c);
        }

        public bool IsTileWalkable(HexCoords c)
        {
            // 战斗中：必须在网格内，且无人占用
            // 注意：HexPathfinder 会使用更高级的 GetMoveCost 来判断地形和单位阻挡
            return Contains(c) && !IsOccupied(c);
        }

        public bool CanStep(Unit unit, HexCoords dst)
        {
            if (unit == null) return false;
            if (unit.Coords.DistanceTo(dst) != 1) return false;
            if (!IsTileWalkable(dst)) return false;

            if (unit.TryGetComponent(out UnitAttributes attrs))
            {
                return attrs.Core.CurrentStride > 0;
            }

            return true;
        }

        public IEnumerable<HexCoords> GetStepCandidates(Unit unit)
        {
            if (unit == null) yield break;
            foreach (var n in unit.Coords.Neighbors())
                if (CanStep(unit, n))
                    yield return n;
        }
    }
}