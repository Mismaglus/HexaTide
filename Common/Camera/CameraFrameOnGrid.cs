using Game.Battle;
using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Common
{
    [ExecuteAlways]
    public class CameraFrameOnGrid : MonoBehaviour
    {
        public enum Mode { OrthographicTopDown, OrthoIso, IsoPerspective } // ← 新增 OrthoIso

        [Header("OrthoIso (no perspective)")]
        [Range(10, 60)] public float orthoIsoPitch = 30f;  // 俯仰
        [Range(-180, 180)] public float orthoIsoYaw = 35f; // 朝向
        public static event Action<Mode> OnModeChanged;

        [Header("Targets")]
        public Camera cam;                 // Inspector 可手动拖；留空则自动找
        public BattleHexGrid grid;

        [Header("Framing")]
        public Mode mode = Mode.OrthographicTopDown;
        [Range(1.0f, 2.0f)] public float margin = 1.12f;
        [Tooltip("与网格最高点保持的最小垂直余量")]
        public float groundClearance = 1.0f;

        [Header("Iso Settings")]
        [Range(20, 75)] public float isoPitch = 55f;
        [Range(-180, 180)] public float isoYaw = 35f;
        [Range(20, 70)] public float fov = 40f;

        [Header("Integration")]
        [Tooltip("切换模式时同时设置相机的透明排序轴")]
        public bool syncTransparencySortAxis = true;
        [Tooltip("顶视时的排序轴（按Z排更直观：0,0,1）")]
        public Vector3 sortAxisTopDown = new Vector3(0, 0, 1);
        [Tooltip("俯角透视时的排序轴（按Y排：0,1,0）")]
        public Vector3 sortAxisIso = new Vector3(0, 1, 0);

        uint _lastGridVersion;

        void Reset() { EnsureRefs(); ApplyModeSettings(); FrameNow(); }
        void OnEnable() { EnsureRefs(); ApplyModeSettings(); FrameNow(); OnModeChanged?.Invoke(mode); }
#if UNITY_EDITOR
        void OnValidate() { if (!isActiveAndEnabled) return; EnsureRefs(); ApplyModeSettings(); FrameNow(); OnModeChanged?.Invoke(mode); }
#endif

        void LateUpdate()
        {
            EnsureRefs();
            if (grid && grid.Version != _lastGridVersion)
            {
                _lastGridVersion = grid.Version;
                FrameNow();
            }
        }

        // —— 公共API：在运行时/Editor按钮里调用以切换模式 ——
        public void SetMode(Mode m, bool reframe = true)
        {
            mode = m;
            ApplyModeSettings();
            if (reframe) FrameNow();
            OnModeChanged?.Invoke(mode); // ← 通知
        }
        public void ApplyModeSettings()
        {
            if (!cam) cam = GetComponent<Camera>() ?? Camera.main;
            if (!cam) return;

            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 2000f;

            if (mode == Mode.OrthographicTopDown)
            {
                cam.orthographic = true;
                cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                if (syncTransparencySortAxis)
                {
                    cam.transparencySortMode = TransparencySortMode.CustomAxis;
                    cam.transparencySortAxis = sortAxisTopDown; // 通常 (0,0,1)
                }
            }
            else if (mode == Mode.OrthoIso) // ← 新增
            {
                cam.orthographic = true;
                cam.transform.rotation = Quaternion.Euler(orthoIsoPitch, orthoIsoYaw, 0f);
                if (syncTransparencySortAxis)
                {
                    cam.transparencySortMode = TransparencySortMode.CustomAxis;
                    cam.transparencySortAxis = sortAxisIso; // 正交斜角下按 Y 排更稳：通常 (0,1,0)
                }
            }
            else // IsoPerspective
            {
                cam.orthographic = false;
                cam.fieldOfView = fov;
                cam.transform.rotation = Quaternion.Euler(isoPitch, isoYaw, 0f);
                if (syncTransparencySortAxis)
                {
                    cam.transparencySortMode = TransparencySortMode.CustomAxis;
                    cam.transparencySortAxis = sortAxisIso; // 通常 (0,1,0)
                }
            }
        }


        // —— 自动找引用 ——
        public void EnsureRefs()
        {
            if (!cam) cam = GetComponent<Camera>() ?? Camera.main;

            if (!grid)
            {
#if UNITY_2023_1_OR_NEWER
                grid = Object.FindFirstObjectByType<BattleHexGrid>(FindObjectsInactive.Exclude);
#else
                grid = Object.FindObjectOfType<BattleHexGrid>();
#endif
            }
        }

        // —— 核心：根据网格包围盒重构相机位置与尺寸 ——
        public void FrameNow()
        {
            EnsureRefs();
            if (!cam || !grid) return;

            var renderers = grid.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;

            var b = new Bounds(renderers[0].bounds.center, Vector3.zero);
            foreach (var r in renderers) if (r.enabled) b.Encapsulate(r.bounds);

            var c = b.center;
            var ex = b.extents;

            if (mode == Mode.OrthographicTopDown)
            {
                float halfW = b.extents.x;
                float halfH = b.extents.z;
                float size = Mathf.Max(halfH, halfW / Mathf.Max(0.0001f, cam.aspect)) * margin;
                cam.orthographicSize = size;

                float y = b.max.y + groundClearance;
                cam.transform.position = new Vector3(c.x, y, c.z);
            }
            else if (mode == Mode.OrthoIso) // ← 新增
            {
                // 将 AABB 的三个轴 (world X/Y/Z) 的半径投影到相机 right/up/forward 上
                Vector3 R = cam.transform.right;
                Vector3 U = cam.transform.up;
                Vector3 F = cam.transform.forward;

                // 投影半宽（屏幕 X 方向）
                float halfW = Mathf.Abs(ex.x * R.x) + Mathf.Abs(ex.y * R.y) + Mathf.Abs(ex.z * R.z);
                // 投影半高（屏幕 Y 方向）
                float halfH = Mathf.Abs(ex.x * U.x) + Mathf.Abs(ex.y * U.y) + Mathf.Abs(ex.z * U.z);
                // 投影视深（沿相机 forward，用来把相机放在包围盒前方）
                float halfD = Mathf.Abs(ex.x * F.x) + Mathf.Abs(ex.y * F.y) + Mathf.Abs(ex.z * F.z);

                float size = Mathf.Max(halfH, halfW / Mathf.Max(0.0001f, cam.aspect)) * margin;
                cam.orthographicSize = size;

                // 把相机放到盒子前方（留出 clearence）
                float dist = halfD * margin + groundClearance;
                cam.transform.position = c - F * dist;
            }
            else // IsoPerspective
            {
                float halfW = b.extents.x;
                float halfH = b.extents.z;
                float radius = Mathf.Sqrt(halfW * halfW + halfH * halfH);

                float halfVert = cam.fieldOfView * 0.5f * Mathf.Deg2Rad;
                float halfHor = Mathf.Atan(Mathf.Tan(halfVert) * cam.aspect);
                float halfMin = Mathf.Min(halfVert, halfHor);
                float dist = (radius * margin) / Mathf.Sin(Mathf.Max(0.001f, halfMin));

                Vector3 fwd = cam.transform.forward;
                Vector3 pos = c - fwd * dist;
                if (pos.y < b.max.y + groundClearance) pos.y = b.max.y + groundClearance;
                cam.transform.position = pos;
            }
        }

    }
}
