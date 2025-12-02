using UnityEngine;
using UnityEngine.UI;
using System.Collections;

namespace Game.UI.Helper
{
    public class SmoothBarController : MonoBehaviour
    {
        [Header("Components")]
        [Tooltip("前景条 (红条) - 必须放在 Hierarchy 下方")]
        public Image foregroundFill;
        [Tooltip("追赶/高亮条 (白条) - 必须放在 Hierarchy 上方")]
        public Image catchUpFill;

        [Header("Configuration")]
        [Tooltip("勾选：受伤条 (0 -> 100)\n不勾选：血条 (100 -> 0)")]
        public bool isInjuryBar = true;

        [Header("Animation")]
        public float animationSpeed = 5f;
        public float changeDelay = 0.5f;

        private float _targetValue;
        private float _currentBase; // 红条高度 (0~1)
        private float _currentHigh; // 追赶目标高度 (0~1)

        private Coroutine _animateRoutine;
        private RectTransform _rectGap;

        void Awake()
        {
            // 1. 自动清理 Slider
            var slider = GetComponent<Slider>();
            if (slider != null) Destroy(slider);

            if (catchUpFill) _rectGap = catchUpFill.rectTransform;

            // 2. 强制初始化图片模式
            ValidateImages();
        }

        public void Initialize(float current, float max)
        {
            float ratio = CalculateRatio(current, max);
            _targetValue = ratio;
            _currentBase = ratio;
            _currentHigh = ratio;
            UpdateVisuals();
        }

        public void UpdateValues(float current, float max)
        {
            float newTarget = CalculateRatio(current, max);

            // 只有数值变化才做动画
            if (Mathf.Abs(newTarget - _targetValue) < 0.001f) return;

            _targetValue = newTarget;

            if (_animateRoutine != null) StopCoroutine(_animateRoutine);
            _animateRoutine = StartCoroutine(AnimateBar(newTarget));
        }

        float CalculateRatio(float current, float max)
        {
            if (max <= 0) return 0;
            float ratio = Mathf.Clamp01(current / max);
            // 受伤条：血越少(ratio小) -> 条越长(1-ratio)
            return isInjuryBar ? (1f - ratio) : ratio;
        }

        IEnumerator AnimateBar(float newTarget)
        {
            // 判断数值变化方向
            bool isGrowing = newTarget > _currentBase;

            if (isGrowing)
            {
                // === 受伤 (Injury模式) ===
                // 1. 瞬间设定最高点 (白条出现)
                // 此时：Base不变(低)，High变高。白条填充中间的空隙。
                _currentHigh = newTarget;
                UpdateVisuals();

                yield return new WaitForSeconds(changeDelay);

                // 2. 红条(Base) 慢慢涨上去，推着白条底部走，直到填满
                while (Mathf.Abs(_currentBase - newTarget) > 0.001f)
                {
                    _currentBase = Mathf.MoveTowards(_currentBase, newTarget, animationSpeed * Time.deltaTime);
                    UpdateVisuals();
                    yield return null;
                }
            }
            else
            {
                // === 治疗 (Health模式 或 Injury恢复) ===
                // 快速同步，不显示Gap
                while (Mathf.Abs(_currentBase - newTarget) > 0.001f)
                {
                    float speed = animationSpeed * 2f;
                    _currentBase = Mathf.MoveTowards(_currentBase, newTarget, speed * Time.deltaTime);
                    _currentHigh = _currentBase; // High 紧跟 Base
                    UpdateVisuals();
                    yield return null;
                }
            }

            _currentBase = newTarget;
            _currentHigh = newTarget;
            UpdateVisuals();
        }

        // ⭐⭐⭐ 核心渲染逻辑 ⭐⭐⭐
        void UpdateVisuals()
        {
            if (foregroundFill == null || catchUpFill == null) return;

            // 1. 红条 (Base)：使用 Fill Amount (最稳，底部绝对不动)
            foregroundFill.fillAmount = _currentBase;

            // 2. 白条 (Gap)：使用 RectTransform 锚点拼接
            // 它的底部 (MinY) 接在红条的头顶 (_currentBase)
            // 它的顶部 (MaxY) 接在目标高度 (_currentHigh)
            // 这样它们在空间上完全不重叠！
            if (_currentHigh > _currentBase + 0.001f)
            {
                catchUpFill.gameObject.SetActive(true);

                // 垂直方向锚点
                _rectGap.anchorMin = new Vector2(0, _currentBase);
                _rectGap.anchorMax = new Vector2(1, _currentHigh);

                // 归零偏移，确保填满锚点区域
                _rectGap.offsetMin = Vector2.zero;
                _rectGap.offsetMax = Vector2.zero;
            }
            else
            {
                catchUpFill.gameObject.SetActive(false);
            }
        }

        // === 自动修复工具 ===
        [ContextMenu("Fix Settings Now")]
        public void ValidateImages()
        {
            // 红条必须是 Filled 模式
            if (foregroundFill)
            {
                foregroundFill.type = Image.Type.Filled;
                foregroundFill.fillMethod = Image.FillMethod.Vertical;
                foregroundFill.fillOrigin = (int)Image.OriginVertical.Bottom;

                // 红条框体全铺满
                RectTransform rt = foregroundFill.rectTransform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                rt.localScale = Vector3.one;
            }

            // 白条必须是 Simple/Sliced 模式 (因为我们要拉伸它的 Rect)
            if (catchUpFill)
            {
                // 如果是 Simple 最好，Sliced 也行
                if (catchUpFill.type == Image.Type.Filled)
                    catchUpFill.type = Image.Type.Simple;

                RectTransform rt = catchUpFill.rectTransform;
                rt.pivot = new Vector2(0.5f, 0f); // Pivot 放到底部方便计算
                rt.localScale = Vector3.one;
            }

            Debug.Log("已强制修复图片模式：红条(Filled), 白条(Simple/Anchored).");
        }

        [ContextMenu("Test Hit 50%")] void T50() => UpdateValues(50, 100);
        [ContextMenu("Test Reset 100%")] void T100() => UpdateValues(100, 100);
    }
}