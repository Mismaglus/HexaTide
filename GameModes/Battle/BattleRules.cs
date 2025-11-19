using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Core.Hex;          // HexCoords / 距离等
using Game.Grid;        // IHexGridProvider / GridRecipe
using Game.Units;       // Unit / UnitMover

namespace Game.Battle
{
    /// <summary>
    /// 战斗模式下的规则中心：阵营判断、可走判定、选择判定等。
    /// 仅包含“规则”，不持有状态（占位表仍由 SelectionManager 维护）。
    /// </summary>
    public class BattleRules : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("任何实现了 IHexGridProvider 的网格组件（如 BattleHexGrid）")]
        [SerializeField] private MonoBehaviour gridComponent;

        [Tooltip("占位/查询在这里做（若留空会自动查找场景中的 SelectionManager）")]
        [SerializeField] private SelectionManager selection;
        [SerializeField] private Game.Grid.GridOccupancy occupancy; // 新增

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
            // 仅允许选中我方单位
            if (unit == null) return false;
            return unit.TryGetComponent(out BattleUnit bu) && bu.isPlayer;
        }

        // —— 格子/行走判定 —— //

        public bool Contains(HexCoords c)
        {
            if (grid == null) return false;

            // 若你的网格类有 Contains(c)，可以换成它；
            // 这里用 EnumerateTiles 做一个通用兜底。
            return grid.EnumerateTiles().Any(t => t != null && t.Coords.Equals(c));
        }

        public bool IsOccupied(HexCoords c)
        {
            return occupancy != null && occupancy.HasUnitAt(c);
        }

        public bool IsTileWalkable(HexCoords c)
        {
            // 战斗中：必须在网格内，且无人占用
            return Contains(c) && !IsOccupied(c);
        }

        public bool CanStep(Unit unit, HexCoords dst)
        {
            if (unit == null) return false;
            if (unit.Coords.DistanceTo(dst) != 1) return false;     // 仅邻格
            if (!IsTileWalkable(dst)) return false;

            // 有步点（Stride）
            if (unit.TryGetComponent(out UnitMover mover))
                return mover.strideLeft > 0;

            return true; // 没挂 UnitMover 的特殊单位，规则放行
        }

        /// <summary>
        /// 返回单位在当前 Stride 下的可达邻格（一步）。
        /// 以后扩展 A* 时可在这上面做多步寻路。
        /// </summary>
        public IEnumerable<HexCoords> GetStepCandidates(Unit unit)
        {
            if (unit == null) yield break;
            foreach (var n in unit.Coords.Neighbors())
                if (CanStep(unit, n))
                    yield return n;
        }
    }
}
