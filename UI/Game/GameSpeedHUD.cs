using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Game.Common;

namespace Game.UI
{
    /// <summary>
    /// Binds a UIToolkit DropdownField to the preset speeds defined in GameSpeedController.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class GameSpeedHUD : MonoBehaviour
    {
        [SerializeField] private UIDocument document;
        [SerializeField] private string dropdownName = "SpeedDropdown";

        DropdownField _dropdown;
        readonly List<float> _presetSpeeds = new();
        GameSpeedController _controller;
        bool _suppressChange;

        void Awake()
        {
            if (document == null)
                document = GetComponent<UIDocument>();
        }

        void OnEnable()
        {
            _controller = GameSpeedController.Instance ?? FindFirstObjectByType<GameSpeedController>(FindObjectsInactive.Exclude);

            if (document != null)
            {
                var root = document.rootVisualElement;
                if (root != null)
                    _dropdown = root.Q<DropdownField>(dropdownName);
            }

            if (_dropdown != null)
            {
                _dropdown.RegisterValueChangedCallback(OnDropdownChanged);
                PopulateOptions();
            }

            if (_controller != null)
                _controller.OnSpeedChanged += HandleSpeedChanged;

            RefreshSelection();
        }

        void OnDisable()
        {
            if (_controller != null)
                _controller.OnSpeedChanged -= HandleSpeedChanged;

            if (_dropdown != null)
                _dropdown.UnregisterValueChangedCallback(OnDropdownChanged);

            _dropdown = null;
            _controller = null;
            _presetSpeeds.Clear();
        }

        void PopulateOptions()
        {
            if (_dropdown == null) return;

            _presetSpeeds.Clear();
            if (_controller != null && _controller.PresetSpeeds != null && _controller.PresetSpeeds.Count > 0)
            {
                foreach (var speed in _controller.PresetSpeeds)
                    _presetSpeeds.Add(Mathf.Max(0.001f, speed));
            }
            else
            {
                _presetSpeeds.Add(_controller != null ? _controller.CurrentSpeed : 1f);
            }

            var choices = new List<string>(_presetSpeeds.Count);
            for (int i = 0; i < _presetSpeeds.Count; i++)
                choices.Add($"x{_presetSpeeds[i]:0.#}");

            _dropdown.choices = choices;
        }

        void HandleSpeedChanged(float value)
        {
            RefreshSelection();
        }

        void RefreshSelection()
        {
            if (_dropdown == null || _presetSpeeds.Count == 0 || _controller == null) return;

            float speed = _controller.CurrentSpeed;
            int closestIndex = 0;
            float closestDiff = Mathf.Abs(speed - _presetSpeeds[0]);
            for (int i = 1; i < _presetSpeeds.Count; i++)
            {
                float diff = Mathf.Abs(speed - _presetSpeeds[i]);
                if (diff < closestDiff)
                {
                    closestDiff = diff;
                    closestIndex = i;
                }
            }

            _suppressChange = true;
            _dropdown.index = closestIndex;
            _dropdown.SetValueWithoutNotify(_dropdown.choices[closestIndex]);
            _suppressChange = false;
        }

        void OnDropdownChanged(ChangeEvent<string> evt)
        {
            if (_suppressChange) return;
            if (_dropdown == null || _controller == null) return;

            int index = _dropdown.choices.IndexOf(evt.newValue);
            if (index < 0 || index >= _presetSpeeds.Count) return;

            _controller.SetSpeed(_presetSpeeds[index]);
        }
    }
}
