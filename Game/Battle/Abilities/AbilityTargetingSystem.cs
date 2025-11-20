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
        public HexHighlighter highlighter;
        public SelectionManager selectionManager;
        public BattleHexInput input;
        public ActionQueue actionQueue;
        public AbilityRunner abilityRunner;
        public SkillBarController skillBarController;

        [Header("Visuals")]
        public BattleCursor gridCursor; // ⭐ 拖入新做的光标物体

        [Header("System Cursors")]
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

        public void EnterTargetingMode(Ability ability)
        {
            var unit = selectionManager.SelectedUnit;
            if (unit == null) return;

            _caster = unit.GetComponent<BattleUnit>();
            _currentAbility = ability;
            _validTiles.Clear();

            Debug.Log($"[Targeting] 开始瞄准: {_currentAbility.name}");

            // 1. 计算并显示绿底范围
            var rangeTiles = TargetingResolver.TilesInRange(grid, unit.Coords, ability.minRange, ability.maxRange);
            foreach (var t in rangeTiles) _validTiles.Add(t);
            highlighter.ApplyRange(_validTiles);

            // 2. 初始化光标 (先隐藏，直到鼠标动)
            if (gridCursor) gridCursor.Hide();
            Cursor.SetCursor(cursorInvalid, cursorHotspot, CursorMode.Auto);
        }

        void HandleHoverChanged(HexCoords? coords)
        {
            if (!IsTargeting) return;

            if (coords.HasValue)
            {
                bool isValid = _validTiles.Contains(coords.Value);

                // ⭐ 移动线框光标 (橙色=有效, 红色=无效)
                if (gridCursor) gridCursor.Show(coords.Value, isValid);

                // 鼠标指针样式
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

        void HandleTileClicked(HexCoords coords)
        {
            if (!IsTargeting) return;

            // 1. 射程校验 (软反馈)
            if (!_validTiles.Contains(coords))
            {
                Debug.Log("[Targeting] 目标太远或无效 (右键取消)");
                return;
            }

            // 2. 目标逻辑校验
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

            // 3. 执行
            Debug.Log($"[Targeting] 释放技能 -> {coords}");
            var action = new AbilityAction(_currentAbility, ctx, abilityRunner);
            actionQueue.Enqueue(action);
            StartCoroutine(actionQueue.RunAll());

            CancelTargeting();
        }

        public void CancelTargeting()
        {
            _currentAbility = null;
            _caster = null;
            _validTiles.Clear();

            highlighter.ClearAll(); // 清除绿底
            if (gridCursor) gridCursor.Hide(); // 隐藏线框

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