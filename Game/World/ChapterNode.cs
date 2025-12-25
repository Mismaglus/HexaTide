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
            // TODO: Update visual state
        }

        public void Interact()
        {
            if (isCleared) return;

            Debug.Log($"[ChapterNode] Interact: {type}");

            // --- Determine Policy & Context ---
            EncounterContext context = new EncounterContext();

            bool isExitNode = (type == ChapterNodeType.Gate_Left ||
                               type == ChapterNodeType.Gate_Right ||
                               type == ChapterNodeType.Gate_Skip ||
                               type == ChapterNodeType.Boss);

            if (isExitNode)
            {
                // Policy: Exit Chapter.
                // Do NOT save map state, because we won't return to this map instance.
                context.policy = ReturnPolicy.ExitChapter;

                // Configure Gate Kind for Act transition logic
                if (type == ChapterNodeType.Gate_Left) context.gateKind = GateKind.LeftGate;
                else if (type == ChapterNodeType.Gate_Right) context.gateKind = GateKind.RightGate;
                else if (type == ChapterNodeType.Gate_Skip) context.gateKind = GateKind.SkipGate;

                // context.nextChapterId will be handled by the BattleOutcome logic based on GateKind
            }
            else
            {
                // Policy: Return To Chapter.
                // MUST save map state so we can restore it after the battle scene.
                context.policy = ReturnPolicy.ReturnToChapter;
                if (ChapterMapManager.Instance != null)
                {
                    ChapterMapManager.Instance.SaveMapState();
                }
            }

            // --- Trigger Logic ---

            if (specificEncounter != null)
            {
                // Pass context to the specific encounter (needs refactoring EncounterNode too if it doesn't support it yet)
                // For now, assuming EncounterNode handles scene loading:
                specificEncounter.StartEncounter();
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
                    TriggerBattle(context);
                    break;

                case ChapterNodeType.Merchant:
                    Debug.Log("Open Merchant UI");
                    break;

                case ChapterNodeType.Treasure:
                    Debug.Log("Open Treasure Chest");
                    SetCleared(true);
                    // Update save immediately for non-scene interactions if we stay in map
                    if (!isExitNode && ChapterMapManager.Instance != null)
                        ChapterMapManager.Instance.SaveMapState();
                    break;
            }
        }

        void TriggerBattle(EncounterContext context)
        {
            var encounter = GetComponent<EncounterNode>();
            if (encounter == null) encounter = gameObject.AddComponent<EncounterNode>();

            // TODO: Pass 'context' to EncounterNode so it can persist it across the scene load.
            // For now, we assume EncounterNode triggers the scene load.
            // You might need a static "FlowManager.CurrentContext = context;" here.

            Debug.Log($"[ChapterNode] Triggering Battle with Policy: {context.policy}");
            encounter.StartEncounter();
        }
    }
}