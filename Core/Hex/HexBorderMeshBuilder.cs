using System.Collections.Generic;
using UnityEngine;

namespace Core.Hex
{
    public static class HexBorderMeshBuilder
    {
        /// <summary>
        /// 生成“唯一边线网格”。支持：
        /// - BorderMode.OuterOnly：仅外轮廓（含洞周边）
        /// - BorderMode.AllUnique：全部边线但公共边去重
        /// 掩码 mask[q,r]=true 表示该格存在；false 表示空（路障/洞）。
        /// </summary>
        public static Mesh Build(
            bool[,] mask, int width, int height,
            float outerRadius, float yOffset, float tileThickness,
            float borderWidth, bool useOddROffset,
            BorderMode mode
        )
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();

            float innerScale = Mathf.Max(0.0001f, (outerRadius - borderWidth) / outerRadius);
            float y = yOffset + tileThickness * 0.5f;

            // 先计算用于居中的边界（只看存在的格子）
            Bounds2D bounds = ComputeBounds(mask, width, height, outerRadius, useOddROffset);
            Vector3 centerOffset = new Vector3(bounds.CenterX, 0f, bounds.CenterZ);

            for (int r = 0; r < height; r++)
            {
                bool odd = (r & 1) == 1;
                var neighborOffsets = odd ? HexMetrics.NEI_ODDR : HexMetrics.NEI_EVENR;

                for (int q = 0; q < width; q++)
                {
                    if (!mask[q, r]) continue;
                    Vector3 center = HexMetrics.GridToWorld(q, r, outerRadius, useOddROffset);
                    center -= centerOffset;
                    center.y = y;

                    // 外/内 六角
                    Vector3[] outerP = new Vector3[6];
                    Vector3[] innerP = new Vector3[6];
                    for (int i = 0; i < 6; i++)
                    {
                        var d = HexMetrics.CORNER_DIRS[i];
                        outerP[i] = center + new Vector3(d.x * outerRadius, 0f, d.y * outerRadius);
                        innerP[i] = center + new Vector3(d.x * outerRadius * innerScale, 0f, d.y * outerRadius * innerScale);
                    }

                    // 决定该格需要绘制哪些边
                    for (int i = 0; i < 6; i++)
                    {
                        int nq = q + neighborOffsets[i].x, nr = r + neighborOffsets[i].y;
                        bool neighborInBounds = (nq >= 0 && nq < width && nr >= 0 && nr < height);
                        bool neighborExists = neighborInBounds && mask[nq, nr];
                        bool draw = false;
                        if (mode == BorderMode.OuterOnly)
                        {
                            // 外轮廓：当且仅当没有邻居（越界或空）
                            draw = !neighborExists;
                        }
                        else if (mode == BorderMode.AllUnique)
                        {
                            // 全网（去重）：无邻居必画；有邻居只让“较小索引单元”画
                            draw = !neighborExists || (neighborExists && (r < nr || (r == nr && q < nq)));
                        }
                        else // None
                        {
                            draw = false;
                        }

                        if (!draw) continue;

                        int ni = (i + 1) % 6;

                        int baseIndex = verts.Count;
                        verts.Add(outerP[i]);   // 0
                        verts.Add(outerP[ni]);  // 1
                        verts.Add(innerP[i]);   // 2
                        verts.Add(innerP[ni]);  // 3

                        // 双面透明材质下，三角顺序只需一致
                        tris.Add(baseIndex + 0);
                        tris.Add(baseIndex + 1);
                        tris.Add(baseIndex + 2);
                        tris.Add(baseIndex + 2);
                        tris.Add(baseIndex + 1);
                        tris.Add(baseIndex + 3);
                    }
                }
            }

            var mesh = new Mesh { name = $"HexBorders_{mode}" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();   // ⭐️ 新增，确保包围盒覆盖最外缘线段
            return mesh;
        }
        public static Bounds ComputeWorldBounds(HexMask mask, float outerRadius, bool useOddROffset)
        {
            bool any = false;
            float minX = float.PositiveInfinity, maxX = float.NegativeInfinity;
            float minZ = float.PositiveInfinity, maxZ = float.NegativeInfinity;

            for (int r = 0; r < mask.Height; r++)
                for (int q = 0; q < mask.Width; q++)
                {
                    if (!mask[q, r]) continue;
                    any = true;
                    var p = HexMetrics.GridToWorld(q, r, outerRadius, useOddROffset);
                    if (p.x < minX) minX = p.x;
                    if (p.x > maxX) maxX = p.x;
                    if (p.z < minZ) minZ = p.z;
                    if (p.z > maxZ) maxZ = p.z;
                }

            if (!any) return new Bounds(Vector3.zero, Vector3.zero);

            Vector3 center = new((minX + maxX) * 0.5f, 0f, (minZ + maxZ) * 0.5f);
            Vector3 size = new(maxX - minX, 0f, maxZ - minZ);
            return new Bounds(center, size);
        }


        /// <summary>只统计存在的格子，用于计算置中偏移。</summary>
        public static Bounds2D ComputeBounds(bool[,] mask, int width, int height, float outerRadius, bool useOddROffset)
        {
            bool any = false;
            float minX = float.PositiveInfinity, maxX = float.NegativeInfinity;
            float minZ = float.PositiveInfinity, maxZ = float.NegativeInfinity;

            for (int r = 0; r < height; r++)
                for (int q = 0; q < width; q++)
                {
                    if (!mask[q, r]) continue;
                    any = true;
                    var p = HexMetrics.GridToWorld(q, r, outerRadius, useOddROffset);
                    if (p.x < minX) minX = p.x;
                    if (p.x > maxX) maxX = p.x;
                    if (p.z < minZ) minZ = p.z;
                    if (p.z > maxZ) maxZ = p.z;
                }

            if (!any) return new Bounds2D(0, 0, 0, 0);
            return new Bounds2D(minX, maxX, minZ, maxZ);
        }

        public static Mesh Build(
            HexMask mask,
            float outerRadius, float yOffset, float tileThickness,
            float borderWidth, bool useOddROffset,
            BorderMode mode
        )
        {
            // 复用原实现（避免重复逻辑）
            return Build(mask.ToArray(), mask.Width, mask.Height,
                         outerRadius, yOffset, tileThickness, borderWidth, useOddROffset, mode);
        }

        public readonly struct Bounds2D
        {
            public readonly float MinX, MaxX, MinZ, MaxZ;
            public float CenterX => (MinX + MaxX) * 0.5f;
            public float CenterZ => (MinZ + MaxZ) * 0.5f;
            public Bounds2D(float minX, float maxX, float minZ, float maxZ)
            { MinX = minX; MaxX = maxX; MinZ = minZ; MaxZ = maxZ; }
        }
    }
}
