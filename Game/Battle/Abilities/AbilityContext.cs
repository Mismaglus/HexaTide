// Scripts/Game/Battle/Abilities/AbilityContext.cs
using System.Collections.Generic;
using Core.Hex;
using Game.Battle;
using Game.Inventory; // ⭐ 必须引用 Inventory 命名空间

namespace Game.Battle.Abilities
{
    public class AbilityContext
    {
        public BattleUnit Caster;
        public HexCoords Origin;
        public List<BattleUnit> TargetUnits = new();
        public List<HexCoords> TargetTiles = new();

        // ⭐ 新增：记录触发这次技能的物品（如果是技能栏触发则为 null）
        public InventoryItem SourceItem;

        public bool HasAnyTarget => TargetUnits.Count > 0 || TargetTiles.Count > 0;
    }
}