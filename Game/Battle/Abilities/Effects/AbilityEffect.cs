// Script/Game/Battle/Abilities/Effects/AbilityEffect.cs
using System.Collections;

namespace Game.Battle.Abilities
{
    public abstract class AbilityEffect : UnityEngine.ScriptableObject
    {
        public abstract IEnumerator Apply(BattleUnit caster, Ability ability, AbilityContext ctx);
    }
}
