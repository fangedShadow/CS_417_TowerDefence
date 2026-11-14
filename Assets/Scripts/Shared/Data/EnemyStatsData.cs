using UnityEngine;

namespace TowerDefence.Shared
{
    [CreateAssetMenu(menuName = "Tower Defence/Data/Enemy Stats")]
    public sealed class EnemyStatsData : ScriptableObject
    {
        [Header("Identity and targeting")]
        public string EnemyId;
        public EnemyKind EnemyType;
        public Faction Faction = Faction.Navy;
        public TargetDomain MovementType = TargetDomain.Water;
        public TargetDomain Targets = TargetDomain.Ground | TargetDomain.Water;
        [Header("Health, movement, and attack")]
        [Tooltip("Runtime CurrentHealth is initialized from this value on each instance.")]
        [Min(1)] public float MaxHealth = 100;
        [Min(0)] public float Armor;
        [Min(0)] public float MovementSpeed = 3;
        [Min(0)] public float AttackDamage = 10;
        [Min(0)] public float AttackRange = 4;
        [Min(0.05f)] public float AttackInterval = 1;
        [Header("Rewards")]
        [Min(0)] public int MoneyReward = 10;
        [Min(0)] public int ExpReward = 5;
        [Header("Status and damage resistance")]
        [Range(0, 1)] public float StatusEffectResistance;
        [Range(0, 1)] public float SlowResistance;
        [Range(0, 1)] public float KineticResistance;
        [Range(0, 1)] public float EnergyResistance;
        [Range(0, 1)] public float PoisonResistance;
    }
}
