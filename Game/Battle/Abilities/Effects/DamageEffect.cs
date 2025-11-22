using System.Collections; // ⭐ 必须引用，用于 IEnumerator
using UnityEngine;
using Game.Units;
using Game.Battle.Combat;
using Game.Battle.Abilities;

namespace Game.Battle.Abilities.Effects
{
    [CreateAssetMenu(menuName = "HexBattle/Effects/Damage")]
    public class DamageEffect : AbilityEffect
    {
        [Header("Damage Logic")]
        public int baseDamage = 10;
        public float scalingFactor = 1.0f;

        [Header("Damage Configuration")]
        public DamageConfig config = DamageConfig.Default();
        // ⭐ 修复：返回类型改为 IEnumerator
        public override IEnumerator Apply(BattleUnit source, Ability ability, AbilityContext ctx)
        {
            // 安全检查
            if (source == null || ctx == null || ctx.TargetUnits == null)
                yield break; // ⭐ 协程中不能用 return; 必须用 yield break;

            foreach (var target in ctx.TargetUnits)
            {
                if (target == null) continue;

                // 1. 计算伤害
                CombatResult result = CombatCalculator.CalculateDamage(source, target, this);

                // 2. 打印日志
                Debug.Log($"[DamageEffect] {source.name} hits {target.name} for {result.finalDamage} dmg " +
                          $"{(result.isCritical ? "(CRIT!)" : "")}");

                // 3. 应用伤害 (TakeDamage 内部会处理动画和死亡)
                target.TakeDamage(result.finalDamage);

                // 💡 可选：如果你希望每个目标的受击之间有微小延迟（增加打击感）
                // yield return new WaitForSeconds(0.1f);
            }

            // 结束协程
            yield break;
        }
        public override string GetDescription()
        {
            string desc = $"Deals {config.basePhysical} Phys";
            if (config.baseMagical > 0) desc += $" + {config.baseMagical} Mag";
            desc += " damage.";
            return desc;
        }
    }
}