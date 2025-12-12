using Core.Hex;
using Game.Common;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Game.Grid;

namespace Game.Battle
{
    [ExecuteAlways]
    public class BattleHexGrid : MonoBehaviour, IHexGridProvider
    {
        [Header("Grid Configuration")]
        [SerializeField] private GridRecipe _recipe;
        [SerializeField, HideInInspector] private uint _version;

        public uint Version
        {
            get => _version;
            private set => _version = value;
        }

        public GridRecipe recipe
        {
            get => _recipe;
            private set => _recipe = value;
        }

        [SerializeField] private bool _useRecipeBorderMode = true;
        [SerializeField] private BorderMode _runtimeBorderMode = BorderMode.AllUnique;
        private BorderMode _lastRuntimeMode;

        [SerializeField] private int _lastRecipeHash;

        const string CHILD_PREFIX_TILES = "Hex_r";

        Material _sharedMat;
        Material _borderMat;
        bool _dirty;

        // Serialized to keep the map centered in the scene view
        [SerializeField, HideInInspector] private Vector3 _serializedCenterOffset;
        public Vector3 CenterOffset
        {
            get => _serializedCenterOffset;
            private set => _serializedCenterOffset = value;
        }

        public IEnumerable<TileTag> EnumerateTiles()
        {
            return GetComponentsInChildren<TileTag>(true);
        }

        void OnEnable() { _dirty = true; }
#if UNITY_EDITOR
        void OnValidate() { _dirty = true; }
#endif
        void Update()
        {
            if (!isActiveAndEnabled) return;

            bool need = _dirty;

            if (recipe != null)
            {
                int now = ComputeRecipeHash();
                if (now != _lastRecipeHash) { _lastRecipeHash = now; need = true; }
            }

            if (!_useRecipeBorderMode && _runtimeBorderMode != _lastRuntimeMode)
            {
                _lastRuntimeMode = _runtimeBorderMode;
                need = true;
            }

            if (need) { _dirty = false; Rebuild(); }
        }

        void ClearChildren()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var ch = transform.GetChild(i).gameObject;
#if UNITY_EDITOR
                if (!Application.isPlaying) DestroyImmediate(ch);
                else Destroy(ch);
#else
                Destroy(ch);
#endif
            }
        }

        public void Rebuild()
        {
            if (!isActiveAndEnabled) return;

            // Default fallback
            if (recipe == null)
            {
                recipe = ScriptableObject.CreateInstance<GridRecipe>();
                recipe.shape = GridShape.Rectangle;
                recipe.width = 6; recipe.height = 6;
                recipe.outerRadius = 1f;
                recipe.useOddROffset = true;
                recipe.borderMode = BorderMode.AllUnique;
            }

            ClearChildren();
            SetupMaterials();

            // 1. Build the Mask (Rectangle or Hexagon)
            var mask = BuildMask(recipe);

            // 2. Compute visual bounds to center the grid
            var worldBounds = HexBorderMeshBuilder.ComputeWorldBounds(mask, recipe.outerRadius, recipe.useOddROffset);
            CenterOffset = new Vector3(worldBounds.center.x, 0f, worldBounds.center.z);

            // 3. Instantiate Tiles
            for (int r = 0; r < mask.Height; r++)
            {
                for (int q = 0; q < mask.Width; q++)
                {
                    if (!mask[q, r]) continue;

                    Vector3 pos = HexMetrics.GridToWorld(q, r, recipe.outerRadius, recipe.useOddROffset) - CenterOffset;

                    var go = new GameObject($"{CHILD_PREFIX_TILES}{r}_c{q}");
                    go.transform.SetParent(transform, false);
                    go.transform.localPosition = pos;

                    var tile = go.AddComponent<HexTile>();
                    tile.outerRadius = recipe.outerRadius;
                    tile.thickness = recipe.thickness;

                    var mr = go.GetComponent<MeshRenderer>();
                    mr.sharedMaterial = _sharedMat;

                    tile.BuildImmediate();

                    var tag = go.AddComponent<TileTag>();
                    tag.Set(q, r);

                    // Add HexCell for logic
                    var cell = go.AddComponent<HexCell>();
                }
            }

            // 4. Build Borders
            BuildBorders(mask);
        }

        private void SetupMaterials()
        {
            if (recipe.tileMaterial != null) _sharedMat = recipe.tileMaterial;
            else if (_sharedMat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                _sharedMat = new Material(shader) { color = new Color(0.18f, 0.2f, 0.25f, 1f) };
            }

            if (recipe.borderMaterial != null) _borderMat = recipe.borderMaterial;
            else if (_borderMat == null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
                _borderMat = new Material(sh);
                if (_borderMat.HasProperty("_Surface")) _borderMat.SetFloat("_Surface", 1f);
                if (_borderMat.HasProperty("_ZWrite")) _borderMat.SetFloat("_ZWrite", 0f);
                if (_borderMat.HasProperty("_Cull")) _borderMat.SetFloat("_Cull", 0f);
                _borderMat.renderQueue = (int)RenderQueue.Transparent;
            }
        }

        private void BuildBorders(HexMask mask)
        {
            var mode = _useRecipeBorderMode ? recipe.borderMode : _runtimeBorderMode;
            if (mode == BorderMode.None) return;

            var bordersGO = new GameObject("GridBorders");
            bordersGO.transform.SetParent(transform, false);

            var mf = bordersGO.AddComponent<MeshFilter>();
            var mr = bordersGO.AddComponent<MeshRenderer>();
            mr.sharedMaterial = _borderMat;

            var mesh = HexBorderMeshBuilder.Build(
                mask,
                recipe.outerRadius, recipe.borderYOffset, recipe.thickness,
                recipe.borderWidth, recipe.useOddROffset,
                mode
            );
            mf.sharedMesh = mesh;

            var mpb = new MaterialPropertyBlock();
            mpb.SetColor("_BaseColor", recipe.borderColor);
            mpb.SetColor("_Color", recipe.borderColor);
            mr.SetPropertyBlock(mpb);
        }

        HexMask BuildMask(GridRecipe rcp)
        {
            HexMask mask;

            if (rcp.shape == GridShape.Hexagon)
            {
                // Hexagon Generation:
                // Create a bounding box large enough (Diameter approx 2*Radius + 1)
                // Use offset coords, iterate, check distance from "Center Hex"
                int size = rcp.radius * 2 + 1;
                // Add padding for safety
                int w = size + 2;
                int h = size + 2;

                mask = new HexMask(w, h);

                // Center roughly in the middle of the mask array
                // For offset coordinates, (Radius, Radius) is roughly center
                HexCoords centerHex = new HexCoords(rcp.radius + 1, rcp.radius + 1);

                for (int r = 0; r < h; r++)
                {
                    for (int q = 0; q < w; q++)
                    {
                        var current = new HexCoords(q, r);
                        if (current.DistanceTo(centerHex) <= rcp.radius)
                        {
                            mask[q, r] = true;
                        }
                    }
                }
            }
            else // Rectangle
            {
                mask = HexMask.Filled(rcp.width, rcp.height);

                // Only apply column holes in rectangle mode for now (or update logic if needed)
                if (rcp.emptyColumns != null)
                {
                    foreach (var col in rcp.emptyColumns)
                        mask.ClearColumn(col);
                }
            }

            // Apply random holes to both shapes
            if (rcp.enableRandomHoles && rcp.holeChance > 0f)
                mask.RandomHoles(rcp.holeChance, rcp.randomSeed);

            Version++;
            return mask;
        }

        public void SetBorderMode(BorderMode mode) { _runtimeBorderMode = mode; _dirty = true; }

        [ContextMenu("Rebuild Grid")]
        void RebuildFromMenu() { _dirty = true; }

        int ComputeRecipeHash()
        {
            if (recipe == null) return 0;
            unchecked
            {
                int h = 17;
                h = h * 31 + (int)recipe.shape;
                h = h * 31 + recipe.width;
                h = h * 31 + recipe.height;
                h = h * 31 + recipe.radius; // Track radius
                h = h * 31 + recipe.useOddROffset.GetHashCode();
                h = h * 31 + recipe.borderMode.GetHashCode();
                h = h * 31 + recipe.outerRadius.GetHashCode();
                h = h * 31 + recipe.thickness.GetHashCode();
                h = h * 31 + recipe.borderWidth.GetHashCode();
                h = h * 31 + recipe.borderYOffset.GetHashCode();
                if (recipe.emptyColumns != null)
                {
                    h = h * 31 + recipe.emptyColumns.Length;
                    for (int i = 0; i < recipe.emptyColumns.Length; i++) h = h * 31 + recipe.emptyColumns[i];
                }
                h = h * 31 + recipe.enableRandomHoles.GetHashCode();
                h = h * 31 + recipe.holeChance.GetHashCode();
                h = h * 31 + recipe.randomSeed;
                return h;
            }
        }

        public Vector3 GetTileWorldPosition(HexCoords c)
        {
            if (recipe == null) return Vector3.zero;
            Vector3 localPos = HexMetrics.GridToWorld(c.q, c.r, recipe.outerRadius, recipe.useOddROffset) - CenterOffset;
            return transform.TransformPoint(localPos);
        }
    }
}