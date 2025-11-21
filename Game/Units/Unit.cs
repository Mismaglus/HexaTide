using Core.Hex;
using Game.Core; // 引用 FactionMembership
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.Battle;
using Game.Battle.Units;
using Game.Grid;

namespace Game.Units
{
    [DisallowMultipleComponent]
    public class Unit : MonoBehaviour
    {
        [Header("Identity")]
        public string unitName = "Unknown";
        public Sprite portrait;

        [Header("Faction")]
        // ? 唯一阵营数据源
        [SerializeField] private FactionMembership _faction;
        public FactionMembership Faction => _faction ? _faction : (_faction = GetComponent<FactionMembership>());

        [Header("Refs")]
        public MonoBehaviour gridComponent;
        IHexGridProvider grid;

        [Header("Motion")]
        public float secondsPerTile = 0.2f;
        public float unitYOffset = 0.02f;
        public bool faceMovement = false;
        public bool autoInitializeIfMissing = true;

        [Header("Animation")]
        [SerializeField] private RuntimeAnimatorController animatorController;

        public HexCoords Coords { get; private set; }
        public bool IsMoving => _moveRoutine != null;
        public System.Action<Unit, HexCoords, HexCoords> OnMoveFinished;

        Dictionary<HexCoords, Transform> _tileMap = new();
        uint _lastGridVersion;
        Coroutine _moveRoutine;
        bool _hasValidCoords;
        UnitVisual2D _visual2D;

        void Awake()
        {
            grid = gridComponent as IHexGridProvider;
            _visual2D = GetComponentInChildren<UnitVisual2D>(true);
            EnsureChildAnimatorController();

            // 确保 Faction 组件存在
            if (!_faction) _faction = GetComponent<FactionMembership>();
        }

        // ... (Start, Update, Move 逻辑保持不变，因为太长了我不重复贴，请保留原有的移动逻辑代码) ...
        // 如果你需要我完整贴出 Unit.cs 的移动逻辑，请告诉我。
        // 为了方便直接替换，这里只包含了开头结构修改。
        // ? 考虑到你要直接替换，我还是贴完整版吧，以免出错。

        void EnsureChildAnimatorController()
        {
            if (animatorController == null) return;
            for (int i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                for (int j = 0; j < child.childCount; j++)
                {
                    var grand = child.GetChild(j);
                    var anim = grand.GetComponent<Animator>();
                    if (anim && !anim.runtimeAnimatorController) anim.runtimeAnimatorController = animatorController;
                }
            }
        }

        void Start()
        {
            if (!autoInitializeIfMissing) return;
            if (grid == null) grid = FindFirstGridProviderInScene();
            if (grid == null) return;
            if (_tileMap.Count == 0) RebuildTileMap();
            if (TryPickTileUnderSelf(out var c)) WarpTo(c);
            else _hasValidCoords = _tileMap.ContainsKey(Coords);
            var sel = FindFirstObjectByType<SelectionManager>(FindObjectsInactive.Exclude);
            sel?.RegisterUnit(this);
        }

        public bool IsPlayerControlled
        {
            get
            {
                if (Faction != null) return Faction.IsPlayerControlled;
                return true;
            }
        }

        IHexGridProvider FindFirstGridProviderInScene()
        {
            var monos = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var m in monos) if (m is IHexGridProvider p) return p;
            return null;
        }

        static readonly RaycastHit[] _hitsTmp = new RaycastHit[4];
        bool TryPickTileUnderSelf(out HexCoords coords)
        {
            coords = default;
            var ray = new Ray(transform.position + Vector3.up * 2f, Vector3.down);
            int n = Physics.RaycastNonAlloc(ray, _hitsTmp, 6f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var h = _hitsTmp[i];
                if (!h.collider) continue;
                var tag = h.collider.GetComponent<Game.Common.TileTag>() ?? h.collider.GetComponentInParent<Game.Common.TileTag>();
                if (tag) { coords = tag.Coords; return true; }
            }
            return false;
        }

        void LateUpdate()
        {
            if (grid != null && _lastGridVersion != grid.Version)
            {
                _lastGridVersion = grid.Version;
                RebuildTileMap();
                if (_hasValidCoords && TryGetTileTopWorld(Coords, out var top)) transform.position = top;
            }
        }

        public void Initialize(IHexGridProvider g, HexCoords start)
        {
            grid = g; gridComponent = g as MonoBehaviour;
            _lastGridVersion = 0; RebuildTileMap(); WarpTo(start);
        }

        public void WarpTo(HexCoords c)
        {
            Coords = c;
            if (TryGetTileTopWorld(c, out var top)) { transform.position = top; _hasValidCoords = true; }
        }

        public bool TryMoveTo(HexCoords target)
        {
            if (IsMoving || grid == null) return false;
            if (Coords.DistanceTo(target) != 1) return false;
            if (!TryGetTileTopWorld(target, out var dst)) return false;
            var src = transform.position;
            _moveRoutine = StartCoroutine(MoveRoutine(src, dst, target));
            return true;
        }

        IEnumerator MoveRoutine(Vector3 src, Vector3 dst, HexCoords target)
        {
            float t = 0f; float dur = Mathf.Max(0.01f, secondsPerTile);
            bool rotate = faceMovement && _visual2D == null;
            if (rotate)
            {
                Vector3 dir = dst - src; dir.y = 0;
                if (dir.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            }
            while (t < 1f)
            {
                t += Time.deltaTime / dur;
                transform.position = Vector3.Lerp(src, dst, Mathf.Clamp01(t));
                yield return null;
            }
            var from = Coords; Coords = target; _moveRoutine = null;
            OnMoveFinished?.Invoke(this, from, target);
        }

        void RebuildTileMap()
        {
            _tileMap.Clear(); if (grid == null) return;
            foreach (var t in grid.EnumerateTiles()) if (t) _tileMap[t.Coords] = t.transform;
        }

        bool TryGetTileTopWorld(HexCoords c, out Vector3 pos)
        {
            pos = default;
            if (!_tileMap.TryGetValue(c, out var tr) || tr == null) return false;
            float top = (grid != null && grid.recipe != null) ? grid.recipe.thickness * 0.5f : 0f;
            pos = tr.position + new Vector3(0, top + unitYOffset, 0);
            return true;
        }
    }
}