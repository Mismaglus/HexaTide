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

        [Header("Visuals (Optional)")]
        [Tooltip("Prefab to spawn on this tile to represent content (e.g. Skull Icon)")]
        public GameObject markerPrefab;
        private GameObject _markerInstance;

        private HexCell _cell;

        void Awake()
        {
            _cell = GetComponent<HexCell>();
        }

        public void Initialize(ChapterNodeType nodeType)
        {
            type = nodeType;
            isCleared = false;

            // Setup specialized terrain logic if needed
            // e.g., Boss node might be visually distinct
            UpdateMarker();
        }

        public void SetCleared(bool cleared)
        {
            isCleared = cleared;
            UpdateMarker();
        }

        void UpdateMarker()
        {
            if (_markerInstance != null) Destroy(_markerInstance);

            // TODO: In Phase 3, we would instantiate specific icons here
            // based on the 'type' enum.
            // For now, simple debug logic or placeholder could go here.
        }

        public void Interact()
        {
            if (isCleared) return;

            Debug.Log($"[ChapterNode] Interaction Triggered: {type}");

            // Logic for interaction will be handled by ChapterMapManager
            // which listens to player arrival.
        }
    }
}