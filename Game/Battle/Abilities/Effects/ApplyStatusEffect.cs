// Scripts/Game/Battle/Abilities/Effects/ApplyStatusEffect.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.Battle.Status;
using Core.Hex;

namespace Game.Battle.Abilities.Effects
{
    [CreateAssetMenu(menuName = "HexBattle/Effects/Apply Status")]
    public class ApplyStatusEffect : AbilityEffect
    {
        [Header("Status Settings")]
        public StatusDefinition statusDefinition;

        // ⭐ 新增：层数配置
        [Tooltip("一次施加几层？(默认1)")]
        [Min(1)] public int stacks = 1;

        [Tooltip("应用给谁？True=给自己(Buff)，False=给目标(Debuff)")]
        public bool applyToSelf = false;

        [Tooltip("施加几率 (0-1)")]
        [Range(0f, 1f)] public float chance = 1.0f;

        public override IEnumerator Apply(BattleUnit caster, Ability ability, AbilityContext ctx)
        {
            if (statusDefinition == null) yield break;

            List<BattleUnit> targets = new List<BattleUnit>();

            if (applyToSelf)
            {
                targets.Add(caster);
            }
            else
            {
                HexCoords origin = ctx.Origin;
                if (ctx.TargetTiles.Count > 0) origin = ctx.TargetTiles[0];
                else if (ctx.TargetUnits.Count > 0) origin = ctx.TargetUnits[0].UnitRef.Coords;

                targets = TargetingResolver.GatherTargets(caster, origin, ability);
            }

            foreach (var target in targets)
            {
                if (target == null) continue;
                if (Random.value > chance) continue;

                if (target.Status)
                {
                    // ⭐ 传入配置的层数
                    target.Status.ApplyStatus(statusDefinition, caster, stacks);
                    Debug.Log($"[Effect] Applied {stacks}x {statusDefinition.statusID} to {target.name}");
                }
            }

            yield break;
        }

        public override string GetDescription(BattleUnit caster)
        {
            string targetStr = applyToSelf ? "Self" : "Target";
            string stackStr = stacks > 1 ? $"{stacks} stacks of " : "";
            return $"Apply {stackStr}{statusDefinition.LocalizedName} to {targetStr}.";
        }
    }
}