using UnityEngine;
using System.Collections;

namespace Game.Units
{
    /// <summary>
    /// 通用视觉反馈控制器。
    /// 支持在“纯代码过程动画”和“Animator美术动画”之间一键切换。
    /// </summary>
    public class UnitVisualFeedback : MonoBehaviour
    {
        [Header("Mode Switch")]
        [Tooltip("勾选：使用代码控制的位移/变色（适合原型）。\n不勾选：使用 Animator 触发动画（适合后期）。")]
        public bool useProcedural = true;

        [Header("Common References")]
        public Transform visualRoot; // 模型的父节点 (用于位移/倾斜)
        public Animator animator;    // 用于播放美术动画
        public Renderer[] renderers; // 用于变色闪烁

        [Header("Procedural Settings (Attack/Hit)")]
        public float lungeDistance = 0.6f;   // 攻击冲刺距离
        public float lungeDuration = 0.15f;  // 攻击冲刺时间 (也是伤害生效延迟)
        public Color hitColor = new Color(1f, 0.3f, 0.3f, 1f); // 受击闪红颜色
        public float hitShakeDuration = 0.15f; // 受击震动时间
        public float hitShakeAmount = 0.08f;   // 受击震动幅度

        [Header("Procedural Settings (Knockback)")]
        [Tooltip("击退滑行时，模型向后仰的角度 (负值表示后仰)")]
        public float knockbackTilt = -15f;
        [Tooltip("击退时的抛物线高度 (0 = 贴地滑行, >0 = 被打飞)")]
        public float knockbackJumpHeight = 0.0f;

        [Header("Animator Settings (Art Only)")]
        public string animAttackTrigger = "Attack";
        public string animHitTrigger = "GetHit";
        public string animKnockbackTrigger = "Knockback"; // 击退触发器
        [Tooltip("播放美术动画时，伤害生效的延迟时间 (用来匹配挥刀动作)")]
        public float animImpactDelay = 0.2f;

        private Vector3 _originalPos;
        private Quaternion _originalRot;
        private MaterialPropertyBlock _mpb;

        void Awake()
        {
            // 1. 优先确定 Visual Root
            if (visualRoot == null) visualRoot = transform.Find("Visual");
            if (visualRoot == null) visualRoot = transform;

            // 2. 自动从 Visual Root 下查找 Animator
            if (animator == null && visualRoot != null)
                animator = visualRoot.GetComponentInChildren<Animator>(true);

            // 3. 查找渲染器
            if (renderers == null || renderers.Length == 0)
            {
                if (visualRoot != null)
                    renderers = visualRoot.GetComponentsInChildren<Renderer>(true);
                else
                    renderers = GetComponentsInChildren<Renderer>(true);
            }

            _originalPos = visualRoot.localPosition;
            _originalRot = visualRoot.localRotation;
            _mpb = new MaterialPropertyBlock();
        }

        // === 1. 攻击反馈 (协程) ===
        public IEnumerator PlayAttack(Vector3 targetWorldPos)
        {
            if (useProcedural)
            {
                // --- 方案 A: 代码冲刺 ---
                Vector3 dir = (targetWorldPos - transform.position).normalized;
                Vector3 localDir = transform.InverseTransformDirection(dir);
                localDir.y = 0;

                Vector3 start = _originalPos;
                Vector3 end = start + localDir * lungeDistance;

                float t = 0;
                while (t < 1f)
                {
                    t += Time.deltaTime / lungeDuration;
                    visualRoot.localPosition = Vector3.Lerp(start, end, t * t);
                    yield return null;
                }

                StartCoroutine(RecoverRoutine(start, end));
            }
            else
            {
                // --- 方案 B: 美术动画 ---
                if (animator) animator.SetTrigger(animAttackTrigger);
                yield return new WaitForSeconds(animImpactDelay);
            }
        }

        private IEnumerator RecoverRoutine(Vector3 start, Vector3 end)
        {
            float t = 0;
            float recoverTime = lungeDuration * 2f;
            while (t < 1f)
            {
                t += Time.deltaTime / recoverTime;
                visualRoot.localPosition = Vector3.Lerp(end, start, t * (2 - t));
                yield return null;
            }
            visualRoot.localPosition = start;
        }

        // === 2. 受击反馈 (即时) ===
        public void PlayHit()
        {
            if (useProcedural)
            {
                StopAllCoroutines();
                ResetVisuals(); // 确保位置归正
                StartCoroutine(ProceduralHitRoutine());
            }
            else
            {
                if (animator) animator.SetTrigger(animHitTrigger);
            }
        }

        IEnumerator ProceduralHitRoutine()
        {
            // 变色
            foreach (var r in renderers)
            {
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor("_BaseColor", hitColor);
                _mpb.SetColor("_Color", hitColor);
                r.SetPropertyBlock(_mpb);
            }

            // 震动
            float elapsed = 0f;
            while (elapsed < hitShakeDuration)
            {
                elapsed += Time.deltaTime;
                visualRoot.localPosition = _originalPos + Random.insideUnitSphere * hitShakeAmount;
                yield return null;
            }

            // 恢复
            ResetVisuals();
            foreach (var r in renderers)
            {
                r.SetPropertyBlock(null);
            }
        }

        // === 3. 击退反馈 (协程 - 由 KnockbackSystem 调用) ===
        public IEnumerator PlayKnockback(Vector3 startPos, Vector3 endPos, float duration)
        {
            // 强行打断当前的任何动作（如受击震动）
            StopAllCoroutines();

            // 如果使用 Animator，触发击退状态 (Loop or Trigger)
            if (!useProcedural && animator)
            {
                animator.SetTrigger(animKnockbackTrigger);
            }

            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / duration;

                // EaseOut Sine: 模拟瞬间受力后因摩擦力减速
                float ease = Mathf.Sin(t * Mathf.PI * 0.5f);

                // 1. 位移插值 (控制根物体)
                Vector3 currentPos = Vector3.Lerp(startPos, endPos, ease);

                // 可选：抛物线高度
                if (knockbackJumpHeight > 0.01f)
                {
                    float height = Mathf.Sin(t * Mathf.PI) * knockbackJumpHeight;
                    currentPos.y += height;
                }

                transform.position = currentPos;

                // 2. 姿态控制 (仅 procedural)
                if (useProcedural)
                {
                    // 在滑行过程中保持后仰，结束时自动回正
                    // 也可以做一个 t 的曲线让它先仰后回，这里简单处理为全程后仰
                    float tiltAmount = Mathf.Lerp(knockbackTilt, 0f, t * t); // 慢慢回正
                    visualRoot.localRotation = _originalRot * Quaternion.Euler(tiltAmount, 0f, 0f);
                }

                yield return null;
            }

            // 确保最终位置准确
            transform.position = endPos;
            ResetVisuals();
        }

        private void ResetVisuals()
        {
            visualRoot.localPosition = _originalPos;
            visualRoot.localRotation = _originalRot;
        }
    }
}