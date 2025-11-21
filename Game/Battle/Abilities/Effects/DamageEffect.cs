using System.Collections;
using UnityEngine;
using Game.Units;
using Game.Battle.Combat;

namespace Game.Battle.Abilities
{
    [CreateAssetMenu(menuName = "Battle/Effects/Damage")]
    public class DamageEffect : AbilityEffect
    {
        public DamageConfig config = DamageConfig.Default();

        public override IEnumerator Apply(BattleUnit caster, Ability ability, AbilityContext ctx)
        {
            foreach (var target in ctx.TargetUnits)
            {
                if (target == null) continue;

                // 计算
                var result = CombatCalculator.ResolveAttack(caster, target, config);

                // 结算
                if (result.isHit)
                {
                    if (target.TryGetComponent<UnitAttributes>(out var attrs))
                    {
                        attrs.Core.HP = Mathf.Max(0, attrs.Core.HP - result.finalDamage);
                    }

                    if (target.TryGetComponent<UnitHitReaction>(out var react)) react.Play();
                    else target.GetComponentInChildren<Animator>()?.SetTrigger("GetHit");
                }

                // Log (带颜色)
                string color = result.isHit ? "white" : "grey";
                if (result.isCrit) color = "red";
                Debug.Log($"<color={color}>[Damage] {result.ToLog()} on {target.name}</color>");

                yield return new WaitForSeconds(0.1f);
            }
        }
    }
}