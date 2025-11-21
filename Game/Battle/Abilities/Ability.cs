using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Battle.Abilities
{
    public enum TargetShape { Self, Single, Disk, Ring, Line } // extend later
    public enum TargetFaction { Any, Ally, Enemy, SelfOnly }
    public enum AbilityType { Physical, Magical, Mixed }

    public abstract class Ability : ScriptableObject
    {
        [Header("Identity")]
        public string abilityId;
        public string displayName;
        [Tooltip("Icon sprite shown for this ability in UI.")]
        public Sprite icon;

        [Header("Costs & Cooldown")]
        public int apCost = 1;
        [Min(0)] public int mpCost = 0; // ⭐ 新增：MP 消耗
        public int cooldownTurns = 0;

        [Header("Targeting")]
        public TargetShape shape = TargetShape.Single;
        public TargetFaction targetFaction = TargetFaction.Enemy;
        public int minRange = 1;
        public int maxRange = 1;
        public bool requiresLoS = false;

        [Header("Classification")]
        [Tooltip("Physical attacks draw on Armor while magical attacks draw on Ward.")]
        public AbilityType abilityType = AbilityType.Physical;

        [Header("Effects")]
        public List<AbilityEffect> effects = new();

        [Header("Animation")]
        [Tooltip("Animator trigger fired on the caster when this ability executes.")]
        public string animTrigger = string.Empty;
        [Tooltip("Rotate the caster to face the first target before playing the animation.")]
        public bool faceTarget = true;
        [Tooltip("Delay between triggering the animation and applying effects.")]
        public float preWindupSeconds = 0.2f;
        [Tooltip("Delay after all effects are applied (e.g., recovery).")]
        public float postRecoverSeconds = 0.2f;
        [Tooltip("If enabled, the runner waits for the specified animator state (name or tag) to finish before ending the ability.")]
        public bool waitForAnimCompletion = true;
        [Tooltip("Animator layer index used when waiting for completion.")]
        public int animLayerIndex = 0;
        [Tooltip("Animator state name (use full path if needed). Leave empty to rely on tag.")]
        public string animStateName = string.Empty;
        [Tooltip("Animator state tag watched when waiting for completion (ignored if state name is provided).")]
        public string animStateTag = string.Empty;
        [Tooltip("Maximum time (seconds) to wait before giving up when monitoring animation completion.")]
        public float animWaitTimeout = 5f;

        // ⭐ 更新：检查 AP 和 MP 是否都足够
        public virtual bool CanUse(BattleUnit caster)
        {
            if (caster == null) return false;

            // 检查 AP
            if (caster.CurAP < apCost) return false;

            // 检查 MP (通过 UnitAttributes)
            if (mpCost > 0)
            {
                // BattleUnit 应该有 Attributes 引用
                if (caster.Attributes == null) return false;
                if (caster.Attributes.Core.MP < mpCost) return false;
            }

            return true;
        }

        public virtual bool IsValidTarget(BattleUnit caster, AbilityContext ctx) => ctx != null && ctx.HasAnyTarget;

        public virtual IEnumerator Execute(BattleUnit caster, AbilityContext ctx, AbilityRunner runner)
        {
            // 双重检查，防止边缘情况
            if (!CanUse(caster) || !IsValidTarget(caster, ctx)) yield break;

            // ⭐ 消耗资源
            caster.TrySpendAP(apCost);
            caster.TrySpendMP(mpCost); // 调用 BattleUnit 里写好的扣蓝逻辑

            // VFX/SFX hooks could be placed here or inside runner.PerformEffects
            yield return runner.PerformEffects(caster, this, ctx, effects);
        }
    }
}