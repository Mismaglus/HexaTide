// Scripts/Game/Common/CameraShakeController.cs
using UnityEngine;
using System.Collections;

namespace Game.Common
{
    public class CameraShakeController : MonoBehaviour
    {
        public static CameraShakeController Instance { get; private set; }

        [Header("Target")]
        [Tooltip("If empty, uses MainCamera")]
        public Transform cameraTransform;

        private Vector3 _originalLocalPos;
        private Coroutine _shakeRoutine;

        // Settings for "Impact" preset
        private const float DEFAULT_INTENSITY = 0.2f;
        private const float DEFAULT_DURATION = 0.15f;

        void Awake()
        {
            Instance = this;
            if (!cameraTransform)
            {
                Camera c = Camera.main;
                if (c) cameraTransform = c.transform;
            }
        }

        public void Shake(float intensity, float duration)
        {
            if (!cameraTransform) return;

            // If already shaking, we can override or add. Overriding is simpler.
            if (_shakeRoutine != null)
            {
                StopCoroutine(_shakeRoutine);
                // Restore logic is inside the routine, but if we interrupt, we might need to reset first
                // Actually, assuming CameraFrameOnGrid might be controlling parent or world pos,
                // we should shake LOCAL position relative to current frame.
                // Simpler: Just add noise.
            }
            _shakeRoutine = StartCoroutine(DoShake(intensity, duration));
        }

        public void ShakeImpact() => Shake(DEFAULT_INTENSITY, DEFAULT_DURATION);

        IEnumerator DoShake(float intensity, float duration)
        {
            // Note: If CameraFrameOnGrid is moving the camera every frame, 
            // modifying position directly might fight. 
            // Ideally, Shake should be on a child object holding the camera, 
            // or modify localPosition if CameraFrameOnGrid sets World Position.
            // Since CameraFrameOnGrid only updates when grid changes (mostly static),
            // saving start pos here is safe for momentary shakes.

            Vector3 startPos = cameraTransform.localPosition;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float percent = 1f - (elapsed / duration);

                // Dampen intensity over time
                float strength = intensity * percent;

                Vector3 offset = Random.insideUnitSphere * strength;
                cameraTransform.localPosition = startPos + offset;

                yield return null;
            }

            cameraTransform.localPosition = startPos;
            _shakeRoutine = null;
        }
    }
}