using UnityEngine;

namespace TowerDefence.Shared
{
    [CreateAssetMenu(menuName = "Tower Defence/Data/Tower Upgrade")]
    public sealed class TowerUpgradeData : ScriptableObject
    {
        public TowerStatsData CurrentStats;
        public TowerStatsData NextStats;
        public GameObject NextPrefab;
        [Min(0)] public int UpgradePointCost;
        public bool HasNextLevel => NextStats != null && NextPrefab != null;
        public int MoneyCost => CurrentStats != null ? CurrentStats.UpgradeCost : 0;
        // Metadata only. The shared construction system owns spending and replacement.
    }
}
