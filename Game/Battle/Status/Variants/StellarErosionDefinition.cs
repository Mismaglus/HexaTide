// Scripts/Game/Battle/Status/Variants/StellarErosionDefinition.cs
using UnityEngine;

namespace Game.Battle.Status
{
    [CreateAssetMenu(menuName = "HexBattle/Status/Variants/Stellar Erosion")]
    public class StellarErosionDefinition : StatusDefinition
    {
        // 回合开始：扣除等于层数的生命值
        public override void OnTurnStart(RuntimeStatus status, BattleUnit unit)
        {
            int damage = status.Stacks;
            if (damage > 0)
            {
                unit.TakeDamage(damage);
                Debug.Log($"[Stellar] Erosion dealt {damage} dmg to {unit.name}");
            }
        }

        // 回合结束：层数 -1
        public override void OnTurnEnd(RuntimeStatus status, BattleUnit unit)
        {
            status.Stacks--;
            // 注意：这里我们不调用 base.OnTurnEnd，因为我们是用层数控制寿命，而不是 Duration
        }
    }
}