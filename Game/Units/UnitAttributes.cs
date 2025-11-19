using System;
using UnityEngine;

namespace Game.Units
{
    /// <summary>
    /// Stores all gameplay-related attributes for a unit in a single component so that
    /// balancing can happen from one place in the inspector.
    /// </summary>
    [DisallowMultipleComponent]
    public class UnitAttributes : MonoBehaviour
    {
        [Header("Core")]
        public CoreAttributes Core = CoreAttributes.Default();

        [Header("Recommended")]
        public RecommendedAttributes Recommended = RecommendedAttributes.Default();

        [Header("Optional")]
        public OptionalAttributes Optional = OptionalAttributes.Default();

        void Reset()
        {
            Core = CoreAttributes.Default();
            Recommended = RecommendedAttributes.Default();
            Optional = OptionalAttributes.Default();
            ClampAll();
        }

        void OnValidate()
        {
            ClampAll();
        }

        void ClampAll()
        {
            // Ensure inspector edits stay inside supported gameplay ranges.
            Core.Clamp();
            Recommended.Clamp();
            Optional.Clamp();
        }

        [Serializable]
        public struct CoreAttributes
        {
            [Tooltip("Str: melee / thrown scaling and light physical derived values.")]
            [Range(0, 50)] public int Str;

            [Tooltip("Int: spell / shield scaling and light spell derived values.")]
            [Range(0, 50)] public int Int;

            [Tooltip("Dex: main source of hit, dodge, initiative, crit chance.")]
            [Range(0, 50)] public int Dex;

            [Tooltip("Faith: healing / buffs / status strength and resistance.")]
            [Range(0, 50)] public int Faith;

            [Tooltip("Maximum hit points.")]
            [Range(50, 300)] public int HPMax;

            [Tooltip("Current hit points.")]
            public int HP;

            [Tooltip("Action points available each round. Baseline 4.")]
            [Range(0, 10)] public int AP;

            [Tooltip("Tiles that can be moved each round. Suggested 3-6.")]
            [Range(0, 10)] public int Stride;

            [Tooltip("Initiative value derived mainly from Dex.")]
            [Range(0, 100)] public int Initiative;

            [Tooltip("Physical damage reduction (0-0.6).")]
            [Range(0f, 0.6f)] public float Armor;

            [Tooltip("Magical damage reduction (0-0.6).")]
            [Range(0f, 0.6f)] public float Ward;

            public static CoreAttributes Default()
            {
                var stats = new CoreAttributes
                {
                    Str = 10,
                    Int = 10,
                    Dex = 10,
                    Faith = 10,
                    HPMax = 100,
                    HP = 100,
                    AP = 4,
                    Stride = 4,
                    Initiative = 20,
                    Armor = 0f,
                    Ward = 0f
                };
                stats.Clamp();
                return stats;
            }

            public void Clamp()
            {
                Str = Mathf.Clamp(Str, 0, 50);
                Int = Mathf.Clamp(Int, 0, 50);
                Dex = Mathf.Clamp(Dex, 0, 50);
                Faith = Mathf.Clamp(Faith, 0, 50);
                HPMax = Mathf.Clamp(HPMax, 50, 300);
                HP = Mathf.Clamp(HP, 0, HPMax);
                AP = Mathf.Clamp(AP, 0, 10);
                Stride = Mathf.Clamp(Stride, 0, 10);
                Initiative = Mathf.Clamp(Initiative, 0, 100);
                Armor = Mathf.Clamp(Armor, 0f, 0.6f);
                Ward = Mathf.Clamp(Ward, 0f, 0.6f);
            }
        }

        [Serializable]
        public struct RecommendedAttributes
        {
            [Tooltip("Accuracy; typically derived from Dex and equipment.")]
            public int Accuracy;

            [Tooltip("Evasion; typically derived from Dex and passives.")]
            public int Evasion;

            [Tooltip("Critical hit chance (0-1).")]
            [Range(0f, 1f)] public float CritChance;

            [Tooltip("Critical damage multiplier (1.0-2.5).")]
            [Range(1f, 2.5f)] public float CritMult;

            [Tooltip("Status / healing / barrier potency (caster side).")]
            public float StatusPotency;

            [Tooltip("Status resistance (target side).")]
            public float StatusResist;

            [Tooltip("Physical penetration (reduces Armor).")]
            [Range(0f, 1f)] public float PenetrationPhys;

            [Tooltip("Magical penetration (reduces Ward).")]
            [Range(0f, 1f)] public float PenetrationMag;

            [Tooltip("Astral element resistance (0-1).")]
            [Range(0f, 1f)] public float ResistAstral;

            [Tooltip("Lunar element resistance (0-1).")]
            [Range(0f, 1f)] public float ResistLunar;

            [Tooltip("Umbral element resistance (0-1).")]
            [Range(0f, 1f)] public float ResistUmbral;

            [Tooltip("Seconds needed to traverse one tile (0.1-1.0).")]
            [Range(0.1f, 1f)] public float SecondsPerTile;

            public static RecommendedAttributes Default()
            {
                var stats = new RecommendedAttributes
                {
                    Accuracy = 0,
                    Evasion = 0,
                    CritChance = 0.1f,
                    CritMult = 1.5f,
                    StatusPotency = 1f,
                    StatusResist = 0f,
                    PenetrationPhys = 0f,
                    PenetrationMag = 0f,
                    ResistAstral = 0f,
                    ResistLunar = 0f,
                    ResistUmbral = 0f,
                    SecondsPerTile = 0.2f
                };
                stats.Clamp();
                return stats;
            }

            public void Clamp()
            {
                CritChance = Mathf.Clamp01(CritChance);
                CritMult = Mathf.Clamp(CritMult, 1f, 2.5f);
                StatusPotency = Mathf.Max(0f, StatusPotency);
                StatusResist = Mathf.Max(0f, StatusResist);
                PenetrationPhys = Mathf.Clamp01(PenetrationPhys);
                PenetrationMag = Mathf.Clamp01(PenetrationMag);
                ResistAstral = Mathf.Clamp01(ResistAstral);
                ResistLunar = Mathf.Clamp01(ResistLunar);
                ResistUmbral = Mathf.Clamp01(ResistUmbral);
                SecondsPerTile = Mathf.Clamp(SecondsPerTile, 0.1f, 1f);
            }
        }

        [Serializable]
        public struct OptionalAttributes
        {
            [Tooltip("Temporary barrier that absorbs damage before HP.")]
            public int Shield;

            [Tooltip("Poise / stagger threshold.")]
            public int Poise;

            [Tooltip("Ammo / charge count for limited abilities.")]
            public int Ammo;

            [Tooltip("Sight range measured in tiles.")]
            public int SightRange;

            [Tooltip("Whether the unit requires line of sight to target.")]
            public bool RequiresLoS;

            [Tooltip("Weight / load value.")]
            public float Weight;

            [Tooltip("Cooldown reduction (0-1).")]
            [Range(0f, 1f)] public float CDR;

            public static OptionalAttributes Default()
            {
                var stats = new OptionalAttributes
                {
                    Shield = 0,
                    Poise = 0,
                    Ammo = 0,
                    SightRange = 6,
                    RequiresLoS = true,
                    Weight = 0f,
                    CDR = 0f
                };
                stats.Clamp();
                return stats;
            }

            public void Clamp()
            {
                Shield = Mathf.Max(0, Shield);
                Poise = Mathf.Max(0, Poise);
                Ammo = Mathf.Max(0, Ammo);
                SightRange = Mathf.Max(0, SightRange);
                // Weight intentionally left without clamping to support custom rules.
                CDR = Mathf.Clamp01(CDR);
            }
        }
    }
}
