using UnityEngine;
using Core.Hex;
using Game.Common;
namespace Game.Grid
{
    // 地形类型枚举
    public enum HexTerrainType
    {
        Ground,     // 平地：消耗 1
        Swamp,      // 沼泽：消耗 2 (限制风筝)
        Obstacle,   // 障碍：阻挡移动和视线
        Wall,       // 墙体：阻挡移动和视线，可被破坏
        Pit         // 深坑：阻挡地面单位，飞行可过
    }

    // 迷雾状态枚举
    public enum FogStatus
    {
        Unknown,    // 未探索 (黑雾)
        Sense,      // 感知中 (波纹)
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
        public TileTag tileTag; // 引用同物体上的 TileTag

        [Header("Game Logic")]
        public HexTerrainType terrainType = HexTerrainType.Ground;
        public FogStatus fogStatus = FogStatus.Unknown;

        // 当前地形是否阻挡视线
        public bool BlocksSight => terrainType == HexTerrainType.Obstacle || terrainType == HexTerrainType.Wall;

        // 当前地形是否可行走 (不考虑单位占用，只看地形)
        public bool IsTerrainWalkable => terrainType != HexTerrainType.Obstacle && terrainType != HexTerrainType.Wall;

        public HexCoords Coords => tileTag != null ? tileTag.Coords : default;

        private void Awake()
        {
            if (!tileTag) tileTag = GetComponent<TileTag>();
        }

        // 获取该格子的基础移动消耗
        public int GetBaseMoveCost()
        {
            switch (terrainType)
            {
                case HexTerrainType.Ground: return 1;
                case HexTerrainType.Swamp: return 2; // 沼泽消耗加倍
                default: return 999; // 不可通行
            }
        }
    }
}