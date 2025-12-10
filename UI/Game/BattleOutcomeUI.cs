// Scripts/UI/Game/BattleOutcomeUI.cs
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro; // Added for TextMeshPro
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

        [Header("Loot Configuration")]
        public Transform lootContainer;
        public InventorySlotUI lootSlotPrefab;

        [Header("Reward Labels (Drag Text Here)")]
        public TextMeshProUGUI labelGold;
        public TextMeshProUGUI labelExp;

        [Header("Buttons")]
        public Button victoryContinueBtn;
        public Button defeatRetryBtn;
        public Button defeatQuitBtn;

        [Header("References")]
        public SkillTooltipController tooltipController;

        private BattleStateMachine _battleSM;
        private BattleRewardResult _cachedResult; // Stores Items + Gold + Exp

        void Awake()
        {
            if (victoryPanel) victoryPanel.SetActive(false);
            if (defeatPanel) defeatPanel.SetActive(false);

            if (victoryContinueBtn) victoryContinueBtn.onClick.AddListener(OnVictoryContinue);
            if (defeatRetryBtn) defeatRetryBtn.onClick.AddListener(OnDefeatRetry);
            if (defeatQuitBtn) defeatQuitBtn.onClick.AddListener(OnDefeatQuit);

            if (!tooltipController) tooltipController = FindFirstObjectByType<SkillTooltipController>(FindObjectsInactive.Include);
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

            if (_battleSM != null)
            {
                _cachedResult = _battleSM.Rewards;
                ShowRewards(_cachedResult);
            }
        }

        void ShowRewards(BattleRewardResult rewards)
        {
            if (rewards == null) return;

            // 1. Show Currency Texts
            if (labelGold) labelGold.text = $"{rewards.gold} G";
            if (labelExp) labelExp.text = $"{rewards.experience} XP";

            // 2. Spawn Items
            if (lootContainer == null || lootSlotPrefab == null) return;

            foreach (Transform child in lootContainer) Destroy(child.gameObject);

            BattleUnit playerUnit = null;
            if (_battleSM != null && _battleSM.PlayerUnits.Count > 0)
                playerUnit = _battleSM.PlayerUnits[0];

            for (int i = 0; i < rewards.items.Count; i++)
            {
                var slotData = rewards.items[i];
                var go = Instantiate(lootSlotPrefab, lootContainer);
                var ui = go.GetComponent<InventorySlotUI>();

                if (ui != null)
                {
                    ui.Setup(slotData.item, slotData.count, i, null, tooltipController, playerUnit);
                    ui.SetHighlightState(false);
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
            Debug.Log("Transitioning to Map/Next Level...");
            // SceneManager.LoadScene("MapScene"); 
        }

        void ClaimRewards()
        {
            if (_cachedResult == null) return;

            // 1. Claim Items
            UnitInventory playerInventory = FindPlayerInventory();
            if (playerInventory != null)
            {
                foreach (var reward in _cachedResult.items)
                {
                    bool success = playerInventory.TryAddItem(reward.item, reward.count);
                    if (!success) Debug.LogWarning($"[BattleOutcome] Inventory Full! Lost {reward.item.name}");
                }
            }

            // 2. Claim Gold & Exp
            // TODO: Connect this to your PlayerData / SaveSystem
            if (_cachedResult.gold > 0)
            {
                Debug.Log($"[Wallet] Added {_cachedResult.gold} Gold to player wallet (Not implemented yet).");
                // Example: PlayerWallet.Instance.AddGold(_cachedResult.gold);
            }

            if (_cachedResult.experience > 0)
            {
                Debug.Log($"[Progression] Added {_cachedResult.experience} EXP to player (Not implemented yet).");
                // Example: PlayerStats.Instance.AddExp(_cachedResult.experience);
            }
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
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        void OnDefeatQuit()
        {
            // SceneManager.LoadScene("MainMenu");
        }
    }
}