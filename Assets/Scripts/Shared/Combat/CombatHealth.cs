using System;
using UnityEngine;

namespace TowerDefence.Shared
{
    // A local damage receiver, not a combat manager. Can be adapted to Billy's damage code.
    public sealed class CombatHealth : MonoBehaviour, IDamageable
    {
        [SerializeField] private bool destroyOnDeath = true;
        [SerializeField] private float currentHealth;
        [SerializeField] private float maxHealth = 100;
        private float armor, kineticResistance, energyResistance, poisonResistance;
        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth;
        public bool IsAlive => currentHealth > 0;
        public event Action<DamageInfo> Damaged;
        public event Action Died;

        public void Initialize(float maximum, float armor = 0, float kinetic = 0,
            float energy = 0, float poison = 0)
        {
            maxHealth = Mathf.Max(1, maximum);
            currentHealth = maxHealth;
            this.armor = Mathf.Max(0, armor);
            kineticResistance = Mathf.Clamp01(kinetic);
            energyResistance = Mathf.Clamp01(energy);
            poisonResistance = Mathf.Clamp01(poison);
        }
        public void TakeDamage(DamageInfo damage)
        {
            if (!IsAlive || damage.Amount <= 0) return;
            float resistance = damage.Kind == DamageKind.Poison ? poisonResistance :
                damage.Kind == DamageKind.Energy ? energyResistance : kineticResistance;
            float mitigation = damage.Kind == DamageKind.Kinetic ?
                100f / (100f + Mathf.Max(0, armor - damage.ArmorPenetration)) : 1;
            currentHealth = Mathf.Max(0, currentHealth - damage.Amount * mitigation * (1 - resistance));
            Damaged?.Invoke(damage);
            if (!IsAlive)
            {
                Died?.Invoke();
                if (destroyOnDeath) Destroy(gameObject);
            }
        }
        public void Repair(float amount)
        {
            if (IsAlive) currentHealth = Mathf.Min(maxHealth, currentHealth + Mathf.Max(0, amount));
        }
    }
}
