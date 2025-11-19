using UnityEngine;

namespace Game.Units
{
    /// <summary>
    /// Handles playing a hit reaction on the unit's animator (trigger-based).
    /// </summary>
    [DisallowMultipleComponent]
    public class UnitHitReaction : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private string triggerName = "GetHit";

        int _triggerHash;

        void Awake()
        {
            if (animator == null)
                animator = GetComponentInChildren<Animator>(true);
            _triggerHash = Animator.StringToHash(triggerName);
        }

        public void Play()
        {
            if (animator == null || string.IsNullOrEmpty(triggerName)) return;
            if (_triggerHash == 0)
                _triggerHash = Animator.StringToHash(triggerName);
            animator.SetTrigger(_triggerHash);
        }
    }
}
