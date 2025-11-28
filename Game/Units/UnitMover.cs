using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Core.Hex;
using Game.Grid;
using Game.Battle;

namespace Game.Units
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UnitAttributes))]
    public class UnitMover : MonoBehaviour
    {
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

        // ⭐ 关键修改：参数改为 (From, To)，以便计算 ZOC
        public Func<HexCoords, HexCoords, int> MovementCostProvider;

        public event Action<HexCoords, HexCoords> OnMoveStarted;
        public event Action<HexCoords, HexCoords> OnMoveFinished;
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
                if (!CanStepTo(nextStep))
                {
                    Debug.LogWarning("[UnitMover] Path interrupted (Not enough resources).");
                    break;
                }

                // ⭐ 关键修改：传入 _mCoords (当前位置) 和 nextStep (目标位置)
                int cost = 1;
                if (MovementCostProvider != null)
                {
                    cost = Mathf.Max(1, MovementCostProvider(_mCoords, nextStep));
                }

                ConsumeResources(cost);

                HexCoords currentPos = _mCoords;

                yield return StartCoroutine(CoMoveLerpOnly(_mCoords, nextStep, secondsPerTile));

                if (_unit) _unit.WarpTo(nextStep);
                else _mCoords = nextStep;

                OnMoveFinished?.Invoke(currentPos, nextStep);
            }

            IsMoving = false;
            OnPathCompleted?.Invoke();

            if (BattleIntentSystem.Instance != null)
                BattleIntentSystem.Instance.UpdateIntents();

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

            if (_unit != null)
            {
                var bu = _unit.GetComponent<BattleUnit>();
                if (bu) bu.NotifyStateChange();
            }
        }

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

        public bool TryStepTo(HexCoords dst, Action onDone = null)
        {
            if (!CanStepTo(dst)) return false;

            // 单步消耗也需要计算
            int cost = 1;
            if (MovementCostProvider != null)
                cost = Mathf.Max(1, MovementCostProvider(_mCoords, dst));

            ConsumeResources(cost);
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

            // ⭐ 预览时也需要计算真实消耗
            int cost = 1;
            if (MovementCostProvider != null)
                cost = Mathf.Max(1, MovementCostProvider(_mCoords, dst));

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