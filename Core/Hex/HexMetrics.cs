using UnityEngine;

namespace Core.Hex
{
    public enum HexOrientation { PointyTop, FlatTop }        // 先支持 PointyTop
    public enum BorderMode { None, OuterOnly, AllUnique }  // 外轮廓 / 全部(去重)

    public static class HexMetrics
    {
        public const float SQRT3 = 1.7320508075688772f;

        // 角 i 的方向（与我们造六边形的顺序一致：角度 = 60*i - 30 度）
        public static readonly Vector2[] CORNER_DIRS = new Vector2[6] {
            new(Mathf.Cos(-30f * Mathf.Deg2Rad), Mathf.Sin(-30f * Mathf.Deg2Rad)), // 0
            new(Mathf.Cos( 30f * Mathf.Deg2Rad), Mathf.Sin( 30f * Mathf.Deg2Rad)), // 1
            new(Mathf.Cos( 90f * Mathf.Deg2Rad), Mathf.Sin( 90f * Mathf.Deg2Rad)), // 2
            new(Mathf.Cos(150f * Mathf.Deg2Rad), Mathf.Sin(150f * Mathf.Deg2Rad)), // 3
            new(Mathf.Cos(210f * Mathf.Deg2Rad), Mathf.Sin(210f * Mathf.Deg2Rad)), // 4
            new(Mathf.Cos(270f * Mathf.Deg2Rad), Mathf.Sin(270f * Mathf.Deg2Rad)), // 5
        };

        // odd-r（尖顶）邻居位移：边序和上面角序对应
        // 边序：0 E, 1 NE, 2 NW, 3 W, 4 SW, 5 SE
        public static readonly Vector2Int[] NEI_EVENR = {
            new(+1, 0), new( 0,+1), new(-1,+1),
            new(-1, 0), new(-1,-1), new( 0,-1)
        };
        public static readonly Vector2Int[] NEI_ODDR = {
            new(+1, 0), new(+1,+1), new( 0,+1),
            new(-1, 0), new( 0,-1), new(+1,-1)
        };

        // odd-r 网格坐标 -> 世界坐标（XZ）
        public static Vector3 GridToWorld(int q, int r, float outerRadius, bool useOddROffset)
        {
            float inner = outerRadius * (0.5f * SQRT3);
            float x = q * (2f * inner);
            if (useOddROffset && (r & 1) == 1) x += inner; // 奇数行右移半格
            float z = r * (1.5f * outerRadius);
            return new Vector3(x, 0f, z);
        }

        // odd-r（尖顶）世界坐标(XZ) -> 网格(q,r)
        // 约定：r 自南向北递增，useOddROffset=true（当前项目就是这种）
        // 实现：world -> axial 浮点 -> cube round -> odd-r
        public static (int q, int r) WorldToGrid(Vector3 worldXZ, float outerRadius, bool useOddROffset)
        {
            // 1) world -> axial(float)
            float qf = (SQRT3 / 3f * worldXZ.x - 1f / 3f * worldXZ.z) / outerRadius;
            float rf = (2f / 3f * worldXZ.z) / outerRadius;

            // 2) axial -> cube(float)
            float xf = qf;
            float zf = rf;
            float yf = -xf - zf;

            // 3) cube round
            int rx = Mathf.RoundToInt(xf);
            int ry = Mathf.RoundToInt(yf);
            int rz = Mathf.RoundToInt(zf);

            float dx = Mathf.Abs(rx - xf);
            float dy = Mathf.Abs(ry - yf);
            float dz = Mathf.Abs(rz - zf);

            if (dx > dy && dx > dz) rx = -ry - rz;
            else if (dy > dz) ry = -rx - rz;
            else rz = -rx - ry;

            // 4) cube -> odd-r（r 北增）
            int r = rz;
            int q = rx + ((r - (r & 1)) >> 1);
            return (q, r);
        }

    }
}
