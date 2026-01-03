using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Defines which bosses can appear in this chapter/region and how boss slots are assigned.
    /// This is intentionally separate from BossIconLibrary (bossId->prefab) to keep responsibilities clean.
    /// </summary>
    [CreateAssetMenu(menuName = "HexaTide/Boss Selection Profile", fileName = "BossSelectionProfile")]
    public sealed class BossSelectionProfile : ScriptableObject
    {
        public enum SlotKind
        {
            ToChapter2,
            ToChapter3,
            ToChapter4,
            Final
        }

        [Serializable]
        public struct Slot
        {
            public GateKind gateKind;
            public SlotKind kind;
        }

        [Header("Slots (what appears on the map)")]
        [Tooltip("Define which top-row nodes should get a bossId, and what kind of pool they draw from.")]
        public List<Slot> slots = new List<Slot>();

        [Header("Boss Pools")]
        public List<string> bossesToChapter2 = new List<string>();
        public List<string> bossesToChapter3 = new List<string>();
        public List<string> bossesToChapter4 = new List<string>();

        [Tooltip("If set, the final/fixed boss id (e.g., chapter 4). If empty, uses bossesToChapter4 or bossesToChapter3 depending on SlotKind.")]
        public string fixedFinalBossId;

        [Header("Selection")]
        [Tooltip("Try to avoid duplicates across slots.")]
        public bool avoidDuplicates = true;

        public List<string> GetPool(SlotKind kind)
        {
            return kind switch
            {
                SlotKind.ToChapter2 => bossesToChapter2,
                SlotKind.ToChapter3 => bossesToChapter3,
                SlotKind.ToChapter4 => bossesToChapter4,
                SlotKind.Final => bossesToChapter4,
                _ => bossesToChapter3,
            };
        }
    }
}
