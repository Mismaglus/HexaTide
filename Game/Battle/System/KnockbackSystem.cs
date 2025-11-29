using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Core.Hex;
using Game.Units;
using Game.Grid;
using Game.Battle.Combat;

namespace Game.Battle
{
    public class KnockbackSystem : MonoBehaviour
    {
        [Header("Config")]
        public int damagePerTileRemaining = 10; // 每格剩余击退距离转化的碰撞伤害
        public float knockbackSpeed = 15f;      // 击退飞行速度

        private GridOccupancy _occupancy;
        private BattleHexGrid _grid;
        private Dictionary<HexCoords, HexCell> _cellCache;

        void Awake()
        {
            _occupancy = FindFirstObjectByType<GridOccupancy>();
            _grid = FindFirstObjectByType<BattleHexGrid>();
        }

        private void RebuildCellCache()
        {
            _cellCache = new Dictionary<HexCoords, HexCell>();
            var cells = FindObjectsByType<HexCell>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var c in cells) _cellCache[c.Coords] = c;
        }

        // === API: 施加击退 ===
        public void ApplyKnockback(BattleUnit attacker, BattleUnit victim, int force)
        {
            if (victim == null || force <= 0) return;
            StartCoroutine(PerformKnockback(attacker, victim, force));
        }

        private IEnumerator PerformKnockback(BattleUnit attacker, BattleUnit victim, int force)
        {
            if (_cellCache == null) RebuildCellCache();

            HexCoords startCoords = victim.UnitRef.Coords;
            HexCoords attackerCoords = attacker.UnitRef.Coords;

            Vector3 worldDir = (victim.transform.position - attacker.transform.position).normalized;
            int bestDir = GetBestHexDirection(attackerCoords, worldDir);

            HexCoords current = startCoords;
            int remainingForce = force;
            bool collided = false;
            BattleUnit collisionUnit = null;

            // 1. 计算逻辑落点
            while (remainingForce > 0)
            {
                HexCoords next = current.Neighbor(bestDir);

                // Check 1: Terrain Block
                bool isWalkable = false;
                if (_cellCache.TryGetValue(next, out var cell)) isWalkable = cell.IsTerrainWalkable;
                else if (_grid.recipe != null)
                {
                    // 简单的防错：如果没有Cell组件但没出界，暂时放行
                    Debug.LogWarning($"[Knockback] Missing HexCell at {next}");
                }

                if (!isWalkable)
                {
                    Debug.Log("Knockback hit Wall/Obstacle!");
                    collided = true;
                    break;
                }

                // Check 2: Unit Block
                if (_occupancy.TryGetUnitAt(next, out var obstacleUnit))
                {
                    Debug.Log($"Knockback hit unit: {obstacleUnit.name}");
                    collisionUnit = obstacleUnit.GetComponent<BattleUnit>();
                    collided = true;
                    break;
                }

                current = next;
                remainingForce--;
            }

            // 2. 执行逻辑位移与视觉动画
            if (!current.Equals(startCoords))
            {
                Vector3 startPos = victim.transform.position;

                // 逻辑瞬间更新 (ForceMove 会触发 OnMoveFinished，更新 GridOccupancy)
                victim.UnitRef.ForceMove(current);

                Vector3 endPos = victim.transform.position;
                victim.transform.position = startPos; // 视觉拉回起点

                var feedback = victim.GetComponent<UnitVisualFeedback>();
                if (feedback != null)
                {
                    float distance = Vector3.Distance(startPos, endPos);
                    float duration = distance / Mathf.Max(1f, knockbackSpeed);

                    // 播放动画并等待
                    yield return feedback.PlayKnockback(startPos, endPos, duration);
                }
                else
                {
                    victim.transform.position = endPos;
                    yield return null;
                }
            }

            // 3. 结算碰撞伤害
            if (collided)
            {
                int collisionDmg = remainingForce * damagePerTileRemaining;
                if (collisionDmg > 0)
                {
                    var feedback = victim.GetComponent<UnitVisualFeedback>();
                    if (feedback) feedback.PlayHit();

                    victim.TakeDamage(collisionDmg, attacker);

                    if (collisionUnit != null)
                    {
                        collisionUnit.TakeDamage(collisionDmg / 2, attacker);
                        var otherFeedback = collisionUnit.GetComponent<UnitVisualFeedback>();
                        if (otherFeedback) otherFeedback.PlayHit();
                    }
                }
            }

            // 4. ⭐⭐⭐ 关键更新：击退结束，位置变了，可能也死人了，必须刷新全场意图
            if (BattleIntentSystem.Instance != null)
            {
                Debug.Log("[Knockback] Updating Intents after physics resolution...");
                BattleIntentSystem.Instance.RefreshEnemyList(); // 有人可能死了，先刷新列表
                BattleIntentSystem.Instance.UpdateIntents();    // 再重算 AI 意图
            }
        }

        private int GetBestHexDirection(HexCoords origin, Vector3 worldDir)
        {
            float maxDot = -1f;
            int bestIdx = 0;

            for (int i = 0; i < 6; i++)
            {
                HexCoords neighbor = origin.Neighbor(i);
                Vector3 hexDir = (neighbor.ToWorld(1f, true) - origin.ToWorld(1f, true)).normalized;
                float dot = Vector3.Dot(worldDir, hexDir);
                if (dot > maxDot)
                {
                    maxDot = dot;
                    bestIdx = i;
                }
            }
            return bestIdx;
        }
    }
}