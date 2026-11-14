using UnityEngine;

namespace TowerDefence.Shared
{
    [RequireComponent(typeof(CombatHealth), typeof(StatusEffects))]
    public sealed class EnemyRuntime : MonoBehaviour, IEnemyTarget
    {
        [SerializeField] private EnemyStatsData stats;
        [SerializeField] private Transform aimPoint;
        public EnemyStatsData Stats => stats;
        public CombatHealth Health { get; private set; }
        public StatusEffects Effects { get; private set; }
        public Transform AimTransform => aimPoint != null ? aimPoint : transform;
        public TargetDomain Domain => stats != null ? stats.MovementType : TargetDomain.None;
        public string EnemyId => stats != null ? stats.EnemyId : string.Empty;
        public bool IsAlive => isActiveAndEnabled && Health != null && Health.IsAlive;
        public float CurrentHealth => Health != null ? Health.CurrentHealth : 0;
        public float MovementSpeed => stats != null ? Mathf.Max(0, stats.MovementSpeed) *
            (Effects != null ? Effects.SpeedMultiplier : 1) : 0;

        private void Awake() => ResetRuntime();
        public void ResetRuntime()
        {
            Health = GetComponent<CombatHealth>();
            Effects = GetComponent<StatusEffects>();
            Effects.Clear();
            if (stats != null) Health.Initialize(stats.MaxHealth, stats.Armor,
                stats.KineticResistance, stats.EnergyResistance, stats.PoisonResistance);
        }
        public void TakeDamage(DamageInfo damage) => Health.TakeDamage(damage);
    }
}
