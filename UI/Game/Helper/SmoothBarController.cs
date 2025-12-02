using UnityEngine;
using UnityEngine.UI;
using System.Collections;

namespace Game.UI.Helper
{
    public class SmoothBarController : MonoBehaviour
    {
        [Header("Components")]
        [Tooltip("主进度条 (Injury模式下是红条，Health模式下是绿条)")]
        public Image foregroundFill;
        [Tooltip("追赶/高亮条 (通常是白条)")]
        public Image catchUpFill;

        [Header("Configuration")]
        [Tooltip("勾选：受伤条 (数值越大条越长，例如 0->100)\n不勾选：血条 (数值越小条越短，例如 100->0)")]
        public bool isInjuryBar = true;

        [Tooltip("方向：勾选为垂直(上下)，不勾选为水平(左右)")]
        public bool isVertical = true;

        [Header("Animation")]
        public float animationSpeed = 5f;
        public float changeDelay = 0.5f;

        private float _targetValue;
        private float _currentFront; // 前景进度 (0~1)
        private float _currentBack;  // 背景/追赶进度 (0~1)

        private Coroutine _animateRoutine;
        private RectTransform _rectFront;
        private RectTransform _rectBack;

        void Awake()
        {
            if (foregroundFill) _rectFront = foregroundFill.rectTransform;
            if (catchUpFill) _rectBack = catchUpFill.rectTransform;

            // 初始化锚点模式，防止错乱
            SetupAnchors(_rectFront);
            SetupAnchors(_rectBack);
        }

        // 强制将锚点初始化为 Stretch 模式的变体，方便代码控制
        void SetupAnchors(RectTransform rt)
        {
            if (rt == null) return;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero; // Reset offsets
            rt.offsetMax = Vector2.zero;
            // 初始设为全 0
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
        }

        public void Initialize(float current, float max)
        {
            float fill = CalculateRatio(current, max);
            _targetValue = fill;
            _currentFront = fill;
            _currentBack = fill;
            UpdateRects();
        }

        public void UpdateValues(float current, float max)
        {
            float newTarget = CalculateRatio(current, max);
            if (Mathf.Abs(newTarget - _targetValue) < 0.001f) return;

            _targetValue = newTarget;
            if (_animateRoutine != null) StopCoroutine(_animateRoutine);
            _animateRoutine = StartCoroutine(AnimateBar(newTarget));
        }

        float CalculateRatio(float current, float max)
        {
            if (max <= 0) return 0;
            float ratio = Mathf.Clamp01(current / max);
            // 如果是受伤条，伤害越高(ratio越小)，条越短？
            // 抱歉，之前的逻辑有点绕。
            // 按照标准： 
            // Health Bar: current=100/100 -> 1.0 (Full).
            // Injury Bar: current=20/100 -> ?
            // 你的 UnitFrameUI 传入的是 curHP / maxHP。
            // 如果是受伤条，我们希望显示的是 (1 - ratio)。
            return isInjuryBar ? (1f - ratio) : ratio;
        }

        IEnumerator AnimateBar(float newTarget)
        {
            // 判定数值走向
            bool isIncreasingDamage = isInjuryBar ? (newTarget > _currentFront) : (newTarget < _currentFront);

            if (isIncreasingDamage)
            {
                // === 受伤逻辑 ===
                if (isInjuryBar)
                {
                    // 1. 伤害增加：白条瞬间变长 (作为新目标的指示器)
                    _currentBack = newTarget;
                    UpdateRects();

                    yield return new WaitForSeconds(changeDelay);

                    // 2. 红条慢慢追上来
                    while (Mathf.Abs(_currentFront - newTarget) > 0.001f)
                    {
                        _currentFront = Mathf.MoveTowards(_currentFront, newTarget, animationSpeed * Time.deltaTime);
                        UpdateRects();
                        yield return null;
                    }
                }
                else // 普通血条逻辑
                {
                    // 1. 血量减少：绿条瞬间减少
                    _currentFront = newTarget;
                    UpdateRects();

                    yield return new WaitForSeconds(changeDelay);

                    // 2. 白条慢慢减少
                    while (Mathf.Abs(_currentBack - newTarget) > 0.001f)
                    {
                        _currentBack = Mathf.MoveTowards(_currentBack, newTarget, animationSpeed * Time.deltaTime);
                        UpdateRects();
                        yield return null;
                    }
                }
            }
            else
            {
                // === 治疗/恢复逻辑 ===
                // 快速同步，不需要那么多特效
                while (Mathf.Abs(_currentFront - newTarget) > 0.001f)
                {
                    float speed = animationSpeed * 2f;
                    _currentFront = Mathf.MoveTowards(_currentFront, newTarget, speed * Time.deltaTime);
                    _currentBack = _currentFront;
                    UpdateRects();
                    yield return null;
                }
            }

            _currentFront = newTarget;
            _currentBack = newTarget;
            UpdateRects();
        }

        // ⭐⭐⭐ 核心修改：无重叠拼接逻辑 ⭐⭐⭐
        void UpdateRects()
        {
            if (_rectFront == null || _rectBack == null) return;

            // 确保数值顺序：Back 总是代表“更长”的那个值（无论是受伤后的目标值，还是扣血前的旧值）
            // Front 总是代表“更短”的那个值
            // 但实际上为了逻辑简单：
            // Front 是主条，Back 是背景条。

            float valFront = _currentFront;
            float valBack = _currentBack;

            if (isVertical)
            {
                // 1. 设置红条 (Front): 0 ~ valFront
                _rectFront.anchorMin = new Vector2(0, 0);
                _rectFront.anchorMax = new Vector2(1, valFront);

                // 2. 设置白条 (Back): 
                // 关键：只显示 Front 到 Back 之间的部分 (Gap)
                // 如果 Back < Front (这种情况一般不该发生，除非治疗)，就隐藏 Back
                if (valBack > valFront)
                {
                    _rectBack.gameObject.SetActive(true);
                    _rectBack.anchorMin = new Vector2(0, valFront); // 起点接在红条头顶
                    _rectBack.anchorMax = new Vector2(1, valBack);  // 终点是目标高度
                }
                else
                {
                    // 没有差距，隐藏白条
                    _rectBack.gameObject.SetActive(false);
                    _rectBack.anchorMin = new Vector2(0, 0);
                    _rectBack.anchorMax = new Vector2(0, 0);
                }
            }
            else // 水平模式 (Horizontal)
            {
                _rectFront.anchorMin = new Vector2(0, 0);
                _rectFront.anchorMax = new Vector2(valFront, 1);

                if (valBack > valFront)
                {
                    _rectBack.gameObject.SetActive(true);
                    _rectBack.anchorMin = new Vector2(valFront, 0);
                    _rectBack.anchorMax = new Vector2(valBack, 1);
                }
                else
                {
                    _rectBack.gameObject.SetActive(false);
                }
            }
        }

        // === 测试按钮 ===
        [ContextMenu("Set 50%")] void T50() => UpdateValues(50, 100);
        [ContextMenu("Set 20%")] void T20() => UpdateValues(20, 100);
        [ContextMenu("Set 80%")] void T80() => UpdateValues(80, 100);
    }
}