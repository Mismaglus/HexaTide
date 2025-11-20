using UnityEngine;
using Core.Hex;
using Game.Battle;

namespace Game.UI
{
    [RequireComponent(typeof(LineRenderer))]
    public class BattleCursor : MonoBehaviour
    {
        [Header("Refs")]
        public BattleHexGrid grid;

        [Header("Settings")]
        public float yOffset = 0.1f;
        public float animationSpeed = 20f;

        [Header("Colors")]
        public Color validColor = new Color(1f, 0.6f, 0f, 1f);   // 橙色
        public Color invalidColor = new Color(1f, 0.2f, 0.2f, 0.5f); // 红色

        private LineRenderer _line;
        private Vector3 _targetPos;
        private bool _isVisible;

        void Awake()
        {
            _line = GetComponent<LineRenderer>();
            SetupLineRenderer();
            if (!grid) grid = FindFirstObjectByType<BattleHexGrid>();
            Hide();
        }

        void SetupLineRenderer()
        {
            _line.positionCount = 7;
            _line.useWorldSpace = false; // 点是局部的，物体跟随移动
            _line.loop = true;
            _line.startWidth = 0.15f;
            _line.endWidth = 0.15f;
            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _line.receiveShadows = false;

            var shader = Shader.Find("Sprites/Default");
            if (shader) _line.material = new Material(shader);
        }

        public void Show(HexCoords coords, bool isValid)
        {
            if (!grid || !grid.recipe) return;

            _isVisible = true;
            _line.enabled = true;

            // 1. 计算网格局部坐标
            // Vector3 localPos = HexMetrics.GridToWorld(coords.q, coords.r, grid.recipe.outerRadius, grid.recipe.useOddROffset);

            // ⭐ 关键修复：局部转世界坐标
            // 这会解决光标偏移到右上角的问题
            // Vector3 worldPos = grid.transform.TransformPoint(localPos);
            Vector3 worldPos = grid.GetTileWorldPosition(coords);

            _targetPos = worldPos + new Vector3(0, yOffset, 0);

            // 2. 颜色
            Color c = isValid ? validColor : invalidColor;
            _line.startColor = c;
            _line.endColor = c;

            // 3. 绘制形状
            DrawHex(grid.recipe.outerRadius);
        }

        public void Hide()
        {
            _isVisible = false;
            _line.enabled = false;
        }

        void Update()
        {
            if (!_isVisible) return;
            // 这里的 Lerp 速度调快一点，让跟随更跟手
            transform.position = Vector3.Lerp(transform.position, _targetPos, Time.deltaTime * animationSpeed);
        }

        void DrawHex(float radius)
        {
            float r = radius * 0.95f; // 稍微内缩，避免和格子边缘重叠
            for (int i = 0; i <= 6; i++)
            {
                float angle_deg = 60 * i - 30;
                float angle_rad = Mathf.Deg2Rad * angle_deg;
                float x = r * Mathf.Cos(angle_rad);
                float z = r * Mathf.Sin(angle_rad);
                _line.SetPosition(i, new Vector3(x, 0, z));
            }
        }
    }
}