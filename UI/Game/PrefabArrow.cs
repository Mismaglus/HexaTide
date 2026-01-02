using UnityEngine;

namespace Game.UI
{
    public class PrefabArrow : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Shaft root/pivot at the tail (start point).")]
        public Transform stretchRoot;
        [Tooltip("Head pivot at the shaft connection point (base of the head).")]
        public Transform head;

        Vector3 _baseScale;
        Renderer[] _renderers;
        bool _initialized;
        Vector3 _stretchBaseScale;
        Vector3 _headBaseScale;
        Vector3 _headBaseLocalPos;
        float _baseConnectionLength;

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

            Vector3 forward = dir.normalized;
            transform.position = start;
            transform.rotation = Quaternion.LookRotation(-forward, Vector3.up);

            if (stretchRoot && head)
            {
                float targetLength = length;
                float scaleFactor = targetLength / Mathf.Max(0.001f, _baseConnectionLength);

                if (targetLength <= _baseConnectionLength)
                {
                    float uniformScale = Mathf.Max(0.001f, scaleFactor);
                    transform.localScale = _baseScale * uniformScale;
                    stretchRoot.localScale = _stretchBaseScale;
                    head.localPosition = _headBaseLocalPos;
                    head.localScale = _headBaseScale;
                }
                else
                {
                    Vector3 stretchScale = _stretchBaseScale;
                    stretchScale.z = _stretchBaseScale.z * scaleFactor;
                    stretchRoot.localScale = stretchScale;

                    head.localPosition = _headBaseLocalPos + Vector3.back * (targetLength - _baseConnectionLength);
                    head.localScale = _headBaseScale;
                    transform.localScale = _baseScale;
                }
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
            _baseConnectionLength = Mathf.Max(0.001f, Mathf.Abs(_headBaseLocalPos.z));
            _initialized = true;
        }

    }
}
