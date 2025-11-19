// Script/Game/Battle/Abilities/AbilityRunner.cs
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

            Animator animator = null;
            if (ability.faceTarget && TryGetFirstTargetWorldPos(ctx, out var facePos))
                FaceCaster(caster, facePos);

            if (!string.IsNullOrEmpty(ability.animTrigger) && TryGetAnimator(caster, out animator))
                animator.SetTrigger(ability.animTrigger);
            else if (animator == null)
                TryGetAnimator(caster, out animator); // attempt to cache for later waits

            if (ability.preWindupSeconds > 0f)
                yield return new WaitForSeconds(ability.preWindupSeconds);

            foreach (var ef in effects)
                if (ef != null)
                    yield return ef.Apply(caster, ability, ctx);

            if (ability.waitForAnimCompletion && animator != null)
                yield return WaitForAnimationCompletion(animator, ability);

            if (ability.postRecoverSeconds > 0f)
                yield return new WaitForSeconds(ability.postRecoverSeconds);
        }

        bool TryGetAnimator(BattleUnit caster, out Animator animator)
        {
            animator = null;
            if (caster == null) return false;

            if (caster.TryGetComponent(out animator) && animator != null)
                return true;

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

            var recipe = grid.recipe;
            pos = HexMetrics.GridToWorld(tile.q, tile.r, recipe.outerRadius, recipe.useOddROffset);
            pos += new Vector3(0f, recipe.thickness * 0.5f, 0f);
            return true;
        }

        void FaceCaster(BattleUnit caster, Vector3 worldPos)
        {
            if (caster == null) return;

            Vector3 dir = worldPos - caster.transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-6f) return;

            // Prefer locomotion helper if available
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

            if (string.IsNullOrEmpty(stateName) && string.IsNullOrEmpty(stateTag))
                yield break;

            int tagHash = string.IsNullOrEmpty(stateName) && !string.IsNullOrEmpty(stateTag)
                ? Animator.StringToHash(stateTag)
                : 0;

            bool Matches(AnimatorStateInfo info)
            {
                if (!string.IsNullOrEmpty(stateName) && info.IsName(stateName))
                    return true;
                if (tagHash != 0 && info.tagHash == tagHash)
                    return true;
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

            // wait for state to start
            while (elapsed < timeout && !LayerHasMatch())
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            // If never entered, bail out
            if (!LayerHasMatch())
                yield break;

            // wait for completion
            while (elapsed < timeout)
            {
                elapsed += Time.deltaTime;

                var info = animator.GetCurrentAnimatorStateInfo(layer);
                bool inTransition = animator.IsInTransition(layer);
                bool finished = !inTransition && info.normalizedTime >= 1f;

                if (!Matches(info) && !(inTransition && Matches(animator.GetNextAnimatorStateInfo(layer))))
                    break; // left state

                if (finished)
                {
                    // allow one more frame for exit transition
                    yield return null;
                    break;
                }

                yield return null;
            }
        }
    }
}
