using UnityEngine;

namespace Game.UI
{
    [RequireComponent(typeof(LineRenderer))]
    public class BattleArrow : MonoBehaviour
    {
        [Header("Settings")]
        public float arcHeight = 1.5f;
        public int resolution = 20;
        public float textureScrollSpeed = -2f;

        private LineRenderer _lr;
        private Vector3 _start;
        private Vector3 _end;
        private bool _active;

        void Awake()
        {
            _lr = GetComponent<LineRenderer>();
            _lr.positionCount = resolution;
            _lr.enabled = false;
        }

        // ⭐ 新增：支持传入颜色
        public void SetPositions(Vector3 start, Vector3 end, Color color)
        {
            _start = start;
            _end = end;
            _active = true;
            _lr.enabled = true;

            // 设置颜色 (注意：使用的材质 shader 需要支持 Vertex Color 或者我们要改 Main Color)
            // 这里简单设置 LineRenderer 的颜色
            _lr.startColor = color;
            _lr.endColor = color;

            UpdateCurve();
        }

        public void Hide()
        {
            _active = false;
            _lr.enabled = false;
        }

        void Update()
        {
            if (!_active) return;

            if (_lr.material != null)
            {
                float offset = Time.time * textureScrollSpeed;
                _lr.material.mainTextureOffset = new Vector2(offset, 0);
            }

            UpdateCurve();
        }

        void UpdateCurve()
        {
            Vector3 mid = (_start + _end) * 0.5f;
            mid.y += arcHeight;

            for (int i = 0; i < resolution; i++)
            {
                float t = i / (float)(resolution - 1);
                Vector3 p = CalculateBezierPoint(t, _start, mid, _end);
                _lr.SetPosition(i, p);
            }
        }

        Vector3 CalculateBezierPoint(float t, Vector3 p0, Vector3 p1, Vector3 p2)
        {
            float u = 1 - t;
            float tt = t * t;
            float uu = u * u;
            return (uu * p0) + (2 * u * t * p1) + (tt * p2);
        }
    }
}