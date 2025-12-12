using UnityEngine;
using Core.Hex;

namespace Game.Battle
{
    public enum GridShape
    {
        Rectangle,
        Hexagon
    }

    [CreateAssetMenu(fileName = "BattleGridRecipe", menuName = "Game/Battle/Grid Recipe")]
    public class GridRecipe : ScriptableObject
    {
        [Header("Layout")]
        public GridShape shape = GridShape.Rectangle;

        [Header("Rectangle Settings")]
        [Min(1)] public int width = 6;
        [Min(1)] public int height = 6;

        [Header("Hexagon Settings")]
        [Tooltip("Radius in hexes (e.g. 3 = map diameter of ~7 hexes)")]
        [Min(1)] public int radius = 4;

        [Header("Hex Configuration")]
        public HexOrientation orientation = HexOrientation.PointyTop;
        public bool useOddROffset = true;

        [Header("Metrics & Visuals")]
        [Min(0.1f)] public float outerRadius = 1f;
        [Range(0f, 0.5f)] public float thickness = 0f;
        public Material tileMaterial;

        [Header("Borders")]
        public BorderMode borderMode = BorderMode.AllUnique;       // None/OuterOnly/AllUnique
        [Min(0.001f)] public float borderWidth = 0.05f;
        [Min(0f)] public float borderYOffset = 0.001f;
        public Color borderColor = new(1f, 1f, 1f, 0.65f);
        public Material borderMaterial;

        [Header("Holes / Empty")]
        public int[] emptyColumns;                  // Only used in Rectangle mode
        public bool enableRandomHoles = false;      // Random holes to create choke points
        [Range(0f, 0.9f)] public float holeChance = 0.0f;
        public int randomSeed = 12345;
    }
}