using UnityEngine;
using UnityEngine.UI;
using System.Collections;

namespace Game.UI.Helper
{
    public class SmoothBarController : MonoBehaviour
    {
        [Header("Components")]
        [Tooltip("最上层的条 (例如：红色的受伤条) - 必须放在 Hierarchy 最下方以显示在顶层")]
        public Image foregroundFill;
        [Tooltip("底层的追赶条 (通常是白色) - 必须放在 Hierarchy 上方")]
        public Image catchUpFill;

        [Header("Mode")]
        [Tooltip("勾选此项：受伤条模式 (血越少条越长)。\n不勾选：普通血条模式 (血越少条越短)。")]
        public bool isInjuryBar = true;

        [Header("Settings")]
        public float animationSpeed = 15f;  // 调快速度，更有打击感
        public float changeDelay = 0.25f;   // 缩短延迟

        private float _targetValue;
        private float _currentFront;
        private float _currentBack;

        private Coroutine _animateRoutine;

        // ⭐ 自动修正图片设置，防止因为 ImageType 不是 Filled 导致血条不动
        void OnValidate()
        {
            ValidateImage(foregroundFill);
            ValidateImage(catchUpFill);
        }

        void ValidateImage(Image img)
        {
            if (img != null && img.type != Image.Type.Filled)
            {
                img.type = Image.Type.Filled;
                img.fillMethod = Image.FillMethod.Vertical; // 根据你的截图，你是垂直条
                img.fillOrigin = (int)Image.OriginVertical.Bottom; // 从下往上
                Debug.Log($"[SmoothBar] Auto-fixed {img.name} to Filled/Vertical/Bottom");
            }
        }

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

            // 只有变化足够大才播放动画
            if (Mathf.Abs(newTarget - _targetValue) < 0.001f) return;

            _targetValue = newTarget;

            if (_animateRoutine != null) StopCoroutine(_animateRoutine);
            _animateRoutine = StartCoroutine(AnimateBar(newTarget));
        }

        float CalculateFill(float current, float max)
        {
            if (max <= 0) return 0;
            float ratio = Mathf.Clamp01(current / max);
            // 受伤条逻辑：满血(1.0) -> Fill 0.0。空血(0.0) -> Fill 1.0。
            return isInjuryBar ? (1f - ratio) : ratio;
        }

        IEnumerator AnimateBar(float newTarget)
        {
            // 判断是否是“受伤/掉血”方向
            // 普通条：新值 < 旧值 = 掉血
            // 受伤条：新值 > 旧值 = 掉血 (条变长了)
            bool isTakingDamage = isInjuryBar ? (newTarget > _currentFront) : (newTarget < _currentFront);

            if (isTakingDamage)
            {
                if (isInjuryBar)
                {
                    // === 受伤条逻辑 (增长) ===
                    // 1. 白条(Back)瞬间增长到目标值 (显示"新伤口")
                    _currentBack = newTarget;
                    // 2. 红条(Front)暂时不动
                    UpdateFills();

                    // 3. 停顿
                    yield return new WaitForSeconds(changeDelay);

                    // 4. 红条慢慢涨上去覆盖白条
                    while (Mathf.Abs(_currentFront - newTarget) > 0.001f)
                    {
                        _currentFront = Mathf.MoveTowards(_currentFront, newTarget, animationSpeed * Time.deltaTime);
                        UpdateFills();
                        yield return null;
                    }
                }
                else
                {
                    // === 普通血条逻辑 (减少) ===
                    // 1. 绿条(Front)瞬间减少
                    _currentFront = newTarget;
                    UpdateFills();

                    // 2. 停顿 (白条还在原位)
                    yield return new WaitForSeconds(changeDelay);

                    // 3. 白条(Back)慢慢减少
                    while (Mathf.Abs(_currentBack - newTarget) > 0.001f)
                    {
                        _currentBack = Mathf.MoveTowards(_currentBack, newTarget, animationSpeed * Time.deltaTime);
                        UpdateFills();
                        yield return null;
                    }
                }
            }
            else
            {
                // === 治疗/恢复逻辑 ===
                // 两者同步平滑变化
                while (Mathf.Abs(_currentFront - newTarget) > 0.001f)
                {
                    float step = animationSpeed * 2f * Time.deltaTime;
                    _currentFront = Mathf.MoveTowards(_currentFront, newTarget, step);
                    _currentBack = _currentFront; // 保持同步，没有白条
                    UpdateFills();
                    yield return null;
                }
            }

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