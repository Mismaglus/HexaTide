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

        private HexCell _cell;

        public void Initialize(ChapterNodeType nodeType)
        {
            type = nodeType;
            isCleared = false;
            _cell = GetComponent<HexCell>();
            _cell.RefreshFogVisuals();
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
