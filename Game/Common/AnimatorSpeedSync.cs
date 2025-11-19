using UnityEngine;

namespace Game.Common
{
    /// <summary>
    /// Optional helper for animators that update in UnscaledTime and need to respect the global GameSpeedController.
    /// Attach it next to an Animator and it will multiply Animator.speed whenever game speed changes.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class AnimatorSpeedSync : MonoBehaviour
    {
        [Tooltip("Multiplicative bias applied on top of game speed (leave at 1 for default).")]
        [SerializeField] private float speedMultiplier = 1f;

        Animator _animator;
        GameSpeedController _controller;

        void Awake()
        {
            _animator = GetComponent<Animator>();
        }

        void OnEnable()
        {
            _controller = GameSpeedController.Instance ?? FindFirstObjectByType<GameSpeedController>(FindObjectsInactive.Exclude);
            if (_controller != null)
            {
                _controller.OnSpeedChanged += HandleSpeedChanged;
                HandleSpeedChanged(_controller.CurrentSpeed);
            }
            else
            {
                HandleSpeedChanged(1f);
            }
        }

        void OnDisable()
        {
            if (_controller != null)
                _controller.OnSpeedChanged -= HandleSpeedChanged;
        }

        void HandleSpeedChanged(float value)
        {
            if (_animator == null) return;
            _animator.speed = Mathf.Max(0f, value * speedMultiplier);
        }
    }
}
