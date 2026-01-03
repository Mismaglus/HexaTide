using UnityEngine;
using System.Collections.Generic;

namespace Game.World
{
    /// <summary>
    /// Simple database of ChapterSettings objects. Provides lookup by chapterId.
    /// </summary>
    [CreateAssetMenu(menuName = "HexaTide/Chapter Settings DB", fileName = "ChapterSettingsDB")]
    public class ChapterSettingsDB : ScriptableObject
    {
        [Tooltip("List of all available chapter settings. Each entry must have a unique chapterId.")]
        public List<ChapterSettings> settingsList = new List<ChapterSettings>();

        /// <summary>
        /// Retrieves the settings object for the given act number (1..4). Returns null if not found.
        /// </summary>
        public ChapterSettings GetSettings(int actNumber)
        {
            if (actNumber <= 0 || settingsList == null)
                return null;
            foreach (var settings in settingsList)
            {
                if (settings != null && settings.actNumber == actNumber)
                    return settings;
            }
            return null;
        }

        /// <summary>
        /// Legacy lookup by chapterId. Supports strings like "ACT_1".."ACT_4".
        /// </summary>
        public ChapterSettings GetSettings(string chapterId)
        {
            if (string.IsNullOrEmpty(chapterId) || settingsList == null)
                return null;

            if (chapterId.StartsWith("ACT_", System.StringComparison.OrdinalIgnoreCase))
            {
                var suffix = chapterId.Substring("ACT_".Length);
                if (int.TryParse(suffix, out var act))
                {
                    var byAct = GetSettings(act);
                    if (byAct != null) return byAct;
                }
            }

            foreach (var settings in settingsList)
            {
                // Best-effort: some old assets might still rely on legacy chapterId.
                // We don't expose it publicly anymore, so only act-based lookup is guaranteed.
                if (settings != null && settings.name == chapterId)
                    return settings;
            }
            return null;
        }
    }
}
