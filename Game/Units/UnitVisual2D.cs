using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.InputSystem;
using Game.Common; // 为拿到 CameraFrameOnGrid

namespace Game.Battle.Units
{
    [RequireComponent(typeof(BattleUnit))]
    public class UnitVisual2D : MonoBehaviour
    {
        [Header("Refs")]
        public SpriteRenderer spriteRenderer;   // 指向“Visual”子物体上的 SR
        public SortingGroup sortingGroup;       // 指向“Visual”子物体上的 SG（可选）

        [Header("Facing / Flip")]
        public bool flipByX = true;             // 俯角时：左右翻转
        public bool flipUseCameraRight = true;  // 用“相机右方向”判断左右（避免相机旋转后翻转反直觉）

        [Header("Sorting")]
        public bool enableDynamicSort = true;   // 开关：动态更新 sortingOrder
        public int orderScale = 1000;

        [Header("Sync With Camera Mode")]
        public bool followCameraMode = true;    // 勾上：随相机模式切换姿态与排序
        public float isoXRotation = 0f;         // 俯角：直立（一般 0）
        public bool isoBillboardY = true;       // 俯角：水平方向看板对齐相机
        public float topDownXRotation = -90f;   // 顶视：躺平（-90）
        public bool topDownUseZSort = true;     // 顶视：按 Z 排序（与相机透明轴(0,0,1)一致）

        Vector3 _lastPos;
        bool _isTopDown = false;

        void Reset()
        {
            if (!spriteRenderer) spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
            if (!sortingGroup) sortingGroup = GetComponentInChildren<SortingGroup>(true);
        }

        void OnEnable()
        {
            Reset();

            // 订阅相机模式变化
            CameraFrameOnGrid.OnModeChanged += HandleCameraModeChanged;

            // 初始化一次（若脚本晚于相机启用）
            var framer = FindFramer();
            if (framer) HandleCameraModeChanged(framer.mode);

            _lastPos = transform.position;
            ApplyFacing(Vector3.right);
            ApplySortOrder();
        }

        void OnDisable()
        {
            CameraFrameOnGrid.OnModeChanged -= HandleCameraModeChanged;
        }

        void Update()
        {
            var pos = transform.position;
            var delta = pos - _lastPos;

            if (delta.sqrMagnitude > 1e-6f)
            {
                ApplyFacing(delta);
                _lastPos = pos;
            }

            if (enableDynamicSort) ApplySortOrder();

            // 俯角时的“看板对齐”（只对齐水平朝向，不改变直立姿态）
            if (!_isTopDown && isoBillboardY && spriteRenderer)
            {
                var cam = Camera.main;
                if (cam)
                {
                    var fwd = cam.transform.forward;
                    fwd.y = 0f;
                    if (fwd.sqrMagnitude > 1e-6f)
                    {
                        // 用世界旋转保证永远正对相机（水平方向）
                        var yOnly = Quaternion.LookRotation(fwd.normalized, Vector3.up);
                        var e = spriteRenderer.transform.localEulerAngles;
                        // 保留本地 X 倾角（isoXRotation），只替换世界Y
                        spriteRenderer.transform.rotation = yOnly * Quaternion.Euler(isoXRotation, 0f, 0f);
                    }
                }
            }
        }

        // —— 相机模式变化时切换姿态/排序策略 ——
        void HandleCameraModeChanged(CameraFrameOnGrid.Mode m)
        {
            _isTopDown = (m == CameraFrameOnGrid.Mode.OrthographicTopDown);
            if (!followCameraMode || spriteRenderer == null) return;

            var tr = spriteRenderer.transform;

            if (_isTopDown)
            {
                // 顶视：躺平，通常不需要看板
                var e = tr.localEulerAngles;
                e.x = topDownXRotation; e.y = 0f; e.z = 0f;
                tr.localEulerAngles = e;
            }
            else
            {
                // 俯角：直立（X=isoXRotation），Y 方向后续在 Update 里看板
                var e = tr.localEulerAngles;
                e.x = isoXRotation; e.z = 0f;
                tr.localEulerAngles = e;
            }

            // 排序策略切换
            ApplySortOrder();
        }

        // —— 左右翻转：基于移动方向对相机“屏幕右”判断 ——
        void ApplyFacing(Vector3 moveDelta)
        {
            if (!flipByX || spriteRenderer == null) return;
            if (moveDelta.sqrMagnitude < 1e-8f) return;

            float signed = moveDelta.x;

            if (flipUseCameraRight)
            {
                var cam = Camera.main;
                if (cam)
                {
                    var right = cam.transform.right; right.y = 0f;
                    if (right.sqrMagnitude > 1e-6f)
                        signed = Vector3.Dot(moveDelta, right.normalized);
                }
            }

            // 屏幕左走：翻转；右走：不翻
            spriteRenderer.flipX = (signed < 0f);
        }

        // —— 排序：俯角按 Y，顶视（躺平）可按 Z ——
        void ApplySortOrder()
        {
            if (sortingGroup == null) return;

            float key = transform.position.y;
            if (_isTopDown && topDownUseZSort) key = transform.position.z;

            sortingGroup.sortingOrder = -Mathf.RoundToInt(key * orderScale);
        }

        CameraFrameOnGrid FindFramer()
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<CameraFrameOnGrid>(FindObjectsInactive.Exclude);
#else
            return Object.FindObjectOfType<CameraFrameOnGrid>();
#endif
        }

        // 对外 API：如果你在生成单位后想主动同步一次
        public void SyncPoseNow()
        {
            var framer = FindFramer();
            if (framer) HandleCameraModeChanged(framer.mode);
        }
    }
}
