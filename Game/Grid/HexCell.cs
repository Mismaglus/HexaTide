using UnityEngine;
using Core.Hex;
using Game.Common;

namespace Game.Grid
{
    // 地形类型枚举
    public enum HexTerrainType
    {
        Ground,     // 平地
        Swamp,      // 沼泽
        Obstacle,   // 障碍
        Wall,       // 墙体
        Pit         // 深坑
    }

    // 迷雾状态枚举
    public enum FogStatus
    {
        Unknown,    // 未探索 (黑雾)
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

        // ⭐ 新增：出生时立刻应用当前的 FogStatus (默认为 Unknown=黑色)
        private void Start()
        {
            UpdateFogVisuals();
        }

        /// <summary>
        /// 更新迷雾状态并改变格子的视觉表现
        /// </summary>
        public void SetFogStatus(FogStatus status)
        {
            if (fogStatus == status) return; // 状态没变就不刷新，节省性能
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
                    targetColor = Color.white; // 原色
                    break;
                case FogStatus.Ghost:
                    targetColor = new Color(0.5f, 0.5f, 0.6f, 1f); // 灰色/暗淡
                    break;
                case FogStatus.Unknown:
                    targetColor = Color.black; // 纯黑
                    break;
            }

            // 兼容不同的 Shader 属性名
            _mpb.SetColor(ColorPropID, targetColor);
            _mpb.SetColor(TintPropID, targetColor);

            _meshRenderer.SetPropertyBlock(_mpb);
        }
    }
}