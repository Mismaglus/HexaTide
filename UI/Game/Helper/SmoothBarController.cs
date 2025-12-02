using UnityEngine;
using UnityEngine.UI;
using System.Collections;

namespace Game.UI.Helper
{
    public class SmoothBarController : MonoBehaviour
    {
        [Header("Components")]
        [Tooltip("最上层的条 (例如：红色的受伤条，或绿色的血条)")]
        public Image foregroundFill;
        [Tooltip("底层的追赶条 (通常是白色，用于高亮变化量)")]
        public Image catchUpFill;

        [Header("Mode")]
        [Tooltip("勾选此项：由于这是'受伤条'，受伤时条会变长 (0 -> 1)。\n不勾选：这是普通血条，受伤时条会变短 (1 -> 0)。")]
        public bool isInjuryBar = true; // ⭐ 默认为 true 方便你使用

        [Header("Settings")]
        public float animationSpeed = 5f;   // 追赶动画速度
        public float changeDelay = 0.5f;    // 延迟时间

        private float _targetValue;
        private float _currentFront;
        private float _currentBack;

        private Coroutine _animateRoutine;

        public void Initialize(float current, float max)
        {
            float fillAmount = CalculateFill(current, max);
            _targetValue = fillAmount;
            _currentFront = fillAmount;
            _currentBack = fillAmount;
            UpdateFills();
        }

        public void UpdateValues(float current, float max)
        {
            float newTarget = CalculateFill(current, max);

            // 如果数值没变，直接返回
            if (Mathf.Abs(newTarget - _targetValue) < 0.001f) return;

            _targetValue = newTarget;

            if (_animateRoutine != null) StopCoroutine(_animateRoutine);
            _animateRoutine = StartCoroutine(AnimateBar(newTarget));
        }

        float CalculateFill(float current, float max)
        {
            float ratio = Mathf.Clamp01(current / max);
            // 如果是受伤条：血越少(ratio越小)，条越长(1-ratio)
            return isInjuryBar ? (1f - ratio) : ratio;
        }

        IEnumerator AnimateBar(float newTarget)
        {
            // === 逻辑分支 ===

            // 情况 A: 受伤了 (InjuryBar 变长 / HealthBar 变短)
            bool isDamage = isInjuryBar ? (newTarget > _currentFront) : (newTarget < _currentFront);

            if (isDamage)
            {
                if (isInjuryBar)
                {
                    // 【受伤条模式】：白条(Back)瞬间弹起到新位置，红条(Front)慢慢追上来
                    // 效果：看到一段白色的“新伤口”，然后被红色填满
                    _currentBack = newTarget;
                    UpdateFills();

                    // 等待一下
                    yield return new WaitForSeconds(changeDelay);

                    // 前景条追赶
                    while (Mathf.Abs(_currentFront - newTarget) > 0.001f)
                    {
                        _currentFront = Mathf.MoveTowards(_currentFront, newTarget, animationSpeed * Time.deltaTime);
                        UpdateFills();
                        yield return null;
                    }
                }
                else
                {
                    // 【普通血条模式】：绿条(Front)瞬间掉落，白条(Back)慢慢减少
                    _currentFront = newTarget;
                    UpdateFills();

                    yield return new WaitForSeconds(changeDelay);

                    while (Mathf.Abs(_currentBack - newTarget) > 0.001f)
                    {
                        _currentBack = Mathf.MoveTowards(_currentBack, newTarget, animationSpeed * Time.deltaTime);
                        UpdateFills();
                        yield return null;
                    }
                }
            }
            // 情况 B: 治疗了 (反向逻辑)
            else
            {
                // 治疗通常不需要延迟动画，直接平滑过渡或者瞬间补齐
                // 这里我们让两者同步快速平滑过去
                while (Mathf.Abs(_currentFront - newTarget) > 0.001f)
                {
                    _currentFront = Mathf.MoveTowards(_currentFront, newTarget, animationSpeed * 2f * Time.deltaTime);
                    _currentBack = _currentFront; // 保持同步
                    UpdateFills();
                    yield return null;
                }
            }

            // 确保最后数值精确
            _currentFront = newTarget;
            _currentBack = newTarget;
            UpdateFills();
        }

        void UpdateFills()
        {
            if (foregroundFill) foregroundFill.fillAmount = _currentFront;
            if (catchUpFill) catchUpFill.fillAmount = _currentBack;
        }
    }
}