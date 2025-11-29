using UnityEngine;
using Core.Hex;
using Game.Common;

namespace Game.Grid
{
    public enum HexTerrainType
    {
        Ground,
        Swamp,
        Obstacle,
        Wall,
        Pit
    }

    public enum FogStatus
    {
        Unknown,    // 黑雾
        Ghost,      // 残影 (灰)
        Visible     // 可见
    }

    [DisallowMultipleComponent]
    public class HexCell : MonoBehaviour
    {
        [Header("Coordinates")]
        public TileTag tileTag;

        [Header("Game Logic")]
        public HexTerrainType terrainType = HexTerrainType.Ground;

        [Header("Fog State")]
        public FogStatus fogStatus = FogStatus.Unknown;

        private MeshRenderer _meshRenderer;
        private MaterialPropertyBlock _mpb;
        private static readonly int ColorPropID = Shader.PropertyToID("_BaseColor");
        private static readonly int TintPropID = Shader.PropertyToID("_Color");

        public bool BlocksSight => terrainType == HexTerrainType.Obstacle || terrainType == HexTerrainType.Wall;
        public bool IsTerrainWalkable => terrainType != HexTerrainType.Obstacle && terrainType != HexTerrainType.Wall;
        public HexCoords Coords => tileTag != null ? tileTag.Coords : default;

        private void Awake()
        {
            if (!tileTag) tileTag = GetComponent<TileTag>();
            _meshRenderer = GetComponent<MeshRenderer>();
            _mpb = new MaterialPropertyBlock();
        }

        public int GetBaseMoveCost()
        {
            switch (terrainType)
            {
                case HexTerrainType.Ground: return 1;
                case HexTerrainType.Swamp: return 2;
                default: return 999;
            }
        }

        public void SetFogStatus(FogStatus status)
        {
            fogStatus = status;
            UpdateFogVisuals();
        }

        private void UpdateFogVisuals()
        {
            if (_meshRenderer == null) return;

            _meshRenderer.GetPropertyBlock(_mpb);

            Color targetColor = Color.white;

            switch (fogStatus)
            {
                case FogStatus.Visible:
                    targetColor = Color.white;
                    break;
                case FogStatus.Ghost:
                    targetColor = new Color(0.5f, 0.5f, 0.6f, 1f); // 变灰
                    break;
                case FogStatus.Unknown:
                    targetColor = Color.black; // 变黑
                    break;
            }

            _mpb.SetColor(ColorPropID, targetColor);
            _mpb.SetColor(TintPropID, targetColor);

            _meshRenderer.SetPropertyBlock(_mpb);
        }
    }
}