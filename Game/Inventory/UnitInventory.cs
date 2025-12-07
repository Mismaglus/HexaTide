// Scripts/Game/Inventory/UnitInventory.cs
using System.Collections.Generic;
using UnityEngine;
using Game.Battle;
using Game.Units;

namespace Game.Inventory
{
    /// <summary>
    /// 每个单位独立的背包组件。
    /// 负责存储物品实例，并在添加/移除时触发 OnAcquire/OnRemove 回调。
    /// </summary>
    [RequireComponent(typeof(BattleUnit))]
    public class UnitInventory : MonoBehaviour
    {
        [System.Serializable]
        public class Slot
        {
            public InventoryItem item;
            public int count;

            public bool IsEmpty => item == null || count <= 0;
            public void Clear() { item = null; count = 0; }
        }

        [Header("Runtime Storage")]
        [SerializeField] private List<Slot> slots = new List<Slot>();

        public event System.Action OnInventoryChanged;

        private BattleUnit _battleUnit;
        private UnitAttributes _attrs;

        public int Capacity => _attrs != null ? _attrs.Optional.MaxInventorySlots : 20;

        public IReadOnlyList<Slot> Slots => slots;

        void Awake()
        {
            _battleUnit = GetComponent<BattleUnit>();
            _attrs = GetComponent<UnitAttributes>();
        }

        /// <summary>
        /// 尝试添加物品。
        /// </summary>
        /// <returns>如果是新占用了格子且格子不够，返回 false</returns>
        public bool TryAddItem(InventoryItem item, int amount = 1)
        {
            if (item == null || amount <= 0) return false;

            // 1. 尝试堆叠 (Stacking)
            if (item.isStackable)
            {
                foreach (var slot in slots)
                {
                    if (slot.item == item && slot.count < item.maxStack)
                    {
                        int space = item.maxStack - slot.count;
                        int toAdd = Mathf.Min(space, amount);

                        slot.count += toAdd;
                        amount -= toAdd;

                        if (amount <= 0)
                        {
                            NotifyChange();
                            return true;
                        }
                    }
                }
            }

            // 2. 放入新格子 (New Slot)
            while (amount > 0)
            {
                if (slots.Count >= Capacity)
                {
                    Debug.LogWarning($"[Inventory] {name} is full! Cannot add {item.name}");
                    NotifyChange();
                    return false;
                }

                int toAdd = Mathf.Min(amount, item.maxStack);
                var newSlot = new Slot { item = item, count = toAdd };
                slots.Add(newSlot);

                // 触发物品的“获得”回调 (遗物生效)
                item.OnAcquire(_battleUnit);

                amount -= toAdd;
            }

            NotifyChange();
            return true;
        }

        /// <summary>
        /// 移除物品（指定索引）
        /// </summary>
        public void RemoveItemAt(int index, int amount = 1)
        {
            if (index < 0 || index >= slots.Count) return;

            var slot = slots[index];
            if (slot.IsEmpty) return;

            slot.count -= amount;

            if (slot.count <= 0)
            {
                // 彻底移除
                InventoryItem itemRef = slot.item;
                slots.RemoveAt(index);

                // 触发物品的“移除”回调 (遗物失效)
                if (itemRef != null) itemRef.OnRemove(_battleUnit);
            }

            NotifyChange();
        }

        /// <summary>
        /// 丢弃物品
        /// </summary>
        public void DropItem(int index)
        {
            RemoveItemAt(index, 999); // 全部丢弃
        }

        /// <summary>
        /// 消耗物品 (索引版本，通常由 UI 直接调用)
        /// </summary>
        public void ConsumeItem(int index)
        {
            if (index < 0 || index >= slots.Count) return;
            if (slots[index].item is ConsumableItem c && c.consumeOnUse)
            {
                RemoveItemAt(index, 1);
            }
        }

        /// <summary>
        /// 消耗物品 (对象版本，通常由 AbilityAction 调用)
        /// ⭐ 这是你需要的新方法
        /// </summary>
        public void ConsumeItem(InventoryItem item, int amount = 1)
        {
            // 简单逻辑：找到第一个匹配的格子并扣除
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].item == item)
                {
                    RemoveItemAt(i, amount);
                    return;
                }
            }
        }

        private void NotifyChange()
        {
            OnInventoryChanged?.Invoke();
        }

        // 调试用
        [ContextMenu("Add Test Potion (Editor Only)")]
        void DebugAddPotion()
        {
#if UNITY_EDITOR
            // 尝试加载一个示例药水
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:ConsumableItem");
            if (guids.Length > 0)
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                var item = UnityEditor.AssetDatabase.LoadAssetAtPath<InventoryItem>(path);
                TryAddItem(item, 1);
                Debug.Log($"[Debug] Added {item.name}");
            }
            else
            {
                Debug.LogWarning("No ConsumableItem found in project!");
            }
#endif
        }
    }
}