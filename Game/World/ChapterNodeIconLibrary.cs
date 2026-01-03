using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Minimal icon mapping for the Chapter Map: ChapterNodeType -> 3D prefab.
    /// </summary>
    [CreateAssetMenu(menuName = "HexaTide/Chapter Node Icon Library", fileName = "ChapterNodeIconLibrary")]
    public sealed class ChapterNodeIconLibrary : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public ChapterNodeType type;
            public GameObject prefab;
        }

        [Header("Mapping")]
        public List<Entry> entries = new List<Entry>();

        private Dictionary<ChapterNodeType, GameObject> _cache;

        private void OnEnable() => RebuildCache();

#if UNITY_EDITOR
        private void OnValidate() => RebuildCache();
#endif

        private void RebuildCache()
        {
            _cache = new Dictionary<ChapterNodeType, GameObject>();
            if (entries == null) return;
            foreach (var e in entries)
            {
                if (e.prefab == null) continue;
                _cache[e.type] = e.prefab;
            }
        }

        public bool TryGetPrefab(ChapterNodeType type, out GameObject prefab)
        {
            if (_cache == null) RebuildCache();
            if (_cache != null && _cache.TryGetValue(type, out prefab)) return prefab != null;
            prefab = null;
            return false;
        }
    }
}
