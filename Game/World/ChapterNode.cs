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
        Boss,        // Generic Boss (Act 2 Top)
        Gate_Left,   // Act 1 Left Exit
        Gate_Right,  // Act 1 Right Exit
        Gate_Skip    // Act 1 Middle Skip (Challenge)
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
            // TODO: Update visual state (e.g. gray out marker)
        }

        public void Interact()
        {
            if (isCleared) return;

            Debug.Log($"[ChapterNode] Interact: {type}");

            // --- 1. Construct the Context based on Node Type ---
            EncounterContext context = new EncounterContext();

            // Check if this node forces us to leave the chapter (Bosses/Gates)
            bool isExitNode = (type == ChapterNodeType.Gate_Left ||
                               type == ChapterNodeType.Gate_Right ||
                               type == ChapterNodeType.Gate_Skip ||
                               type == ChapterNodeType.Boss);

            if (isExitNode)
            {
                // Policy: Exit Chapter.
                // We do NOT save map state, because the chapter is effectively over.
                context.policy = ReturnPolicy.ExitChapter;

                // Configure Gate Kind so BattleOutcome knows where to go next
                if (type == ChapterNodeType.Gate_Left) context.gateKind = GateKind.LeftGate;
                else if (type == ChapterNodeType.Gate_Right) context.gateKind = GateKind.RightGate;
                else if (type == ChapterNodeType.Gate_Skip) context.gateKind = GateKind.SkipGate;
                else context.gateKind = GateKind.None; // Generic Boss
            }
            else
            {
                // Policy: Return To Chapter.
                // We MUST save map state so we can restore player position and tide after battle.
                context.policy = ReturnPolicy.ReturnToChapter;

                if (ChapterMapManager.Instance != null)
                {
                    ChapterMapManager.Instance.SaveMapState();
                }
            }

            // --- 2. Trigger the Interaction ---

            // If a specific encounter component is attached, use it
            if (specificEncounter != null)
            {
                specificEncounter.StartEncounter(context); // <--- PASS CONTEXT HERE
                return;
            }

            switch (type)
            {
                case ChapterNodeType.NormalEnemy:
                case ChapterNodeType.EliteEnemy:
                case ChapterNodeType.Boss:
                case ChapterNodeType.Gate_Left:
                case ChapterNodeType.Gate_Right:
                case ChapterNodeType.Gate_Skip:
                    TriggerBattle(context); // <--- PASS CONTEXT HERE
                    break;

                case ChapterNodeType.Merchant:
                    Debug.Log("Open Merchant UI (Not Implemented)");
                    break;

                case ChapterNodeType.Treasure:
                    Debug.Log("Open Treasure Chest");
                    SetCleared(true);
                    // Update save immediately for non-battle interactions if we stay in map
                    if (!isExitNode && ChapterMapManager.Instance != null)
                        ChapterMapManager.Instance.SaveMapState();
                    break;
            }
        }

        void TriggerBattle(EncounterContext context)
        {
            // Dynamically get or add EncounterNode if missing
            var encounter = GetComponent<EncounterNode>();
            if (encounter == null) encounter = gameObject.AddComponent<EncounterNode>();

            Debug.Log($"[ChapterNode] Triggering Generic Battle. Policy: {context.policy}");
            encounter.StartEncounter(context); // <--- PASS CONTEXT HERE
        }
    }
}