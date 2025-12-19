using UnityEngine;
using Core.Hex;
using Game.Common;

namespace Game.Grid
{
    // Terrain Types
    public enum HexTerrainType
    {
        Ground,     // Cost: 1
        Swamp,      // Cost: 2
        Obstacle,   // Blocked
        Wall,       // Blocked + Sight Block
        Pit         // Blocked (Fall)
    }

    // Fog Status
    public enum FogStatus
    {
        Unknown,    // Black fog
        Sensed,     // Ripples
        Ghost,      // Explored but not current
        Visible     // Active vision
    }

    [DisallowMultipleComponent]
    public class HexCell : MonoBehaviour
    {
        [Header("Coordinates")]
        public TileTag tileTag;

        [Header("Game Logic")]
        public HexTerrainType terrainType = HexTerrainType.Ground;

        [Header("Tide State")]
        [SerializeField] private bool _isFlooded = false;
        public bool IsFlooded => _isFlooded;

        [Header("Fog State")]
        public FogStatus fogStatus = FogStatus.Unknown;

        [Header("Colors (Config)")]
        // Default to White (Neutral) so standard logic isn't messed up
        [SerializeField] private Color colorVisible = Color.white;
        [SerializeField] private Color colorGhost = new Color(0.7f, 0.7f, 0.7f, 1f);
        [SerializeField] private Color colorSensed = new Color(0.6f, 0.4f, 0.4f, 1f);
        [SerializeField] private Color colorUnknown = new Color(0.5f, 0.5f, 0.5f, 0.1f);

        [Header("Tide Visual")]
        [SerializeField] private Color colorFlooded = new Color(0.1f, 0.0f, 0.2f, 1f); // Dark Purple

        // Public accessors for Highlighter
        public Color FogColorVisible => _isFlooded ? colorFlooded : colorVisible;
        public Color FogColorGhost => _isFlooded ? colorFlooded : colorGhost;
        public Color FogColorSensed => _isFlooded ? colorFlooded : colorSensed;
        public Color FogColorUnknown => _isFlooded ? colorFlooded : colorUnknown;

        // Visuals
        private MeshRenderer _meshRenderer;
        private MaterialPropertyBlock _mpb;
        private static readonly int ColorPropID = Shader.PropertyToID("_BaseColor");
        private static readonly int TintPropID = Shader.PropertyToID("_Color");

        // Logic Accessors
        public bool BlocksSight => terrainType == HexTerrainType.Obstacle || terrainType == HexTerrainType.Wall;

        // Walkable if terrain allows AND not flooded
        public bool IsTerrainWalkable =>
            !_isFlooded &&
            terrainType != HexTerrainType.Obstacle &&
            terrainType != HexTerrainType.Wall;

        public HexCoords Coords => tileTag != null ? tileTag.Coords : default;

        private void Awake()
        {
            if (!tileTag) tileTag = GetComponent<TileTag>();
            _meshRenderer = GetComponent<MeshRenderer>();
            _mpb = new MaterialPropertyBlock();
        }

        private void Start()
        {
            RefreshFogVisuals();
        }

        public int GetBaseMoveCost()
        {
            if (_isFlooded) return 999;

            switch (terrainType)
            {
                case HexTerrainType.Ground: return 1;
                case HexTerrainType.Swamp: return 2;
                default: return 999;
            }
        }

        // === Tide Logic ===
        public void SetFlooded(bool state)
        {
            if (_isFlooded == state) return;
            _isFlooded = state;
            RefreshFogVisuals();
        }

        public void SetFogStatus(FogStatus status)
        {
            if (fogStatus == status) return;
            fogStatus = status;
            RefreshFogVisuals();
        }

        public void RefreshFogVisuals()
        {
            if (_meshRenderer == null) return;

            // 1. Start clean: Remove all overrides so the default Material shows through
            _meshRenderer.GetPropertyBlock(_mpb);
            _mpb.Clear();

            bool applyTint = false;
            Color targetColor = Color.white;

            // 2. Determine if we need to apply a tint (Tide OR Fog)
            if (_isFlooded)
            {
                applyTint = true;
                targetColor = colorFlooded;
            }
            else
            {
                switch (fogStatus)
                {
                    case FogStatus.Visible:
                        // ⭐ CRITICAL: Do NOT apply tint. Let the material render naturally.
                        applyTint = false;
                        break;

                    case FogStatus.Ghost:
                        applyTint = true;
                        targetColor = colorGhost;
                        break;
                    case FogStatus.Sensed:
                        applyTint = true;
                        targetColor = colorSensed;
                        break;
                    case FogStatus.Unknown:
                        applyTint = true;
                        targetColor = colorUnknown;
                        break;
                }
            }

            // 3. Apply override only if necessary
            if (applyTint)
            {
                _mpb.SetColor(ColorPropID, targetColor);
                _mpb.SetColor(TintPropID, targetColor);
            }

            _meshRenderer.SetPropertyBlock(_mpb);
        }
    }
}