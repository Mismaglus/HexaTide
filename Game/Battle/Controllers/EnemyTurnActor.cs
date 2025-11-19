// Script/Game/Battle/Controllers/EnemyTurnActor.cs
using System.Collections;
using System.Linq;
using UnityEngine;
using Core.Hex;
using Game.Units;
using Game.Battle.Actions;
using Game.Battle.Abilities;

namespace Game.Battle
{
    public class EnemyTurnActor : MonoBehaviour, ITurnActor
    {
        public ActionQueue queue;
        public AbilityRunner runner;
        public BattleUnit battleUnit;
        public Ability basicAttack; // Single, min/maxRange = 1
        void Awake()
        {
            if (queue == null)
                queue = Object.FindFirstObjectByType<ActionQueue>(FindObjectsInactive.Exclude);
            if (battleUnit == null)
            {
                if (!TryGetComponent(out battleUnit))
                {
                    Debug.LogWarning($"EnemyTurnActor on {name} could not find a BattleUnit on the same GameObject.", this);
                }
            }
            if (runner == null)
                runner = Object.FindFirstObjectByType<AbilityRunner>(FindObjectsInactive.Exclude);
        }
        public void OnTurnStart()
        {
            battleUnit?.ResetTurnResources();
        }

        public bool HasPendingAction => false; // AI runs once in TakeTurn

        public IEnumerator TakeTurn()
        {
            if (battleUnit == null) yield break;

            // naive placeholder: if adjacent enemy exists → attack; else不动（或朝最近玩家走一步）
            var enemies = Object
                .FindObjectsByType<BattleUnit>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(u => u.isPlayer)
                .ToList();
            if (enemies.Count == 0) yield break;
            var unitMover = battleUnit.GetComponent<UnitMover>();
            var target = enemies.OrderBy(u => unitMover._mCoords.DistanceTo(unitMover._mCoords)).First();
            var targetMover = target.GetComponent<UnitMover>();

            if (unitMover._mCoords.DistanceTo(targetMover._mCoords) == 1 && basicAttack != null)
            {
                var ctx = new AbilityContext
                {
                    Caster = battleUnit,
                    Origin = unitMover._mCoords,
                };
                ctx.TargetUnits.Add(target);

                queue.Enqueue(new AbilityAction(basicAttack, ctx, runner));
                yield return queue.RunAll();
            }
            else
            {
                // optional: move 1 hex toward target（如果你已有寻路，这里替换）
                var step = Towards(unitMover._mCoords, targetMover._mCoords);
                if (unitMover.CanStepTo(step))
                {
                    queue.Enqueue(new MoveAction(unitMover, step));
                    yield return queue.RunAll();
                }
            }
        }

        private HexCoords Towards(HexCoords from, HexCoords to)
        {
            // 简化：从邻居中选一个让距离下降
            var best = from;
            int bestD = from.DistanceTo(to);
            foreach (var n in from.Neighbors())
            {
                int d = n.DistanceTo(to);
                if (d < bestD) { bestD = d; best = n; }
            }
            return best;
        }
    }
}
