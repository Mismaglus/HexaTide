// Scripts/Game/World/EncounterNode.cs
using UnityEngine;
using UnityEngine.SceneManagement;
using Game.Battle;     // For BattleContext
using Game.Inventory;  // For LootTableSO

namespace Game.World
{
    public class EncounterNode : MonoBehaviour
    {
        [Header("Battle Settings")]
        public string battleSceneName = "BattleScene";

        [Header("Rewards")]
        [Tooltip("Drag 'Loot_Act1_Mobs' or 'Loot_Boss' here")]
        public LootTableSO specificLootTable;

        // Runtime Context
        public EncounterContext Context;

        // Call this when player clicks the node or collides with enemy
        public void StartEncounter()
        {
            // 1. Configure the Bridge
            BattleContext.Reset(); // Clean up old data
            BattleContext.ActiveLootTable = specificLootTable;
            BattleContext.EncounterContext = Context;

            // 2. Load the generic Battle Scene
            Debug.Log($"[Map] Loading battle with loot: {specificLootTable?.name}");
            SceneManager.LoadScene(battleSceneName);
        }
    }
}