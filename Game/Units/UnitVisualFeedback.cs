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
        public Transform visualRoot; // 模型的父节点 (用于位移)
        public Animator animator;    // 用于播放美术动画
        public Renderer[] renderers; // 用于变色闪烁

        [Header("Procedural Settings (Code Only)")]
        public float lungeDistance = 0.6f;   // 攻击冲刺距离
        public float lungeDuration = 0.15f;  // 攻击冲刺时间 (也是伤害生效延迟)
        public Color hitColor = new Color(1f, 0.3f, 0.3f, 1f); // 受击闪红颜色
        public float hitShakeDuration = 0.15f; // 受击震动时间
        public float hitShakeAmount = 0.08f;   // 受击震动幅度

        [Header("Animator Settings (Art Only)")]
        public string animAttackTrigger = "Attack";
        public string animHitTrigger = "GetHit";
        [Tooltip("播放美术动画时，伤害生效的延迟时间 (用来匹配挥刀动作)")]
        public float animImpactDelay = 0.2f;

        private Vector3 _originalPos;
        private MaterialPropertyBlock _mpb;

        void Awake()
        {
            // 1. 优先确定 Visual Root
            // 尝试找名为 "Visual" 的子物体，找不到就用自己兜底
            if (visualRoot == null) visualRoot = transform.Find("Visual");
            if (visualRoot == null) visualRoot = transform;

            // 2. ⭐ 核心修改：自动从 Visual Root 下查找 Animator
            // 这样即使你没拖拽，只要 Animator 在 Visual 下面就能找到
            if (animator == null && visualRoot != null)
                animator = visualRoot.GetComponentInChildren<Animator>(true);

            // 3. 查找渲染器 (同样优先从 Visual Root 下找，防止把血条 UI 的 Renderer 找进来)
            if (renderers == null || renderers.Length == 0)
            {
                if (visualRoot != null)
                    renderers = visualRoot.GetComponentsInChildren<Renderer>(true);
                else
                    renderers = GetComponentsInChildren<Renderer>(true);
            }

            _originalPos = visualRoot.localPosition;
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
                visualRoot.localPosition = _originalPos;
                StartCoroutine(ProceduralHitRoutine());
            }
            else
            {
                if (animator) animator.SetTrigger(animHitTrigger);
            }
        }

        IEnumerator ProceduralHitRoutine()
        {
            foreach (var r in renderers)
            {
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor("_BaseColor", hitColor);
                _mpb.SetColor("_Color", hitColor);
                r.SetPropertyBlock(_mpb);
            }

            float elapsed = 0f;
            while (elapsed < hitShakeDuration)
            {
                elapsed += Time.deltaTime;
                visualRoot.localPosition = _originalPos + Random.insideUnitSphere * hitShakeAmount;
                yield return null;
            }

            visualRoot.localPosition = _originalPos;
            foreach (var r in renderers)
            {
                r.SetPropertyBlock(null);
            }
        }
    }
}