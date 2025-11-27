using UnityEngine;

namespace Game.Battle.Status
{
    [CreateAssetMenu(menuName = "HexBattle/Status/Variants/Stellar Erosion")]
    public class StellarErosionDefinition : StatusDefinition
    {
        public override void OnTurnStart(RuntimeStatus status, BattleUnit unit)
        {
            // 1. 造成伤害 (始终在回合开始)
            int damage = status.Stacks;
            if (damage > 0)
            {
                unit.TakeDamage(damage);
                Debug.Log($"[Stellar] Erosion dealt {damage} dmg to {unit.name}");
            }

            // 2. 减少层数 (根据配置)
            if (decreaseStackAtStart)
            {
                status.Stacks--;
            }
        }

        public override void OnTurnEnd(RuntimeStatus status, BattleUnit unit)
        {
            // 减少层数 (根据配置)
            if (!decreaseStackAtStart)
            {
                status.Stacks--;
            }
        }
    }
}