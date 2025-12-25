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
        LeftGate,
        RightGate,
        SkipGate
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

            // Setup Encounter Context
            EncounterContext.Current = new EncounterContext();

            // Try to get coords
            var tileTag = GetComponent<Game.Common.TileTag>();
            if (tileTag != null) EncounterContext.Current.nodeCoords = tileTag.Coords;

            // Set Chapter ID (Placeholder)
            EncounterContext.Current.chapterId = "Act1_Chapter1";

            // Determine Policy
            if (type == ChapterNodeType.LeftGate || type == ChapterNodeType.RightGate || type == ChapterNodeType.SkipGate)
            {
                EncounterContext.Current.returnPolicy = ReturnPolicy.ExitChapter;

                if (type == ChapterNodeType.LeftGate)
                {
                    EncounterContext.Current.gateKind = GateKind.Left;
                    EncounterContext.Current.destination = "MapScene_Act2_Left";
                }
                else if (type == ChapterNodeType.RightGate)
                {
                    EncounterContext.Current.gateKind = GateKind.Right;
                    EncounterContext.Current.destination = "MapScene_Act2_Right";
                }
                else
                {
                    EncounterContext.Current.gateKind = GateKind.Skip;
                    EncounterContext.Current.destination = "Act3Scene";
                }

                EncounterContext.Current.encounterKind = EncounterKind.BossGate;

                // Boss Gate: Do NOT save map state (as we are leaving)
            }
            else
            {
                EncounterContext.Current.returnPolicy = ReturnPolicy.ReturnToChapter;
                EncounterContext.Current.encounterKind = EncounterKind.Normal;

                if (type == ChapterNodeType.EliteEnemy) EncounterContext.Current.encounterKind = EncounterKind.Elite;
                if (type == ChapterNodeType.Boss) EncounterContext.Current.encounterKind = EncounterKind.BossGate;

                // Normal Node: Save Map State
                if (ChapterMapManager.Instance != null)
                {
                    ChapterMapManager.Instance.SaveMapState();
                }
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
                case ChapterNodeType.LeftGate:
                case ChapterNodeType.RightGate:
                case ChapterNodeType.SkipGate:
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