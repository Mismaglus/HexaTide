using UnityEngine;
using Core.Hex;
using Game.Common;

namespace Game.Grid
{
    // 地形类型枚举
    public enum HexTerrainType
    {
        Ground,     // 平地：消耗 1
        Swamp,      // 沼泽：消耗 2
        Obstacle,   // 障碍：阻挡
        Wall,       // 墙体：阻挡视线
        Pit         // 深坑
    }

    // 迷雾状态枚举
    public enum FogStatus
    {
        Unknown,    // 未探索 (黑雾)
        Sensed,     // 感知范围内 (浅灰/模糊)
        Ghost,      // 记忆中 (残影)
        Visible     // 可见
    }

    /// <summary>
    /// 存储单个六边形格子的逻辑数据 (地形、迷雾、阻挡等)
    /// </summary>
    [DisallowMultipleComponent]
    public class HexCell : MonoBehaviour
    {
        [Header("Coordinates")]
        public TileTag tileTag;

        [Header("Game Logic")]
        public HexTerrainType terrainType = HexTerrainType.Ground;

        [Header("Fog State")]
        public FogStatus fogStatus = FogStatus.Unknown;

        [Header("Fog Colors")]
        [SerializeField] private Color colorVisible = Color.white;
        [SerializeField] private Color colorGhost = new Color(0.25f, 0.23f, 0.28f, 1f);   // 柔和的暖灰紫
        [SerializeField] private Color colorSensed = new Color(0.32f, 0.30f, 0.35f, 1f); // 介于可见与记忆之间
        [SerializeField] private Color colorUnknown = new Color(0.06f, 0.08f, 0.12f, 1f); // 深蓝灰，而非纯黑

        // 供外部读取（例如 HexHighlighter 想使用每个格子的自定义雾色）
        public Color FogColorVisible => colorVisible;
        public Color FogColorGhost => colorGhost;
        public Color FogColorSensed => colorSensed;
        public Color FogColorUnknown => colorUnknown;

        // 缓存渲染器用于变色
        private MeshRenderer _meshRenderer;
        private MaterialPropertyBlock _mpb;
        private static readonly int ColorPropID = Shader.PropertyToID("_BaseColor"); // URP Lit
        private static readonly int TintPropID = Shader.PropertyToID("_Color");      // Standard/Unlit

        // 当前地形是否阻挡视线
        public bool BlocksSight => terrainType == HexTerrainType.Obstacle || terrainType == HexTerrainType.Wall;

        // 当前地形是否可行走
        public bool IsTerrainWalkable => terrainType != HexTerrainType.Obstacle && terrainType != HexTerrainType.Wall;

        public HexCoords Coords => tileTag != null ? tileTag.Coords : default;

        private void Awake()
        {
            if (!tileTag) tileTag = GetComponent<TileTag>();
            _meshRenderer = GetComponent<MeshRenderer>();
            _mpb = new MaterialPropertyBlock();
        }

        // 出生时立刻应用当前的 FogStatus
        private void Start()
        {
            UpdateFogVisuals();
        }

        // ⭐⭐⭐ 补回丢失的方法：获取移动消耗 ⭐⭐⭐
        public int GetBaseMoveCost()
        {
            switch (terrainType)
            {
                case HexTerrainType.Ground: return 1;
                case HexTerrainType.Swamp: return 2;
                default: return 999; // 不可通行
            }
        }

        /// <summary>
        /// 更新迷雾状态并改变格子的视觉表现
        /// </summary>
        public void SetFogStatus(FogStatus status)
        {
            if (fogStatus == status) return;
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
                    targetColor = colorVisible; // 原色
                    break;
                case FogStatus.Ghost:
                    targetColor = colorGhost;
                    break;
                case FogStatus.Sensed:
                    targetColor = colorSensed;
                    break;
                case FogStatus.Unknown:
                    targetColor = colorUnknown;
                    break;
            }

            // 兼容不同的 Shader 属性名
            _mpb.SetColor(ColorPropID, targetColor);
            _mpb.SetColor(TintPropID, targetColor);

            _meshRenderer.SetPropertyBlock(_mpb);
        }
    }
}
