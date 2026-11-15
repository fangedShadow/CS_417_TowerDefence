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
        [SerializeField,Min(1)] private float turnSpeed=160;
        private int lastAimFrame=-1;
        private float scanRemaining;
        private TowerRuntime runtime;
        private IEnemyTarget target;
        private int shotStep, barrelCursor;
        public Transform MountPoint => mountPoint != null ? mountPoint : transform;
        public Transform[] Muzzles => muzzles;
        public NavyProjectile ProjectilePrefab => projectilePrefab;
        public float ShotInterval
        {
            get
            {
                if(runtime==null)runtime=GetComponent<TowerRuntime>();
                bool sequenced=runtime.Kind==TowerKind.Turret&&muzzles!=null&&muzzles.Length>1&&runtime.Stats.BarrelShotInterval>0;
                return sequenced?runtime.Stats.BarrelShotInterval*runtime.AttackInterval/Mathf.Max(.05f,runtime.Stats.AttackInterval):runtime.AttackInterval;
            }
        }
        public Vector3? TargetAim => HasValidTarget ? PredictedAim(target,transform.position) : (Vector3?)null;
        public int ShotsFired { get; private set; }
        public event System.Action<Transform, NavyProjectile> Fired;
        public bool HasValidTarget => NavyTargeting.Valid(target, runtime != null && runtime.Stats != null ? runtime.Stats.Targets : TargetDomain.None);
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
            AimAt(PredictedAim(target,transform.position));
            TryFire(target);
        }
        private void AimAt(Vector3 position)
        {
            if (yawPivot == null || lastAimFrame==Time.frameCount) return;
            lastAimFrame=Time.frameCount;
            Vector3 heading = position - yawPivot.position;
            heading.y = 0;
            if (heading.sqrMagnitude > 0.001f) yawPivot.rotation = Quaternion.RotateTowards(yawPivot.rotation,Quaternion.LookRotation(heading),turnSpeed*Time.deltaTime);
        }
        public bool TryFire(IEnemyTarget enemy)
        {
            if (runtime == null) runtime = GetComponent<TowerRuntime>();
            if (!isActiveAndEnabled || !runtime.CanReceiveBuff || runtime.FireCooldown > 0 || runtime.AbilityCooldown > 0 ||
                projectilePrefab == null || muzzles == null || muzzles.Length == 0 ||
                !NavyTargeting.Valid(enemy, runtime.Stats.Targets) ||
                (enemy.AimTransform.position - transform.position).sqrMagnitude >
                runtime.AttackRange * runtime.AttackRange) return false;
            AimAt(PredictedAim(enemy,transform.position));
            int fired = 0;
            bool sequenced = runtime.Kind == TowerKind.Turret && muzzles.Length > 1;
            // L2: one alternating barrel. L3: groups of 1, 2, 1, rotating the
            // starting barrel each cycle so every authored muzzle participates.
            int count = sequenced ? muzzles.Length >= 3 && shotStep % 3 == 1 ? 2 : 1 : muzzles.Length;
            for (int i = 0; i < count; i++)
            {
                var muzzle = muzzles[sequenced ? barrelCursor++ % muzzles.Length : i];
                if (muzzle == null) continue;
                var projectile = Instantiate(projectilePrefab, muzzle.position, muzzle.rotation);
                projectile.Launch(runtime, PredictedAim(enemy,muzzle.position) - muzzle.position);
                Fired?.Invoke(muzzle, projectile);
                fired++;
            }
            if (fired == 0) return false;
            ShotsFired += fired;
            shotStep++;
            runtime.FireCooldown = ShotInterval;
            if (projectilePrefab.Kind == NavyProjectileKind.Wave)
                runtime.AbilityCooldown = runtime.Stats.AbilityCooldown;
            return true;
        }
        private Vector3 PredictedAim(IEnemyTarget enemy,Vector3 from)
        {
            var point=enemy.AimTransform.position;
            var moving=(enemy as Component)?.GetComponent<NavalEnemy>();
            if(moving==null||moving.Velocity.sqrMagnitude<.0001f)return point;
            var velocity=moving.Velocity;var offset=point-from;float speed=Mathf.Max(.1f,runtime.Stats.ProjectileSpeed);
            float a=Vector3.Dot(velocity,velocity)-speed*speed,b=2*Vector3.Dot(offset,velocity),c=offset.sqrMagnitude,t=0;
            if(Mathf.Abs(a)<.0001f){if(Mathf.Abs(b)>.0001f)t=-c/b;}
            else
            {
                float discriminant=b*b-4*a*c;
                if(discriminant>=0)
                {
                    float first=(-b-Mathf.Sqrt(discriminant))/(2*a),second=(-b+Mathf.Sqrt(discriminant))/(2*a);
                    t=first>0?first:second;
                }
            }
            return point+velocity*Mathf.Clamp(t,0,(runtime.AttackRange+5)/speed);
        }
    }
}
