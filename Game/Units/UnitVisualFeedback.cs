using UnityEngine;
using System.Collections;
using Game.Common;      // 引用 CameraShakeController
using Game.UI.Feedback; // 引用 FloatingTextManager

namespace Game.Units
{
    /// <summary>
    /// 通用视觉反馈控制器。
    /// 支持在“纯代码过程动画”和“Animator美术动画”之间一键切换。
    /// 现已集成飘字和震动反馈。
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

        [Header("Knockback Settings")]
        [Tooltip("击退滑行时，模型向后仰的角度 (负值表示后仰)")]
        public float knockbackTilt = -15f;
        [Tooltip("击退时的抛物线高度 (0 = 贴地滑行, >0 = 被打飞)")]
        public float knockbackJumpHeight = 0.0f;

        [Header("Visual Juice")]
        public bool enableCameraShake = true;
        public float hitShakeIntensity = 0.2f;
        public float critShakeIntensity = 0.4f;

        [Header("Animator Settings (Art Only)")]
        public string animAttackTrigger = "Attack";
        public string animHitTrigger = "GetHit";
        public string animKnockbackTrigger = "Knockback"; // 击退触发器
        [Tooltip("播放美术动画时，伤害生效的延迟时间 (用来匹配挥刀动作)")]
        public float animImpactDelay = 0.2f;

        private Vector3 _originalPos;
        private Quaternion _originalRot;
        private MaterialPropertyBlock _mpb;

        // 追踪当前正在运行的协程
        private Coroutine _activeRoutine;

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

            if (visualRoot)
            {
                _originalPos = visualRoot.localPosition;
                _originalRot = visualRoot.localRotation;
            }
            _mpb = new MaterialPropertyBlock();
        }

        // --- 核心辅助：安全开启新协程 ---
        private void StartNewRoutine(IEnumerator routine)
        {
            // 1. 如果有旧协程在跑，先停止
            if (_activeRoutine != null)
            {
                StopCoroutine(_activeRoutine);
            }

            // 2. 每次打断旧动作时，必须强制重置状态 (颜色、位置)
            ResetVisuals();
            ResetColor();

            // 3. 启动新协程并记录
            _activeRoutine = StartCoroutine(routine);
        }

        private void ResetVisuals()
        {
            if (visualRoot)
            {
                visualRoot.localPosition = _originalPos;
                visualRoot.localRotation = _originalRot;
            }
        }

        private void ResetColor()
        {
            if (renderers == null) return;
            foreach (var r in renderers)
            {
                if (r) r.SetPropertyBlock(null);
            }
        }

        // =========================================================

        public IEnumerator PlayAttack(Vector3 targetWorldPos)
        {
            IEnumerator Routine()
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

                    // 回弹
                    t = 0;
                    float recoverTime = lungeDuration * 2f;
                    while (t < 1f)
                    {
                        t += Time.deltaTime / recoverTime;
                        visualRoot.localPosition = Vector3.Lerp(end, start, t * (2 - t));
                        yield return null;
                    }
                    visualRoot.localPosition = start;
                }
                else
                {
                    // --- 方案 B: 美术动画 ---
                    if (animator) animator.SetTrigger(animAttackTrigger);
                    yield return new WaitForSeconds(animImpactDelay);
                }
                _activeRoutine = null;
            }

            StartNewRoutine(Routine());
            yield break; // Attack 不需要像 Knockback 那样返回 handle，外部等待时间即可
        }

        // ⭐ 修改：接收伤害数值和暴击标记
        public void PlayHit(int damage = 0, bool isCrit = false)
        {
            // 1. 飘字 (Floating Text)
            if (FloatingTextManager.Instance != null && damage > 0)
            {
                // 在头顶稍微高一点的位置生成
                Vector3 spawnPos = transform.position + Vector3.up * 1.8f;
                FloatingTextManager.Instance.ShowDamage(spawnPos, damage, isCrit);
            }

            // 2. 震屏 (Camera Shake)
            if (enableCameraShake && CameraShakeController.Instance != null)
            {
                float intensity = isCrit ? critShakeIntensity : hitShakeIntensity;
                // 震动时间固定 0.2s 即可，产生瞬间冲击感
                CameraShakeController.Instance.Shake(intensity, 0.2f);
            }

            // 3. 模型闪烁/震动 (Visual Animation)
            IEnumerator Routine()
            {
                if (useProcedural)
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
                        if (visualRoot)
                            visualRoot.localPosition = _originalPos + Random.insideUnitSphere * hitShakeAmount;
                        yield return null;
                    }

                    // 恢复
                    ResetColor();
                    ResetVisuals();
                }
                else
                {
                    if (animator) animator.SetTrigger(animHitTrigger);
                    yield return null;
                }
                _activeRoutine = null;
            }

            StartNewRoutine(Routine());
        }

        // ⭐ 新增：治疗反馈
        public void PlayHeal(int amount)
        {
            if (FloatingTextManager.Instance != null && amount > 0)
            {
                Vector3 spawnPos = transform.position + Vector3.up * 1.8f;
                FloatingTextManager.Instance.ShowHeal(spawnPos, amount);
            }
            // 以后可以在这里加绿色粒子特效
        }

        // === 3. 击退反馈 (协程) ===
        public Coroutine PlayKnockback(Vector3 startPos, Vector3 endPos, float duration)
        {
            IEnumerator Routine()
            {
                if (!useProcedural && animator)
                {
                    animator.SetTrigger(animKnockbackTrigger);
                }

                float t = 0f;
                while (t < 1f)
                {
                    t += Time.deltaTime / duration;
                    float ease = Mathf.Sin(t * Mathf.PI * 0.5f);

                    // 1. 位移
                    Vector3 currentPos = Vector3.Lerp(startPos, endPos, ease);
                    if (knockbackJumpHeight > 0.01f)
                    {
                        float height = Mathf.Sin(t * Mathf.PI) * knockbackJumpHeight;
                        currentPos.y += height;
                    }
                    transform.position = currentPos;

                    // 2. 姿态 (仅 procedural)
                    if (useProcedural && visualRoot)
                    {
                        float tiltAmount = Mathf.Lerp(knockbackTilt, 0f, t * t);
                        visualRoot.localRotation = _originalRot * Quaternion.Euler(tiltAmount, 0f, 0f);
                    }

                    yield return null;
                }

                // 确保最终位置准确
                transform.position = endPos;
                ResetVisuals();
                _activeRoutine = null;
            }

            StartNewRoutine(Routine());

            return _activeRoutine;
        }
    }
}