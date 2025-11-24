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

        public override string GetDescription(BattleUnit caster)
        {
            // 1. 基础描述
            System.Text.StringBuilder sb = new System.Text.StringBuilder();

            // 2. 物理部分
            if (config.basePhysical > 0 || HasScaling(config.physScaling))
            {
                int bonus = 0;
                if (caster != null)
                {
                    bonus = Mathf.RoundToInt(config.physScaling.Evaluate(caster.Attributes.Core));
                }

                sb.Append($"造成 <color=#FF4444>{config.basePhysical}</color>");
                if (bonus > 0) sb.Append($" + <color=#FFAAAA>{bonus}</color>");
                sb.Append(" 物理伤害");

                // 显示详细加成公式
                AppendScalingDetails(config.physScaling, sb);
            }

            // 3. 魔法部分 (类似逻辑)
            if (config.baseMagical > 0 || HasScaling(config.magScaling))
            {
                if (sb.Length > 0) sb.Append(" 以及 ");

                int bonus = 0;
                if (caster != null) bonus = Mathf.RoundToInt(config.magScaling.Evaluate(caster.Attributes.Core));

                sb.Append($"<color=#4444FF>{config.baseMagical}</color>");
                if (bonus > 0) sb.Append($" + <color=#AAAAFF>{bonus}</color>");
                sb.Append(" 魔法伤害");

                AppendScalingDetails(config.magScaling, sb);
            }

            return sb.ToString();
        }

        bool HasScaling(Combat.ScalingMatrix m)
        {
            return m.Str > 0 || m.Dex > 0 || m.Int > 0 || m.Faith > 0;
        }

        void AppendScalingDetails(Combat.ScalingMatrix m, System.Text.StringBuilder sb)
        {
            if (m.Str > 0) sb.Append($" (+{m.Str:P0} 力量)");
            if (m.Dex > 0) sb.Append($" (+{m.Dex:P0} 敏捷)");
            if (m.Int > 0) sb.Append($" (+{m.Int:P0} 智力)");
            if (m.Faith > 0) sb.Append($" (+{m.Faith:P0} 信仰)");
        }
    }
}
