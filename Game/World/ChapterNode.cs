using UnityEngine;
using Game.Battle;
using Game.Units;
using System;
using Game.Grid;

namespace Game.World
{
    public enum ChapterNodeType
    {
        Start,
        NormalEnemy,
        EliteEnemy,
        Merchant,
        Mystery,
        Treasure,
        Boss,
        Gate_Left,
        Gate_Right,
        Gate_Skip
    }

    [RequireComponent(typeof(HexCell))]
    public class ChapterNode : MonoBehaviour
    {
        public ChapterNodeType type { get; private set; } = ChapterNodeType.NormalEnemy;
        public bool isCleared { get; private set; } = false;

        // Optional content identity for Boss nodes (e.g., "BOSS_001").
        public string BossId { get; private set; }

        private HexCell _cell;

        private const string ICON_NAME_PREFIX = "ChapterNodeIcon";

        public void Initialize(ChapterNodeType nodeType, string bossId = null)
        {
            type = nodeType;
            isCleared = false;
            BossId = string.IsNullOrEmpty(bossId) ? null : bossId;
            _cell = GetComponent<HexCell>();
            _cell.RefreshFogVisuals();
        }

        public void ApplyIcon(ChapterNodeIconLibrary library, BossIconLibrary bossLibrary)
        {
            if (!this) return;
            if (_cell == null) _cell = GetComponent<HexCell>();

            var coords = _cell != null ? _cell.Coords : default;
            string desiredName = type == ChapterNodeType.Boss && !string.IsNullOrEmpty(BossId)
                ? $"{ICON_NAME_PREFIX}_q{coords.q}_r{coords.r}_{type}_{BossId}"
                : $"{ICON_NAME_PREFIX}_q{coords.q}_r{coords.r}_{type}";

            var iconTransform = FindExistingIconTransform();
            if (iconTransform == null)
            {
                var iconGO = new GameObject(desiredName);
                iconTransform = iconGO.transform;
                iconTransform.SetParent(transform, false);
                iconTransform.localPosition = Vector3.zero;
                iconTransform.localRotation = Quaternion.identity;
                iconTransform.localScale = Vector3.one;
            }
            else
            {
                iconTransform.gameObject.name = desiredName;
            }

            GameObject prefab = null;

            // If BossId is set, prefer boss-specific prefab (supports Boss and Gate nodes).
            if (!string.IsNullOrEmpty(BossId))
            {
                if (bossLibrary != null && bossLibrary.TryGetPrefab(BossId, out prefab) && prefab != null)
                {
                    // ok
                }
            }

            // Otherwise fall back to nodeType mapping.
            if (prefab == null)
            {
                if (library != null && library.TryGetPrefab(type, out prefab) && prefab != null)
                {
                    // ok
                }
            }

            if (prefab == null)
            {
                // No mapping => no icon
                if (iconTransform != null)
                {
#if UNITY_EDITOR
                    if (!Application.isPlaying) DestroyImmediate(iconTransform.gameObject);
                    else Destroy(iconTransform.gameObject);
#else
                    Destroy(iconTransform.gameObject);
#endif
                }
                return;
            }

            // Placement: always relative to the tile/node transform (stable regardless of CenterOffset)
            Vector3 localOffset = library.localOffset;
            Vector3 localEuler = library.localEuler;
            float localScale = Mathf.Max(0.0001f, library.localScale);

            iconTransform.localPosition = localOffset;
            iconTransform.localRotation = Quaternion.Euler(localEuler);
            iconTransform.localScale = new Vector3(localScale, localScale, localScale);

            RemoveSpriteRendererIfAny(iconTransform);
            EnsurePrefabInstance(iconTransform, prefab);
        }

        private Transform FindExistingIconTransform()
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                if (child != null && child.name.StartsWith(ICON_NAME_PREFIX, StringComparison.Ordinal))
                {
                    return child;
                }
            }
            return null;
        }

        private static void EnsurePrefabInstance(Transform iconRoot, GameObject prefab)
        {
            // Minimal behavior: keep at most one instantiated icon model under root.
            // If something already exists, replace it to match the configured prefab.
            bool needsReplace = true;
            if (iconRoot.childCount == 1)
            {
                var existing = iconRoot.GetChild(0);
                if (existing != null)
                {
                    // Compare by name prefix (best-effort). Exact prefab identity isn't reliable at runtime.
                    if (existing.name == prefab.name) needsReplace = false;
                }
            }

            if (!needsReplace) return;

            DestroyAllChildren(iconRoot);
            var instance = Instantiate(prefab, iconRoot);
            instance.name = prefab.name;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
        }

        private static void RemoveSpriteRendererIfAny(Transform t)
        {
            var sr = t.GetComponent<SpriteRenderer>();
            if (sr == null) return;
#if UNITY_EDITOR
            if (!Application.isPlaying) DestroyImmediate(sr);
            else Destroy(sr);
#else
            Destroy(sr);
#endif
        }

        private static void DestroyAllChildren(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
            {
                var child = t.GetChild(i);
                if (child == null) continue;
#if UNITY_EDITOR
                if (!Application.isPlaying) DestroyImmediate(child.gameObject);
                else Destroy(child.gameObject);
#else
                Destroy(child.gameObject);
#endif
            }
        }

        public void SetCleared(bool cleared)
        {
            isCleared = cleared;
            // 可以根据 cleard 状态调整显示（如变灰、关闭点击）
        }

        /// <summary>
        /// 玩家踩到此格子或点击此节点时调用。
        /// 这里根据节点类型创建 EncounterContext 并启动遭遇。
        /// </summary>
        public void Interact()
        {
            if (isCleared) return;

            var ctx = new EncounterContext();
            ctx.bossId = BossId;

            // 设置奖励配置 id
            switch (type)
            {
                case ChapterNodeType.NormalEnemy:
                    ctx.rewardProfileId = "Normal";
                    ctx.policy = ReturnPolicy.ReturnToChapter;
                    ctx.gateKind = GateKind.None;
                    break;
                case ChapterNodeType.EliteEnemy:
                    ctx.rewardProfileId = "Elite";
                    ctx.policy = ReturnPolicy.ReturnToChapter;
                    ctx.gateKind = GateKind.None;
                    break;
                case ChapterNodeType.Merchant:
                    ctx.rewardProfileId = "Normal"; // 商人遭遇仍然回章，奖励普通掉落
                    ctx.policy = ReturnPolicy.ReturnToChapter;
                    ctx.gateKind = GateKind.None;
                    break;
                case ChapterNodeType.Mystery:
                    ctx.rewardProfileId = "Normal"; // 神秘事件可按需修改为单独配置
                    ctx.policy = ReturnPolicy.ReturnToChapter;
                    ctx.gateKind = GateKind.None;
                    break;
                case ChapterNodeType.Treasure:
                    ctx.rewardProfileId = "Chest";
                    ctx.policy = ReturnPolicy.ReturnToChapter;
                    ctx.gateKind = GateKind.None;
                    break;
                case ChapterNodeType.Gate_Left:
                    ctx.rewardProfileId = "BossGate";
                    ctx.policy = ReturnPolicy.ExitChapter;
                    ctx.gateKind = GateKind.LeftGate;
                    ctx.nextChapterId = "Act2_LeftBiome";
                    break;
                case ChapterNodeType.Gate_Right:
                    ctx.rewardProfileId = "BossGate";
                    ctx.policy = ReturnPolicy.ExitChapter;
                    ctx.gateKind = GateKind.RightGate;
                    ctx.nextChapterId = "Act2_RightBiome";
                    break;
                case ChapterNodeType.Gate_Skip:
                    ctx.rewardProfileId = "BossGate";
                    ctx.policy = ReturnPolicy.ExitChapter;
                    ctx.gateKind = GateKind.SkipGate;
                    ctx.nextChapterId = "Act3_StarreachPeak";
                    break;
                case ChapterNodeType.Boss:
                    ctx.rewardProfileId = "BossGate";
                    ctx.policy = ReturnPolicy.ExitChapter;
                    ctx.gateKind = GateKind.None;
                    ctx.nextChapterId = null; // 后续由 BattleOutcomeUI 决定去哪里
                    break;
                default:
                    ctx.rewardProfileId = "Normal";
                    ctx.policy = ReturnPolicy.ReturnToChapter;
                    ctx.gateKind = GateKind.None;
                    break;
            }

            // 保存地图状态（除 ExitChapter 外）
            if (ctx.policy == ReturnPolicy.ReturnToChapter)
            {
                ChapterMapManager.Instance.SaveMapState();
            }

            // 传递上下文给 EncounterNode 并启动战斗或事件
            var encounter = GetComponent<EncounterNode>();
            if (encounter != null)
            {
                encounter.StartEncounter(ctx);
            }
            else
            {
                // 如果没有 EncounterNode，则直接标记为清理并返回
                SetCleared(true);
            }
        }
    }
}
