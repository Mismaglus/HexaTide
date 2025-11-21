using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Battle.Abilities
{
    public enum TargetShape { Self, Single, Disk, Ring, Line }
    public enum TargetFaction { Any, Ally, Enemy, SelfOnly }
    public enum AbilityType { Physical, Magical, Mixed }

    public abstract class Ability : ScriptableObject
    {
        [Header("Identity")]
        public string abilityId;
        public string displayName;
        public Sprite icon;

        [Header("Costs & Cooldown")]
        public int apCost = 1;
        [Min(0)] public int mpCost = 0; // ⭐ 新增：MP消耗
        public int cooldownTurns = 0;

        [Header("Targeting")]
        public TargetShape shape = TargetShape.Single;
        public TargetFaction targetFaction = TargetFaction.Enemy;
        public int minRange = 1;
        public int maxRange = 1;
        public bool requiresLoS = false;

        [Header("Classification")]
        public AbilityType abilityType = AbilityType.Physical;

        [Header("Effects")]
        public List<AbilityEffect> effects = new();

        [Header("Animation")]
        public string animTrigger = string.Empty;
        public bool faceTarget = true;
        public float preWindupSeconds = 0.2f;
        public float postRecoverSeconds = 0.2f;
        public bool waitForAnimCompletion = true;
        public int animLayerIndex = 0;
        public string animStateName = string.Empty;
        public string animStateTag = string.Empty;
        public float animWaitTimeout = 5f;

        public virtual bool CanUse(BattleUnit caster)
        {
            if (caster == null) return false;

            // Check AP
            if (caster.CurAP < apCost) return false;

            // ⭐ Check MP
            if (mpCost > 0)
            {
                if (caster.Attributes == null) return false;
                if (caster.Attributes.Core.MP < mpCost) return false;
            }

            return true;
        }

        public virtual bool IsValidTarget(BattleUnit caster, AbilityContext ctx) => ctx != null && ctx.HasAnyTarget;

        public virtual IEnumerator Execute(BattleUnit caster, AbilityContext ctx, AbilityRunner runner)
        {
            if (!CanUse(caster) || !IsValidTarget(caster, ctx)) yield break;

            // ⭐ 消耗资源
            caster.TrySpendAP(apCost);
            if (mpCost > 0) caster.TrySpendMP(mpCost);

            yield return runner.PerformEffects(caster, this, ctx, effects);
        }
    }
}