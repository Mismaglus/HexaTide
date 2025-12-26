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
        /// Retrieves the settings object for the given chapterId. Returns null if not found.
        /// </summary>
        public ChapterSettings GetSettings(string chapterId)
        {
            if (string.IsNullOrEmpty(chapterId) || settingsList == null)
                return null;
            foreach (var settings in settingsList)
            {
                if (settings != null && settings.chapterId == chapterId)
                    return settings;
            }
            return null;
        }
    }
}
