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
        public Color outlineColor = new Color(0.2f, 0.8f, 1.0f, 1.0f);
        public float width = 0.1f;
        public float yOffset = 0.15f;

        [Header("Rendering Order")]
        [Tooltip("值越大，显示越靠前 (覆盖在值小的上面)")]
        public int sortingOrder = 0; // ⭐ 新增：排序层级

        private MeshFilter _mf;
        private MeshRenderer _mr;
        private MaterialPropertyBlock _mpb;

        void Awake()
        {
            _mf = GetComponent<MeshFilter>();
            _mr = GetComponent<MeshRenderer>();
            if (!grid) grid = FindFirstObjectByType<BattleHexGrid>();
            _mpb = new MaterialPropertyBlock();

            if (_mr.sharedMaterial == null)
            {
                var shader = Shader.Find("Sprites/Default");
                if (shader) _mr.sharedMaterial = new Material(shader);
            }
        }

        void LateUpdate()
        {
            if (grid != null)
            {
                transform.position = grid.transform.position;
                transform.rotation = grid.transform.rotation;
            }
        }

#if UNITY_EDITOR
        // 让编辑器调整数值时实时生效
        void OnValidate()
        {
            if (_mr == null) _mr = GetComponent<MeshRenderer>();
            if (_mr != null) _mr.sortingOrder = sortingOrder;
        }
#endif

        public void Show(HashSet<HexCoords> tiles)
        {
            if (!grid || !grid.recipe) return;
            if (tiles == null || tiles.Count == 0) { Hide(); return; }

            _mr.enabled = true;

            // ⭐ 强制应用渲染顺序
            _mr.sortingOrder = sortingOrder;

            var recipe = grid.recipe;
            var mask = new HexMask(recipe.width, recipe.height);
            foreach (var t in tiles)
            {
                if (mask.InBounds(t.q, t.r)) mask[t.q, t.r] = true;
            }

            var mesh = HexBorderMeshBuilder.Build(
                mask,
                recipe.outerRadius,
                recipe.borderYOffset + yOffset,
                recipe.thickness,
                width,
                recipe.useOddROffset,
                BorderMode.OuterOnly,
                grid.CenterOffset
            );

            _mf.sharedMesh = mesh;
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