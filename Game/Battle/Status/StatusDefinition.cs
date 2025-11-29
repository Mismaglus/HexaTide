using UnityEngine;
using Game.Localization;
using Game.Battle;

namespace Game.Battle.Status
{
    public enum StatusType { Buff, Debuff, Neutral }
    public enum StackBehavior { None, AddDuration, MaxDuration, IncreaseStacks }

    [CreateAssetMenu(menuName = "HexBattle/Status/Definition (Basic)")]
    public class StatusDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string statusID;
        public Sprite icon;
        public StatusType type = StatusType.Debuff;

        public string LocalizedName => LocalizationManager.Get($"{statusID}_NAME");
        public string LocalizedDesc => LocalizationManager.Get($"{statusID}_DESC");

        [Header("Rules")]
        public int defaultDuration = 3;
        public bool isPermanent = false;
        public int maxStacks = 1;
        public StackBehavior stackBehavior = StackBehavior.MaxDuration;

        [Header("Timing")]
        [Tooltip("勾选：回合开始时扣除层数/时间（即时消耗）。\n不勾选：回合结束时扣除（延迟消耗）。")]
        public bool decreaseStackAtStart = true;

        // ⭐ 已移除：public bool isSprintState; 
        // 现在通过类类型 (SprintStatusDefinition) 来判断，保持基类干净。

        [Header("Visuals")]
        public Color effectColor = Color.white;
        public GameObject vfxPrefab;

        // === 核心逻辑钩子 ===

        public virtual void OnTurnStart(RuntimeStatus status, BattleUnit unit)
        {
            // 默认逻辑：如果勾选了 Start 扣减，且不是永久状态，则扣时间
            if (decreaseStackAtStart && !isPermanent)
            {
                status.TickDuration();
            }
        }

        public virtual void OnTurnEnd(RuntimeStatus status, BattleUnit unit)
        {
            // 默认逻辑：如果不勾选 Start 扣减，且不是永久状态，则在 End 扣时间
            if (!decreaseStackAtStart && !isPermanent)
            {
                status.TickDuration();
            }
        }

        public virtual void OnStackAdded(RuntimeStatus status, int amount) { }

        public virtual int ModifyIncomingDamage(RuntimeStatus status, int rawDamage, BattleUnit attacker)
        {
            return rawDamage;
        }
    }
}