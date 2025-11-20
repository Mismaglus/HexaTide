using UnityEngine;
using Core.Hex;
using Game.Battle; // 引用 BattleHexGrid

namespace Game.UI
{
    [RequireComponent(typeof(LineRenderer))]
    public class BattleCursor : MonoBehaviour
    {
        [Header("Refs")]
        public BattleHexGrid grid; // 需要读取网格半径配置

        [Header("Settings")]
        public float yOffset = 0.1f; // 稍微浮在地面上方
        public float animationSpeed = 10f; // 移动平滑度

        [Header("Colors")]
        public Color validColor = new Color(1f, 0.6f, 0f, 1f); // 橙色 (有效目标)
        public Color invalidColor = new Color(1f, 0.2f, 0.2f, 0.5f); // 红色 (无效)

        private LineRenderer _line;
        private Vector3 _targetPos;
        private bool _isVisible;

        void Awake()
        {
            _line = GetComponent<LineRenderer>();
            SetupLineRenderer();

            if (!grid) grid = FindFirstObjectByType<BattleHexGrid>();

            // 初始隐藏
            Hide();
        }

        void SetupLineRenderer()
        {
            _line.positionCount = 7; // 六边形闭环需要7个点
            _line.useWorldSpace = false; // 本地坐标，方便移动父物体
            _line.loop = true;
            _line.startWidth = 0.15f;
            _line.endWidth = 0.15f;
            _line.material = new Material(Shader.Find("Sprites/Default")); // 使用简单材质
        }

        public void Show(HexCoords coords, bool isValid)
        {
            if (!grid || !grid.recipe) return;

            _isVisible = true;
            _line.enabled = true;

            // 1. 计算目标位置
            _targetPos = HexMetrics.GridToWorld(coords.q, coords.r, grid.recipe.outerRadius, grid.recipe.useOddROffset);
            _targetPos.y += yOffset;

            // 2. 设置颜色
            Color c = isValid ? validColor : invalidColor;
            _line.startColor = c;
            _line.endColor = c;

            // 3. 重新生成网格形状 (以防半径变化)
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
            // 平滑移动到目标格子
            transform.position = Vector3.Lerp(transform.position, _targetPos, Time.deltaTime * animationSpeed);
        }

        void DrawHex(float radius)
        {
            // 生成尖顶六边形的6个角点 (从本地坐标原点)
            for (int i = 0; i <= 6; i++)
            {
                float angle_deg = 60 * i - 30;
                float angle_rad = Mathf.Deg2Rad * angle_deg;
                float x = radius * Mathf.Cos(angle_rad);
                float z = radius * Mathf.Sin(angle_rad);
                _line.SetPosition(i, new Vector3(x, 0, z));
            }
        }
    }
}