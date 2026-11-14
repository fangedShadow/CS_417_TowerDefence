using System.Collections.Generic;
using UnityEngine;

namespace TowerDefence.Shared
{
    [RequireComponent(typeof(CombatHealth))]
    public sealed class TowerRuntime : MonoBehaviour, ITowerBuffReceiver
    {
        [SerializeField] private TowerStatsData stats;
        [SerializeField] private TowerUpgradeData upgrade;
        [SerializeField] private float fireCooldown;
        [SerializeField] private float abilityCooldown;
        private readonly Dictionary<Object, TowerBuff> buffs = new Dictionary<Object, TowerBuff>();
        public TowerStatsData Stats => stats;
        public TowerUpgradeData Upgrade => upgrade;
        public CombatHealth Health { get; private set; }
        public Faction Faction => stats != null ? stats.Faction : Faction.Navy;
        public TowerKind Kind => stats != null ? stats.Kind : TowerKind.Resource;
        public bool CanReceiveBuff => isActiveAndEnabled && stats != null && Health != null && Health.IsAlive;
        public Transform BuffTransform => transform;
        public float CurrentHealth => Health != null ? Health.CurrentHealth : 0;
        public float FireCooldown { get => fireCooldown; set => fireCooldown = Mathf.Max(0, value); }
        public float AbilityCooldown { get => abilityCooldown; set => abilityCooldown = Mathf.Max(0, value); }
        private float Strongest(int field)
        {
            float result = 0;
            foreach (var buff in buffs.Values)
                result = Mathf.Max(result, field == 0 ? buff.Damage : field == 1 ? buff.Range : buff.DefenseFrequency);
            return result;
        }
        public float Damage => stats != null ? Mathf.Max(0, stats.Damage) * (1 + Strongest(0)) : 0;
        public float FoundationRangeMultiplier
        {
            get
            {
                // The nearest foundation applies once. Reparenting/removal needs no registrations
                // or permanent stat edits, and nested foundations cannot multiply their bonuses.
                for (var parent = transform.parent; parent != null; parent = parent.parent)
                {
                    var modifier = parent.GetComponent<ITowerRangeModifier>();
                    if (modifier != null) return Mathf.Max(1, modifier.GetRangeMultiplier(Faction));
                }
                return 1;
            }
        }
        public float RangeMultiplier => FoundationRangeMultiplier * (1 + Strongest(1));
        public float AttackRange => stats != null ? Mathf.Max(0, stats.AttackRange) * RangeMultiplier : 0;
        public float SupportRange => stats != null ? Mathf.Max(0, stats.SupportRange) * RangeMultiplier : 0;
        public float WaveTravelRange => stats != null ? Mathf.Max(0, stats.WaveRange) * RangeMultiplier : 0;
        public float AttackInterval => stats != null ? Mathf.Max(0.05f, stats.AttackInterval) /
            (1 + Strongest(2)) : 1;
        private void Awake() => ResetRuntime();
        public void ResetRuntime()
        {
            Health = GetComponent<CombatHealth>();
            buffs.Clear();
            fireCooldown = abilityCooldown = 0;
            if (stats != null) Health.Initialize(stats.MaxHealth, stats.Armor);
        }
        private void Update() => AdvanceCooldowns(Time.deltaTime);
        public void AdvanceCooldowns(float deltaTime)
        {
            if (deltaTime <= 0) return;
            fireCooldown = Mathf.Max(0, fireCooldown - deltaTime);
            abilityCooldown = Mathf.Max(0, abilityCooldown - deltaTime);
        }
        public void SetBuff(Object sourceId, TowerBuff buff) => buffs[sourceId] = buff;
        public void RemoveBuff(Object sourceId) => buffs.Remove(sourceId);
        private void OnDisable() => buffs.Clear();
    }
}
