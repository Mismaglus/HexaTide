using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Core.Hex;
using Game.Grid;
using Game.Units;

namespace Game.Battle
{
    /// <summary>
    /// 战斗模式下的规则中心：阵营判断、可走判定、选择判定等。
    /// </summary>
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
            return Contains(c) && !IsOccupied(c);
        }

        public bool CanStep(Unit unit, HexCoords dst)
        {
            if (unit == null) return false;
            if (unit.Coords.DistanceTo(dst) != 1) return false;
            if (!IsTileWalkable(dst)) return false;

            // ? 修复：检查移动力 (Stride)
            // 现在数据归一化到了 UnitAttributes
            if (unit.TryGetComponent(out UnitAttributes attrs))
            {
                return attrs.Core.CurrentStride > 0;
            }

            return true; // 无属性的特殊单位默认放行
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