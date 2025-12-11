// Scripts/UI/Game/BattleOutcomeUI.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Battle;
using Game.Inventory;
using Game.Units;
using Game.UI.Inventory;
using System.Linq;
using UnityEngine.SceneManagement;

namespace Game.UI
{
    public class BattleOutcomeUI : MonoBehaviour
    {
        [Header("Panels")]
        [Tooltip("Assign the Child Panel 'VictoryPanel' here. Keep Main Object Active.")]
        public GameObject victoryPanel;
        [Tooltip("Assign the Child Panel 'DefeatPanel' here.")]
        public GameObject defeatPanel;

        [Header("Defeat Section")]
        public TextMeshProUGUI defeatScoreText;

        [Header("Loot List Configuration")]
        public Transform lootContainer;
        public LootSlotUI lootSlotPrefab;

        [Header("Loot Details Panel")]
        public TextMeshProUGUI descriptionText;
        public TextMeshProUGUI flavorText;
        [TextArea] public string defaultHint = "Select an item to view details.";

        [Header("Reward Summary Labels")]
        public TextMeshProUGUI labelGold;
        public TextMeshProUGUI labelExp;

        [Header("Buttons")]
        public Button victoryContinueBtn;
        public Button defeatRetryBtn;
        public Button defeatQuitBtn;

        [Header("References")]
        public SkillTooltipController tooltipController;

        private BattleStateMachine _battleSM;
        private BattleRewardResult _cachedResult;

        void Awake()
        {
            // Initial State: Panels Hidden, but THIS script's object must be Active
            if (victoryPanel) victoryPanel.SetActive(false);
            if (defeatPanel) defeatPanel.SetActive(false);

            if (victoryContinueBtn) victoryContinueBtn.onClick.AddListener(OnVictoryContinue);
            if (defeatRetryBtn) defeatRetryBtn.onClick.AddListener(OnDefeatRetry);
            if (defeatQuitBtn) defeatQuitBtn.onClick.AddListener(OnDefeatQuit);

            if (!tooltipController) tooltipController = FindFirstObjectByType<SkillTooltipController>(FindObjectsInactive.Include);
        }

        void OnEnable()
        {
            // Subscribe here to catch re-enabling
            BindToStateMachine();
        }

        void OnDisable()
        {
            if (_battleSM != null)
            {
                _battleSM.OnVictory -= HandleVictory;
                _battleSM.OnDefeat -= HandleDefeat;
            }
        }

        void BindToStateMachine()
        {
            if (_battleSM == null)
                _battleSM = BattleStateMachine.Instance ?? FindFirstObjectByType<BattleStateMachine>(FindObjectsInactive.Include);

            if (_battleSM != null)
            {
                // Unsub first to prevent duplicates
                _battleSM.OnVictory -= HandleVictory;
                _battleSM.OnDefeat -= HandleDefeat;

                _battleSM.OnVictory += HandleVictory;
                _battleSM.OnDefeat += HandleDefeat;
                Debug.Log("[BattleOutcomeUI] Successfully bound to BattleStateMachine events.");
            }
            else
            {
                Debug.LogWarning("[BattleOutcomeUI] Waiting for BattleStateMachine...");
            }
        }

        void HandleVictory()
        {
            Debug.Log("[BattleOutcomeUI] VICTORY EVENT RECEIVED");

            // 1. Activate Panel
            if (victoryPanel)
            {
                victoryPanel.SetActive(true);
                ForcePanelVisibility(victoryPanel);
            }

            // 2. Reset Details
            if (descriptionText) descriptionText.text = defaultHint;
            if (flavorText) flavorText.text = "";

            // 3. Show Loot
            if (_battleSM != null)
            {
                _cachedResult = _battleSM.Rewards;
                ShowRewards(_cachedResult);
            }
        }

        void ForcePanelVisibility(GameObject panel)
        {
            // Fix alpha issues
            var cg = panel.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.alpha = 1f;
                cg.interactable = true;
                cg.blocksRaycasts = true;
            }

            // Fix layout issues
            LayoutRebuilder.ForceRebuildLayoutImmediate(panel.transform as RectTransform);

            // Ensure on top
            panel.transform.SetAsLastSibling();
        }

        void ShowRewards(BattleRewardResult rewards)
        {
            if (rewards == null) return;

            if (labelGold) labelGold.text = $"{rewards.gold} G";
            if (labelExp) labelExp.text = $"{rewards.experience} XP";

            if (lootContainer && lootSlotPrefab)
            {
                foreach (Transform child in lootContainer) Destroy(child.gameObject);

                for (int i = 0; i < rewards.items.Count; i++)
                {
                    var slotData = rewards.items[i];
                    var go = Instantiate(lootSlotPrefab, lootContainer);
                    var ui = go.GetComponent<LootSlotUI>();
                    if (ui) ui.Setup(slotData.item, UpdateDetails);
                }
            }
        }

        void UpdateDetails(InventoryItem item)
        {
            if (item == null) return;
            if (descriptionText) descriptionText.text = item.GetDynamicDescription(null);
            if (flavorText)
            {
                if (item is ConsumableItem c && c.abilityToCast != null)
                    flavorText.text = $"<i>\"{c.abilityToCast.LocalizedFlavor}\"</i>";
                else flavorText.text = "";
            }
        }

        void HandleDefeat()
        {
            Debug.Log("[BattleOutcomeUI] DEFEAT EVENT RECEIVED");
            if (defeatPanel)
            {
                defeatPanel.SetActive(true);
                ForcePanelVisibility(defeatPanel);
            }
            if (defeatScoreText) defeatScoreText.text = BattleScoreCalculator.GetScoreReport();
        }

        // ... Button Handlers remain same ...
        void OnVictoryContinue()
        {
            ClaimRewards();
            BattleContext.Reset();
            Debug.Log("Loading Next Scene...");
            // SceneManager.LoadScene("MapScene"); 
        }

        void OnDefeatRetry() { BattleContext.Reset(); SceneManager.LoadScene(SceneManager.GetActiveScene().name); }
        void OnDefeatQuit() { BattleContext.Reset(); /* Load Main Menu */ }

        void ClaimRewards()
        {
            if (_cachedResult == null) return;

            // Find Inventory logic...
            UnitInventory playerInv = FindObjectsByType<BattleUnit>(FindObjectsSortMode.None).FirstOrDefault(u => u.isPlayer)?.GetComponent<UnitInventory>();

            if (playerInv)
            {
                foreach (var r in _cachedResult.items) playerInv.TryAddItem(r.item, r.count);
                Debug.Log($"Claimed {_cachedResult.items.Count} stacks of items.");
            }
        }
    }
}