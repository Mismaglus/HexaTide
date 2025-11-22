using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Core.Hex;
using Game.Grid;

namespace Game.Units
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UnitAttributes))]
    public class UnitMover : MonoBehaviour
    {
        // 只读属性，兼容性访问
        public int strideLeft => _attributes != null ? _attributes.Core.CurrentStride : 0;

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

        // ⭐ 新增：路径移动完成事件 (SelectionManager 可监听此事件来刷新范围)
        public event Action OnPathCompleted;

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
            if (_unit) { _unit.WarpTo(c); return; }
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

        // ⭐⭐⭐ 核心新增：沿路径移动 ⭐⭐⭐
        public void FollowPath(List<HexCoords> path, Action onComplete = null)
        {
            if (IsMoving || path == null || path.Count == 0) return;
            StartCoroutine(CoFollowPath(path, onComplete));
        }

        IEnumerator CoFollowPath(List<HexCoords> path, Action onComplete)
        {
            IsMoving = true;
            OnMoveStarted?.Invoke(_mCoords, path[0]);

            foreach (var nextStep in path)
            {
                // 1. 再次检查下一步是否合法 (防止动态障碍)
                if (!CanStepTo(nextStep))
                {
                    Debug.LogWarning("[UnitMover] Path interrupted (blocked or out of resources).");
                    break;
                }

                // 2. 扣费
                int cost = MovementCostProvider != null ? Mathf.Max(1, MovementCostProvider(nextStep)) : 1;
                ConsumeResources(cost);

                // 3. 执行单步动画
                yield return StartCoroutine(CoMoveLerpOnly(_mCoords, nextStep, secondsPerTile));

                // 4. 更新逻辑坐标
                if (_unit) _unit.WarpTo(nextStep);
                else _mCoords = nextStep;

                // 5. 触发单步完成事件 (用于刷新 SelectionManager 或触发陷阱)
                // 注意：这里 from 参数此时已经不准确了，但这通常用于 UI 刷新，影响不大
                OnMoveFinished?.Invoke(nextStep, nextStep);
            }

            IsMoving = false;
            OnPathCompleted?.Invoke();
            onComplete?.Invoke();
        }

        void ConsumeResources(int cost)
        {
            if (_attributes == null) return;

            if (_attributes.Core.CurrentStride >= cost)
            {
                _attributes.Core.CurrentStride -= cost;
            }
            else
            {
                int apNeeded = cost - _attributes.Core.CurrentStride;
                _attributes.Core.CurrentStride = 0;
                _attributes.Core.CurrentAP = Mathf.Max(0, _attributes.Core.CurrentAP - apNeeded);
            }
        }

        // 纯动画协程 (不处理逻辑)
        IEnumerator CoMoveLerpOnly(HexCoords from, HexCoords dst, float dur)
        {
            Vector3 a; TryGetTileTopWorld(from, out a);
            Vector3 b; TryGetTileTopWorld(dst, out b);

            Vector3 dir = b - a;
            if (dir.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(dir, Vector3.up);

            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(0.0001f, dur);
                transform.position = Vector3.Lerp(a, b, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }
            transform.position = b;
        }

        // 旧的单步方法 (保留兼容性)
        public bool TryStepTo(HexCoords dst, Action onDone = null)
        {
            if (!CanStepTo(dst)) return false;
            ConsumeResources(1);
            OnMoveStarted?.Invoke(_mCoords, dst);
            StartCoroutine(CoMoveOneStepWrapper(_mCoords, dst, secondsPerTile, onDone));
            return true;
        }

        IEnumerator CoMoveOneStepWrapper(HexCoords f, HexCoords d, float dur, Action done)
        {
            IsMoving = true;
            yield return CoMoveLerpOnly(f, d, dur);
            if (_unit) _unit.WarpTo(d); else _mCoords = d;
            IsMoving = false;
            OnMoveFinished?.Invoke(f, d);
            done?.Invoke();
        }

        public bool CanStepTo(HexCoords dst)
        {
            if (_grid == null) return false;
            if (_mCoords.DistanceTo(dst) != 1) return false;

            // 检查资源是否足够 (Stride + AP)
            int cost = MovementCostProvider != null ? Mathf.Max(1, MovementCostProvider(dst)) : 1;
            int totalRes = _attributes ? (_attributes.Core.CurrentStride + _attributes.Core.CurrentAP) : 0;

            return totalRes >= cost;
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
            if (_cachedGridVersion != _grid.Version) RebuildTileCache();
            if (!_tileCache.TryGetValue(coords, out var tr) || tr == null) return false;
            float top = (_grid.recipe != null ? _grid.recipe.thickness * 0.5f : 0f);
            float unitY = _unit != null ? _unit.unitYOffset : 0f;
            pos = tr.position + new Vector3(0f, top + unitY, 0f);
            return true;
        }
    }
}