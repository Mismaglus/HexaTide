// Scripts/Game/Inventory/MaterialItem.cs
using UnityEngine;

namespace Game.Inventory
{
    [CreateAssetMenu(menuName = "HexBattle/Inventory/Material Item")]
    public class MaterialItem : InventoryItem
    {
        public MaterialItem()
        {
            type = ItemType.Material;
            isStackable = true;
        }
    }
}