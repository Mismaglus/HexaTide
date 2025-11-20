using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;  // 新输入系统
using Core.Hex;
using Game.Common;

namespace Game.Battle
{
    /// <summary>读取鼠标（Input System），把 Hover/Click 映射到瓦片。</summary>
    public class BattleHexInput : MonoBehaviour
    {
        [Header("Refs")]
        public Camera cam;                 // 留空则自动取 MainCamera
        public BattleHexGrid grid;         // 拖你的网格对象
        public HexHighlighter highlighter; // 拖上面的高亮器

        [Header("Options")]
        public bool ignoreWhenPointerOverUI = true;
        public LayerMask raycastMask = ~0; // 可选：只打到瓦片层

        // 点击回调（以后你可以在别的脚本订阅）
        public System.Action<HexCoords> OnTileClicked;
        public System.Action<HexCoords?> OnHoverChanged;
        HexCoords? _lastHover;
        void Reset()
        {
            cam = Camera.main;
            if (!grid) grid = FindFirstObjectByType<BattleHexGrid>(FindObjectsInactive.Exclude);
            if (!highlighter)
            {
                highlighter = FindFirstObjectByType<HexHighlighter>(FindObjectsInactive.Exclude);
                if (highlighter && !highlighter.grid) highlighter.grid = grid;
            }
        }

        void Update()
        {
            if (!cam) cam = Camera.main;
            if (!grid || !highlighter) return;

            var controlService = PlayerControlService.Instance;
            if (controlService != null && !controlService.IsControlEnabled)
            {
                // highlighter.SetHover(null);
                if (_lastHover.HasValue)
                {
                    _lastHover = null;
                    OnHoverChanged?.Invoke(null);
                }
                return;
            }

            // 没有鼠标设备就别读
            if (Mouse.current == null) return;

            // UI 覆盖时不交互（可关）
            if (ignoreWhenPointerOverUI && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                // highlighter.SetHover(null);
                if (_lastHover.HasValue)
                {
                    _lastHover = null;
                    OnHoverChanged?.Invoke(null);
                }
                return;
            }

            Vector2 pos = Mouse.current.position.ReadValue();
            if (HexRaycaster.TryPick(cam, pos, out var go, out var tag, (int)raycastMask))
            {
                var h = tag.Coords;
                // highlighter.SetHover(h);
                if (!_lastHover.HasValue || !_lastHover.Value.Equals(h))
                {
                    _lastHover = h;
                    OnHoverChanged?.Invoke(h);
                }
                // 左键点击
                if (Mouse.current.leftButton.wasPressedThisFrame)
                {
                    OnTileClicked?.Invoke(h);
                    // 先打个日志，后续你可以在外部订阅上层逻辑
                    // Debug.Log($"[Click] Hex {h}");
                }
            }
            else
            {
                // highlighter.SetHover(null);
                if (_lastHover.HasValue)
                {
                    _lastHover = null;
                    OnHoverChanged?.Invoke(null);
                }
            }
        }
    }
}
