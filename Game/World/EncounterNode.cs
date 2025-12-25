using UnityEngine;
using UnityEngine.SceneManagement;
using Game.Battle; // 引用 BattleContext

namespace Game.World
{
    public class EncounterNode : MonoBehaviour
    {
        [Header("Settings")]
        public string battleSceneName = "BattleScene";

        [Header("Default Context (Inspector)")]
        // Inspector 中配置的默认值，仅用于直接测试或特殊情况
        public EncounterContext defaultContext;

        /// <summary>
        /// Starts the encounter with a specific context passed from the Chapter Node.
        /// </summary>
        /// <param name="context">Context containing return policy and next destination.</param>
        public void StartEncounter(EncounterContext context)
        {
            Debug.Log($"[EncounterNode] Starting Encounter. Policy: {context.policy}, Gate: {context.gateKind}");

            // 1. Pass data to the static BattleContext
            BattleContext.EncounterContext = context;

            // 2. Load the Battle Scene
            if (string.IsNullOrEmpty(battleSceneName))
            {
                Debug.LogError("[EncounterNode] Battle Scene Name is empty!");
                return;
            }

            SceneManager.LoadScene(battleSceneName);
        }

        /// <summary>
        /// Overload for starting without args (e.g. from UnityEvent or Debug), using inspector defaults.
        /// </summary>
        public void StartEncounter()
        {
            Debug.LogWarning("[EncounterNode] Starting with Default/Inspector Context.");
            StartEncounter(defaultContext);
        }
    }
}