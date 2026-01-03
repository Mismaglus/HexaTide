// Scripts/UI/Game/BattleOutcomeUI.cs
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using Game.Battle;
using Game.World;
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
        [Tooltip("The GameObject containing the Victory UI elements (Background, Loot, Buttons)")]
        public GameObject victoryPanel;
        [Tooltip("The GameObject containing the Defeat UI elements")]
        public GameObject defeatPanel;

        [Header("Defeat Section")]
        [Tooltip("Text label to display score/stats on defeat")]
        public TextMeshProUGUI defeatScoreText;

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

        [Header("References")]
        public SkillTooltipController tooltipController;

        private BattleStateMachine _battleSM;
        private BattleRewardResult _cachedResult;

        // Track if we have already displayed the result to prevent repeated calls in Update
        private bool _hasShownOutcome = false;

        void Awake()
        {
            // Ensure we start clean, but keep THIS script's GameObject active to run Update()
            if (victoryPanel) victoryPanel.SetActive(false);
            if (defeatPanel) defeatPanel.SetActive(false);

            if (victoryContinueBtn) victoryContinueBtn.onClick.AddListener(OnVictoryContinue);
            if (defeatRetryBtn) defeatRetryBtn.onClick.AddListener(OnDefeatRetry);
            if (defeatQuitBtn) defeatQuitBtn.onClick.AddListener(OnDefeatQuit);

            if (!tooltipController) tooltipController = Object.FindFirstObjectByType<SkillTooltipController>(FindObjectsInactive.Include);
        }

        void Start()
        {
            _battleSM = BattleStateMachine.Instance ?? Object.FindFirstObjectByType<BattleStateMachine>(FindObjectsInactive.Include);

            // Note: We primarily rely on Update() polling now for safety, but Events are okay too.
        }

        // Robustness: Check State every frame.
        // This solves the issue where OnVictory event fires before this UI is ready/subscribed.
        void Update()
        {
            if (_hasShownOutcome) return;
            if (_battleSM == null)
            {
                _battleSM = BattleStateMachine.Instance;
                return;
            }

            // Check State Machine
            if (_battleSM.State == BattleState.Victory)
            {
                ShowVictoryPanel();
                _hasShownOutcome = true;
            }
            else if (_battleSM.State == BattleState.Defeat)
            {
                ShowDefeatPanel();
                _hasShownOutcome = true;
            }
        }

        void ShowVictoryPanel()
        {
            Debug.Log("[BattleOutcomeUI] Showing Victory Panel");

            if (victoryPanel)
            {
                victoryPanel.SetActive(true);

                // Force Visual Properties in case they were stuck
                CanvasGroup cg = victoryPanel.GetComponent<CanvasGroup>();
                if (cg == null) cg = victoryPanel.AddComponent<CanvasGroup>();
                cg.alpha = 1f;
                cg.blocksRaycasts = true;
                cg.interactable = true;

                // Bring to front
                victoryPanel.transform.SetAsLastSibling();
            }
            else
            {
                Debug.LogError("[BattleOutcomeUI] Victory Panel reference missing!");
            }

            // Reset Texts
            if (descriptionText) descriptionText.text = defaultHint;
            if (flavorText) flavorText.text = "";

            // Populate Data
            if (_battleSM != null && _battleSM.Rewards != null)
            {
                _cachedResult = _battleSM.Rewards;
                PopulateRewards(_cachedResult);
            }
        }

        void ShowDefeatPanel()
        {
            Debug.Log("[BattleOutcomeUI] Showing Defeat Panel");

            if (defeatPanel)
            {
                defeatPanel.SetActive(true);

                CanvasGroup cg = defeatPanel.GetComponent<CanvasGroup>();
                if (cg == null) cg = defeatPanel.AddComponent<CanvasGroup>();
                cg.alpha = 1f;
                cg.blocksRaycasts = true;
                cg.interactable = true;

                defeatPanel.transform.SetAsLastSibling();
            }

            if (defeatScoreText)
            {
                defeatScoreText.text = BattleScoreCalculator.GetScoreReport();
            }
        }

        void PopulateRewards(BattleRewardResult rewards)
        {
            if (rewards == null) return;

            // 1. Show Currency
            if (labelGold) labelGold.text = $"{rewards.gold} G";
            if (labelExp) labelExp.text = $"{rewards.experience} XP";

            // 2. Spawn Items
            if (lootContainer && lootSlotPrefab)
            {
                foreach (Transform child in lootContainer) Destroy(child.gameObject);

                for (int i = 0; i < rewards.items.Count; i++)
                {
                    var slotData = rewards.items[i];
                    var go = Instantiate(lootSlotPrefab, lootContainer);
                    var ui = go.GetComponent<LootSlotUI>();
                    if (ui)
                    {
                        ui.Setup(slotData.item, OnItemClicked);
                    }
                }

                // Force Layout Rebuild to fix Unity UI 0-size bugs on first frame
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(lootContainer as RectTransform);
            }
        }

        // Callback from LootSlotUI
        void OnItemClicked(InventoryItem item)
        {
            if (item == null) return;

            // Update Description
            if (descriptionText) descriptionText.text = item.GetDynamicDescription(null);

            // Update Flavor
            if (flavorText)
            {
                if (item is ConsumableItem c && c.abilityToCast != null)
                {
                    flavorText.text = $"<i>\"{c.abilityToCast.LocalizedFlavor}\"</i>";
                }
                else
                {
                    flavorText.text = "";
                }
            }
        }

        void OnVictoryContinue()
        {
            ClaimRewards();

            // Check Encounter Context for Return Policy
            var context = BattleContext.EncounterContext;

            if (context.HasValue && context.Value.policy == ReturnPolicy.ExitChapter)
            {
                var exitContext = context.Value;
                var destinationRegionId = exitContext.nextChapterId;
                var destinationAct = exitContext.nextAct;

                Debug.Log($"[BattleOutcome] Exiting Chapter via {exitContext.gateKind}. NextAct: {destinationAct}, NextRegion: {destinationRegionId}");

                // Clear Map Data as we are leaving the chapter
                MapRuntimeData.Clear();

                // Single MapScene: destination is treated as ChapterId, not SceneName
                if (!string.IsNullOrEmpty(destinationRegionId))
                {
                    if (destinationAct > 0) FlowContext.CurrentAct = destinationAct;
                    FlowContext.CurrentChapterId = destinationRegionId;
                }
                else
                {
                    Debug.LogError("[BattleOutcome] ExitChapter policy set but no destination provided!");
                }

                // Reset Context
                BattleContext.Reset();

                // Load MapScene for next chapter generation
                SceneManager.LoadScene("MapScene");
                return;
            }

            // Default: Return to Chapter
            // 1) Mark the current node as cleared in our persistent data
            if (MapRuntimeData.HasData)
            {
                // We clear the node where the player currently stands
                MapRuntimeData.ClearedNodes.Add(MapRuntimeData.PlayerPosition);
                Debug.Log($"[BattleOutcome] Node at {MapRuntimeData.PlayerPosition} marked as cleared.");

                // Keep FlowContext aligned
                if (!string.IsNullOrEmpty(MapRuntimeData.CurrentChapterId))
                {
                    FlowContext.CurrentChapterId = MapRuntimeData.CurrentChapterId;
                }
            }

            // 2) Return to the Map Scene
            // Make sure "MapScene" is added to your Build Settings!
            BattleContext.Reset();
            SceneManager.LoadScene("MapScene");
        }

        void OnDefeatRetry()
        {
            Debug.Log("[BattleOutcomeUI] Retry clicked. Reloading scene...");
            // Usually retry implies same battle settings, so we might NOT clear context here.
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        void OnDefeatQuit()
        {
            Debug.Log("[BattleOutcomeUI] Quit clicked.");
            BattleContext.Reset();
            // SceneManager.LoadScene("MainMenu");
        }

        void ClaimRewards()
        {
            if (_cachedResult == null) return;

            // Find Player Inventory
            UnitInventory playerInventory = null;

            // Method 1: Ask SM
            if (_battleSM != null && _battleSM.PlayerUnits.Count > 0)
            {
                // Note: The player unit might be null if they died, but usually 1 persists or we grab from roster
                var p = _battleSM.PlayerUnits.FirstOrDefault(u => u != null);
                if (p) playerInventory = p.GetComponent<UnitInventory>();
            }

            // Method 2: Fallback search
            if (playerInventory == null)
            {
                var p = FindObjectsByType<BattleUnit>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                        .FirstOrDefault(u => u.isPlayer);
                if (p) playerInventory = p.GetComponent<UnitInventory>();
            }

            if (playerInventory != null)
            {
                foreach (var reward in _cachedResult.items)
                {
                    bool success = playerInventory.TryAddItem(reward.item, reward.count);
                    if (success) Debug.Log($"[BattleOutcome] Claimed {reward.count}x {reward.item.name}");
                    else Debug.LogWarning($"[BattleOutcome] Inventory Full! Lost {reward.item.name}");
                }
            }
            else
            {
                Debug.LogError("[BattleOutcomeUI] Could not find Player UnitInventory to claim rewards!");
            }

            // Claim Currency
            if (_cachedResult.gold > 0) Debug.Log($"[Wallet] Added {_cachedResult.gold} Gold (System not connected).");
            if (_cachedResult.experience > 0) Debug.Log($"[Progression] Added {_cachedResult.experience} EXP (System not connected).");
        }
    }
}
