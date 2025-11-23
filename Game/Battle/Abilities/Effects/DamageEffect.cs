using System.Collections;
using System.Collections.Generic; // List 需要
using UnityEngine;
using Game.Units;
using Game.Battle.Combat;
using Game.Battle.Abilities;
using Core.Hex; // HexCoords 需要

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

        public override IEnumerator Apply(BattleUnit source, Ability ability, AbilityContext ctx)
        {
            if (source == null || ctx == null) yield break;

            // ⭐ 1. 确定施法中心点 (Anchor)
            HexCoords origin = ctx.Origin; // 默认施法者位置

            // 如果有选中的地块，优先用地块
            if (ctx.TargetTiles.Count > 0)
                origin = ctx.TargetTiles[0];
            // 如果有选中的单位，用单位脚下的地块
            else if (ctx.TargetUnits.Count > 0 && ctx.TargetUnits[0] != null)
                origin = ctx.TargetUnits[0].GetComponent<Unit>().Coords;

            // ⭐ 2. 使用 Resolver 重新搜寻目标 (AOE + 筛选)
            List<BattleUnit> finalTargets = TargetingResolver.GatherTargets(source, origin, ability);

            if (finalTargets.Count == 0)
            {
                Debug.Log("[DamageEffect] No valid targets found in area.");
                yield break;
            }

            // ⭐ 3. 对筛选后的目标造成伤害
            foreach (var target in finalTargets)
            {
                if (target == null) continue;

                CombatResult result = CombatCalculator.CalculateDamage(source, target, this);

                Debug.Log($"[DamageEffect] {source.name} hits {target.name} for {result.finalDamage} dmg " +
                          $"{(result.isCritical ? "(CRIT!)" : "")}");

                target.TakeDamage(result.finalDamage);

                // 可选：微小延迟增加打击感
                // yield return new WaitForSeconds(0.1f);
            }

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