using UnityEngine;
using UnityEngine.UIElements;

namespace Game.UI
{
    /// <summary>
    /// Lightweight service that toggles a common settings panel (UIToolkit) used in combat or world scenes.
    /// </summary>
    public class SettingsPanelService : MonoBehaviour
    {
        static SettingsPanelService _instance;

        [Header("UI References")]
        [SerializeField] private UIDocument settingsDocument;
        [SerializeField] private string rootElementName = "SettingsPanel";

        VisualElement _rootElement;
        bool _isShowing;

        public static bool IsShowing => _instance != null && _instance._isShowing;

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            if (settingsDocument == null)
                settingsDocument = GetComponent<UIDocument>();

            if (settingsDocument != null)
            {
                _rootElement = string.IsNullOrEmpty(rootElementName)
                    ? settingsDocument.rootVisualElement
                    : settingsDocument.rootVisualElement.Q<VisualElement>(rootElementName);

                if (_rootElement != null)
                    _rootElement.style.display = DisplayStyle.None;
            }
        }

        void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        public static void ShowOrToggle()
        {
            EnsureInstance();
            if (_instance == null) return;

            if (_instance._isShowing)
                _instance.HideInternal();
            else
                _instance.ShowInternal();
        }

        public static void Show()
        {
            EnsureInstance();
            _instance?.ShowInternal();
        }

        public static void Hide()
        {
            _instance?.HideInternal();
        }

        void ShowInternal()
        {
            if (_rootElement == null) return;
            _rootElement.style.display = DisplayStyle.Flex;
            _isShowing = true;
        }

        void HideInternal()
        {
            if (_rootElement == null) return;
            _rootElement.style.display = DisplayStyle.None;
            _isShowing = false;
        }

        static void EnsureInstance()
        {
            if (_instance != null) return;
            _instance = FindFirstObjectByType<SettingsPanelService>(FindObjectsInactive.Include);
            if (_instance == null)
            {
                var go = new GameObject(nameof(SettingsPanelService));
                _instance = go.AddComponent<SettingsPanelService>();
            }
        }
    }
}

