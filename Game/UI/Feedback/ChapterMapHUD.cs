using UnityEngine;
using TMPro;
using Game.World;

namespace Game.UI.World
{
    public class ChapterMapHUD : MonoBehaviour
    {
        [Header("UI References")]
        public TextMeshProUGUI tideLabel; // "Tide Rises in: 3"
        public TextMeshProUGUI rowLabel;  // "Current Depth: Row 2"

        void Update()
        {
            if (ChapterMapManager.Instance == null) return;

            var manager = ChapterMapManager.Instance;

            // Update Labels
            if (tideLabel)
            {
                int moves = manager.MovesBeforeNextTide;
                tideLabel.text = $"Tide Rises In: <b>{moves}</b> moves";

                // Color coding for danger
                tideLabel.color = (moves <= 1) ? Color.red : Color.white;
            }

            if (rowLabel)
            {
                rowLabel.text = $"Tide Row: {manager.CurrentTideRow}";
            }
        }
    }
}