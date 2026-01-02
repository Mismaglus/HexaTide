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
        Vector3 _stretchBaseLocalPos;
        Vector3 _headBaseScale;
        Vector3 _headBaseLocalPos;
        float _baseConnectionLength;

        bool _hasTailAnchor;
        Vector3 _tailAnchorLocal; // in stretchRoot local space

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
                    stretchRoot.localPosition = _stretchBaseLocalPos;
                    head.localPosition = _headBaseLocalPos;
                    head.localScale = _headBaseScale;

                    SnapTailToStart();
                }
                else
                {
                    Vector3 stretchScale = _stretchBaseScale;
                    stretchScale.z = _stretchBaseScale.z * scaleFactor;
                    stretchRoot.localScale = stretchScale;
                    stretchRoot.localPosition = _stretchBaseLocalPos;

                    head.localPosition = _headBaseLocalPos + Vector3.back * (targetLength - _baseConnectionLength);
                    head.localScale = _headBaseScale;
                    transform.localScale = _baseScale;

                    SnapTailToStart();
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
            if (stretchRoot) _stretchBaseLocalPos = stretchRoot.localPosition;
            if (head)
            {
                _headBaseScale = head.localScale;
                _headBaseLocalPos = head.localPosition;
            }
            _baseConnectionLength = Mathf.Max(0.001f, Mathf.Abs(_headBaseLocalPos.z));

            // Precompute a stable tail anchor in stretchRoot local space.
            // We pick the center of the farthest (+Z) bounds face across shaft renderers.
            _hasTailAnchor = false;
            _tailAnchorLocal = Vector3.zero;
            if (stretchRoot)
            {
                var shaftRenderers = stretchRoot.GetComponentsInChildren<Renderer>(true);
                bool any = false;
                float bestMaxZ = float.NegativeInfinity;
                Vector3 bestCenter = Vector3.zero;
                foreach (var r in shaftRenderers)
                {
                    if (!r) continue;

                    Bounds b;
                    // Most renderers have localBounds; if not, fall back to world bounds.
                    if (r is SkinnedMeshRenderer smr)
                        b = smr.localBounds;
                    else
                        b = r.localBounds;

                    Vector3 c = b.center;
                    Vector3 e = b.extents;

                    float minX = float.PositiveInfinity, maxX = float.NegativeInfinity;
                    float minY = float.PositiveInfinity, maxY = float.NegativeInfinity;
                    float minZ = float.PositiveInfinity, maxZ = float.NegativeInfinity;

                    // 8 corners of the renderer bounds (in renderer-local), transformed into stretchRoot-local.
                    for (int xi = -1; xi <= 1; xi += 2)
                        for (int yi = -1; yi <= 1; yi += 2)
                            for (int zi = -1; zi <= 1; zi += 2)
                            {
                                Vector3 cornerLocal = c + Vector3.Scale(e, new Vector3(xi, yi, zi));
                                Vector3 cornerWorld = r.transform.TransformPoint(cornerLocal);
                                Vector3 cornerInStretch = stretchRoot.InverseTransformPoint(cornerWorld);

                                if (cornerInStretch.x < minX) minX = cornerInStretch.x;
                                if (cornerInStretch.x > maxX) maxX = cornerInStretch.x;
                                if (cornerInStretch.y < minY) minY = cornerInStretch.y;
                                if (cornerInStretch.y > maxY) maxY = cornerInStretch.y;
                                if (cornerInStretch.z < minZ) minZ = cornerInStretch.z;
                                if (cornerInStretch.z > maxZ) maxZ = cornerInStretch.z;
                                any = true;
                            }

                    if (maxZ > bestMaxZ)
                    {
                        bestMaxZ = maxZ;
                        bestCenter = new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, maxZ);
                    }
                }

                if (any && !float.IsInfinity(bestMaxZ) && !float.IsNaN(bestMaxZ))
                {
                    _tailAnchorLocal = bestCenter;
                    _hasTailAnchor = true;
                }
            }
            _initialized = true;
        }

        void SnapTailToStart()
        {
            if (!_hasTailAnchor || !stretchRoot || !head) return;

            // Where is our chosen tail anchor *currently* (after scaling) in root-local space?
            Vector3 tailWorld = stretchRoot.TransformPoint(_tailAnchorLocal);
            Vector3 tailInRoot = transform.InverseTransformPoint(tailWorld);

            // Shift shaft + head together so that tail anchor becomes (0,0,0) => start.
            stretchRoot.localPosition -= tailInRoot;
            head.localPosition -= tailInRoot;
        }

    }
}
