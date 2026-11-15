using UnityEngine;

namespace TowerDefence.Shared
{
    [RequireComponent(typeof(CombatHealth), typeof(StatusEffects))]
    public sealed class EnemyRuntime : MonoBehaviour, IEnemyTarget
    {
        [SerializeField] private EnemyStatsData stats;
        [SerializeField] private Transform aimPoint;
        public EnemyStatsData Stats => stats;
        float speedMultiplier=1,damageMultiplier=1,frequencyMultiplier=1,rewardMultiplier=1;
        public float AttackDamage=>stats!=null?stats.AttackDamage*damageMultiplier:0;
        public float AttackInterval=>stats!=null?Mathf.Max(.1f,stats.AttackInterval/Mathf.Max(.1f,frequencyMultiplier)):1;
        public int MoneyReward=>stats!=null?Mathf.RoundToInt(stats.MoneyReward*rewardMultiplier):0;
        public int ExpReward=>stats!=null?Mathf.RoundToInt(stats.ExpReward*rewardMultiplier):0;
        public CombatHealth Health { get; private set; }
        public StatusEffects Effects { get; private set; }
        public Transform AimTransform => aimPoint != null ? aimPoint : transform;
        public TargetDomain Domain => stats != null ? stats.MovementType : TargetDomain.None;
        public string EnemyId => stats != null ? stats.EnemyId : string.Empty;
        public bool IsAlive => isActiveAndEnabled && Health != null && Health.IsAlive;
        public float CurrentHealth => Health != null ? Health.CurrentHealth : 0;
        public float MovementSpeed => stats != null ? Mathf.Max(0, stats.MovementSpeed) *
            speedMultiplier*(Effects != null ? Effects.SpeedMultiplier : 1) : 0;

        private void Awake() => ResetRuntime();
        public void ResetRuntime()
        {
            speedMultiplier=damageMultiplier=frequencyMultiplier=rewardMultiplier=1;
            Health = GetComponent<CombatHealth>();
            Effects = GetComponent<StatusEffects>();
            Effects.Clear();
            if (stats != null) Health.Initialize(stats.MaxHealth, stats.Armor,
                stats.KineticResistance, stats.EnergyResistance, stats.PoisonResistance);
        }
        public void ApplyDifficulty(float health,float damage,float armor,float speed,float frequency,float rewards)
        {
            if(stats==null)return;
            speedMultiplier=Mathf.Clamp(speed,1,1.5f);damageMultiplier=Mathf.Max(1,damage);
            frequencyMultiplier=Mathf.Clamp(frequency,1,1.8f);rewardMultiplier=Mathf.Clamp(rewards,1,2);
            Health.Initialize(stats.MaxHealth*Mathf.Max(1,health),stats.Armor+Mathf.Max(0,armor),stats.KineticResistance,stats.EnergyResistance,stats.PoisonResistance);
        }
        public void TakeDamage(DamageInfo damage) => Health.TakeDamage(damage);
    }
}
