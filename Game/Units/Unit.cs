using Core.Hex;                 // 你原有的 HexCoords/DistanceTo
using Game.Common;             // 你原来的 SelectionManager 所在命名空间（如不同请改）
using Game.Grid;               // IHexGridProvider, GridRecipe
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.Battle;             // SelectionManager
using Game.Battle.Units;       // UnitVisual2D
using Game.Core;

namespace Game.Units
{
    [DisallowMultipleComponent]
    public class Unit : MonoBehaviour
    {
        [Header("Refs")]
        public MonoBehaviour gridComponent;   // Inspector 拖任何实现 IHexGridProvider 的组件
        IHexGridProvider grid;
        // 在 Unit.cs 里的开头部分添加：
        [Header("Identity")]
        public string unitName = "Unknown Unit";
        public Sprite portrait; // 用于 UI 显示的头像
        [Header("Motion")]
        [Tooltip("移动一格所需秒数")]
        public float secondsPerTile = 0.2f;
        [Tooltip("单位在顶面之上额外抬高的高度")]
        public float unitYOffset = 0.02f;
        [Tooltip("移动时朝向目标")]
        public bool faceMovement = false;
        [Tooltip("若未显式初始化，则在 Start 自动寻找 Grid 并注册到 SelectionManager。")]
        public bool autoInitializeIfMissing = true;

        [Header("Animation")]
        [SerializeField] private RuntimeAnimatorController animatorController;

        public HexCoords Coords { get; private set; }
        public bool IsMoving => _moveRoutine != null;
        public FactionMembership _faction;
        private BattleUnit _bu;
        // 移动完成事件：from -> to
        public System.Action<Unit, HexCoords, HexCoords> OnMoveFinished;

        // —— 缓存 —— //
        readonly Dictionary<HexCoords, Transform> _tileMap = new();
        uint _lastGridVersion;
        Coroutine _moveRoutine;
        bool _hasValidCoords;
        UnitVisual2D _visual2D;

        void Awake()
        {
            grid = gridComponent as IHexGridProvider;
            _visual2D = GetComponentInChildren<UnitVisual2D>(true);
            EnsureChildAnimatorController();
        }

        void EnsureChildAnimatorController()
        {
            if (animatorController == null) return;

            for (int childIndex = 0; childIndex < transform.childCount; childIndex++)
            {
                var child = transform.GetChild(childIndex);
                if (child == null) continue;

                for (int grandIndex = 0; grandIndex < child.childCount; grandIndex++)
                {
                    var grandChild = child.GetChild(grandIndex);
                    if (grandChild == null) continue;

                    var childAnimator = grandChild.GetComponent<Animator>();
                    if (childAnimator == null) continue;

                    if (childAnimator.runtimeAnimatorController == null)
                    {
                        childAnimator.runtimeAnimatorController = animatorController;
                    }
                }
            }
        }

        void Start()
        {
            if (!autoInitializeIfMissing) return;

            // 若未初始化（grid 为空），尝试自动寻找任何实现 IHexGridProvider 的对象
            if (grid == null)
                grid = FindFirstGridProviderInScene();

            if (grid == null) return; // 场景里没有网格就不处理

            // 补齐 tile 映射
            if (_tileMap.Count == 0) RebuildTileMap();

            // 用脚下射线拿真实格；成功就 WarpTo
            if (TryPickTileUnderSelf(out var c))
            {
                WarpTo(c); // 设置 Coords + _hasValidCoords = true
            }
            else
            {
                // 没打到瓦片，仅根据已有 Coords 判断是否有效，避免被拉回 (0,0)
                _hasValidCoords = _tileMap.ContainsKey(Coords);
            }

            // 注册到 SelectionManager（若未注册）
            var sel = FindFirstObjectByType<SelectionManager>(FindObjectsInactive.Exclude);
            if (sel != null)
                sel.RegisterUnit(this);
        }

        public bool IsPlayerControlled
        {
            get
            {
                if (_faction == null) TryGetComponent(out _faction);
                if (_faction != null) return _faction.IsPlayerControlled;
                return true; // 无组件时默认可控
            }
            set
            {
                if (_faction == null) TryGetComponent(out _faction);
                if (_faction != null) { _faction.playerControlled = value; return; }
            }
        }

        IHexGridProvider FindFirstGridProviderInScene()
        {
            var monos = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < monos.Length; i++)
            {
                if (monos[i] is IHexGridProvider p) return p;
            }
            return null;
        }

        static readonly RaycastHit[] _hitsTmp = new RaycastHit[4];

        bool TryPickTileUnderSelf(out HexCoords coords)
        {
            coords = default;

            // 从头顶稍微高一点往下打一根射线，命中任意带 TileTag 的瓦片即可
            var ray = new Ray(transform.position + Vector3.up * 2f, Vector3.down);
            int n = Physics.RaycastNonAlloc(ray, _hitsTmp, 6f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var h = _hitsTmp[i];
                if (!h.collider) continue;
                var tag = h.collider.GetComponent<TileTag>() ?? h.collider.GetComponentInParent<TileTag>();
                if (tag != null)
                {
                    coords = tag.Coords;
                    return true;
                }
            }
            return false;
        }

        void LateUpdate()
        {
            if (grid != null && _lastGridVersion != grid.Version)
            {
                _lastGridVersion = grid.Version;
                RebuildTileMap();

                // 网格变了：若当前位置仍有效，则对齐到顶面位置
                if (_hasValidCoords && TryGetTileTopWorld(Coords, out var top))
                    transform.position = top;
            }
        }

        // 外部初始化（由 Spawner/Controller 调用）
        public void Initialize(IHexGridProvider g, HexCoords start)
        {
            grid = g;
            gridComponent = g as MonoBehaviour;
            _lastGridVersion = 0;
            RebuildTileMap();
            WarpTo(start);
        }

        public void WarpTo(HexCoords c)
        {
            Coords = c;
            if (TryGetTileTopWorld(c, out var topPos))
            {
                transform.position = topPos;
                _hasValidCoords = true;
            }
        }

        // 仅允许“邻格”移动；路径规划以后再加（A*）
        public bool TryMoveTo(HexCoords target)
        {
            if (IsMoving || grid == null) return false;
            if (Coords.DistanceTo(target) != 1) return false;              // 邻格限制
            if (!TryGetTileTopWorld(target, out var dst)) return false;    // 目标不存在

            var src = transform.position;
            _moveRoutine = StartCoroutine(MoveRoutine(src, dst, target));
            return true;
        }

        IEnumerator MoveRoutine(Vector3 src, Vector3 dst, HexCoords target)
        {
            float t = 0f;
            float dur = Mathf.Max(0.01f, secondsPerTile);
            bool shouldRotate = faceMovement && _visual2D == null;
            if (shouldRotate)
            {
                Vector3 dir = (dst - src);
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.0001f)
                    transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            }

            while (t < 1f)
            {
                t += Time.deltaTime / dur;
                transform.position = Vector3.Lerp(src, dst, Mathf.Clamp01(t));
                yield return null;
            }

            var from = Coords;
            Coords = target;
            _moveRoutine = null;
            OnMoveFinished?.Invoke(this, from, target);
        }

        // —— 工具：格子世界坐标/顶面高度 —— //

        void RebuildTileMap()
        {
            _tileMap.Clear();
            if (grid == null) return;

            foreach (var t in grid.EnumerateTiles())
            {
                if (t != null && !_tileMap.ContainsKey(t.Coords))
                    _tileMap.Add(t.Coords, t.transform);
            }
        }

        bool TryGetTileTopWorld(HexCoords c, out Vector3 pos)
        {
            pos = default;
            if (!_tileMap.TryGetValue(c, out var tr) || tr == null) return false;

            float top = (grid != null && grid.recipe != null) ? grid.recipe.thickness * 0.5f : 0f;
            pos = tr.position + new Vector3(0f, top + unitYOffset, 0f);
            return true;
        }
    }
}
