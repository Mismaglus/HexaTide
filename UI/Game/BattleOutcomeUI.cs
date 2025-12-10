// Scripts/UI/Game/BattleOutcomeUI.cs
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using Game.Battle;
using Game.Inventory;
using Game.Units;
using Game.UI.Inventory;
using System.Collections.Generic;
using System.Linq;

namespace Game.UI
{
    public class BattleOutcomeUI : MonoBehaviour
    {
        [Header("Panels")]
        public GameObject victoryPanel;
        public GameObject defeatPanel;

        [Header("Loot List Configuration")]
        public Transform lootContainer;
        public LootSlotUI lootSlotPrefab;

        [Header("Loot Details Panel")]
        [Tooltip("Text to show description of selected item")]
        public TextMeshProUGUI descriptionText;
        [Tooltip("Text to show flavor/lore of selected item")]
        public TextMeshProUGUI flavorText;
        [TextArea]
        public string defaultHint = "Select an item to view details.";

        [Header("Reward Summary Labels")]
        public TextMeshProUGUI labelGold;
        public TextMeshProUGUI labelExp;

        [Header("Buttons")]
        public Button victoryContinueBtn;
        public Button defeatRetryBtn;
        public Button defeatQuitBtn;

        private BattleStateMachine _battleSM;
        private BattleRewardResult _cachedResult;

        void Awake()
        {
            if (victoryPanel) victoryPanel.SetActive(false);
            if (defeatPanel) defeatPanel.SetActive(false);

            if (victoryContinueBtn) victoryContinueBtn.onClick.AddListener(OnVictoryContinue);
            if (defeatRetryBtn) defeatRetryBtn.onClick.AddListener(OnDefeatRetry);
            if (defeatQuitBtn) defeatQuitBtn.onClick.AddListener(OnDefeatQuit);
        }

        void Start()
        {
            _battleSM = BattleStateMachine.Instance;
            if (_battleSM == null)
                _battleSM = FindFirstObjectByType<BattleStateMachine>(FindObjectsInactive.Include);

            if (_battleSM != null)
            {
                _battleSM.OnVictory += HandleVictory;
                _battleSM.OnDefeat += HandleDefeat;
            }
        }

        void OnDestroy()
        {
            if (_battleSM != null)
            {
                _battleSM.OnVictory -= HandleVictory;
                _battleSM.OnDefeat -= HandleDefeat;
            }
        }

        void HandleVictory()
        {
            if (victoryPanel) victoryPanel.SetActive(true);

            // Reset Detail View
            if (descriptionText) descriptionText.text = defaultHint;
            if (flavorText) flavorText.text = "";

            if (_battleSM != null)
            {
                _cachedResult = _battleSM.Rewards;
                ShowRewards(_cachedResult);
            }
        }

        void ShowRewards(BattleRewardResult rewards)
        {
            if (rewards == null) return;

            // 1. Show Currency
            if (labelGold) labelGold.text = $"{rewards.gold} G";
            if (labelExp) labelExp.text = $"{rewards.experience} XP";

            // 2. Spawn Items
            if (lootContainer == null || lootSlotPrefab == null) return;

            foreach (Transform child in lootContainer) Destroy(child.gameObject);

            for (int i = 0; i < rewards.items.Count; i++)
            {
                var slotData = rewards.items[i];
                var go = Instantiate(lootSlotPrefab, lootContainer);
                var ui = go.GetComponent<LootSlotUI>();

                if (ui != null)
                {
                    // Pass the UpdateDetails method as the callback
                    ui.Setup(slotData.item, UpdateDetails);
                }
            }
        }

        // ⭐ Called when a LootSlotUI is clicked
        void UpdateDetails(InventoryItem item)
        {
            if (item == null) return;

            // 1. Description
            // We pass 'null' as holder because we are viewing loot, not checking player stats scaling
            if (descriptionText)
            {
                descriptionText.text = item.GetDynamicDescription(null);
            }

            // 2. Flavor
            if (flavorText)
            {
                // Try to get flavor from the wrapped Ability if it's a consumable
                if (item is ConsumableItem consumable && consumable.abilityToCast != null)
                {
                    // Assuming ability has a LocalizedFlavor field
                    flavorText.text = $"<i>\"{consumable.abilityToCast.LocalizedFlavor}\"</i>";
                }
                else
                {
                    // For now, other items might not have specific flavor text
                    flavorText.text = "";
                }
            }
        }

        void HandleDefeat()
        {
            if (defeatPanel) defeatPanel.SetActive(true);
        }

        void OnVictoryContinue()
        {
            ClaimRewards();
            BattleContext.Reset();
            Debug.Log("Transitioning to Map/Next Level...");
            // SceneManager.LoadScene("MapScene"); 
        }

        void ClaimRewards()
        {
            if (_cachedResult == null) return;

            UnitInventory playerInventory = FindPlayerInventory();
            if (playerInventory != null)
            {
                foreach (var reward in _cachedResult.items)
                {
                    bool success = playerInventory.TryAddItem(reward.item, reward.count);
                    if (success) Debug.Log($"[BattleOutcome] Claimed {reward.count}x {reward.item.name}");
                    else Debug.LogWarning($"[BattleOutcome] Inventory Full! Lost {reward.item.name}");
                }
            }

            if (_cachedResult.gold > 0) Debug.Log($"[Wallet] Added {_cachedResult.gold} Gold.");
            if (_cachedResult.experience > 0) Debug.Log($"[Progression] Added {_cachedResult.experience} EXP.");
        }

        UnitInventory FindPlayerInventory()
        {
            if (_battleSM != null)
            {
                var playerUnit = _battleSM.PlayerUnits.FirstOrDefault(u => u != null);
                if (playerUnit != null) return playerUnit.GetComponent<UnitInventory>();
            }
            var allUnits = FindObjectsByType<BattleUnit>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            var player = allUnits.FirstOrDefault(u => u.isPlayer);
            return player != null ? player.GetComponent<UnitInventory>() : null;
        }

        void OnDefeatRetry()
        {
            BattleContext.Reset();
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        void OnDefeatQuit()
        {
            BattleContext.Reset();
            // SceneManager.LoadScene("MainMenu");
        }
    }
}