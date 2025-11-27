// Scripts/Game/Battle/Status/StatusDefinition.cs
using UnityEngine;
using Game.Localization;
using Game.Battle; // 引用 BattleUnit

namespace Game.Battle.Status
{
    public enum StatusType { Buff, Debuff, Neutral }
    public enum StackBehavior { None, AddDuration, MaxDuration, IncreaseStacks }

    // 基础类，允许继承
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

        [Header("Visuals")]
        public Color effectColor = Color.white;
        public GameObject vfxPrefab;

        // === 核心逻辑钩子 (Virtual Methods) ===

        // 1. 回合开始时 (星蚀 / 月痕 在这里触发)
        public virtual void OnTurnStart(RuntimeStatus status, BattleUnit unit) { }

        // 2. 回合结束时 (夜烬 在这里触发，普通 Buff 也就是在这里扣时间)
        public virtual void OnTurnEnd(RuntimeStatus status, BattleUnit unit)
        {
            // 默认行为：如果是临时状态，回合结束扣时间
            status.TickDuration();
        }

        // 3. 堆叠层数增加时 (比如再次施加)
        public virtual void OnStackAdded(RuntimeStatus status, int amount) { }

        // 4. 受伤修正 (月痕的易伤逻辑在这里)
        public virtual int ModifyIncomingDamage(RuntimeStatus status, int rawDamage, BattleUnit attacker)
        {
            return rawDamage;
        }
    }
}