using UnityEngine;

namespace Game.Core
{
    public enum Side { Player, Enemy, Neutral }

    [DisallowMultipleComponent]
    public class FactionMembership : MonoBehaviour
    {
        [Header("Faction")]
        public Side side = Side.Player;

        [Header("Control")]
        [Tooltip("If true, this unit accepts local player commands.")]
        public bool playerControlled = true;

        public bool IsPlayerControlled => playerControlled && side == Side.Player;
    }
}
