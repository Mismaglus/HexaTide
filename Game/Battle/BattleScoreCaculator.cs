// Scripts/Game/Battle/BattleScoreCalculator.cs
using UnityEngine;

namespace Game.Battle
{
    /// <summary>
    /// Calculates and formats the result score for the battle.
    /// Currently a dummy implementation for the Defeat screen.
    /// </summary>
    public static class BattleScoreCalculator
    {
        public static string GetScoreReport()
        {
            // In the future, this will read from BattleStateMachine or a statistics tracker
            // e.g. int turns = BattleStateMachine.Instance.TurnCount;

            System.Text.StringBuilder sb = new System.Text.StringBuilder();

            sb.AppendLine("<b>Battle Statistics</b>");
            sb.AppendLine("----------------");
            sb.AppendLine($"Turns Survived: {Random.Range(3, 15)}");
            sb.AppendLine($"Enemies Slain: {Random.Range(0, 5)}");
            sb.AppendLine($"Damage Dealt: {Random.Range(500, 2000)}");
            sb.AppendLine("");
            sb.AppendLine($"<size=150%>Rank: {GetRandomRank()}</size>");

            return sb.ToString();
        }

        private static string GetRandomRank()
        {
            string[] ranks = { "D", "C", "B", "A", "S" };
            return ranks[Random.Range(0, ranks.Length)];
        }
    }
}