using UnityEngine;

namespace TowerDefence.Shared
{
    [CreateAssetMenu(menuName = "Tower Defence/Data/Tower Stats")]
    public sealed class TowerStatsData : ScriptableObject
    {
        [Header("Identity and upgrade level")]
        public string TowerId;
        public Faction Faction = Faction.Navy;
        public TowerKind Kind;
        public StructureCategory Category;
        [Min(1)] public int UpgradeLevel = 1;
        [Header("Health and combat")]
        [Tooltip("Runtime CurrentHealth is initialized from this value on each instance.")]
        [Min(1)] public float MaxHealth = 200;
        [Min(0)] public float Armor;
        [Min(0)] public float Damage = 10;
        [Min(0)] public float AttackRange = 8;
        [Min(0.05f)] public float AttackInterval = 1;
        public TargetDomain Targets = TargetDomain.Water;
        [Header("Projectiles and special attacks")]
        [Min(0.1f)] public float ProjectileSpeed = 15;
        [Min(0.01f)] public float ProjectileSize = 1;
        [Min(0)] public float SplashRadius;
        [Min(0)] public float ArmorPenetration;
        [Min(0)] public float WaveWidth = 5;
        [Min(0)] public float WaveRange = 18;
        [Min(0)] public float AbilityCooldown;
        [Header("Economy")]
        [Min(0)] public int PurchaseCost = 100;
        [Min(0)] public int UpgradeCost;
        [Min(0)] public int SellValue = 50;
        [Min(0)] public int RepairCost = 20;
        [Header("Slow and poison payload")]
        [Range(0, 1)] public float SlowStrength;
        [Min(0)] public float SlowDuration = 1.5f;
        [Min(0)] public float PoisonDamage;
        [Min(0)] public float PoisonDuration;
        [Min(0.05f)] public float PoisonTickInterval = 1;
        [Header("Support")]
        [Range(0, 2)] public float BuffStrength = 0.25f;
        [Min(0)] public float SupportRange = 8;
        [Header("Production: Money, EXP, Upgrade Points")]
        [Min(0)] public int ResourceGenerationAmount = 25;
        [Min(0)] public int ResourceExpGenerationAmount;
        [Min(0)] public int ResourceUpgradePointGenerationAmount;
        [Min(0.05f)] public float ResourceGenerationInterval = 10;
    }
}
