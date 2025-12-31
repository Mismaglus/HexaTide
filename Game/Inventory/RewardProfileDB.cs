using UnityEngine;
using System.Collections.Generic;

namespace Game.Inventory
{
    /// <summary>
    /// RewardProfile 的数据库，方便通过 profileId 查找配置。
    /// </summary>
    [CreateAssetMenu(menuName = "HexaTide/Reward/Reward Profile DB", fileName = "RewardProfileDB")]
    public class RewardProfileDB : ScriptableObject
    {
        [Tooltip("所有奖励配置列表，每个 profileId 应唯一")]
        public List<RewardProfileSO> profiles = new List<RewardProfileSO>();

        /// <summary>
        /// 根据 profileId 返回 RewardProfileSO，找不到时返回 null。
        /// </summary>
        public RewardProfileSO GetProfile(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var p in profiles)
            {
                if (p != null && p.profileId == id)
                    return p;
            }
            return null;
        }
    }
}
