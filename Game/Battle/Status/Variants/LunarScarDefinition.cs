// Scripts/Game/Battle/Status/Variants/LunarScarDefinition.cs
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
            int maxHP = unit.Attributes.Core.HPMax;
            int currentHP = unit.Attributes.Core.HP;
            int missingHP = maxHP - currentHP;

            if (missingHP <= 0) return;

            float pct = status.Stacks * percentMissingPerStack;
            int damage = Mathf.FloorToInt(missingHP * pct);

            if (damage < 1 && missingHP > 0) damage = 1; // 保底1点

            if (damage > 0)
            {
                unit.TakeDamage(damage);
                Debug.Log($"[Lunar] Scar dealt {damage} dmg ({pct:P0} of missing) to {unit.name}");
            }
        }

        public override void OnTurnEnd(RuntimeStatus status, BattleUnit unit)
        {
            status.Stacks--;
        }
    }
}