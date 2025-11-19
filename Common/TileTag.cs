using UnityEngine;
using Core.Hex;

namespace Game.Common
{
    /// <summary>挂到每个 Hex_r?_c? 子物体上，提供 (q,r) 坐标。</summary>
    [DisallowMultipleComponent]
    public class TileTag : MonoBehaviour
    {
        [SerializeField] int _q;
        [SerializeField] int _r;
        public HexCoords Coords => new HexCoords(_q, _r);
        public void Set(int q, int r) { _q = q; _r = r; }
    }
}
