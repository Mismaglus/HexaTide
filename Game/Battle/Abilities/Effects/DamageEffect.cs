using System.Collections;
using System.Collections.Generic; // List 需要
using UnityEngine;
using Game.Units;
using Game.Battle.Combat;
using Game.Battle.Abilities;
using Core.Hex; // HexCoords 需要
using Game.Common;        // 引用 TextIcons
using Game.Localization;  // 引用 LocalizationManager

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

            HexCoords origin = ctx.Origin;
            if (ctx.TargetTiles.Count > 0) origin = ctx.TargetTiles[0];
            else if (ctx.TargetUnits.Count > 0 && ctx.TargetUnits[0] != null)
                origin = ctx.TargetUnits[0].GetComponent<Unit>().Coords;

            List<BattleUnit> finalTargets = TargetingResolver.GatherTargets(source, origin, ability);

            if (finalTargets.Count == 0) yield break;

            foreach (var target in finalTargets)
            {
                if (target == null) continue;
                CombatResult result = CombatCalculator.CalculateDamage(source, target, this);
                Debug.Log($"[DamageEffect] Hit {target.name} for {result.finalDamage}");
                target.TakeDamage(result.finalDamage);
            }
        }

        public override string GetDescription(BattleUnit caster)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();

            // 获取常用词汇
            string dealText = LocalizationManager.Get("UI_DEAL"); // "造成"
            string andText = LocalizationManager.Get("UI_AND");  // "以及"

            // 1. 物理部分
            if (config.basePhysical > 0 || HasScaling(config.physScaling))
            {
                int bonus = 0;
                if (caster != null)
                    bonus = Mathf.RoundToInt(config.physScaling.Evaluate(caster.Attributes.Core));

                sb.Append($"{dealText} <color=#FFFFFF>{config.basePhysical}</color>");
                if (bonus > 0) sb.Append($" + <color={TextIcons.COL_PHYS}>{bonus}</color>");

                // 使用本地化的名字: "物理伤害"
                sb.Append($" {TextIcons.PhysName}");

                AppendScalingDetails(config.physScaling, sb);
            }

            // 2. 魔法部分
            if (config.baseMagical > 0 || HasScaling(config.magScaling))
            {
                if (sb.Length > 0) sb.Append($" {andText} "); // 连接词
                else sb.Append($"{dealText} ");

                int bonus = 0;
                if (caster != null)
                    bonus = Mathf.RoundToInt(config.magScaling.Evaluate(caster.Attributes.Core));

                sb.Append($"<color=#FFFFFF>{config.baseMagical}</color>");
                if (bonus > 0) sb.Append($" + <color={TextIcons.COL_MAG}>{bonus}</color>");

                sb.Append($" {TextIcons.MagName}");

                AppendScalingDetails(config.magScaling, sb);
            }

            return sb.ToString();
        }

        bool HasScaling(ScalingMatrix m) => m.Str > 0 || m.Dex > 0 || m.Int > 0 || m.Faith > 0;

        void AppendScalingDetails(ScalingMatrix m, System.Text.StringBuilder sb)
        {
            if (m.Str > 0) sb.Append($" (+{m.Str:P0} {TextIcons.StrName})");
            if (m.Dex > 0) sb.Append($" (+{m.Dex:P0} {TextIcons.DexName})");
            if (m.Int > 0) sb.Append($" (+{m.Int:P0} {TextIcons.IntName})");
            if (m.Faith > 0) sb.Append($" (+{m.Faith:P0} {TextIcons.FaiName})");
        }
    }
}