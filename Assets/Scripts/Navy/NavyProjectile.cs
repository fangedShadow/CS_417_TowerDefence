using System.Collections.Generic;
using TowerDefence.Shared;
using UnityEngine;

namespace TowerDefence.Navy
{
    public enum NavyProjectileKind { Round, Cannonball, HeavyCannonball, Wave, SlowBolt, ToxicBolt }

    public sealed class NavyProjectile : MonoBehaviour
    {
        [SerializeField] private NavyProjectileKind kind;
        [SerializeField, Min(0.01f)] private float collisionRadius = 0.12f;
        [SerializeField] private LayerMask targetLayers = ~0;
        private readonly HashSet<Transform> hit = new HashSet<Transform>();
        private DamageInfo damage;
        private TargetDomain domains;
        private Vector3 direction;
        private float speed, remainingDistance, radius, splashRadius;
        private bool launched, finished;
        private Object statusSource;
        private float slowStrength, slowDuration, poisonDamage, poisonDuration, poisonTickInterval;
        public NavyProjectileKind Kind => kind;
        public bool IsLaunched => launched && !finished;

        public void Launch(TowerRuntime owner, Vector3 heading)
        {
            var stats = owner.Stats;
            direction = heading.sqrMagnitude > 0.0001f ? heading.normalized : transform.forward;
            transform.rotation = Quaternion.LookRotation(direction);
            speed = Mathf.Max(0.1f, stats.ProjectileSpeed);
            remainingDistance = kind == NavyProjectileKind.Wave ? Mathf.Max(0.1f, owner.WaveTravelRange) :
                Mathf.Max(1, owner.AttackRange + 5);
            domains = stats.Targets;
            DamageKind damageKind = kind == NavyProjectileKind.Wave || kind == NavyProjectileKind.SlowBolt ?
                DamageKind.Energy : kind == NavyProjectileKind.ToxicBolt ? DamageKind.Poison : DamageKind.Kinetic;
            damage = new DamageInfo(owner.Damage, damageKind, owner, stats.ArmorPenetration);
            // Snapshot the launcher's configuration. Every bolt from it shares one status source,
            // so rapid hits refresh existing effects instead of accumulating per-projectile stacks.
            statusSource = owner;
            slowStrength = stats.SlowStrength;
            slowDuration = stats.SlowDuration;
            poisonDamage = kind == NavyProjectileKind.ToxicBolt ? stats.PoisonDamage : 0;
            poisonDuration = stats.PoisonDuration;
            poisonTickInterval = stats.PoisonTickInterval;
            splashRadius = Mathf.Max(0, stats.SplashRadius);
            float size = Mathf.Max(0.01f, stats.ProjectileSize);
            transform.localScale *= size;
            radius = collisionRadius * size;
            if (kind == NavyProjectileKind.Wave)
            {
                radius = Mathf.Max(0.05f, stats.WaveWidth * 0.5f);
                transform.localScale = new Vector3(stats.WaveWidth, 0.6f, 1.2f);
            }
            hit.Clear();
            launched = true;
            finished = false;
        }
        private void Update() => Advance(Time.deltaTime);
        public void Advance(float deltaTime)
        {
            if (!IsLaunched || deltaTime <= 0) return;
            float step = Mathf.Min(remainingDistance, speed * deltaTime);
            Vector3 start = transform.position;
            Vector3 end = start + direction * step;
            if (kind == NavyProjectileKind.Wave)
            {
                // Swept capsule avoids tunnelling; the hit set allows one hit per enemy per wave.
                foreach (var collider in Physics.OverlapCapsule(start, end, radius, targetLayers,
                    QueryTriggerInteraction.Collide))
                {
                    var target = collider.GetComponentInParent<IEnemyTarget>();
                    if (NavyTargeting.Valid(target, domains)) HitTarget(target);
                }
            }
            else
            {
                // Include initial overlaps (SphereCast alone does not report these).
                IEnemyTarget initial = null;
                foreach (var collider in Physics.OverlapSphere(start, radius, targetLayers,
                    QueryTriggerInteraction.Collide))
                {
                    var target = collider.GetComponentInParent<IEnemyTarget>();
                    if (NavyTargeting.Valid(target, domains)) { initial = target; break; }
                }
                if (initial != null) { Impact(initial, start); return; }
                var collisions = Physics.SphereCastAll(start, radius, direction, step,
                    targetLayers, QueryTriggerInteraction.Collide);
                System.Array.Sort(collisions, (a, b) => a.distance.CompareTo(b.distance));
                foreach (var collision in collisions)
                {
                    var target = collision.collider.GetComponentInParent<IEnemyTarget>();
                    if (!NavyTargeting.Valid(target, domains)) continue;
                    Impact(target, start + direction * collision.distance);
                    return;
                }
            }
            transform.position = end;
            remainingDistance -= step;
            if (remainingDistance <= 0) Finish();
        }
        private void Impact(IEnemyTarget direct, Vector3 position)
        {
            // Always hit the direct target, even when its aim point lies outside the splash sphere.
            HitTarget(direct);
            if (splashRadius > 0)
                foreach (var target in NavyTargeting.EnemiesInRange(position, splashRadius, domains, targetLayers))
                    HitTarget(target);
            Finish();
        }
        private void HitTarget(IEnemyTarget target)
        {
            if (!hit.Add(target.AimTransform)) return;
            target.TakeDamage(damage);
            if ((kind != NavyProjectileKind.SlowBolt && kind != NavyProjectileKind.ToxicBolt) ||
                !NavyTargeting.Valid(target, domains)) return;
            var receiver = (target as Component)?.GetComponentInParent<IStatusEffectReceiver>();
            if (receiver == null) return;
            receiver.ApplySlow(statusSource, slowStrength, slowDuration);
            if (poisonDamage > 0)
                receiver.ApplyPoison(statusSource, poisonDamage, poisonDuration, poisonTickInterval);
        }
        private void Finish() { finished = true; Destroy(gameObject); }
    }
}
