using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.World
{
    [Serializable]
    public class RegionTheme
    {
        public string regionId;
        public Material tileMaterial;
        public Material borderMaterial;
    }

    [CreateAssetMenu(menuName = "HexaTide/Region Theme DB", fileName = "RegionThemeDB")]
    public class RegionThemeDB : ScriptableObject
    {
        public List<RegionTheme> themes = new List<RegionTheme>();

        public bool TryGetTheme(string regionId, out RegionTheme theme)
        {
            theme = null;
            if (string.IsNullOrEmpty(regionId) || themes == null) return false;

            for (int i = 0; i < themes.Count; i++)
            {
                var t = themes[i];
                if (t == null) continue;
                if (string.Equals(t.regionId, regionId, StringComparison.OrdinalIgnoreCase))
                {
                    theme = t;
                    return true;
                }
            }
            return false;
        }
    }
}
