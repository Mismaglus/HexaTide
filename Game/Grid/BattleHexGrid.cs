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
        [Header("网格配方")]
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
        [SerializeField] private bool _useRecipeBorderMode = true; // 勾上=跟随配方；关掉=用运行时覆盖
        [SerializeField] private BorderMode _runtimeBorderMode = BorderMode.AllUnique;
        private BorderMode _lastRuntimeMode;                      // 运行时变更检测

        [SerializeField] private int _lastRecipeHash;                      // 配方哈希（运行时检测用）

        const string CHILD_PREFIX_TILES = "Hex_r";
        const string CHILD_BORDERS = "GridBorders";

        Material _sharedMat;   // 瓦片材质
        Material _borderMat;   // 边线材质
        bool _dirty;
        public IEnumerable<TileTag> EnumerateTiles()
        {
            return GetComponentsInChildren<TileTag>(true);
        }

        void OnEnable() { _dirty = true; }
#if UNITY_EDITOR
        void OnValidate() { _dirty = true; }    // 这里只打标记
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

            // 临时配方（展开就能跑）
            if (recipe == null)
            {
                recipe = ScriptableObject.CreateInstance<GridRecipe>();
                recipe.width = 6; recipe.height = 6;
                recipe.outerRadius = 1f; recipe.thickness = 0f;
                recipe.useOddROffset = true;
                recipe.borderMode = BorderMode.AllUnique;
            }

            ClearChildren();

            // === 材质 ===
            if (recipe.tileMaterial != null) _sharedMat = recipe.tileMaterial;
            else if (_sharedMat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                _sharedMat = new Material(shader) { color = new Color(0.18f, 0.2f, 0.25f, 1f) };
            }

            if (recipe.borderMaterial != null) _borderMat = recipe.borderMaterial;
            else if (_borderMat == null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Unlit");
                _borderMat = new Material(sh);
                _borderMat.SetFloat("_Surface", 1f); // Transparent
                _borderMat.SetFloat("_ZWrite", 0f);  // Depth Off
                _borderMat.SetFloat("_Cull", 0f);    // Double-sided
                _borderMat.renderQueue = (int)RenderQueue.Transparent;
            }

            // === 生成掩码（带空列/随机洞）===
            var mask = BuildMask(recipe);

            // === 居中偏移（只看存在的格子）===
            var worldBounds = HexBorderMeshBuilder.ComputeWorldBounds(mask, recipe.outerRadius, recipe.useOddROffset);
            Vector3 centerOffset = new Vector3(worldBounds.center.x, 0f, worldBounds.center.z);

            // === 铺瓦片（只放存在的格子）===
            for (int r = 0; r < recipe.height; r++)
            {
                for (int q = 0; q < recipe.width; q++)
                {
                    if (!mask[q, r]) continue;

                    Vector3 pos = HexMetrics.GridToWorld(q, r, recipe.outerRadius, recipe.useOddROffset) - centerOffset;

                    var go = new GameObject($"{CHILD_PREFIX_TILES}{r}_c{q}");
                    go.transform.SetParent(transform, false);
                    go.transform.localPosition = pos;

                    var tile = go.AddComponent<HexTile>();
                    tile.outerRadius = recipe.outerRadius;
                    tile.thickness = recipe.thickness;

                    var mr = go.GetComponent<MeshRenderer>();
                    mr.sharedMaterial = _sharedMat;

                    tile.BuildImmediate();

                    var tag = go.AddComponent<Game.Common.TileTag>();
                    tag.Set(q, r);

                }
            }

            // === 边线（唯一网格）===
            var mode = _useRecipeBorderMode ? recipe.borderMode : _runtimeBorderMode;
            if (mode != BorderMode.None)
            {
                var bordersGO = new GameObject("GridBorders");
                bordersGO.transform.SetParent(transform, false);

                var mf = bordersGO.AddComponent<MeshFilter>();
                var mr = bordersGO.AddComponent<MeshRenderer>();
                mr.sharedMaterial = _borderMat;

                var mesh = HexBorderMeshBuilder.Build(
                    /* HexMask */ mask,
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
        }

        // 生成掩码：默认全 True；可剔除整列或随机挖洞（用于章节地图原型）
        HexMask BuildMask(GridRecipe rcp)
        {
            int w = rcp.width, h = rcp.height;
            var mask = HexMask.Filled(w, h);

            // 整列清空
            if (rcp.emptyColumns != null)
            {
                foreach (var col in rcp.emptyColumns)
                {
                    if (col < 0 || col >= w) continue;
                    mask.ClearColumn(col);
                }
            }

            // 随机挖洞（可选）
            if (rcp.enableRandomHoles && rcp.holeChance > 0f)
                mask.RandomHoles(rcp.holeChance, rcp.randomSeed);
            Version++;
            return mask;
        }


        // 运行期接口：外部脚本可切换边线模式
        public void SetBorderMode(BorderMode mode) { _runtimeBorderMode = mode; _dirty = true; }

        [ContextMenu("Rebuild Grid")]
        void RebuildFromMenu() { _dirty = true; }

        int ComputeRecipeHash()
        {
            if (recipe == null) return 0;
            unchecked
            {
                int h = 17;
                h = h * 31 + recipe.width;
                h = h * 31 + recipe.height;
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


    }
}
