using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Core.Hex;
using Game.Grid;

namespace Game.Units
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UnitAttributes))] // ⭐ 现在强依赖 Attributes
    public class UnitMover : MonoBehaviour
    {
        // ❌ strideLeft 和 strideMax 都移除了，完全代理给 Attributes

        [Header("Motion")]
        public float secondsPerTile = 0.18f;

        [SerializeField] private Unit _unit;
        [SerializeField] private UnitAttributes _attributes;

        private HexCoords _fallbackCoords;

        public HexCoords _mCoords
        {
            get => _unit ? _unit.Coords : _fallbackCoords;
            private set
            {
                if (_unit) _unit.WarpTo(value);
                else _fallbackCoords = value;
            }
        }

        public bool IsMoving { get; private set; }

        public Func<HexCoords, int> MovementCostProvider;

        public event Action<HexCoords, HexCoords> OnMoveStarted;
        public event Action<HexCoords, HexCoords> OnMoveFinished;

        [SerializeField] private MonoBehaviour _gridProviderObject;
        private IHexGridProvider _grid;
        readonly Dictionary<HexCoords, Transform> _tileCache = new();
        uint _cachedGridVersion;

        void Reset()
        {
            if (!_unit) _unit = GetComponent<Unit>();
            if (_gridProviderObject == null && _unit && _unit.gridComponent)
                _gridProviderObject = _unit.gridComponent;
        }

        void Awake()
        {
            if (!_unit) _unit = GetComponent<Unit>();
            if (!_attributes) _attributes = GetComponent<UnitAttributes>();

            if (_gridProviderObject == null)
            {
                var all = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                foreach (var mb in all)
                {
                    if (mb is IHexGridProvider) { _gridProviderObject = mb; break; }
                }
            }

            _grid = _gridProviderObject as IHexGridProvider;
            RebuildTileCache();
        }

        public void WarpTo(HexCoords c)
        {
            if (_unit)
            {
                _unit.WarpTo(c);
                return;
            }

            _mCoords = c;
            if (!TryGetTileTopWorld(c, out var top))
            {
                if (_grid != null && _grid.recipe != null)
                    top = HexMetrics.GridToWorld(c.q, c.r, _grid.recipe.outerRadius, _grid.recipe.useOddROffset);
                else
                    top = transform.position;
            }

            transform.position = top;
        }

        public bool TryStepTo(HexCoords dst, Action onDone = null)
        {
            if (!CanStepTo(dst))
            {
                return false;
            }

            // 消耗 Attributes 里的步数
            int cost = MovementCostProvider != null ? Mathf.Max(1, MovementCostProvider(dst)) : 1;
            if (_attributes) _attributes.Core.CurrentStride -= cost;

            OnMoveStarted?.Invoke(_mCoords, dst);
            StartCoroutine(CoMoveOneStep(_mCoords, dst, secondsPerTile, onDone));
            return true;
        }

        public bool CanStepTo(HexCoords dst)
        {
            if (IsMoving) return false;
            if (_grid == null)
            {
                Debug.LogWarning("[UnitMover] No IHexGridProvider. TryStepTo denied.");
                return false;
            }

            if (_mCoords.DistanceTo(dst) != 1) return false;

            int cost = MovementCostProvider != null ? Mathf.Max(1, MovementCostProvider(dst)) : 1;

            // 检查 Attributes 里的步数
            int current = _attributes ? _attributes.Core.CurrentStride : 0;
            if (current < cost) return false;

            return true;
        }

        IEnumerator CoMoveOneStep(HexCoords from, HexCoords dst, float dur, Action onDone)
        {
            IsMoving = true;

            Vector3 a;
            if (!TryGetTileTopWorld(from, out a))
            {
                if (_grid != null && _grid.recipe != null)
                    a = HexMetrics.GridToWorld(from.q, from.r, _grid.recipe.outerRadius, _grid.recipe.useOddROffset);
                else
                    a = transform.position;
            }

            Vector3 b;
            if (!TryGetTileTopWorld(dst, out b))
            {
                if (_grid != null && _grid.recipe != null)
                    b = HexMetrics.GridToWorld(dst.q, dst.r, _grid.recipe.outerRadius, _grid.recipe.useOddROffset);
                else
                    b = transform.position;
            }

            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(0.0001f, dur);
                transform.position = Vector3.Lerp(a, b, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }

            if (_unit)
            {
                _unit.WarpTo(dst);
            }
            else
            {
                _mCoords = dst;
                transform.position = b;
            }

            IsMoving = false;
            OnMoveFinished?.Invoke(from, dst);
            onDone?.Invoke();
        }

        void RebuildTileCache()
        {
            _tileCache.Clear();
            if (_grid == null) return;

            foreach (var tile in _grid.EnumerateTiles())
            {
                if (tile != null && !_tileCache.ContainsKey(tile.Coords))
                    _tileCache[tile.Coords] = tile.transform;
            }

            _cachedGridVersion = _grid.Version;
        }

        bool TryGetTileTopWorld(HexCoords coords, out Vector3 pos)
        {
            pos = default;
            if (_grid == null) return false;

            if (_cachedGridVersion != _grid.Version)
                RebuildTileCache();

            if (!_tileCache.TryGetValue(coords, out var tr) || tr == null)
                return false;

            float top = (_grid.recipe != null ? _grid.recipe.thickness * 0.5f : 0f);
            float unitY = _unit != null ? _unit.unitYOffset : 0f;
            pos = tr.position + new Vector3(0f, top + unitY, 0f);
            return true;
        }
    }
}