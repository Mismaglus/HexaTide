// Scripts/Game/Battle/Actions/AbilityAction.cs
using System.Collections;
using UnityEngine;
using Game.Battle.Abilities;
using Game.Inventory; // ⭐ 引用 Inventory

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
            // 1. 执行技能动画和效果
            yield return _ability.Execute(_ctx.Caster, _ctx, _runner);

            // 2. ⭐ 技能结束后，检查是否需要扣除物品
            if (_ctx.SourceItem != null)
            {
                TryConsumeSourceItem();
            }
        }

        void TryConsumeSourceItem()
        {
            var item = _ctx.SourceItem;
            var caster = _ctx.Caster;

            // 检查是否为消耗品且需要消耗
            if (item is ConsumableItem consumable && consumable.consumeOnUse)
            {
                var inventory = caster.GetComponent<UnitInventory>();
                if (inventory != null)
                {
                    // 调用 UnitInventory 的新重载方法
                    inventory.ConsumeItem(item, 1);
                    Debug.Log($"[AbilityAction] Consumed 1x {item.name} from {caster.name}");
                }
            }
        }
    }
}