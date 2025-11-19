using UnityEngine;
using UnityEngine.UIElements;
using Game.Common;

namespace Game.UI
{
    /// <summary>
    /// Binds a UIToolkit button to cycle between preset game speeds defined in GameSpeedController.
    /// Reusable in any UI that exposes the button via UIDocument.
    /// </summary>
    public class GameSpeedButtonBinder : MonoBehaviour
    {
        [SerializeField] private UIDocument document;
        [SerializeField] private string buttonName = "SpeedToggleBtn";
        [SerializeField] private string labelFormat = "x{0:0.#}";

        Button _button;
        GameSpeedController _controller;

        void Awake()
        {
            if (document == null)
                document = GetComponent<UIDocument>() ?? GetComponentInParent<UIDocument>() ?? GetComponentInChildren<UIDocument>();
        }

        void OnEnable()
        {
            _controller = GameSpeedController.Instance ?? FindFirstObjectByType<GameSpeedController>(FindObjectsInactive.Exclude);
            if (document == null)
                document = GetComponent<UIDocument>() ?? GetComponentInParent<UIDocument>() ?? GetComponentInChildren<UIDocument>();
            if (document != null)
            {
                var root = document.rootVisualElement;
                if (root != null)
                    _button = root.Q<Button>(buttonName);
            }

            if (_button != null)
                _button.clicked += OnButtonClicked;

            if (_controller != null)
                _controller.OnSpeedChanged += HandleSpeedChanged;

            RefreshLabel();
        }

        void OnDisable()
        {
            if (_controller != null)
                _controller.OnSpeedChanged -= HandleSpeedChanged;

            if (_button != null)
                _button.clicked -= OnButtonClicked;

            _button = null;
            _controller = null;
        }

        void OnButtonClicked()
        {
            if (_controller != null)
                _controller.AdvanceToNextPreset();
        }

        void HandleSpeedChanged(float value)
        {
            RefreshLabel();
        }

        void RefreshLabel()
        {
            if (_button == null) return;

            float speed = 1f;
            if (_controller != null)
                speed = _controller.CurrentSpeed;

            _button.text = string.Format(labelFormat, speed);
        }
    }
}
