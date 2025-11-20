using UnityEngine;
using System.Collections.Generic;
using Core.Hex;
using Game.Battle;

namespace Game.UI
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class RangeOutlineDrawer : MonoBehaviour
    {
        [Header("Refs")]
        public BattleHexGrid grid;

        [Header("Appearance")]
        [Tooltip("轮廓线颜色")]
        public Color outlineColor = new Color(0.2f, 0.8f, 1.0f, 1.0f); // 亮青色
        [Tooltip("轮廓线宽度")]
        public float width = 0.1f;
        [Tooltip("垂直高度偏移 (防止与地面穿模)")]
        public float yOffset = 0.15f;

        private MeshFilter _mf;
        private MeshRenderer _mr;
        private MaterialPropertyBlock _mpb;

        void Awake()
        {
            _mf = GetComponent<MeshFilter>();
            _mr = GetComponent<MeshRenderer>();
            if (!grid) grid = FindFirstObjectByType<BattleHexGrid>();

            // 初始化材质属性块
            _mpb = new MaterialPropertyBlock();

            // 尝试设置一个默认材质 (防止变紫)
            if (_mr.sharedMaterial == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (!shader) shader = Shader.Find("Unlit/Color");
                if (shader) _mr.sharedMaterial = new Material(shader);
            }
        }

        public void Show(HashSet<HexCoords> tiles)
        {
            if (!grid || !grid.recipe) return;
            if (tiles == null || tiles.Count == 0) { Hide(); return; }

            _mr.enabled = true;

            // 1. 构建掩码 (HexMask)
            var recipe = grid.recipe;
            var mask = new HexMask(recipe.width, recipe.height);

            foreach (var t in tiles)
            {
                if (mask.InBounds(t.q, t.r))
                    mask[t.q, t.r] = true;
            }

            // 2. 利用 HexBorderMeshBuilder 生成轮廓网格
            // 关键参数：BorderMode.OuterOnly (只画最外圈)
            var mesh = HexBorderMeshBuilder.Build(
                mask,
                recipe.outerRadius,
                recipe.borderYOffset + yOffset, // 抬高一点
                recipe.thickness,
                width,
                recipe.useOddROffset,
                BorderMode.OuterOnly // ⭐ 核心：只要轮廓
            );

            _mf.sharedMesh = mesh;

            // 3. 设置颜色
            _mpb.SetColor("_BaseColor", outlineColor);
            _mpb.SetColor("_Color", outlineColor);
            _mr.SetPropertyBlock(_mpb);
        }

        public void Hide()
        {
            _mr.enabled = false;
            _mf.sharedMesh = null;
        }
    }
}