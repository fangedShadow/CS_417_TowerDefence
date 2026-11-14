using UnityEngine;

namespace TowerDefence.Shared
{
    // One shared objective for every faction. Wave/endgame systems subscribe to Health.Died.
    [RequireComponent(typeof(CombatHealth))]
    public sealed class CenterBase : MonoBehaviour
    {
        [SerializeField, Min(1)] private float maxHealth = 2000;
        [SerializeField, Min(0)] private float armor = 25;
        public float MaxHealth => maxHealth;
        public CombatHealth Health => GetComponent<CombatHealth>();
        private void Awake() => ResetHealth();
        public void ResetHealth() => Health.Initialize(maxHealth, armor);
    }
}
