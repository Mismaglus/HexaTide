using UnityEngine;

namespace Game.Battle.Status
{
    /// <summary>
    /// 疾跑状态定义。
    /// 这是一个标记类 (Marker Class)。
    /// UnitStatusController 会检测单位身上是否有这个类型的状态，从而开启“无视地形/ZOC”的移动模式。
    /// </summary>
    [CreateAssetMenu(menuName = "HexBattle/Status/Variants/Sprint")]
    public class SprintStatusDefinition : StatusDefinition
    {
        // 这里可以留空。
        // 它的主要作用是类型识别： status.Definition is SprintStatusDefinition

        // 如果以后你想给疾跑加特殊效果（比如：疾跑结束时自爆？或者疾跑时受到伤害增加？），
        // 可以在这里重写 OnTurnStart / ModifyIncomingDamage 等方法。
    }
}