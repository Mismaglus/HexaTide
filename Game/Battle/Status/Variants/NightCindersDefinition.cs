// Scripts/Game/Battle/Status/Variants/NightCindersDefinition.cs
using UnityEngine;

namespace Game.Battle.Status
{
    [CreateAssetMenu(menuName = "HexBattle/Status/Variants/Night Cinders")]
    public class NightCindersDefinition : StatusDefinition
    {
        [Header("Cinder Config")]
        [Tooltip("每层造成的当前生命百分比 (0.01 = 1%)")]
        public float percentCurrentPerStack = 0.01f;

        public override void OnTurnEnd(RuntimeStatus status, BattleUnit unit)
        {
            // 1. 结算伤害 (基于当前生命)
            int currentHP = unit.Attributes.Core.HP;
            float pct = status.Stacks * percentCurrentPerStack;

            int damage = Mathf.FloorToInt(currentHP * pct);
            if (damage < 1 && status.Stacks > 0) damage = 1;

            if (damage > 0)
            {
                unit.TakeDamage(damage);
                Debug.Log($"[Night] Cinders dealt {damage} dmg ({pct:P0} of current) to {unit.name}");
            }

            // 2. 结算衰减 (层数减半)
            status.Stacks = Mathf.FloorToInt(status.Stacks / 2f);
        }
    }
}