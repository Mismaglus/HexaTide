// Scripts/Game/UI/Feedback/FloatingTextManager.cs
using UnityEngine;
using System.Collections.Generic;
using Game.Common;

namespace Game.UI.Feedback
{
    public class FloatingTextManager : MonoBehaviour
    {
        public static FloatingTextManager Instance { get; private set; }

        [Header("Assets")]
        public FloatingText textPrefab;
        public Canvas targetCanvas;

        [Header("Settings")]
        public Color damageColor = new Color(1f, 0.2f, 0.2f);
        public Color critColor = new Color(1f, 0.8f, 0.2f);
        public Color healColor = new Color(0.2f, 1f, 0.4f);
        public float critScale = 1.5f;

        // Object Pool
        private Queue<FloatingText> _pool = new Queue<FloatingText>();

        void Awake()
        {
            Instance = this;
            if (!targetCanvas) targetCanvas = FindFirstObjectByType<Canvas>();
        }

        public void ShowDamage(Vector3 worldPos, int amount, bool isCrit)
        {
            Color c = isCrit ? critColor : damageColor;
            float scale = isCrit ? critScale : 1.0f;
            string text = isCrit ? $"{amount}!" : amount.ToString();

            Spawn(worldPos, text, c, scale);
        }

        public void ShowHeal(Vector3 worldPos, int amount)
        {
            Spawn(worldPos, $"+{amount}", healColor, 1.0f);
        }

        public void ShowText(Vector3 worldPos, string text, Color color)
        {
            Spawn(worldPos, text, color, 1.0f);
        }

        private void Spawn(Vector3 worldPos, string text, Color color, float scale)
        {
            if (!targetCanvas || !textPrefab) return;

            FloatingText instance = GetFromPool();

            // Map World -> Screen
            if (BattleRuntimeRefs.Instance != null && BattleRuntimeRefs.Instance.battleCamera != null)
            {
                Camera cam = BattleRuntimeRefs.Instance.battleCamera;
                Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(cam, worldPos);

                // Add random jitter to prevent overlap
                screenPoint += new Vector2(GameRandom.Range(-20f, 20f), GameRandom.Range(-10f, 10f));

                RectTransform rect = instance.GetComponent<RectTransform>();

                // Convert Screen Point to Local Point in Canvas
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    targetCanvas.transform as RectTransform,
                    screenPoint,
                    targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : cam,
                    out Vector2 localPoint))
                {
                    rect.localPosition = localPoint;
                }
            }
            else
            {
                // Fallback if camera is missing
                instance.transform.position = worldPos;
            }

            instance.Setup(text, color, scale);
        }

        private FloatingText GetFromPool()
        {
            if (_pool.Count > 0)
            {
                var t = _pool.Dequeue();
                t.gameObject.SetActive(true);
                return t;
            }

            var newObj = Instantiate(textPrefab, targetCanvas.transform);
            return newObj;
        }

        public void ReturnToPool(FloatingText txt)
        {
            txt.gameObject.SetActive(false);
            _pool.Enqueue(txt);
        }
    }
}