// Scripts/Game/Battle/Abilities/AbilityContext.cs
using System.Collections.Generic;
using Core.Hex;
using Game.Battle;
using Game.Inventory; // ⭐ 新增引用

namespace Game.Battle.Abilities
{
    public class AbilityContext
    {
        public BattleUnit Caster;
        public HexCoords Origin;
        public List<BattleUnit> TargetUnits = new();
        public List<HexCoords> TargetTiles = new();

        // ⭐ 新增：记录来源物品 (如果是普通技能则为 null)
        public InventoryItem SourceItem;

        public bool HasAnyTarget => TargetUnits.Count > 0 || TargetTiles.Count > 0;
    }
}