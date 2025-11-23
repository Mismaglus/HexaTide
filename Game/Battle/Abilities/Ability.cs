using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Battle.Abilities
{
    public enum TargetShape { Self, Single, Disk, Ring, Line, Cone }
    public enum AbilityType { Physical, Magical, Mixed }
    public enum AbilityTargetType { EnemyUnit, FriendlyUnit, EmptyTile, AnyTile, Self }

    public abstract class Ability : ScriptableObject
    {
        [Header("Identity")]
        public string abilityId;
        public string displayName;
        public Sprite icon;

        [Header("Costs")]
        public int apCost = 1;
        [Min(0)] public int mpCost = 0;
        public int cooldownTurns = 0;

        [Header("Targeting Input")]
        // 这里定义“光标能点哪里”
        public AbilityTargetType targetType = AbilityTargetType.EnemyUnit;
        public int minRange = 1;
        public int maxRange = 1;
        public bool requiresLoS = false;

        [Header("Area of Effect (AOE)")]
        // 这里定义“实际打哪里”
        public TargetShape shape = TargetShape.Single;

        [Tooltip("影响半径 (0 = 仅中心, 1 = 周围一圈)")]
        [Min(0)] public int aoeRadius = 0;

        [Header("Target Filtering")]
        // 这里定义“会打到谁”
        public bool affectEnemies = true;
        public bool affectAllies = false;
        public bool affectSelf = false; // 旋风斩的关键：选 False 就不会砍自己

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
            if (caster.CurAP < apCost) return false;
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
            if (!CanUse(caster)) yield break;
            if (!IsValidTarget(caster, ctx)) yield break;

            caster.TrySpendAP(apCost);
            if (mpCost > 0) caster.TrySpendMP(mpCost);

            yield return runner.PerformEffects(caster, this, ctx, effects);
        }
    }
}