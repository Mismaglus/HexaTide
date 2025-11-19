// Script/Game/Battle/Actions/AbilityAction.cs
using System.Collections;
using Game.Battle.Abilities;

namespace Game.Battle.Actions
{
    public class AbilityAction : IAction
    {
        private readonly Ability _ability;
        private readonly AbilityContext _ctx;
        private readonly AbilityRunner _runner;

        public AbilityAction(Ability ability, AbilityContext ctx, AbilityRunner runner)
        {
            _ability = ability;
            _ctx = ctx;
            _runner = runner;
        }

        public bool IsValid => _ability != null && _ctx != null && _runner != null;

        public IEnumerator Execute()
        {
            yield return _ability.Execute(_ctx.Caster, _ctx, _runner);
        }
    }
}
