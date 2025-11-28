using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.Units;
using Game.Battle.Combat; // 引用 ScalingMatrix
using Game.Common;        // 引用 TextIcons, GameRandom
using Game.Localization;  // 引用 LocalizationManager
using Core.Hex;

namespace Game.Battle.Abilities.Effects
{
    [CreateAssetMenu(menuName = "HexBattle/Effects/Heal")]
    public class HealEffect : AbilityEffect
    {
        [Header("Heal Logic")]
        public int baseHeal = 15;

        [Header("Scaling")]
        [Tooltip("配置属性加成 (例如 Faith S级加成)")]
        public ScalingMatrix scaling;

        [Header("Variance")]
        [Tooltip("治疗浮动范围 (0.1 = +/- 10%)")]
        public float variance = 0.1f;

        public override IEnumerator Apply(BattleUnit caster, Ability ability, AbilityContext ctx)
        {
            if (caster == null || ctx == null) yield break;

            // 1. 确定中心点 (复用 DamageEffect 的逻辑)
            HexCoords origin = ctx.Origin;
            if (ctx.TargetTiles.Count > 0) origin = ctx.TargetTiles[0];
            else if (ctx.TargetUnits.Count > 0 && ctx.TargetUnits[0] != null)
                origin = ctx.TargetUnits[0].UnitRef.Coords;

            // 2. 获取目标 (支持 AOE 治疗)
            List<BattleUnit> finalTargets = TargetingResolver.GatherTargets(caster, origin, ability);

            if (finalTargets.Count == 0) yield break;

            // 3. 计算治疗量
            // 基础 + (属性 *系数)
            float statBonus = 0f;
            if (caster != null)
                statBonus = scaling.Evaluate(caster.Attributes.Core);

            float totalRaw = baseHeal + statBonus;

            foreach (var target in finalTargets)
            {
                if (target == null) continue;

                // 计算浮动
                float mult = 1.0f;
                if (variance > 0)
                    mult = GameRandom.Range(1f - variance, 1f + variance);

                int finalAmount = Mathf.RoundToInt(totalRaw * mult);
                if (finalAmount < 1) finalAmount = 1;

                // 应用治疗
                target.Heal(finalAmount);
            }
        }

        public override string GetDescription(BattleUnit caster)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();

            // 尝试获取本地化的 "Restores" 或 "Heals"
            // 如果没有 Key，暂时硬编码，你可以之后在 Localization/en/UI.json 添加 "UI_RESTORE": "Restores"
            string actionText = LocalizationManager.Get("UI_RESTORE");
            if (string.IsNullOrEmpty(actionText) || actionText == "UI_RESTORE") actionText = "Restores";

            int bonus = 0;
            if (caster != null)
                bonus = Mathf.RoundToInt(scaling.Evaluate(caster.Attributes.Core));

            // 格式: Restores 15 (+5) HP
            sb.Append($"{actionText} <color=#FFFFFF>{baseHeal}</color>");

            // 只有当有加成时才显示 (+X)
            if (bonus > 0 || HasScaling())
            {
                // 如果有 Caster 实例，显示实际计算值
                if (caster != null)
                    sb.Append($" + <color={TextIcons.COL_FAI}>{bonus}</color>"); // 假设治疗通常是 Faith 颜色，或者用绿色
            }

            sb.Append(" HP");

            // 显示缩放细节 (如: +50% FAI)
            AppendScalingDetails(sb);

            return sb.ToString();
        }

        bool HasScaling() => scaling.Str > 0 || scaling.Dex > 0 || scaling.Int > 0 || scaling.Faith > 0;

        void AppendScalingDetails(System.Text.StringBuilder sb)
        {
            if (!HasScaling()) return;
            sb.Append(" (");
            bool first = true;

            if (scaling.Faith > 0) { if (!first) sb.Append(" "); sb.Append($"+{scaling.Faith:P0} {TextIcons.FaiName}"); first = false; }
            if (scaling.Int > 0) { if (!first) sb.Append(" "); sb.Append($"+{scaling.Int:P0} {TextIcons.IntName}"); first = false; }
            if (scaling.Str > 0) { if (!first) sb.Append(" "); sb.Append($"+{scaling.Str:P0} {TextIcons.StrName}"); first = false; }
            if (scaling.Dex > 0) { if (!first) sb.Append(" "); sb.Append($"+{scaling.Dex:P0} {TextIcons.DexName}"); first = false; }

            sb.Append(")");
        }
    }
}