using UnityEngine;

namespace Game.UI
{
    public class PrefabArrow : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("Prefab length in world units when scale is 1.")]
        public float baseLength = 1f;
        public float minLength = 0.1f;
        public float maxLength = 100f;
        [Tooltip("0 = start (tail), 0.5 = center, 1 = end (tip) of the mesh.")]
        [Range(0f, 1f)] public float pivotOffset = 0.5f;

        [Header("Segmented Stretch")]
        [Tooltip("Optional: scale only this part along local Z.")]
        public Transform stretchRoot;
        [Tooltip("Optional: keep this part unscaled (tip).")]
        public Transform head;
        [Tooltip("Fixed head length in world units when scale is 1.")]
        public float headLength = 0.3f;

        Vector3 _baseScale;
        Renderer[] _renderers;
        bool _initialized;
        Vector3 _stretchBaseScale;
        Vector3 _headBaseScale;
        Vector3 _headBaseLocalPos;
        float _bodyBaseLength;

        void Awake()
        {
            InitializeIfNeeded();
        }

        public void SetPositions(Vector3 start, Vector3 end, Color color)
        {
            InitializeIfNeeded();
            Vector3 dir = end - start;
            float length = dir.magnitude;
            if (length <= 0.001f)
            {
                Hide();
                return;
            }

            float clampedLength = Mathf.Clamp(length, minLength, maxLength);
            float scaleFactor = clampedLength / Mathf.Max(baseLength, 0.001f);

            Vector3 forward = dir.normalized;
            float offset = clampedLength * Mathf.Clamp01(pivotOffset);
            transform.position = start + forward * offset;
            transform.rotation = Quaternion.LookRotation(-forward, Vector3.up);

            if (stretchRoot && head)
            {
                float bodyTargetLength = Mathf.Max(0f, clampedLength - Mathf.Max(0f, headLength));
                float bodyScaleFactor = bodyTargetLength / Mathf.Max(_bodyBaseLength, 0.001f);

                Vector3 stretchScale = _stretchBaseScale;
                stretchScale.z = _stretchBaseScale.z * bodyScaleFactor;
                stretchRoot.localScale = stretchScale;

                head.localPosition = _headBaseLocalPos + Vector3.back * (bodyTargetLength - _bodyBaseLength);
                head.localScale = _headBaseScale;

                transform.localScale = _baseScale;
            }
            else
            {
                Vector3 scaled = _baseScale;
                scaled.z = _baseScale.z * scaleFactor;
                transform.localScale = scaled;
            }

            SetVisible(true);
        }

        public void Hide()
        {
            InitializeIfNeeded();
            SetVisible(false);
        }

        void SetVisible(bool visible)
        {
            if (visible && !gameObject.activeSelf) gameObject.SetActive(true);
            if (_renderers == null) return;
            foreach (var r in _renderers)
            {
                if (r) r.enabled = visible;
            }
        }

        void InitializeIfNeeded()
        {
            if (_initialized) return;
            _baseScale = transform.localScale;
            if (_baseScale == Vector3.zero) _baseScale = Vector3.one;
            _renderers = GetComponentsInChildren<Renderer>(true);
            if (stretchRoot) _stretchBaseScale = stretchRoot.localScale;
            if (head)
            {
                _headBaseScale = head.localScale;
                _headBaseLocalPos = head.localPosition;
            }
            _bodyBaseLength = Mathf.Max(0.001f, baseLength - Mathf.Max(0f, headLength));
            _initialized = true;
        }

    }
}
