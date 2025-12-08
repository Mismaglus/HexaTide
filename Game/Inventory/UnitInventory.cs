// Scripts/Game/Inventory/UnitInventory.cs
using System.Collections.Generic;
using UnityEngine;
using Game.Battle;
using Game.Units;

namespace Game.Inventory
{
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
                    // 即使只加了一部分也刷新，或者你可以选择回滚
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

        public void DropItem(int index)
        {
            RemoveItemAt(index, 999); // 全部丢弃
        }

        // 索引版本 (UI 直接调用时使用)
        public void ConsumeItem(int index)
        {
            if (index < 0 || index >= slots.Count) return;
            if (slots[index].item is ConsumableItem c && c.consumeOnUse)
            {
                RemoveItemAt(index, 1);
            }
        }

        // ⭐ 对象版本 (AbilityAction 调用这个)
        // 之前被截断的部分就在这里
        public void ConsumeItem(InventoryItem item, int amount = 1)
        {
            // 简单逻辑：找到第一个匹配的格子并扣除
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].item == item)
                {
                    RemoveItemAt(i, amount);
                    return; // 找到并扣除后立即返回，不继续查找
                }
            }
            Debug.LogWarning($"[UnitInventory] 试图消耗 {item.name} 但背包中未找到该物品！");
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