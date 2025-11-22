using System.Collections;
using UnityEngine;

namespace Game.Battle.Abilities
{
    public abstract class AbilityEffect : ScriptableObject
    {
        public abstract IEnumerator Apply(BattleUnit caster, Ability ability, AbilityContext ctx);

        // ⭐ 新增：提供默认描述方法，供子类重写
        public virtual string GetDescription()
        {
            return name;
        }
    }
}