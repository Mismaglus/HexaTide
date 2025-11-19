// Script/Game/Battle/Abilities/Effects/DamageEffect.cs
using System.Collections;
using Game.Units;
using UnityEngine;

namespace Game.Battle.Abilities
{
    [CreateAssetMenu(menuName = "Battle/Effects/Damage")]
    public class DamageEffect : AbilityEffect
    {
        public int baseDamage = 10;

        public override IEnumerator Apply(BattleUnit caster, Ability ability, AbilityContext ctx)
        {
            foreach (var u in ctx.TargetUnits)
            {
                if (u == null) continue;

                // TODO: replace with your own HP component
                if (u.TryGetComponent<DemoHP>(out var hp))
                {
                    hp.ApplyDamage(baseDamage);
                }

                if (u.TryGetComponent<UnitHitReaction>(out var hitReaction))
                {
                    hitReaction.Play();
                }
                else
                {
                    var anim = u.GetComponentInChildren<Animator>(true);
                    if (anim != null)
                    {
                        anim.SetTrigger("GetHit");
                    }
                }

                yield return null; // frame break to allow VFX timing
            }
        }
    }

    // Demo: simple HP holder (replace with your real stat system)
    public class DemoHP : MonoBehaviour
    {
        public int MaxHP = 100;
        public int CurHP = 100;

        public void ApplyDamage(int dmg)
        {
            CurHP = Mathf.Max(0, CurHP - Mathf.Max(0, dmg));
            // death check / events here
        }
    }
}
