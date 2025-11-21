using System;
using UnityEngine;

namespace Game.Battle.Combat
{
    [Serializable]
    public struct ScalingMatrix
    {
        [Tooltip("Strength scaling")] public float Str;
        [Tooltip("Dexterity scaling")] public float Dex;
        [Tooltip("Intelligence scaling")] public float Int;
        [Tooltip("Faith scaling")] public float Faith;

        public float Evaluate(Game.Units.UnitAttributes.CoreAttributes stats)
        {
            return (stats.Str * Str) + (stats.Dex * Dex) + (stats.Int * Int) + (stats.Faith * Faith);
        }
    }

    [Serializable]
    public struct DamageConfig
    {
        [Header("Physical Component")]
        public int basePhysical;
        public ScalingMatrix physScaling;

        [Header("Magical Component")]
        public int baseMagical;
        public ScalingMatrix magScaling;

        [Header("Global Settings")]
        [Tooltip("0.05 means +/- 5% variance")]
        public float variance;

        public static DamageConfig Default()
        {
            return new DamageConfig
            {
                basePhysical = 10,
                physScaling = new ScalingMatrix { Str = 1.0f },
                variance = 0.05f
            };
        }
    }

    public struct CombatResult
    {
        public bool isHit;
        public bool isCrit;
        public int finalDamage;
        public int rawPhysical;
        public int rawMagical;
        public int dmgPhysical;
        public int dmgMagical;

        public string ToLog()
        {
            if (!isHit) return "MISS";
            string critStr = isCrit ? " [CRIT!]" : "";
            return $"{finalDamage} (P:{dmgPhysical} + M:{dmgMagical}){critStr}";
        }
    }
}