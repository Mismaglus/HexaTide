using System.Collections.Generic;
using System;
using UnityEngine;

namespace Game.Common
{
    /// <summary>
    /// Central place to control the global game speed (Time.timeScale & fixedDeltaTime).
    /// </summary>
    [DisallowMultipleComponent]
    public class GameSpeedController : MonoBehaviour
    {
        static GameSpeedController _instance;
        public static GameSpeedController Instance
        {
            get
            {
                if (_instance != null) return _instance;
                _instance = FindFirstObjectByType<GameSpeedController>(FindObjectsInactive.Exclude);
                return _instance;
            }
        }

        [Header("Speed Settings")]
        [SerializeField, Min(0.01f)] private float defaultSpeed = 1f;
        [SerializeField, Min(0f)] private float minSpeed = 0.1f;
        [SerializeField, Min(0f)] private float maxSpeed = 3f;

        [Header("Preset Speeds")]
        [SerializeField] private float[] presetSpeeds = new[] { 1f, 2f, 3f };

        public float CurrentSpeed { get; private set; } = 1f;
        public float MinSpeed
        {
            get
            {
                float baseMin = Mathf.Max(0.001f, minSpeed);
                if (presetSpeeds != null && presetSpeeds.Length > 0)
                {
                    float presetMin = presetSpeeds[0];
                    for (int i = 1; i < presetSpeeds.Length; i++)
                        presetMin = Mathf.Min(presetMin, presetSpeeds[i]);
                    baseMin = Mathf.Min(baseMin, presetMin);
                }
                return Mathf.Max(0.001f, baseMin);
            }
        }
        public float MaxSpeed
        {
            get
            {
                float baseMax = Mathf.Max(MinSpeed + 0.001f, maxSpeed);
                if (presetSpeeds != null && presetSpeeds.Length > 0)
                {
                    float presetMax = presetSpeeds[0];
                    for (int i = 1; i < presetSpeeds.Length; i++)
                        presetMax = Mathf.Max(presetMax, presetSpeeds[i]);
                    baseMax = Mathf.Max(baseMax, presetMax);
                }
                return Mathf.Max(MinSpeed + 0.001f, baseMax);
            }
        }
        public IReadOnlyList<float> PresetSpeeds => presetSpeeds;

        public event Action<float> OnSpeedChanged;

        float _baseFixedDelta;

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            CurrentSpeed = Mathf.Clamp(defaultSpeed, MinSpeed, MaxSpeed);
            _baseFixedDelta = Time.fixedDeltaTime;
            ApplySpeed(CurrentSpeed);
        }

        void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        public void ResetSpeed() => SetSpeed(defaultSpeed);

        public void SetSpeed(float value)
        {
            float clamped = Mathf.Clamp(value, MinSpeed, MaxSpeed);
            if (Mathf.Approximately(clamped, CurrentSpeed)) return;
            CurrentSpeed = clamped;
            ApplySpeed(CurrentSpeed);
            OnSpeedChanged?.Invoke(CurrentSpeed);
        }

        public void StepSpeed(float delta)
        {
            SetSpeed(CurrentSpeed + delta);
        }

        public void AdvanceToNextPreset()
        {
            if (presetSpeeds == null || presetSpeeds.Length == 0) return;

            int closestIndex = 0;
            float closestDiff = Mathf.Abs(CurrentSpeed - presetSpeeds[0]);
            for (int i = 1; i < presetSpeeds.Length; i++)
            {
                float diff = Mathf.Abs(CurrentSpeed - presetSpeeds[i]);
                if (diff < closestDiff)
                {
                    closestDiff = diff;
                    closestIndex = i;
                }
            }

            int nextIndex = (closestIndex + 1) % presetSpeeds.Length;
            SetSpeed(presetSpeeds[nextIndex]);
        }

        public void SetSpeedFromPreset(int index)
        {
            if (presetSpeeds == null || presetSpeeds.Length == 0) return;
            index = Mathf.Clamp(index, 0, presetSpeeds.Length - 1);
            SetSpeed(presetSpeeds[index]);
        }

        void ApplySpeed(float value)
        {
            Time.timeScale = value;
            Time.fixedDeltaTime = _baseFixedDelta * value;
        }
    }
}
