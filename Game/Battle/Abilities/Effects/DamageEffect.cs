using System.Collections;
using UnityEngine;
using Game.Units;
using Game.Battle.Combat;

namespace Game.Battle.Abilities
{
    [CreateAssetMenu(menuName = "Battle/Effects/Damage")]
    public class DamageEffect : AbilityEffect
    {
        [Header("Damage Configuration")]
        public DamageConfig config = DamageConfig.Default();

        public override IEnumerator Apply(BattleUnit caster, Ability ability, AbilityContext ctx)
        {
            foreach (var target in ctx.TargetUnits)
            {
                if (target == null) continue;

                CombatResult result = CombatCalculator.ResolveAttack(caster, target, config);

                if (result.isHit)
                {
                    if (target.TryGetComponent<UnitAttributes>(out var attrs))
                    {
                        attrs.Core.HP = Mathf.Max(0, attrs.Core.HP - result.finalDamage);
                    }

                    if (target.TryGetComponent<UnitHitReaction>(out var hitReaction))
                    {
                        hitReaction.Play();
                    }
                    else
                    {
                        var anim = target.GetComponentInChildren<Animator>(true);
                        if (anim) anim.SetTrigger("GetHit");
                    }
                }

                string logColor = result.isHit ? (result.isCrit ? "red" : "white") : "grey";
                string msg = result.isHit ? $"-{result.finalDamage}" : "MISS";
                if (result.isCrit) msg += "!";

                Debug.Log($"<color={logColor}><b>{msg}</b></color> on {target.name} ({result.ToLog()})");

                yield return new WaitForSeconds(0.1f);
            }
        }
    }
}