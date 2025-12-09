// Scripts/UI/Game/Inventory/InventorySideBarUI.cs
using System.Collections.Generic;
using UnityEngine;
using Game.Inventory;
using Game.Battle;
using Game.Units;
using System.Linq;
using Game.UI; // For SkillTooltipController

namespace Game.UI.Inventory
{
    public class InventorySideBarUI : MonoBehaviour
    {
        [Header("Configuration")]
        [Tooltip("Type of items to show (Consumable/Relic)")]
        public ItemType targetType;

        [Header("Scene References")]
        public Transform contentRoot;
        public InventorySlotUI slotPrefab;

        [Header("System Refs (Auto-Found)")]
        public AbilityTargetingSystem targetingSystem;
        public SkillTooltipController tooltipController; // ⭐ New

        // Cache
        private UnitInventory _currentInventory;
        private List<InventorySlotUI> _activeSlots = new List<InventorySlotUI>();

        // State
        private bool _isPlayerBound = false;
        private int _currentSelectedIndex = -1;

        void Start()
        {
            ResolveRefs();
            FindAndBindPlayerInventory();
        }

        void Update()
        {
            if (!_isPlayerBound) FindAndBindPlayerInventory();
            if (targetingSystem == null) ResolveRefs();

            if (targetingSystem != null && !targetingSystem.IsTargeting && _currentSelectedIndex != -1)
            {
                ClearSelection();
            }
        }

        void ResolveRefs()
        {
            if (targetingSystem == null)
                targetingSystem = FindFirstObjectByType<AbilityTargetingSystem>(FindObjectsInactive.Include);

            if (tooltipController == null)
                tooltipController = FindFirstObjectByType<SkillTooltipController>(FindObjectsInactive.Include);
        }

        void OnDestroy()
        {
            if (_currentInventory != null)
                _currentInventory.OnInventoryChanged -= Refresh;
        }

        void FindAndBindPlayerInventory()
        {
            var allUnits = FindObjectsByType<BattleUnit>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            var playerUnit = allUnits.FirstOrDefault(u => u.isPlayer);

            if (playerUnit != null)
            {
                _currentInventory = playerUnit.GetComponent<UnitInventory>();
                if (_currentInventory != null)
                {
                    _currentInventory.OnInventoryChanged -= Refresh;
                    _currentInventory.OnInventoryChanged += Refresh;
                    Refresh();
                    _isPlayerBound = true;
                    Debug.Log($"[InventorySideBar] Successfully bound to player: {playerUnit.name}");
                }
            }
        }

        void Refresh()
        {
            foreach (Transform child in contentRoot) Destroy(child.gameObject);
            _activeSlots.Clear();
            _currentSelectedIndex = -1;

            if (_currentInventory == null) return;

            // Get owner for tooltip calculations (stats scaling etc.)
            BattleUnit owner = _currentInventory.GetComponent<BattleUnit>();

            var slotsData = _currentInventory.Slots;
            for (int i = 0; i < slotsData.Count; i++)
            {
                var data = slotsData[i];
                if (data.IsEmpty) continue;
                if (data.item.type != targetType) continue;

                var go = Instantiate(slotPrefab, contentRoot);
                var ui = go.GetComponent<InventorySlotUI>();

                if (ui != null)
                {
                    // ⭐ Updated Setup call with Tooltip Controller and Owner
                    ui.Setup(data.item, data.count, i, OnSlotClicked, tooltipController, owner);
                    _activeSlots.Add(ui);
                }
            }
        }

        void OnSlotClicked(int inventoryIndex)
        {
            if (targetingSystem == null) ResolveRefs();

            if (_currentInventory == null) return;
            if (inventoryIndex < 0 || inventoryIndex >= _currentInventory.Slots.Count) return;

            var slot = _currentInventory.Slots[inventoryIndex];
            var item = slot.item;

            Debug.Log($"[Inventory] Clicked {item.name} (Type: {item.type})");

            if (item.type == ItemType.Consumable && item is ConsumableItem consumable)
            {
                if (consumable.abilityToCast != null)
                {
                    if (targetingSystem != null)
                    {
                        var owner = _currentInventory.GetComponent<BattleUnit>();
                        targetingSystem.EnterTargetingMode(consumable.abilityToCast, consumable, owner);

                        UpdateHighlightVisuals(inventoryIndex);
                        _currentSelectedIndex = inventoryIndex;
                    }
                    else
                    {
                        Debug.LogError("[Inventory] AbilityTargetingSystem not found!");
                    }
                }
            }
            else if (item.type == ItemType.Relic)
            {
                Debug.Log("Relic selected (Passive).");
            }
        }

        void UpdateHighlightVisuals(int selectedInventoryIndex)
        {
            foreach (var ui in _activeSlots)
            {
                bool shouldHighlight = (ui.Index == selectedInventoryIndex);
                ui.SetHighlightState(shouldHighlight);
            }
        }

        void ClearSelection()
        {
            _currentSelectedIndex = -1;
            foreach (var ui in _activeSlots)
            {
                ui.SetHighlightState(false);
            }
        }
    }
}