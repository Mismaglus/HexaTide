using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Core.Hex;
using Game.Units;
using Game.Battle.Actions;
using Game.Battle.Abilities;
using Game.Grid; // 引用 Pathfinder

namespace Game.Battle
{
    public class EnemyTurnActor : MonoBehaviour, ITurnActor
    {
        [Header("Systems")]
        public ActionQueue queue;
        public AbilityRunner runner;
        public BattleRules rules; // ⭐ 新增：需要规则来判断地形

        [Header("Unit Data")]
        public BattleUnit battleUnit;
        public Ability basicAttack;

        void Awake()
        {
            // 自动查找引用
            if (queue == null) queue = FindFirstObjectByType<ActionQueue>(FindObjectsInactive.Exclude);
            if (runner == null) runner = FindFirstObjectByType<AbilityRunner>(FindObjectsInactive.Exclude);
            if (rules == null) rules = FindFirstObjectByType<BattleRules>(FindObjectsInactive.Exclude);

            if (battleUnit == null && !TryGetComponent(out battleUnit))
            {
                Debug.LogWarning($"EnemyTurnActor on {name} missing BattleUnit.", this);
            }
        }

        public void OnTurnStart()
        {
            battleUnit?.ResetTurnResources();
        }

        public bool HasPendingAction => false;

        public IEnumerator TakeTurn()
        {
            if (battleUnit == null || basicAttack == null) yield break;

            var unitMover = battleUnit.GetComponent<UnitMover>();
            if (unitMover == null) yield break;

            // 1. 寻找最近的玩家单位 (Naive Target Selection)
            var enemies = FindObjectsByType<BattleUnit>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(u => u.isPlayer) // 找玩家
                .ToList();

            if (enemies.Count == 0) yield break;

            // 按距离排序，找最近的
            var target = enemies
                .OrderBy(u => unitMover._mCoords.DistanceTo(u.UnitRef.Coords))
                .First();

            var targetCoords = target.UnitRef.Coords;

            // 2. 判断是否需要移动
            // 如果已经在攻击范围内 (默认为1格)，直接打
            if (unitMover._mCoords.DistanceTo(targetCoords) > basicAttack.maxRange)
            {
                // 需要移动：找到目标身边最近的一个“可站立”空位
                HexCoords? bestDest = GetBestAttackPosition(unitMover._mCoords, targetCoords);

                if (bestDest.HasValue)
                {
                    // 计算路径
                    var path = HexPathfinder.FindPath(unitMover._mCoords, bestDest.Value, rules, battleUnit.UnitRef);

                    if (path != null && path.Count > 0)
                    {
                        // ⭐ 关键：使用 PathMoveAction 平滑移动
                        queue.Enqueue(new PathMoveAction(unitMover, path));
                    }
                }
            }

            // 3. 尝试攻击 (无论是否移动过，都尝试打一下)
            // 注意：这里不直接判断距离，而是让 AbilityAction 内部去校验
            // 或者是我们预判一下，防止空挥
            // 为了简单，这里我们假设 ActionQueue 执行完移动后，我们会再次检查距离
            // 但 ActionQueue 是队列执行的，所以我们需要把攻击指令也塞进去，
            // 如果移动后够不着，AbilityAction 内部的 IsValidTarget 会拦截并取消

            var ctx = new AbilityContext
            {
                Caster = battleUnit,
                Origin = unitMover._mCoords // 注意：这里其实有瑕疵，因为移动后坐标变了，但AbilityAction是在执行时重新获取位置的，所以没问题
            };
            ctx.TargetUnits.Add(target);

            // 只有当预估距离足够时才入队，或者让 Action 自己判断
            queue.Enqueue(new AbilityAction(basicAttack, ctx, runner));

            // 4. 执行所有指令
            yield return queue.RunAll();
        }

        // 寻找目标周围最近的可行走格子
        private HexCoords? GetBestAttackPosition(HexCoords myPos, HexCoords targetPos)
        {
            HexCoords? best = null;
            int minDist = int.MaxValue;

            // 遍历目标周围一圈
            foreach (var neighbor in targetPos.Neighbors())
            {
                // 如果这个格子不可走 (障碍物或被占)，且不是我自己站的位置，就跳过
                if (!rules.IsTileWalkable(neighbor) && !neighbor.Equals(myPos)) continue;

                int dist = myPos.DistanceTo(neighbor);
                if (dist < minDist)
                {
                    minDist = dist;
                    best = neighbor;
                }
            }
            return best;
        }
    }

    /// <summary>
    /// 一个简单的 Action，用于执行多步路径移动
    /// </summary>
    public class PathMoveAction : IAction
    {
        private readonly UnitMover _mover;
        private readonly List<HexCoords> _path;

        public PathMoveAction(UnitMover mover, List<HexCoords> path)
        {
            _mover = mover;
            _path = path;
        }

        public bool IsValid =>
            _mover != null &&
            !_mover.IsMoving &&
            _path != null &&
            _path.Count > 0;

        public IEnumerator Execute()
        {
            bool finished = false;
            // 调用 UnitMover 的 FollowPath
            _mover.FollowPath(_path, () => finished = true);

            while (!finished)
                yield return null;
        }
    }
}