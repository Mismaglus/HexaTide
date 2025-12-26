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

        private bool _hasShownOutcome = false;

        void Awake()
        {
            if (victoryPanel) victoryPanel.SetActive(false);
            if (defeatPanel) defeatPanel.SetActive(false);

            if (victoryContinueBtn) victoryContinueBtn.onClick.AddListener(OnVictoryContinue);
            if (defeatRetryBtn) defeatRetryBtn.onClick.AddListener(OnDefeatRetry);
            if (defeatQuitBtn) defeatQuitBtn.onClick.AddListener(OnDefeatQuit);

            if (!tooltipController)
                tooltipController = Object.FindFirstObjectByType<SkillTooltipController>(FindObjectsInactive.Include);
        }

        void Start()
        {
            _battleSM = BattleStateMachine.Instance
                ?? Object.FindFirstObjectByType<BattleStateMachine>(FindObjectsInactive.Include);
        }

        void Update()
        {
            if (_hasShownOutcome) return;

            if (_battleSM == null)
            {
                _battleSM = BattleStateMachine.Instance;
                return;
            }

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

                CanvasGroup cg = victoryPanel.GetComponent<CanvasGroup>();
                if (cg == null) cg = victoryPanel.AddComponent<CanvasGroup>();
                cg.alpha = 1f;
                cg.blocksRaycasts = true;
                cg.interactable = true;

                victoryPanel.transform.SetAsLastSibling();
            }
            else
            {
                Debug.LogError("[BattleOutcomeUI] Victory Panel reference missing!");
            }

            if (descriptionText) descriptionText.text = defaultHint;
            if (flavorText) flavorText.text = "";

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
                    if (ui)
                    {
                        ui.Setup(slotData.item, OnItemClicked);
                    }
                }

                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(lootContainer as RectTransform);
            }
        }

        void OnItemClicked(InventoryItem item)
        {
            if (item == null) return;

            if (descriptionText) descriptionText.text = item.GetDynamicDescription(null);

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

            var context = BattleContext.EncounterContext;

            if (context.HasValue && context.Value.policy == ReturnPolicy.ExitChapter)
            {
                var exitContext = context.Value;

                var destinationChapterId = exitContext.nextChapterId;
                if (string.IsNullOrEmpty(destinationChapterId))
                {
                    switch (exitContext.gateKind)
                    {
                        case GateKind.SkipGate:
                            destinationChapterId = "Act3_StarreachPeak";
                            break;
                        case GateKind.LeftGate:
                            destinationChapterId = "Act2_LeftBiome";
                            break;
                        case GateKind.RightGate:
                            destinationChapterId = "Act2_RightBiome";
                            break;
                    }
                }

                Debug.Log($"[BattleOutcome] ExitChapter via {exitContext.gateKind}. Next ChapterId: {destinationChapterId}");

                MapRuntimeData.Clear();

                if (!string.IsNullOrEmpty(destinationChapterId))
                {
                    FlowContext.CurrentChapterId = destinationChapterId;
                }

                BattleContext.Reset();
                SceneManager.LoadScene("MapScene");
                return;
            }

            if (MapRuntimeData.HasData)
            {
                MapRuntimeData.ClearedNodes.Add(MapRuntimeData.PlayerPosition);

                if (!string.IsNullOrEmpty(MapRuntimeData.CurrentChapterId))
                    FlowContext.CurrentChapterId = MapRuntimeData.CurrentChapterId;
            }

            BattleContext.Reset();
            SceneManager.LoadScene("MapScene");
        }

        void OnDefeatRetry()
        {
            Debug.Log("[BattleOutcomeUI] Retry clicked. Reloading scene...");
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

            UnitInventory playerInventory = null;

            if (_battleSM != null && _battleSM.PlayerUnits.Count > 0)
            {
                var p = _battleSM.PlayerUnits.FirstOrDefault(u => u != null);
                if (p) playerInventory = p.GetComponent<UnitInventory>();
            }

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

            if (_cachedResult.gold > 0) Debug.Log($"[Wallet] Added {_cachedResult.gold} Gold (System not connected).");
            if (_cachedResult.experience > 0) Debug.Log($"[Progression] Added {_cachedResult.experience} EXP (System not connected).");
        }
    }
}
