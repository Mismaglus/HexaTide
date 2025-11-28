using System;
using UnityEngine;

namespace Game.Units
{
    [DisallowMultipleComponent]
    public class UnitAttributes : MonoBehaviour
    {
        [Header("Core")]
        public CoreAttributes Core = CoreAttributes.Default();

        [Header("Recommended")]
        public RecommendedAttributes Recommended = RecommendedAttributes.Default();

        [Header("Optional")]
        public OptionalAttributes Optional = OptionalAttributes.Default();

        void Reset() { Core = CoreAttributes.Default(); Recommended = RecommendedAttributes.Default(); Optional = OptionalAttributes.Default(); ClampAll(); }
        void OnValidate() { ClampAll(); }
        void ClampAll() { Core.Clamp(); Recommended.Clamp(); Optional.Clamp(); }

        [Serializable]
        public struct CoreAttributes
        {
            [Header("Primary Stats")]
            [Range(0, 50)] public int Str;
            [Range(0, 50)] public int Int;
            [Range(0, 50)] public int Dex;
            [Range(0, 50)] public int Faith;

            [Header("Vitals")]
            [Range(1, 999)] public int HPMax;
            public int HP;

            [Header("Action Resources")]
            [Range(0, 10)] public int MaxAP;
            public int CurrentAP;

            [Range(0, 200)] public int MPMax;
            public int MP;
            [Range(0, 50)] public int MPRecovery;

            [Header("Movement & Defense")]
            [Tooltip("Max movement steps per turn.")]
            [Range(2, 6)] public int Stride;

            [Tooltip("Current movement steps remaining.")]
            public int CurrentStride;

            [Range(0, 100)] public int Initiative;
            [Range(0f, 0.6f)] public float Armor;
            [Range(0f, 0.6f)] public float Ward;

            public static CoreAttributes Default()
            {
                return new CoreAttributes
                {
                    Str = 10,
                    Int = 10,
                    Dex = 10,
                    Faith = 10,
                    HPMax = 100,
                    HP = 100,
                    MaxAP = 4,
                    CurrentAP = 4,
                    MPMax = 50,
                    MP = 50,
                    MPRecovery = 2,
                    Stride = 3,
                    CurrentStride = 3,
                    Initiative = 20,
                    Armor = 0f,
                    Ward = 0f
                };
            }

            public void Clamp()
            {
                HPMax = Mathf.Max(1, HPMax); HP = Mathf.Clamp(HP, 0, HPMax);
                MPMax = Mathf.Max(0, MPMax); MP = Mathf.Clamp(MP, 0, MPMax); MPRecovery = Mathf.Max(0, MPRecovery);
                MaxAP = Mathf.Clamp(MaxAP, 0, 10); CurrentAP = Mathf.Clamp(CurrentAP, 0, MaxAP);
                Stride = Mathf.Clamp(Stride, 2, 6); CurrentStride = Mathf.Clamp(CurrentStride, 0, Stride);
            }
        }

        [Serializable]
        public struct RecommendedAttributes
        {
            public int Accuracy;
            public int Evasion;
            [Range(0f, 1f)] public float CritChance;
            [Range(1f, 2.5f)] public float CritMult;
            public float StatusPotency;
            public float StatusResist;
            [Range(0f, 1f)] public float PenetrationPhys;
            [Range(0f, 1f)] public float PenetrationMag;

            public static RecommendedAttributes Default() => new RecommendedAttributes { CritChance = 0.1f, CritMult = 1.5f, StatusPotency = 1f, SecondsPerTile = 0.2f };

            [Range(0.1f, 1f)] public float SecondsPerTile;
            public void Clamp() { CritChance = Mathf.Clamp01(CritChance); CritMult = Mathf.Clamp(CritMult, 1f, 2.5f); }
        }

        [Serializable]
        public struct OptionalAttributes
        {
            public int Shield;
            public int Poise;
            public int Ammo;
            public int SightRange;
            public bool RequiresLoS;

            // ⭐ 新增：是否有能力看到未来（黄色）意图
            [Tooltip("If true, this unit can see Future (Yellow/Orange) intents.")]
            public bool CanSeeFutureIntents;

            public static OptionalAttributes Default() => new OptionalAttributes { SightRange = 6, RequiresLoS = true, CanSeeFutureIntents = false };
            public void Clamp() { Shield = Mathf.Max(0, Shield); }
        }
    }
}