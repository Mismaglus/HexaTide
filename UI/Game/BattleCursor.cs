using UnityEngine;
using Core.Hex;
using Game.Battle;

namespace Game.UI
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class BattleCursor : MonoBehaviour
    {
        [Header("Refs")]
        public BattleHexGrid grid;

        [Header("Appearance")]
        public float width = 0.1f;
        public float yOffset = 0.1f;
        public Color validColor = new Color(1f, 0.6f, 0f, 1f);   // 橙色
        public Color invalidColor = new Color(1f, 0.2f, 0.2f, 0.5f); // 红色

        [Header("Follow")]
        public float animationSpeed = 20f;

        private MeshFilter _mf;
        private MeshRenderer _mr;
        private MaterialPropertyBlock _mpb;
        private Vector3 _targetPos;
        private bool _isVisible;
        private LineRenderer _legacyLine; // 旧版残留，禁用避免重复绘制

        void Awake()
        {
            _mf = GetComponent<MeshFilter>();
            _mr = GetComponent<MeshRenderer>();
            _legacyLine = GetComponent<LineRenderer>();
            if (_legacyLine) _legacyLine.enabled = false;
            _mpb = new MaterialPropertyBlock();
            if (!grid) grid = FindFirstObjectByType<BattleHexGrid>();

            if (_mr.sharedMaterial == null)
            {
                var shader = Shader.Find("Sprites/Default");
                if (shader) _mr.sharedMaterial = new Material(shader);
            }

            Hide();
        }

        public void Show(HexCoords coords, bool isValid)
        {
            if (!grid || !grid.recipe) return;

            bool wasHidden = !_isVisible;
            _isVisible = true;
            _mr.enabled = true;

            // 单格掩码，生成均匀宽度的六边形描边网格
            var mask = HexMask.Filled(1, 1);
            var recipe = grid.recipe;
            float borderYOffset = recipe.borderYOffset + yOffset;

            var mesh = HexBorderMeshBuilder.Build(
                mask,
                recipe.outerRadius,
                borderYOffset,
                recipe.thickness,
                width,
                recipe.useOddROffset,
                BorderMode.OuterOnly,
                Vector3.zero
            );
            _mf.sharedMesh = mesh;

            _mpb.SetColor("_Color", isValid ? validColor : invalidColor);
            _mpb.SetColor("_BaseColor", isValid ? validColor : invalidColor);
            _mr.SetPropertyBlock(_mpb);

            // 位置：直接放在目标格中心（世界坐标）
            Vector3 worldPos = grid.GetTileWorldPosition(coords);
            _targetPos = worldPos;

            // 如果之前隐藏过，直接跳到新位置，避免旧位置缓动过来
            if (wasHidden) transform.position = _targetPos;
        }

        public void Hide()
        {
            _isVisible = false;
            _mr.enabled = false;
            _mf.sharedMesh = null;
        }

        void Update()
        {
            if (!_isVisible) return;
            transform.position = Vector3.Lerp(transform.position, _targetPos, Time.deltaTime * animationSpeed);
        }
    }
}
