// Scripts/Game/Inventory/UnitInventory.cs
using System.Collections.Generic;
using UnityEngine;
using Game.Battle; // for BattleUnit
using Game.Units;  // for UnitAttributes

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

        [Header("Runtime Data")]
        [SerializeField] private List<Slot> slots = new List<Slot>();

        private BattleUnit _battleUnit;
        private UnitAttributes _attrs;

        // UI 监听事件
        public event System.Action OnInventoryChanged;

        public int Capacity => _attrs != null ? _attrs.Optional.MaxInventorySlots : 0;
        public int ItemCount
        {
            get
            {
                int c = 0;
                foreach (var s in slots) if (!s.IsEmpty) c++;
                return c;
            }
        }

        public IReadOnlyList<Slot> Slots => slots;

        void Awake()
        {
            _battleUnit = GetComponent<BattleUnit>();
            _attrs = GetComponent<UnitAttributes>();
        }

        void Start()
        {
            // 确保 Slot 列表大小匹配容量 (可选，或者动态增长)
            // 这里我们采用动态列表，但受 Capacity 限制
        }

        /// <summary>
        /// 尝试添加物品。
        /// </summary>
        /// <returns>如果是新占用了格子且格子不够，返回 false</returns>
        public bool TryAddItem(InventoryItem item, int amount = 1)
        {
            if (item == null || amount <= 0) return false;

            // 1. 尝试堆叠
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

            // 2. 放入新格子 (如果还有剩余 amount)
            while (amount > 0)
            {
                if (ItemCount >= Capacity)
                {
                    Debug.LogWarning($"[Inventory] {name} is full! Cannot add {item.name}");
                    return false; // 背包满
                }

                var newSlot = new Slot { item = item, count = Mathf.Min(amount, item.maxStack) };
                slots.Add(newSlot);

                // 触发物品的“获得”回调 (遗物生效)
                item.OnAcquire(_battleUnit);

                amount -= newSlot.count;
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
                InventoryItem removedItem = slot.item;
                slots.RemoveAt(index);

                // 触发物品的“移除”回调 (遗物失效)
                removedItem.OnRemove(_battleUnit);
            }

            NotifyChange();
        }

        /// <summary>
        /// 丢弃物品 (逻辑同移除，但未来可能加上掉落在地上的逻辑)
        /// </summary>
        public void DropItem(int index)
        {
            RemoveItemAt(index, 999); // 全部丢弃
        }

        /// <summary>
        /// 消耗物品 (通常由 ConsumableItem 调用)
        /// </summary>
        public void ConsumeItem(int index)
        {
            if (index < 0 || index >= slots.Count) return;
            var slot = slots[index];
            if (slot.IsEmpty) return;

            // 检查是否是消耗品
            if (slot.item is ConsumableItem cons && cons.consumeOnUse)
            {
                RemoveItemAt(index, 1);
            }
        }

        void NotifyChange()
        {
            OnInventoryChanged?.Invoke();
        }

        // 调试用
        [ContextMenu("Add Test Potion")]
        void DebugAddPotion()
        {
#if UNITY_EDITOR
            var p = UnityEditor.AssetDatabase.LoadAssetAtPath<InventoryItem>("Assets/_Assets/Items/Consumable/BasicPotion.asset");
            if (p != null) TryAddItem(p);
            else Debug.LogWarning("Could not find BasicPotion_C at Assets/_Assets/Items/Consumable/BasicPotion.asset");
#endif
        }
    }
}