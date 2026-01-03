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
        [Tooltip("Act number used to select this settings asset. Expected range: 1..4.")]
        [Min(1)]
        public int actNumber = 1;

        // Legacy fields kept to avoid breaking existing serialized assets.
        // Not used by runtime anymore.
        [SerializeField, HideInInspector]
        private string chapterId;

        [SerializeField, HideInInspector]
        private bool isAct1 = true;

        [Header("Map Generation")]
        [Tooltip("Recipe used to build the hex grid for this chapter.")]
        public GridRecipe gridRecipe;
        public bool IsAct1 => actNumber == 1;
        [Tooltip("Number of elite encounters to place on the map.")]
        public int eliteCount = 3;
        [Tooltip("Number of merchant encounters to place on the map.")]
        public int merchantCount = 2;
        [Tooltip("Number of mystery nodes to place on the map.")]
        public int mysteryCount = 4;
        [Tooltip("Number of empty tiles to place (reduces map density). Empty tiles have no encounter.")]
        public int emptyCount = 6;

        [Header("Generation Rules")]
        [Tooltip("No elites can appear within the bottom X rows (counting upward from the minimum row). 0 = no restriction.")]
        public int noEliteBottomRows = 0;

        [Tooltip("Minimum spacing between merchants, measured in hex distance. Must be strictly greater than this value. 0 = no restriction.")]
        public int merchantMinSeparation = 0;

        [Header("Tide Settings")]
        [Tooltip("Number of player moves before the tide rises by one row.")]
        public int movesPerTideStep = 3;
        [Tooltip("Delay in seconds before the tide animation plays when the tide rises.")]
        public float tideAnimationDelay = 0.5f;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (actNumber < 1) actNumber = 1;
            if (actNumber > 4) actNumber = 4;
        }
#endif
    }
}
