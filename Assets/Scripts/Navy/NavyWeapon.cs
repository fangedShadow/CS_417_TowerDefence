using TowerDefence.Shared;
using UnityEngine;

namespace TowerDefence.Navy
{
    [RequireComponent(typeof(TowerRuntime))]
    public sealed class NavyWeapon : MonoBehaviour
    {
        [SerializeField] private Transform yawPivot;
        [SerializeField] private Transform mountPoint;
        [SerializeField] private Transform[] muzzles;
        [SerializeField] private NavyProjectile projectilePrefab;
        [SerializeField] private LayerMask enemyLayers = ~0;
        [SerializeField, Min(0.05f)] private float scanInterval = 0.1f;
        private float scanRemaining;
        private TowerRuntime runtime;
        private IEnemyTarget target;
        public Transform MountPoint => mountPoint != null ? mountPoint : transform;
        public Transform[] Muzzles => muzzles;
        public NavyProjectile ProjectilePrefab => projectilePrefab;
        public int ShotsFired { get; private set; }
        private void Awake() => runtime = GetComponent<TowerRuntime>();
        private void Update()
        {
            if (!runtime.CanReceiveBuff) return;
            scanRemaining -= Time.deltaTime;
            if (scanRemaining <= 0)
            {
                target = NavyTargeting.Nearest(transform.position, runtime.AttackRange,
                    runtime.Stats.Targets, enemyLayers);
                scanRemaining = scanInterval;
            }
            if (!NavyTargeting.Valid(target, runtime.Stats.Targets)) return;
            AimAt(target.AimTransform.position);
            TryFire(target);
        }
        private void AimAt(Vector3 position)
        {
            if (yawPivot == null) return;
            Vector3 heading = position - yawPivot.position;
            heading.y = 0;
            if (heading.sqrMagnitude > 0.001f) yawPivot.rotation = Quaternion.LookRotation(heading);
        }
        public bool TryFire(IEnemyTarget enemy)
        {
            if (runtime == null) runtime = GetComponent<TowerRuntime>();
            if (!isActiveAndEnabled || !runtime.CanReceiveBuff || runtime.FireCooldown > 0 || runtime.AbilityCooldown > 0 ||
                projectilePrefab == null || muzzles == null || muzzles.Length == 0 ||
                !NavyTargeting.Valid(enemy, runtime.Stats.Targets) ||
                (enemy.AimTransform.position - transform.position).sqrMagnitude >
                runtime.AttackRange * runtime.AttackRange) return false;
            AimAt(enemy.AimTransform.position);
            int fired = 0;
            foreach (var muzzle in muzzles)
            {
                if (muzzle == null) continue;
                var projectile = Instantiate(projectilePrefab, muzzle.position, muzzle.rotation);
                projectile.Launch(runtime, enemy.AimTransform.position - muzzle.position);
                fired++;
            }
            if (fired == 0) return false;
            ShotsFired += fired;
            runtime.FireCooldown = runtime.AttackInterval;
            if (projectilePrefab.Kind == NavyProjectileKind.Wave)
                runtime.AbilityCooldown = runtime.Stats.AbilityCooldown;
            return true;
        }
    }
}
