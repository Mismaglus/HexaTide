using UnityEngine;

namespace Game.Battle.Status
{
    [CreateAssetMenu(menuName = "HexBattle/Status/Variants/Lunar Scar")]
    public class LunarScarDefinition : StatusDefinition
    {
        [Header("Lunar Config")]
        [Tooltip("每层造成的已损生命百分比 (0.02 = 2%)")]
        public float percentMissingPerStack = 0.02f;

        public override void OnTurnStart(RuntimeStatus status, BattleUnit unit)
        {
            // 1. 造成伤害
            int maxHP = unit.Attributes.Core.HPMax;
            int currentHP = unit.Attributes.Core.HP;
            int missingHP = maxHP - currentHP;

            if (missingHP > 0)
            {
                float pct = status.Stacks * percentMissingPerStack;
                int damage = Mathf.FloorToInt(missingHP * pct);

                if (damage < 1) damage = 1;

                unit.TakeDamage(damage);
                Debug.Log($"[Lunar] Scar dealt {damage} dmg ({pct:P0} of missing) to {unit.name}");
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

        // 易伤逻辑
        public override int ModifyIncomingDamage(RuntimeStatus status, int rawDamage, BattleUnit attacker)
        {
            float vulnerability = 0.2f;
            return Mathf.RoundToInt(rawDamage * (1 + vulnerability));
        }
    }
}