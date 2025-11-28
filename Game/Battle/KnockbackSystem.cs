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
        public float knockbackSpeed = 15f;      // 击退飞行速度 (格/秒)，建议稍微快一点更有打击感

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

            // 1. 计算击退方向 (吸附到最近的六边形方向)
            Vector3 worldDir = (victim.transform.position - attacker.transform.position).normalized;
            int bestDir = GetBestHexDirection(attackerCoords, worldDir);

            // 2. 预计算最终落点 (Instant Logic Calculation)
            HexCoords current = startCoords;
            int remainingForce = force;
            bool collided = false;
            BattleUnit collisionUnit = null;

            while (remainingForce > 0)
            {
                HexCoords next = current.Neighbor(bestDir);

                // Check 1: Terrain Block
                if (!_cellCache.ContainsKey(next) || !_cellCache[next].IsTerrainWalkable)
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

            // 3. 执行逻辑位移与视觉动画
            if (!current.Equals(startCoords))
            {
                // A. 记录起点
                Vector3 startPos = victim.transform.position;

                // B. 逻辑瞬移 (Logical Snap)
                // 这会更新 Coords 并触发 OnMoveFinished，同时把 transform.position 设为终点
                victim.UnitRef.ForceMove(current);

                // C. 记录终点
                Vector3 endPos = victim.transform.position;

                // D. 视觉回滚 (把模型强行放回起点，准备播放动画)
                victim.transform.position = startPos;

                // E. 播放平滑动画 (Animation)
                var feedback = victim.GetComponent<UnitVisualFeedback>();
                if (feedback != null)
                {
                    float distance = Vector3.Distance(startPos, endPos);
                    float duration = distance / Mathf.Max(1f, knockbackSpeed);

                    // 等待动画播放完毕
                    yield return feedback.StartCoroutine(feedback.PlayKnockback(startPos, endPos, duration));
                }
                else
                {
                    // 没有反馈组件就直接跳到终点 (ForceMove 已经做了，这里只需补个帧)
                    victim.transform.position = endPos;
                    yield return null;
                }
            }

            // 4. 结算碰撞伤害 (Impact)
            if (collided)
            {
                int collisionDmg = remainingForce * damagePerTileRemaining;
                if (collisionDmg > 0)
                {
                    // 播放受击反馈 (这是撞墙后的震动，和滑行是分开的)
                    var feedback = victim.GetComponent<UnitVisualFeedback>();
                    if (feedback) feedback.PlayHit();

                    // 扣血
                    victim.TakeDamage(collisionDmg, attacker);

                    if (collisionUnit != null)
                    {
                        collisionUnit.TakeDamage(collisionDmg / 2, attacker);
                        // 被撞的人也播个受击
                        var otherFeedback = collisionUnit.GetComponent<UnitVisualFeedback>();
                        if (otherFeedback) otherFeedback.PlayHit();
                    }
                }
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