using TowerDefence.Shared;
using UnityEngine;

namespace TowerDefence.Navy
{
    // Movement is shared with manual integrations. A wave can bind a water route and an existing projectile.
    [RequireComponent(typeof(EnemyRuntime))]
    public sealed class NavalEnemy : MonoBehaviour
    {
        private EnemyRuntime runtime;
        public EnemyRuntime Runtime => runtime != null ? runtime : runtime = GetComponent<EnemyRuntime>();
        public event System.Action<NavalEnemy,bool> Defeated;
        public event System.Action<NavalEnemy> Removed;
        public event System.Action<Vector3,Quaternion,NavyProjectile> Attacked;
        public bool AssaultActive { get; private set; }
        public int AttackCount { get; private set; }
        public Vector3 Velocity {get;private set;}
        public Vector3 AttackAim {get;private set;}
        public bool HasAttackTarget {get;private set;}
        NavyShipWeapons weapons;
        private Vector3[] route;
        private int waypoint;
        private CenterBase objective;
        private Vector3 attackPoint,muzzleLocal;
        private NavyProjectile ammunition;
        private float attackRemaining;
        private CombatHealth burstTarget;
        private Vector3 burstAim;
        private int burstRemaining;
        private float burstShotRemaining;
        public int PendingShots=>burstRemaining;
        public float BurstShotInterval=>Mathf.Max(.03f,Runtime.Stats.BurstShotInterval*Runtime.AttackInterval/Mathf.Max(.05f,Runtime.Stats.AttackInterval));
        public float AttackCycleInterval=>Mathf.Max(Runtime.AttackInterval,(Mathf.Clamp(Runtime.Stats.ShotsPerAttack,1,16)-1)*BurstShotInterval+.1f);
        private bool playerDamage;
        private CombatHealth health;
        private Collider hull;
        private float towerScanRemaining;
        private TowerRuntime passingTower;
        private void Awake(){runtime=GetComponent<EnemyRuntime>();health=GetComponent<CombatHealth>();hull=GetComponent<Collider>();weapons=GetComponent<NavyShipWeapons>();}
        private void OnEnable(){health.Damaged+=Damaged;health.Died+=Died;}
        public void BindAssault(Vector3[] waterRoute,CenterBase target,Vector3 targetPoint,NavyProjectile projectile,Vector3 muzzle)
        {
            route=waterRoute;waypoint=1;objective=target;attackPoint=targetPoint;ammunition=projectile;muzzleLocal=muzzle;
            CancelBurst();attackRemaining=0;towerScanRemaining=0;passingTower=null;playerDamage=false;AttackCount=0;AssaultActive=route!=null&&route.Length>1&&target!=null&&projectile!=null;
        }
        private void Update()=>AdvanceAssault(Time.deltaTime);
        public void AdvanceAssault(float deltaTime)
        {
            if(!AssaultActive||deltaTime<=0||!Runtime.IsAlive||objective==null||!objective.Health.IsAlive){Velocity=Vector3.zero;HasAttackTarget=false;CancelBurst();return;}
            HasAttackTarget=false;
            attackRemaining=Mathf.Max(0,attackRemaining-deltaTime);
            Vector3 heading=attackPoint-transform.position;heading.y=0;
            float range=Mathf.Max(.1f,Runtime.Stats.AttackRange);
            if(heading.magnitude<=range+.01f)
            {
                Velocity=Vector3.zero;
                if(burstRemaining>0&&burstTarget!=objective.Health)CancelBurst();
                AdvanceBurst(deltaTime);AttackAim=attackPoint;HasAttackTarget=true;
                if(attackRemaining<=0)
                {
                    BeginBurst(objective.Health,attackPoint);

                }
                return;
            }
            AdvanceBurst(deltaTime);
            AttackPassingTower(deltaTime);
            if(waypoint>=route.Length)waypoint=route.Length-1;
            var destination=route[waypoint];
            if(waypoint==route.Length-1)
            {
                var direction=heading.normalized;destination=attackPoint-direction*(range*.95f);destination.y=transform.position.y;
            }
            MoveTowards(destination,deltaTime);
            var offset=destination-transform.position;offset.y=0;
            if(offset.sqrMagnitude<.08f*.08f&&waypoint<route.Length-1)waypoint++;
        }
        public void StopAssault(){CancelBurst();AssaultActive=false;objective=null;Velocity=Vector3.zero;HasAttackTarget=false;}
        void AttackPassingTower(float deltaTime)
        {
            if(burstRemaining>0){AttackAim=burstAim;HasAttackTarget=true;return;}
            towerScanRemaining-=deltaTime;
            if(towerScanRemaining<=0)
            {
                towerScanRemaining=.25f;passingTower=null;float nearest=float.MaxValue;
                float radius=Runtime.Stats.AttackRange+(hull!=null?hull.bounds.extents.magnitude:2)+4;
                foreach(var hit in Physics.OverlapSphere(transform.position,radius,~(1<<2|1<<5|1<<8|1<<9),QueryTriggerInteraction.Ignore))
                {
                    var tower=hit.GetComponentInParent<TowerRuntime>();
                    if(tower==null||!tower.CanReceiveBuff||tower.Stats.Kind==TowerKind.Foundation)continue;
                    float gap=TowerGap(hit);
                    if(gap<=Runtime.Stats.AttackRange&&gap<nearest){nearest=gap;passingTower=tower;}
                }
            }
            if(passingTower==null||!passingTower.CanReceiveBuff)return;
            var target=passingTower.GetComponent<Collider>();if(target==null||TowerGap(target)>Runtime.Stats.AttackRange)return;
            Vector3 aim=target.bounds.center;AttackAim=aim;HasAttackTarget=true;
            if(attackRemaining<=0)BeginBurst(passingTower.Health,aim);
        }
        void BeginBurst(CombatHealth target,Vector3 aim)
        {
            if(burstRemaining>0||target==null||!target.IsAlive)return;
            burstTarget=target;burstAim=aim;burstRemaining=Mathf.Clamp(Runtime.Stats.ShotsPerAttack,1,16);
            burstShotRemaining=0;attackRemaining=AttackCycleInterval;
            EmitShot();
        }
        bool BurstTargetValid()
        {
            if(burstTarget==null||!burstTarget.IsAlive)return false;
            if(objective!=null&&burstTarget==objective.Health)
            {var offset=attackPoint-transform.position;offset.y=0;return offset.magnitude<=Runtime.Stats.AttackRange+.1f;}
            var tower=burstTarget.GetComponent<TowerRuntime>();var collider=burstTarget.GetComponent<Collider>();
            if(tower==null||!tower.CanReceiveBuff||collider==null||TowerGap(collider)>Runtime.Stats.AttackRange)return false;
            burstAim=collider.bounds.center;return true;
        }
        void AdvanceBurst(float deltaTime)
        {
            if(burstRemaining<=0)return;
            if(!BurstTargetValid()){CancelBurst();return;}
            AttackAim=burstAim;HasAttackTarget=true;burstShotRemaining-=deltaTime;
            while(burstRemaining>0&&burstShotRemaining<=0)EmitShot();
        }
        void EmitShot()
        {
            if(burstTarget==null||!burstTarget.IsAlive){CancelBurst();return;}
            var muzzle=weapons!=null?weapons.NextMuzzle(burstAim):null;
            Vector3 position=muzzle!=null?muzzle.position:transform.TransformPoint(muzzleLocal);
            var heading=burstAim-position;if(heading.sqrMagnitude<.0001f)heading=transform.forward;
            var shot=Instantiate(ammunition,position,Quaternion.LookRotation(heading));
            shot.LaunchAtStructure(Runtime,burstTarget,burstAim);AttackCount++;
            weapons?.NotifyFired(muzzle);burstRemaining--;burstShotRemaining+=BurstShotInterval;
            Attacked?.Invoke(position,shot.transform.rotation,shot);
        }
        void CancelBurst(){burstRemaining=0;burstTarget=null;burstShotRemaining=0;}

        float TowerGap(Collider target)
        {
            var point=target.ClosestPoint(transform.position);
            var from=hull!=null?hull.ClosestPoint(point):transform.position;
            var gap=point-from;gap.y=0;return gap.magnitude;
        }
        private void Damaged(DamageInfo damage)=>playerDamage=damage.Source is TowerRuntime||damage.Source is StatusEffects;
        private void Died()=>Defeated?.Invoke(this,playerDamage);
        private void OnDisable(){health.Damaged-=Damaged;health.Died-=Died;StopAssault();Removed?.Invoke(this);}
        public void MoveTowards(Vector3 destination, float deltaTime)
        {
            if (!isActiveAndEnabled || !Runtime.IsAlive || deltaTime <= 0) return;
            destination.y = transform.position.y;
            Vector3 heading = destination - transform.position;
            if (heading.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(heading);
            Vector3 before=transform.position;
            transform.position = Vector3.MoveTowards(transform.position, destination,
                Runtime.MovementSpeed * deltaTime);
            Velocity=(transform.position-before)/deltaTime;
        }
    }
}
