using UnityEngine;
using Game.Battle;

namespace Game.Battle.Status
{
    [CreateAssetMenu(menuName = "HexBattle/Status/Variants/Night Cinders")]
    public class NightCindersDefinition : StatusDefinition
    {
        [Header("Cinder Config")]
        [Tooltip("每层造成的当前生命百分比 (0.01 = 1%)")]
        public float percentCurrentPerStack = 0.01f;

        // 把核心逻辑提取出来，复用
        private void ExecuteCindersLogic(RuntimeStatus status, BattleUnit unit)
        {
            // 1. 结算伤害 (基于当前生命)
            int currentHP = unit.Attributes.Core.HP;
            float pct = status.Stacks * percentCurrentPerStack;

            int damage = Mathf.FloorToInt(currentHP * pct);
            if (damage < 1 && status.Stacks > 0) damage = 1;

            if (damage > 0)
            {
                unit.TakeDamage(damage, status.Source);
                Debug.Log($"[Night] Cinders dealt {damage} dmg ({pct:P0} of current) to {unit.name}");
            }

            // 2. 结算衰减 (层数减半)
            int oldStacks = status.Stacks;
            status.Stacks = Mathf.FloorToInt(status.Stacks / 2f);

            Debug.Log($"[Night] Stacks halved: {oldStacks} -> {status.Stacks}");
        }

        // === 根据父类的开关决定何时触发 ===

        public override void OnTurnStart(RuntimeStatus status, BattleUnit unit)
        {
            // 如果勾选了 "Decrease Stack At Start"，就在回合开始触发
            if (decreaseStackAtStart)
            {
                ExecuteCindersLogic(status, unit);
            }
        }

        public override void OnTurnEnd(RuntimeStatus status, BattleUnit unit)
        {
            // 如果没勾选，就在回合结束触发 (默认行为)
            if (!decreaseStackAtStart)
            {
                ExecuteCindersLogic(status, unit);
            }
            // 注意：夜烬不扣除 Duration，只衰减层数，所以不调用 base.OnTurnEnd
        }
    }
}