using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.Units;
using Game.Localization;

namespace Game.Battle.Abilities
{
    public enum TargetShape { Self, Single, Disk, Ring, Line, Cone }
    public enum AbilityType { Physical, Magical, Mixed }
    public enum AbilityTargetType { EnemyUnit, FriendlyUnit, EmptyTile, AnyTile, Self }

    public abstract class Ability : ScriptableObject
    {
        [Header("Localization Identity")]
        public string abilityID;
        public Sprite icon;

        public string LocalizedName => LocalizationManager.Get($"{abilityID}_NAME");
        public string LocalizedFlavor => LocalizationManager.Get($"{abilityID}_FLAVOR");

        [Header("Behavior")]
        [Tooltip("勾选后，点击技能图标会立即释放（例如：疾跑、自我治疗），而不会进入选人/选地块模式。")]
        public bool triggerImmediately = false; // ⭐ 新增开关
        [Tooltip("即时技能点击后等待的延迟（秒），用于放慢一闪而过的体验。非即时技能忽略。")]
        [Min(0f)] public float triggerImmediateDelay = 0f;

        [Header("Costs")]
        public int apCost = 1;
        [Min(0)] public int mpCost = 0;
        public int cooldownTurns = 0;

        [Header("Targeting Input")]
        public AbilityTargetType targetType = AbilityTargetType.EnemyUnit;
        public int minRange = 1;
        public int maxRange = 1;
        public bool requiresLoS = false;

        [Header("Area of Effect (AOE)")]
        public TargetShape shape = TargetShape.Single;

        [Tooltip("-1 代表无限/全图")]
        [Min(-1)] public int aoeRadius = 0;

        [Header("Shape Settings")]
        [Range(30f, 180f)] public float coneAngle = 60f;
        [Min(0.1f)] public float lineWidth = 1.0f;

        [Header("Target Filtering")]
        public bool affectEnemies = true;
        public bool affectAllies = false;
        public bool affectSelf = false;

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
            if (caster.Attributes != null && caster.Attributes.Core.MP < mpCost) return false;
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

        public string GetDynamicDescription(BattleUnit caster)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            foreach (var effect in effects)
            {
                if (effect != null)
                    sb.AppendLine(effect.GetDescription(caster));
            }
            return sb.ToString();
        }
    }
}
