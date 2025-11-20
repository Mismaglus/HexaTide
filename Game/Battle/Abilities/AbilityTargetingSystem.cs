using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Core.Hex;
using Game.Common;
using Game.Units;
using Game.Battle.Abilities;
using Game.Battle.Actions;
using Game.UI;
using UnityEngine.InputSystem;

namespace Game.Battle
{
    public class AbilityTargetingSystem : MonoBehaviour
    {
        [Header("Core References")]
        public BattleHexGrid grid;
        public SelectionManager selectionManager;
        public BattleHexInput input;
        public ActionQueue actionQueue;
        public AbilityRunner abilityRunner;
        public SkillBarController skillBarController;

        [Header("Visual Systems")]
        public HexHighlighter highlighter;     // 用于清理 SelectionManager 的高亮
        public RangeOutlineDrawer rangeDrawer; // ⭐ 负责画技能范围轮廓 (确保场景里有并拖入)
        public BattleCursor gridCursor;        // ⭐ 负责画打击线框 (确保场景里有并拖入)

        [Header("Cursors (Mouse Icon)")]
        public Texture2D cursorTarget;
        public Texture2D cursorInvalid;
        public Vector2 cursorHotspot = Vector2.zero;

        // 运行时状态
        private Ability _currentAbility;
        private BattleUnit _caster;
        private HashSet<HexCoords> _validTiles = new HashSet<HexCoords>();

        public bool IsTargeting => _currentAbility != null;

        void Awake()
        {
            if (!grid) grid = FindFirstObjectByType<BattleHexGrid>();
            if (!highlighter) highlighter = FindFirstObjectByType<HexHighlighter>();
            if (!selectionManager) selectionManager = FindFirstObjectByType<SelectionManager>();
            if (!input) input = FindFirstObjectByType<BattleHexInput>();
            if (!actionQueue) actionQueue = FindFirstObjectByType<ActionQueue>();
            if (!abilityRunner) abilityRunner = FindFirstObjectByType<AbilityRunner>();
            if (!skillBarController) skillBarController = FindFirstObjectByType<SkillBarController>();

            if (!rangeDrawer) rangeDrawer = FindFirstObjectByType<RangeOutlineDrawer>();
            if (!gridCursor) gridCursor = FindFirstObjectByType<BattleCursor>();
        }

        void OnEnable()
        {
            if (skillBarController) skillBarController.OnAbilitySelected += EnterTargetingMode;
            if (input)
            {
                input.OnTileClicked += HandleTileClicked;
                input.OnHoverChanged += HandleHoverChanged;
            }
        }

        void OnDisable()
        {
            if (skillBarController) skillBarController.OnAbilitySelected -= EnterTargetingMode;
            if (input)
            {
                input.OnTileClicked -= HandleTileClicked;
                input.OnHoverChanged -= HandleHoverChanged;
            }
        }

        // 1. 进入瞄准模式
        public void EnterTargetingMode(Ability ability)
        {
            var unit = selectionManager.SelectedUnit;
            if (unit == null) return;

            _caster = unit.GetComponent<BattleUnit>();
            _currentAbility = ability;
            _validTiles.Clear();

            Debug.Log($"[Targeting] 开始瞄准: {_currentAbility.name}");

            // 计算范围
            var rangeTiles = TargetingResolver.TilesInRange(grid, unit.Coords, ability.minRange, ability.maxRange);
            foreach (var t in rangeTiles) _validTiles.Add(t);

            // === 视觉处理 ===

            // A. 彻底清理 SelectionManager 留下的高亮 (选中蓝/移动绿/悬停黄)
            highlighter.ClearAll();

            // B. 显示范围轮廓 (Outline)
            if (rangeDrawer) rangeDrawer.Show(_validTiles);

            // C. 初始化线框光标 (先隐藏)
            if (gridCursor) gridCursor.Hide();

            // D. 初始化鼠标样式
            Cursor.SetCursor(cursorInvalid, cursorHotspot, CursorMode.Auto);
        }

        // 2. 处理悬停
        void HandleHoverChanged(HexCoords? coords)
        {
            if (!IsTargeting) return;

            if (coords.HasValue)
            {
                bool isValid = _validTiles.Contains(coords.Value);

                // A. 移动线框光标 (BattleCursor 会自己变色: 橙/红)
                if (gridCursor) gridCursor.Show(coords.Value, isValid);

                // B. 切换鼠标图标
                var cursorTex = isValid ? cursorTarget : cursorInvalid;
                Cursor.SetCursor(cursorTex, cursorHotspot, CursorMode.Auto);
            }
            else
            {
                // 移出地图
                if (gridCursor) gridCursor.Hide();
                Cursor.SetCursor(cursorInvalid, cursorHotspot, CursorMode.Auto);
            }
        }

        // 3. 处理点击
        void HandleTileClicked(HexCoords coords)
        {
            if (!IsTargeting) return;

            // 范围校验
            if (!_validTiles.Contains(coords))
            {
                Debug.Log("[Targeting] 目标无效 (不在范围内)");
                return;
            }

            // 目标校验
            selectionManager.TryGetUnitAt(coords, out Unit targetUnit);
            BattleUnit targetBattleUnit = targetUnit ? targetUnit.GetComponent<BattleUnit>() : null;

            var ctx = new AbilityContext
            {
                Caster = _caster,
                Origin = _caster.GetComponent<Unit>().Coords
            };
            if (targetBattleUnit != null) ctx.TargetUnits.Add(targetBattleUnit);
            ctx.TargetTiles.Add(coords);

            if (!_currentAbility.IsValidTarget(_caster, ctx))
            {
                Debug.Log("[Targeting] 目标类型无效");
                return;
            }

            // 执行
            Debug.Log($"[Targeting] 释放技能 -> {coords}");
            var action = new AbilityAction(_currentAbility, ctx, abilityRunner);
            actionQueue.Enqueue(action);
            StartCoroutine(actionQueue.RunAll());

            CancelTargeting();
        }

        // 4. 退出
        public void CancelTargeting()
        {
            _currentAbility = null;
            _caster = null;
            _validTiles.Clear();

            // 关闭轮廓
            if (rangeDrawer) rangeDrawer.Hide();
            // 关闭光标
            if (gridCursor) gridCursor.Hide();
            // 清理
            highlighter.ClearAll();

            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            Debug.Log("[Targeting] 瞄准结束");
        }

        void Update()
        {
            if (IsTargeting)
            {
                if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
                {
                    CancelTargeting();
                }
            }
        }
    }
}