using System.Collections.Generic;
using UnityEngine;

namespace TowerDefence.Shared
{
    [RequireComponent(typeof(CombatHealth))]
    public sealed class StatusEffects : MonoBehaviour, IStatusEffectReceiver
    {
        private sealed class Effect
        {
            public float Slow, SlowRemaining, Poison, PoisonRemaining, TickInterval, TickRemaining;
        }
        private readonly Dictionary<Object, Effect> effects = new Dictionary<Object, Effect>();
        private readonly List<Object> expired = new List<Object>();
        public float SpeedMultiplier { get; private set; } = 1;
        public float SlowRemaining { get; private set; }
        public float PoisonRemaining { get; private set; }
        private EnemyStatsData Stats => GetComponent<EnemyRuntime>()?.Stats;
        private float DurationScale => 1 - Mathf.Clamp01(Stats != null ? Stats.StatusEffectResistance : 0);

        private Effect Get(Object source)
        {
            if (!effects.TryGetValue(source, out var effect))
            {
                effect = new Effect();
                effects.Add(source, effect);
            }
            return effect;
        }
        public void ApplySlow(Object sourceId, float strength, float duration)
        {
            if (duration <= 0 || DurationScale <= 0) return;
            var effect = Get(sourceId);
            effect.Slow = Mathf.Clamp01(strength) * (1 - Mathf.Clamp01(Stats != null ? Stats.SlowResistance : 0));
            effect.SlowRemaining = duration * DurationScale;
            RefreshVisibleValues();
        }
        public void ApplyPoison(Object sourceId, float damagePerTick, float duration, float tickInterval)
        {
            if (damagePerTick <= 0 || duration <= 0 || DurationScale <= 0) return;
            var effect = Get(sourceId);
            float interval = Mathf.Max(0.05f, tickInterval);
            // Refresh duration without restarting the tick, which otherwise starves DoT.
            if (effect.PoisonRemaining <= 0) effect.TickRemaining = interval;
            effect.Poison = damagePerTick;
            effect.PoisonRemaining = duration * DurationScale;
            effect.TickInterval = interval;
            RefreshVisibleValues();
        }
        private void Update() => Advance(Time.deltaTime);
        public void Advance(float deltaTime)
        {
            if (deltaTime <= 0) return;
            Effect strongest = null;
            foreach (var effect in effects.Values)
                if (effect.PoisonRemaining > 0 && (strongest == null ||
                    effect.Poison / effect.TickInterval > strongest.Poison / strongest.TickInterval))
                    strongest = effect;
            expired.Clear();
            float poisonDamage = 0;
            foreach (var pair in effects)
            {
                var effect = pair.Value;
                if (effect.PoisonRemaining > 0)
                {
                    effect.TickRemaining -= Mathf.Min(deltaTime, effect.PoisonRemaining);
                    if (effect.TickRemaining <= 0)
                    {
                        int ticks = 1 + Mathf.FloorToInt(-effect.TickRemaining / effect.TickInterval);
                        if (effect == strongest) poisonDamage += ticks * effect.Poison;
                        effect.TickRemaining += ticks * effect.TickInterval;
                    }
                }
                effect.SlowRemaining = Mathf.Max(0, effect.SlowRemaining - deltaTime);
                effect.PoisonRemaining = Mathf.Max(0, effect.PoisonRemaining - deltaTime);
                if (effect.SlowRemaining <= 0 && effect.PoisonRemaining <= 0) expired.Add(pair.Key);
            }
            foreach (Object id in expired) effects.Remove(id);
            RefreshVisibleValues();
            if (poisonDamage > 0) GetComponent<CombatHealth>().TakeDamage(
                new DamageInfo(poisonDamage, DamageKind.Poison, this));
        }
        private void RefreshVisibleValues()
        {
            float slow = 0;
            SlowRemaining = PoisonRemaining = 0;
            foreach (var effect in effects.Values)
            {
                if (effect.SlowRemaining > 0) slow = Mathf.Max(slow, effect.Slow);
                SlowRemaining = Mathf.Max(SlowRemaining, effect.SlowRemaining);
                PoisonRemaining = Mathf.Max(PoisonRemaining, effect.PoisonRemaining);
            }
            SpeedMultiplier = 1 - slow;
        }
        public void Clear() { effects.Clear(); RefreshVisibleValues(); }
        private void OnDisable() => Clear();
    }
}
