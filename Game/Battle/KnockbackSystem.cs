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
        public float knockbackSpeed = 10f;

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

            // 2. 计算路径
            HexCoords current = startCoords;
            int remainingForce = force;
            bool collided = false;
            BattleUnit collisionUnit = null;

            while (remainingForce > 0)
            {
                HexCoords next = current.Neighbor(bestDir);

                // Check 1: 是否出界/撞墙 (Terrain)
                if (!_cellCache.ContainsKey(next) || !_cellCache[next].IsTerrainWalkable)
                {
                    Debug.Log("Knockback hit Wall/Obstacle!");
                    collided = true;
                    break;
                }

                // Check 2: 是否撞人 (Unit)
                if (_occupancy.TryGetUnitAt(next, out var obstacleUnit))
                {
                    Debug.Log($"Knockback hit unit: {obstacleUnit.name}");
                    collisionUnit = obstacleUnit.GetComponent<BattleUnit>();
                    collided = true;
                    break;
                }

                // 无阻挡，继续飞
                current = next;
                remainingForce--;
            }

            // 3. 执行位移 (Visual)
            // 这里简单用 UnitMover 的 Warp，你可以换成 Lerp 动画
            // 为了表现力，我们暂时瞬移，理想情况是播放滑动动画
            victim.UnitRef.WarpTo(current);

            // 4. 结算碰撞伤害 (双输规则)
            if (collided)
            {
                int collisionDmg = remainingForce * damagePerTileRemaining;
                if (collisionDmg > 0)
                {
                    // 受击者扣血
                    victim.TakeDamage(collisionDmg, attacker);

                    // 如果撞到了人，那个人也扣血 (双输)
                    if (collisionUnit != null)
                    {
                        collisionUnit.TakeDamage(collisionDmg / 2, attacker); // 被撞的人受一半伤害
                    }
                }

                // 播放震动反馈
                var feedback = victim.GetComponent<UnitVisualFeedback>();
                if (feedback) feedback.PlayHit();
            }

            yield return null;
        }

        // 根据世界方向向量，找到最接近的六边形邻居索引 (0-5)
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