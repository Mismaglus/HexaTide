using UnityEngine;
using Game.Units;
using Game.Battle.Combat;

namespace Game.Battle.Abilities.Effects
{
    [CreateAssetMenu(menuName = "HexBattle/Effects/Damage")]
    public class DamageEffect : AbilityEffect
    {
        [Header("Damage Logic")]
        public int baseDamage = 10;
        public float scalingFactor = 1.0f;

        public override void Apply(BattleUnit source, BattleUnit target, AbilityContext ctx)
        {
            if (source == null || target == null) return;

            // 1. 计算伤害
            CombatResult result = CombatCalculator.CalculateDamage(source, target, this);

            // 2. 打印日志 (可选：如果 CombatCalculator 没打，这里打)
            Debug.Log($"[DamageEffect] {source.name} hits {target.name} for {result.finalDamage} dmg " +
                      $"{(result.isCritical ? "(CRIT!)" : "")}");

            // 3. ⭐⭐⭐ 核心修改：调用 TakeDamage 触发受击/死亡流程 ⭐⭐⭐
            // 之前是: target.Attributes.Core.HP -= result.finalDamage;
            target.TakeDamage(result.finalDamage);

            // (这里未来可以添加飘字 UI 调用，比如 FloatingTextManager.Show(result.finalDamage))
        }

        public override string GetDescription()
        {
            return $"Deals {baseDamage} + ({scalingFactor:P0} Stats) damage.";
        }
    }
}