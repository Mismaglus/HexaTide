using UnityEngine;
using Game.Units;

namespace Game.Battle.Combat
{
    public static class CombatCalculator
    {
        public static CombatResult ResolveAttack(
            BattleUnit attacker,
            BattleUnit defender,
            DamageConfig config)
        {
            var result = new CombatResult();
            var attStats = attacker.Attributes;
            var defStats = defender.Attributes;

            // 1. 命中判定 (Hit Check)
            float hitChance = 100f + (attStats.Recommended.Accuracy - defStats.Recommended.Evasion);
            if (UnityEngine.Random.Range(0f, 100f) > hitChance)
            {
                result.isHit = false;
                return result;
            }
            result.isHit = true;

            // 2. 暴击判定 (Crit Check)
            float critRoll = UnityEngine.Random.value;
            result.isCrit = critRoll < attStats.Recommended.CritChance;
            float critMult = result.isCrit ? attStats.Recommended.CritMult : 1.0f;

            // 3. 浮动系数 (Variance)
            float varianceMult = 1.0f;
            if (config.variance > 0)
            {
                varianceMult = UnityEngine.Random.Range(1f - config.variance, 1f + config.variance);
            }

            // 统一的全局乘区 (暴击 * 浮动)
            float globalMult = critMult * varianceMult;

            // === 核心修改：分离计算 ===

            // A. 物理部分
            // Raw = 基础 + (力量*加成 + 敏捷*加成...)
            float rawPhys = config.basePhysical + config.physScaling.Evaluate(attStats.Core);
            // 减伤 = Armor - 穿透
            float defPhys = Mathf.Clamp01(defStats.Core.Armor - attStats.Recommended.PenetrationPhys);
            // 结算 = Raw * (1-减伤) * 全局乘区
            float finalPhys = rawPhys * (1f - defPhys) * globalMult;

            // B. 魔法部分
            // Raw = 基础 + (智力*加成 + 信仰*加成...)
            float rawMag = config.baseMagical + config.magScaling.Evaluate(attStats.Core);
            // 减伤 = Ward - 穿透
            float defMag = Mathf.Clamp01(defStats.Core.Ward - attStats.Recommended.PenetrationMag);
            // 结算
            float finalMag = rawMag * (1f - defMag) * globalMult;

            // =========================

            // 4. 汇总结果
            result.rawPhysical = Mathf.RoundToInt(rawPhys);
            result.rawMagical = Mathf.RoundToInt(rawMag);

            result.dmgPhysical = Mathf.RoundToInt(finalPhys);
            result.dmgMagical = Mathf.RoundToInt(finalMag);

            result.finalDamage = result.dmgPhysical + result.dmgMagical;

            // 保底伤害 (如果原本是有伤害技能，却被减到了0，通常给1点强制伤害)
            if (result.finalDamage < 1 && (rawPhys + rawMag > 0))
                result.finalDamage = 1;

            return result;
        }
    }
}