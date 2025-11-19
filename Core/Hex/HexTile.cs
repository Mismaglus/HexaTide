using UnityEngine;

namespace Core.Hex
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class HexTile : MonoBehaviour
    {
        [Min(0.1f)] public float outerRadius = 1f;
        [Range(0f, 0.5f)] public float thickness = 0f;

        Mesh _mesh;

        // 运行时（Play）创建时仍然自动建一次
        void Awake()
        {
            if (Application.isPlaying) BuildImmediate();
        }

        // ❌ 不要在 OnValidate 里改 Mesh（会触发 Unity 的 SendMessage 警告）
        // #if UNITY_EDITOR
        // void OnValidate() { if (isActiveAndEnabled) BuildImmediate(); }
        // #endif

        // ✅ 由外部在安全时机调用
        public void BuildImmediate()
        {
            if (_mesh == null)
            {
                _mesh = new Mesh { name = "HexTileMesh" };
                GetComponent<MeshFilter>().sharedMesh = _mesh;
            }

            // ======= 以下是你原来的几何生成逻辑（保持不变）=======
            // 尖顶式六点
            Vector3[] top = new Vector3[6];
            for (int i = 0; i < 6; i++)
            {
                float ang = Mathf.Deg2Rad * (60 * i - 30);
                top[i] = new Vector3(Mathf.Cos(ang) * outerRadius,
                                     thickness * 0.5f,
                                     Mathf.Sin(ang) * outerRadius);
            }

            if (thickness <= 0f)
            {
                var verts = new Vector3[7];
                var norms = new Vector3[7];
                var uvs = new Vector2[7];

                verts[0] = Vector3.zero; norms[0] = Vector3.up; uvs[0] = new Vector2(0.5f, 0.5f);
                for (int i = 0; i < 6; i++)
                {
                    verts[i + 1] = top[i];
                    norms[i + 1] = Vector3.up;
                    uvs[i + 1] = new Vector2(0.5f + top[i].x / (2f * outerRadius),
                                               0.5f + top[i].z / (2f * outerRadius));
                }

                int[] tris = new int[18];
                for (int i = 0; i < 6; i++)
                {
                    int a = 0, b = i + 1, c = (i == 5) ? 1 : i + 2;
                    int t = i * 3;
                    tris[t] = a; tris[t + 1] = c; tris[t + 2] = b; // 注意：a,c,b 以避免背面剔除
                }

                _mesh.Clear();
                _mesh.vertices = verts;
                _mesh.triangles = tris;
                _mesh.normals = norms;
                _mesh.uv = uvs;
            }
            else
            {
                Vector3[] bot = new Vector3[6];
                for (int i = 0; i < 6; i++)
                    bot[i] = new Vector3(top[i].x, -thickness * 0.5f, top[i].z);

                var verts = new System.Collections.Generic.List<Vector3>();
                var norms = new System.Collections.Generic.List<Vector3>();
                var uvs = new System.Collections.Generic.List<Vector2>();
                var tris = new System.Collections.Generic.List<int>();

                // 顶面（a,c,b 顺序）
                int ts = verts.Count;
                verts.Add(new Vector3(0, thickness * 0.5f, 0)); norms.Add(Vector3.up); uvs.Add(new Vector2(0.5f, 0.5f));
                for (int i = 0; i < 6; i++) { verts.Add(top[i]); norms.Add(Vector3.up); uvs.Add(Vector2.one * 0.5f); }
                for (int i = 0; i < 6; i++)
                {
                    int a = ts, b = ts + i + 1, c = (i == 5) ? ts + 1 : ts + i + 2;
                    tris.AddRange(new[] { a, c, b });
                }

                // 底面
                int bs = verts.Count;
                verts.Add(new Vector3(0, -thickness * 0.5f, 0)); norms.Add(Vector3.down); uvs.Add(new Vector2(0.5f, 0.5f));
                for (int i = 0; i < 6; i++) { verts.Add(bot[i]); norms.Add(Vector3.down); uvs.Add(Vector2.one * 0.5f); }
                for (int i = 0; i < 6; i++)
                {
                    int a = bs, b = (i == 5) ? bs + 1 : bs + i + 2, c = bs + i + 1;
                    tris.AddRange(new[] { a, b, c });
                }

                // 侧面
                for (int i = 0; i < 6; i++)
                {
                    int ni = (i + 1) % 6;
                    Vector3 v0 = top[i], v1 = top[ni], v2 = bot[i], v3 = bot[ni];

                    int s = verts.Count;
                    verts.AddRange(new[] { v0, v1, v2, v3 });
                    Vector3 n = Vector3.Cross(v1 - v0, v2 - v0).normalized;
                    norms.AddRange(new[] { n, n, n, n });
                    uvs.AddRange(new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one });
                    tris.AddRange(new[] { s + 0, s + 1, s + 2, s + 2, s + 1, s + 3 });
                }

                _mesh.Clear();
                _mesh.SetVertices(verts);
                _mesh.SetTriangles(tris, 0);
                _mesh.SetNormals(norms);
                _mesh.SetUVs(0, uvs);

                var col = GetComponent<MeshCollider>();
                if (col == null) col = gameObject.AddComponent<MeshCollider>();
                col.sharedMesh = _mesh;
            }
            EnsureCollider();
        }

        MeshCollider _col; // 可选缓存
        void EnsureCollider()
        {
            var col = _col ? _col : (_col = GetComponent<MeshCollider>());
            if (col == null) col = _col = gameObject.AddComponent<MeshCollider>();
            col.sharedMesh = _mesh;   // 不要设 Convex；默认即可
            col.convex = false;
            col.isTrigger = false;
        }

    }
}
