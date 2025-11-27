using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Core.Hex;
using Game.Units;

namespace Game.Battle.Abilities
{
    public class AbilityRunner : MonoBehaviour
    {
        public IEnumerator PerformEffects(BattleUnit caster, Ability ability, AbilityContext ctx, IList<AbilityEffect> effects)
        {
            if (ability == null) yield break;

            // 1. 朝向目标
            if (ability.faceTarget && TryGetFirstTargetWorldPos(ctx, out var facePos))
                FaceCaster(caster, facePos);

            // 2. 执行攻击动作 (视觉反馈)
            // 尝试获取统一反馈组件
            var feedback = caster.GetComponent<UnitVisualFeedback>();

            if (feedback != null)
            {
                // === 新逻辑：使用 UnitVisualFeedback ===
                Vector3 targetPos = caster.transform.position + caster.transform.forward;
                if (TryGetFirstTargetWorldPos(ctx, out var p)) targetPos = p;

                // 播放攻击 (代码位移 或 Animator动画)，并等待打击点
                yield return feedback.PlayAttack(targetPos);
            }
            else
            {
                // === 旧逻辑兜底：如果没有反馈组件 ===
                Animator animator = null;
                if (!string.IsNullOrEmpty(ability.animTrigger) && TryGetAnimator(caster, out animator))
                    animator.SetTrigger(ability.animTrigger);

                if (ability.preWindupSeconds > 0f)
                    yield return new WaitForSeconds(ability.preWindupSeconds);
            }

            // 3. 造成伤害 / 施加效果
            foreach (var ef in effects)
                if (ef != null)
                    yield return ef.Apply(caster, ability, ctx);

            // 4. 动画后摇等待 (可选，配合 Animator 的 State 检查)
            if (ability.waitForAnimCompletion)
            {
                // 重新获取 Animator (可能在 feedback 逻辑里没获取)
                if (TryGetAnimator(caster, out var anim))
                    yield return WaitForAnimationCompletion(anim, ability);
            }

            // 5. 固定后摇时间
            if (ability.postRecoverSeconds > 0f)
                yield return new WaitForSeconds(ability.postRecoverSeconds);
        }

        // --- 辅助方法 ---

        bool TryGetAnimator(BattleUnit caster, out Animator animator)
        {
            animator = null;
            if (caster == null) return false;
            if (caster.TryGetComponent(out animator) && animator != null) return true;
            animator = caster.GetComponentInChildren<Animator>(true);
            return animator != null;
        }

        bool TryGetFirstTargetWorldPos(AbilityContext ctx, out Vector3 pos)
        {
            pos = default;
            if (ctx == null) return false;

            foreach (var target in ctx.TargetUnits)
            {
                if (target == null) continue;
                pos = target.transform.position;
                return true;
            }

            foreach (var tile in ctx.TargetTiles)
            {
                if (TryResolveTileWorld(tile, out pos))
                    return true;
            }
            return false;
        }

        bool TryResolveTileWorld(HexCoords tile, out Vector3 pos)
        {
            pos = default;
            var grid = FindFirstObjectByType<Game.Battle.BattleHexGrid>(FindObjectsInactive.Exclude);
            if (grid == null || grid.recipe == null) return false;

            pos = HexMetrics.GridToWorld(tile.q, tile.r, grid.recipe.outerRadius, grid.recipe.useOddROffset);
            return true;
        }

        void FaceCaster(BattleUnit caster, Vector3 worldPos)
        {
            if (caster == null) return;

            Vector3 dir = worldPos - caster.transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-6f) return;

            var locomotion = caster.GetComponentInChildren<Unit3DLocomotion>();
            if (locomotion != null)
            {
                locomotion.FaceWorldDirection(dir);
                return;
            }

            if (caster.TryGetComponent<Unit>(out var unit))
            {
                unit.transform.forward = dir.normalized;
                return;
            }
            caster.transform.forward = dir.normalized;
        }

        IEnumerator WaitForAnimationCompletion(Animator animator, Ability ability)
        {
            if (animator == null) yield break;
            int layerCount = animator.layerCount;
            if (layerCount <= 0) yield break;

            int layer = Mathf.Clamp(ability.animLayerIndex, 0, Mathf.Max(0, layerCount - 1));
            float timeout = Mathf.Max(0.1f, ability.animWaitTimeout);
            string stateName = ability.animStateName;
            string stateTag = ability.animStateTag;

            if (string.IsNullOrEmpty(stateName) && string.IsNullOrEmpty(stateTag)) yield break;

            int tagHash = string.IsNullOrEmpty(stateName) && !string.IsNullOrEmpty(stateTag)
                ? Animator.StringToHash(stateTag) : 0;

            bool Matches(AnimatorStateInfo info)
            {
                if (!string.IsNullOrEmpty(stateName) && info.IsName(stateName)) return true;
                if (tagHash != 0 && info.tagHash == tagHash) return true;
                return false;
            }

            bool LayerHasMatch()
            {
                var info = animator.GetCurrentAnimatorStateInfo(layer);
                if (Matches(info)) return true;
                if (animator.IsInTransition(layer))
                {
                    var next = animator.GetNextAnimatorStateInfo(layer);
                    if (Matches(next)) return true;
                }
                return false;
            }

            float elapsed = 0f;
            while (elapsed < timeout && !LayerHasMatch()) { elapsed += Time.deltaTime; yield return null; }
            if (!LayerHasMatch()) yield break;

            while (elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                var info = animator.GetCurrentAnimatorStateInfo(layer);
                bool inTransition = animator.IsInTransition(layer);
                bool finished = !inTransition && info.normalizedTime >= 1f;

                if (!Matches(info) && !(inTransition && Matches(animator.GetNextAnimatorStateInfo(layer)))) break;
                if (finished) { yield return null; break; }
                yield return null;
            }
        }
    }
}