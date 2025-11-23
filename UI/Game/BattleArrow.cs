using UnityEngine;

namespace Game.UI
{
    [RequireComponent(typeof(LineRenderer))]
    public class BattleArrow : MonoBehaviour
    {
        [Header("Settings")]
        public float arcHeight = 1.5f; // 抛物线高度
        public int resolution = 20;    // 曲线平滑度
        public float textureScrollSpeed = -2f; // 纹理滚动速度 (箭头流动效果)

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

        public void SetPositions(Vector3 start, Vector3 end)
        {
            _start = start;
            _end = end;
            _active = true;
            _lr.enabled = true;
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

            // 1. 纹理流动动画
            if (_lr.material != null)
            {
                float offset = Time.time * textureScrollSpeed;
                _lr.material.mainTextureOffset = new Vector2(offset, 0);
            }

            // 2. 实时更新曲线 (以防目标或相机微动)
            UpdateCurve();
        }

        void UpdateCurve()
        {
            // 简单的二次贝塞尔曲线
            // 控制点在起点和终点的中点上方
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