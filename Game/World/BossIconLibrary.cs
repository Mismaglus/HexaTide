using System;
using System.Collections.Generic;
using Game.Localization;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Boss-specific icon mapping.
    /// - Boss roster (boss ids) is sourced from localization files: Resources/Localization/en/Bosses.json
    /// - Prefab mapping is configured here: bossId -> 3D prefab
    /// </summary>
    [CreateAssetMenu(menuName = "HexaTide/Boss Icon Library", fileName = "BossIconLibrary")]
    public sealed class BossIconLibrary : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string bossId;
            public GameObject prefab;
        }

        [Header("Prefab Mapping")]
        public List<Entry> entries = new List<Entry>();

        [Tooltip("Optional fallback if bossId isn't mapped.")]
        public GameObject defaultBossPrefab;

        private Dictionary<string, GameObject> _prefabById;

        private void OnEnable() => RebuildCache();

#if UNITY_EDITOR
        private void OnValidate() => RebuildCache();
#endif

        private void RebuildCache()
        {
            _prefabById = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);
            if (entries == null) return;
            foreach (var e in entries)
            {
                if (string.IsNullOrEmpty(e.bossId) || e.prefab == null) continue;
                _prefabById[e.bossId] = e.prefab;
            }
        }

        /// <summary>
        /// Loads boss ids from localization JSON (stable roster source).
        /// We use English file for stability across language selection.
        /// </summary>
        public static string[] LoadBossIdsFromLocalization()
        {
            var asset = Resources.Load<TextAsset>("Localization/en/Bosses");
            if (asset == null) return Array.Empty<string>();

            try
            {
                var parsed = JsonUtility.FromJson<LocalizationItemFile>(asset.text);
                if (parsed?.items == null || parsed.items.Length == 0) return Array.Empty<string>();

                var ids = new List<string>(parsed.items.Length);
                foreach (var item in parsed.items)
                {
                    if (item == null) continue;
                    if (string.IsNullOrEmpty(item.id)) continue;
                    ids.Add(item.id);
                }
                return ids.ToArray();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        /// <summary>
        /// Deterministically picks a boss id given a seed.
        /// Does not touch GameRandom to avoid affecting other generation randomness.
        /// </summary>
        public string PickBossId(int seed)
        {
            var ids = LoadBossIdsFromLocalization();
            if (ids.Length == 0) return null;
            var rng = new System.Random(unchecked(seed * 73856093) ^ 0x2F3A5B7D);
            return ids[rng.Next(0, ids.Length)];
        }

        public bool TryGetPrefab(string bossId, out GameObject prefab)
        {
            prefab = null;
            if (_prefabById == null) RebuildCache();
            if (!string.IsNullOrEmpty(bossId) && _prefabById != null && _prefabById.TryGetValue(bossId, out prefab) && prefab != null)
                return true;

            prefab = defaultBossPrefab;
            return prefab != null;
        }

        public static string GetLocalizedBossName(string bossId)
        {
            if (string.IsNullOrEmpty(bossId)) return string.Empty;
            return LocalizationManager.Get($"{bossId}_NAME");
        }
    }
}
