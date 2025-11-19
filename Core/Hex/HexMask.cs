using System.Collections.Generic;

namespace Core.Hex
{
    /// <summary>
    /// 带宽高的存在性掩码（包装 bool[,]），提供列/行清空、并差、随机挖洞等操作
    /// </summary>
    public sealed class HexMask
    {
        readonly bool[,] _bits;
        public int Width { get; }
        public int Height { get; }

        public HexMask(int width, int height)
        {
            Width = width; Height = height;
            _bits = new bool[width, height];
        }

        public static HexMask Filled(int width, int height)
        {
            var m = new HexMask(width, height);
            m.FillAll();
            return m;
        }

        public bool this[int q, int r]
        {
            get => _bits[q, r];
            set => _bits[q, r] = value;
        }
        public bool this[HexCoords h]
        {
            get => _bits[h.q, h.r];
            set => _bits[h.q, h.r] = value;
        }

        public HexMask Clone()
        {
            var c = new HexMask(Width, Height);
            for (int q = 0; q < Width; q++)
                for (int r = 0; r < Height; r++)
                    c._bits[q, r] = _bits[q, r];
            return c;
        }

        public void ClearAll()
        {
            for (int q = 0; q < Width; q++)
                for (int r = 0; r < Height; r++)
                    _bits[q, r] = false;
        }
        public void FillAll()
        {
            for (int q = 0; q < Width; q++)
                for (int r = 0; r < Height; r++)
                    _bits[q, r] = true;
        }

        public bool InBounds(int q, int r) => q >= 0 && q < Width && r >= 0 && r < Height;
        public int CountTrue()
        {
            int c = 0;
            for (int q = 0; q < Width; q++)
                for (int r = 0; r < Height; r++)
                    if (_bits[q, r]) c++;
            return c;
        }

        public IEnumerable<HexCoords> All()
        {
            for (int q = 0; q < Width; q++)
                for (int r = 0; r < Height; r++)
                    if (_bits[q, r]) yield return new HexCoords(q, r);
        }

        public void ClearColumn(int col)
        {
            if (col < 0 || col >= Width) return;
            for (int r = 0; r < Height; r++) _bits[col, r] = false;
        }
        public void ClearRow(int row)
        {
            if (row < 0 || row >= Height) return;
            for (int q = 0; q < Width; q++) _bits[q, row] = false;
        }
        public void SetRect(int qMin, int rMin, int qMaxInclusive, int rMaxInclusive, bool value)
        {
            for (int q = qMin; q <= qMaxInclusive; q++)
                for (int r = rMin; r <= rMaxInclusive; r++)
                    if (InBounds(q, r)) _bits[q, r] = value;
        }

        public void UnionWith(HexMask other)
        {
            for (int q = 0; q < Width; q++)
                for (int r = 0; r < Height; r++)
                    _bits[q, r] |= other._bits[q, r];
        }
        public void IntersectWith(HexMask other)
        {
            for (int q = 0; q < Width; q++)
                for (int r = 0; r < Height; r++)
                    _bits[q, r] &= other._bits[q, r];
        }
        public void ExceptWith(HexMask other)
        {
            for (int q = 0; q < Width; q++)
                for (int r = 0; r < Height; r++)
                    if (other._bits[q, r]) _bits[q, r] = false;
        }

        /// <summary>原型用的“随机挖洞”</summary>
        public void RandomHoles(float chance, int seed)
        {
            if (chance <= 0f) return;
            var rng = new System.Random(seed);
            for (int q = 0; q < Width; q++)
                for (int r = 0; r < Height; r++)
                    if (_bits[q, r] && rng.NextDouble() < chance) _bits[q, r] = false;
        }

        /// <summary>如需调用旧 API，可导出为 bool[,]</summary>
        public bool[,] ToArray()
        {
            var arr = new bool[Width, Height];
            for (int q = 0; q < Width; q++)
                for (int r = 0; r < Height; r++)
                    arr[q, r] = _bits[q, r];
            return arr;
        }
    }
}
