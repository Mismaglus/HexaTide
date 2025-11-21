using UnityEngine;
using Game.Units;
using Game.Common;

namespace Game.Battle.Combat
{
    public static class CombatCalculator
    {
        public static CombatResult ResolveAttack(BattleUnit attacker, BattleUnit defender, DamageConfig config)
        {
            var result = new CombatResult();
            var attStats = attacker.Attributes;
            var defStats = defender.Attributes;

            // 1. 命中判定 (Hit Check)
            float hitChance = 100f + (attStats.Recommended.Accuracy - defStats.Recommended.Evasion);
            if (GameRandom.Range(0f, 100f) >= hitChance)
            {
                result.isHit = false;
                return result;
            }
            result.isHit = true;

            // 2. 暴击判定 (Crit Check)
            result.isCrit = GameRandom.Value < attStats.Recommended.CritChance;
            float critMult = result.isCrit ? attStats.Recommended.CritMult : 1.0f;

            // 3. 浮动系数 (Variance)
            float varianceMult = 1.0f;
            if (config.variance > 0)
            {
                varianceMult = GameRandom.Range(1f - config.variance, 1f + config.variance);
            }

            float globalMult = critMult * varianceMult;

            // 4. 分离计算 (Phys vs Mag)

            // Phys
            float rawPhys = config.basePhysical + config.physScaling.Evaluate(attStats.Core);
            float defPhys = Mathf.Clamp01(defStats.Core.Armor - attStats.Recommended.PenetrationPhys);
            float finalPhys = rawPhys * (1f - defPhys) * globalMult;

            // Mag
            float rawMag = config.baseMagical + config.magScaling.Evaluate(attStats.Core);
            float defMag = Mathf.Clamp01(defStats.Core.Ward - attStats.Recommended.PenetrationMag);
            float finalMag = rawMag * (1f - defMag) * globalMult;

            // 5. 汇总
            result.rawPhysical = Mathf.RoundToInt(rawPhys);
            result.rawMagical = Mathf.RoundToInt(rawMag);
            result.dmgPhysical = Mathf.RoundToInt(finalPhys);
            result.dmgMagical = Mathf.RoundToInt(finalMag);
            result.finalDamage = result.dmgPhysical + result.dmgMagical;

            // 保底伤害
            if (result.finalDamage < 1 && (rawPhys + rawMag > 0))
                result.finalDamage = 1;

            return result;
        }
    }
}