using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.Battle.Status; // 引用 Status 系统
using Core.Hex;

namespace Game.Battle.Abilities.Effects
{
    [CreateAssetMenu(menuName = "HexBattle/Effects/Apply Status")]
    public class ApplyStatusEffect : AbilityEffect
    {
        [Header("Status Settings")]
        public StatusDefinition statusDefinition;

        [Tooltip("应用给谁？True=给自己(Buff)，False=给目标(Debuff)")]
        public bool applyToSelf = false;

        [Tooltip("施加几率 (0-1)")]
        [Range(0f, 1f)] public float chance = 1.0f;

        public override IEnumerator Apply(BattleUnit caster, Ability ability, AbilityContext ctx)
        {
            if (statusDefinition == null) yield break;

            // 1. 确定目标集合
            List<BattleUnit> targets = new List<BattleUnit>();

            if (applyToSelf)
            {
                targets.Add(caster);
            }
            else
            {
                // 获取技能选中的所有目标
                // 注意：这里复用了 TargetingResolver 的逻辑，确保范围正确
                HexCoords origin = ctx.Origin;
                if (ctx.TargetTiles.Count > 0) origin = ctx.TargetTiles[0];
                else if (ctx.TargetUnits.Count > 0) origin = ctx.TargetUnits[0].UnitRef.Coords;

                targets = TargetingResolver.GatherTargets(caster, origin, ability);
            }

            // 2. 施加状态
            foreach (var target in targets)
            {
                if (target == null) continue;

                // 随机判定
                if (Random.value > chance) continue;

                if (target.Status)
                {
                    target.Status.ApplyStatus(statusDefinition, caster);
                    Debug.Log($"[Effect] Applied {statusDefinition.statusID} to {target.name}");
                }
            }

            yield break; // 这里的 Apply 是瞬间的，不需要等待
        }

        public override string GetDescription(BattleUnit caster)
        {
            string targetStr = applyToSelf ? "Self" : "Target";
            // 简单描述，你可以根据 LocalizationManager 优化
            return $"Apply {statusDefinition.LocalizedName} to {targetStr}.";
        }
    }
}