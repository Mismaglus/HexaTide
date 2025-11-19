using Game.Battle;
using Game.Common;
using System.Collections.Generic;

namespace Game.Grid
{
    // 任何“六边网格运行时”都实现它（BattleHexGrid、以后 WorldHexGrid 也行）
    public interface IHexGridProvider
    {
        uint Version { get; }                 // 网格版本，变化时让 Unit 重建缓存
        GridRecipe recipe { get; }            // 网格厚度等参数（已有 ScriptableObject）
        IEnumerable<TileTag> EnumerateTiles();// 返回所有 TileTag（含隐藏）
    }
}
