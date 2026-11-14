using UnityEngine;

namespace TowerDefence.Shared
{
    [CreateAssetMenu(menuName = "Tower Defence/Data/Player Resources")]
    public sealed class PlayerResourceConfig : ScriptableObject
    {
        [Header("Exactly three independent resource balances")]
        [Min(0)] public int InitialMoney = 250;
        [Min(0)] public int InitialExp;
        [Min(0)] public int InitialUpgradePoints;
        public PlayerResources CreateRuntime() =>
            new PlayerResources(InitialMoney, InitialExp, InitialUpgradePoints);
    }
}
