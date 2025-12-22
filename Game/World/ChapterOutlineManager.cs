using UnityEngine;
using System.Collections.Generic;
using Core.Hex;
using Game.Grid;
using Game.Battle;

namespace Game.World
{
    /// <summary>
    /// A lightweight version of GridOutlineManager specifically for the Chapter Map.
    /// It only handles the "Movement" outline state (white borders).
    /// </summary>
    public class ChapterOutlineManager : MonoBehaviour
    {
        [Header("References")]
        public BattleHexGrid grid;
        public Material outlineMaterial; // Assign "Mat_GridOutline" or similar

        [Header("Settings")]
        public float yOffset = 0.05f;
        public float width = 0.05f;
        public Color color = Color.white;

        private GameObject _outlineGO;
        private MeshFilter _mf;
        private MeshRenderer _mr;
        private MaterialPropertyBlock _mpb;

        void Awake()
        {
            if (!grid) grid = FindFirstObjectByType<BattleHexGrid>();

            _outlineGO = new GameObject("ChapterOutlines");
            _outlineGO.transform.SetParent(transform);

            _mf = _outlineGO.AddComponent<MeshFilter>();
            _mr = _outlineGO.AddComponent<MeshRenderer>();

            if (outlineMaterial == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (!shader) shader = Shader.Find("Unlit/Color");
                outlineMaterial = new Material(shader);
            }
            _mr.sharedMaterial = outlineMaterial;

            _mpb = new MaterialPropertyBlock();
            _mpb.SetColor("_BaseColor", color);
            _mpb.SetColor("_Color", color);
            _mr.SetPropertyBlock(_mpb);

            _outlineGO.SetActive(false);
        }

        public void ShowOutline(IEnumerable<HexCoords> coords)
        {
            if (grid == null || grid.recipe == null) return;

            var recipe = grid.recipe;

            // Determine mask size based on shape to match BattleHexGrid generation logic
            int w = recipe.width;
            int h = recipe.height;
            if (recipe.shape == GridShape.Hexagon)
            {
                int size = recipe.radius * 2 + 1;
                w = size + 2;
                h = size + 2;
            }

            // Use full grid size mask to ensure coordinate system matches exactly
            var mask = new HexMask(w, h);
            bool any = false;

            foreach (var c in coords)
            {
                if (mask.InBounds(c.q, c.r))
                {
                    mask[c.q, c.r] = true;
                    any = true;
                }
            }

            if (!any)
            {
                Hide();
                return;
            }

            // Build Mesh using Grid's CenterOffset
            var mesh = HexBorderMeshBuilder.Build(
                mask.GetRawBits(),
                mask.Width,
                mask.Height,
                recipe.outerRadius,
                recipe.borderYOffset + yOffset,
                recipe.thickness,
                width,
                recipe.useOddROffset,
                BorderMode.OuterOnly,
                grid.CenterOffset
            );

            _mf.sharedMesh = mesh;

            // Align perfectly with Grid
            _outlineGO.transform.position = grid.transform.position;
            _outlineGO.SetActive(true);
        }

        public void Hide()
        {
            _outlineGO.SetActive(false);
        }
    }
}
