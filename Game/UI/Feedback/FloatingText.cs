// Scripts/Game/UI/Feedback/FloatingText.cs
using UnityEngine;
using TMPro;
using System.Collections;

namespace Game.UI.Feedback
{
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class FloatingText : MonoBehaviour
    {
        public TextMeshProUGUI label;

        [Header("Animation")]
        public float moveSpeed = 100f;
        public float fadeDuration = 1.0f;
        public Vector3 moveDirection = Vector3.up;
        public AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0, 0, 0.2f, 1);

        private float _timer;
        private Color _startColor;
        private Vector3 _startPos;
        private bool _active;

        void Awake()
        {
            if (!label) label = GetComponent<TextMeshProUGUI>();
        }

        public void Setup(string text, Color color, float sizeScale = 1f)
        {
            label.text = text;
            label.color = color;
            _startColor = color;

            transform.localScale = Vector3.one * sizeScale;
            _active = true;
            _timer = 0f;

            // Start animation
            StartCoroutine(AnimateRoutine());
        }

        IEnumerator AnimateRoutine()
        {
            while (_timer < fadeDuration)
            {
                _timer += Time.deltaTime;
                float progress = _timer / fadeDuration;

                // Move
                transform.position += moveDirection * moveSpeed * Time.deltaTime;

                // Scale (Pop in)
                float scale = scaleCurve.Evaluate(progress);
                if (_timer > 0.2f) scale = 1f; // settle

                // Fade out near end
                float alpha = 1f;
                if (progress > 0.7f)
                {
                    alpha = 1f - ((progress - 0.7f) / 0.3f);
                }

                label.color = new Color(_startColor.r, _startColor.g, _startColor.b, alpha);

                yield return null;
            }

            gameObject.SetActive(false); // Return to pool (handled by manager usually, or just hide)
        }
    }
}