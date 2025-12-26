using UnityEngine;
using Game.Grid;
using Game.Battle;

namespace Game.World
{
    /// <summary>
    /// ScriptableObject that defines generation rules and theme for a single chapter.
    /// This allows a single MapScene to load different Acts/chapters based on settings rather than unique scenes.
    /// </summary>
    [CreateAssetMenu(menuName = "HexaTide/Chapter Settings", fileName = "ChapterSettings")]
    public class ChapterSettings : ScriptableObject
    {
        [Header("Identification")]
        [Tooltip("Unique ID used to identify this chapter in FlowContext.")]
        public string chapterId;

        [Header("Map Generation")]
        [Tooltip("Recipe used to build the hex grid for this chapter.")]
        public GridRecipe gridRecipe;
        [Tooltip("If true, this chapter uses the Act1 logic (three gates at the top row). If false, a single boss will be placed.")]
        public bool isAct1 = true;
        [Tooltip("Number of elite encounters to place on the map.")]
        public int eliteCount = 3;
        [Tooltip("Number of merchant encounters to place on the map.")]
        public int merchantCount = 2;
        [Tooltip("Number of mystery nodes to place on the map.")]
        public int mysteryCount = 4;

        [Header("Tide Settings")]
        [Tooltip("Number of player moves before the tide rises by one row.")]
        public int movesPerTideStep = 3;
        [Tooltip("Delay in seconds before the tide animation plays when the tide rises.")]
        public float tideAnimationDelay = 0.5f;
    }
}
