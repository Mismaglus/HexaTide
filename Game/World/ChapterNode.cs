using UnityEngine;
using Game.Grid;

namespace Game.World
{
    public enum ChapterNodeType
    {
        None,
        Start,
        NormalEnemy,
        EliteEnemy,
        Merchant,
        Treasure,
        Mystery,
        Boss
    }

    [RequireComponent(typeof(HexCell))]
    public class ChapterNode : MonoBehaviour
    {
        [Header("Node Data")]
        public ChapterNodeType type = ChapterNodeType.NormalEnemy;
        public bool isCleared = false;

        [Header("Content Integration")]
        public EncounterNode specificEncounter;

        [Header("Visuals")]
        public GameObject markerPrefab;

        private HexCell _cell;

        void Awake()
        {
            _cell = GetComponent<HexCell>();
        }

        public void Initialize(ChapterNodeType nodeType)
        {
            type = nodeType;
            isCleared = false;
        }

        public void SetCleared(bool cleared)
        {
            isCleared = cleared;
            // TODO: Update visual state (e.g. gray out icon)
        }

        public void Interact()
        {
            if (isCleared) return;

            Debug.Log($"[ChapterNode] Interact: {type}");

            // ⭐ CRITICAL: Save Map State before leaving scene
            if (ChapterMapManager.Instance != null)
            {
                // Note: We mark THIS node as cleared assuming player will win.
                // If they lose/flee, we might handle that differently (e.g. reload save).
                // For now, let's assume entry = cleared for non-battle nodes, 
                // but for Battle nodes, we might want to clear it ONLY on return.

                // Strategy: Mark it cleared in the *saved data* now? 
                // Or let the BattleOutcomeUI mark it cleared in MapData upon Victory?
                // Let's rely on BattleOutcomeUI or a "Return" handler to finalize the clear.
                // But we must save the player position here.
                ChapterMapManager.Instance.SaveMapState();
            }

            // Trigger Logic
            if (specificEncounter != null)
            {
                specificEncounter.StartEncounter();
                return;
            }

            switch (type)
            {
                case ChapterNodeType.NormalEnemy:
                case ChapterNodeType.EliteEnemy:
                case ChapterNodeType.Boss:
                    TriggerGenericBattle();
                    break;

                case ChapterNodeType.Merchant:
                    Debug.Log("Open Merchant UI");
                    break;

                case ChapterNodeType.Treasure:
                    Debug.Log("Open Treasure Chest");
                    SetCleared(true);
                    // Update save immediately for non-scene interactions
                    ChapterMapManager.Instance.SaveMapState();
                    break;
            }
        }

        void TriggerGenericBattle()
        {
            var encounter = GetComponent<EncounterNode>();
            if (encounter == null) encounter = gameObject.AddComponent<EncounterNode>();

            // Note: EncounterNode.StartEncounter will load the Battle Scene
            encounter.StartEncounter();
        }
    }
}