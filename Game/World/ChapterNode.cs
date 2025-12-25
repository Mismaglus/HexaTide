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
        Boss,
        GateLeft,
        GateRight,
        GateSkip
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

        public ReturnPolicy Policy
        {
            get
            {
                if (type == ChapterNodeType.GateLeft || type == ChapterNodeType.GateRight || type == ChapterNodeType.GateSkip)
                {
                    return ReturnPolicy.ExitChapter;
                }
                return ReturnPolicy.ReturnToChapter;
            }
        }

        public void Interact()
        {
            if (isCleared) return;

            Debug.Log($"[ChapterNode] Interact: {type}");

            // Setup Encounter Context
            EncounterContext.Current = new EncounterContext();
            EncounterContext.Current.nodeType = type;
            EncounterContext.Current.policy = Policy;

            // Determine Policy
            if (Policy == ReturnPolicy.ExitChapter)
            {
                if (type == ChapterNodeType.GateLeft)
                {
                    EncounterContext.Current.nextChapterId = "MapScene_Act2_Left";
                }
                else if (type == ChapterNodeType.GateRight)
                {
                    EncounterContext.Current.nextChapterId = "MapScene_Act2_Right";
                }
                else
                {
                    EncounterContext.Current.nextChapterId = "Act3Scene";
                }

                // Boss Gate: Do NOT save map state (as we are leaving)
            }
            else
            {
                // Normal Node: Save Map State
                if (ChapterMapManager.Instance != null)
                {
                    ChapterMapManager.Instance.SaveMapState();
                }
            }

            // Trigger Logic
            if (specificEncounter != null)
            {
                specificEncounter.Context = EncounterContext.Current;
                specificEncounter.StartEncounter();
                return;
            }

            switch (type)
            {
                case ChapterNodeType.NormalEnemy:
                case ChapterNodeType.EliteEnemy:
                case ChapterNodeType.Boss:
                case ChapterNodeType.GateLeft:
                case ChapterNodeType.GateRight:
                case ChapterNodeType.GateSkip:
                    TriggerGenericBattle(EncounterContext.Current);
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

        void TriggerGenericBattle(EncounterContext context)
        {
            var encounter = GetComponent<EncounterNode>();
            if (encounter == null) encounter = gameObject.AddComponent<EncounterNode>();

            encounter.Context = context;
            // Note: EncounterNode.StartEncounter will load the Battle Scene
            encounter.StartEncounter();
        }
    }
}