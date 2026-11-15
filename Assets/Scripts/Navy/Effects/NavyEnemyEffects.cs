using TowerDefence.Shared;
using UnityEngine;

namespace TowerDefence.Navy
{
    [RequireComponent(typeof(EnemyRuntime))]
    public sealed class NavyEnemyEffects : MonoBehaviour
    {
        EnemyRuntime enemy;
        CombatHealth health;
        StatusEffects status;
        NavyEffect slow,poison;
        NavyOutlineTarget outline;
        float flashUntil;
        NavalEnemy naval;
        public bool SlowVisible=>slow!=null&&slow.IsLeased;
        public bool PoisonVisible=>poison!=null&&poison.IsLeased;
        void Awake(){enemy=GetComponent<EnemyRuntime>();health=GetComponent<CombatHealth>();status=GetComponent<StatusEffects>();outline=GetComponent<NavyOutlineTarget>();naval=GetComponent<NavalEnemy>();}
        void OnEnable(){health.Damaged+=Damaged;health.Died+=Died;if(naval!=null)naval.Attacked+=Fired;}
        void Fired(Vector3 point,Quaternion direction,NavyProjectile shot)
        {
            var id=shot.Kind==NavyProjectileKind.HeavyCannonball?NavyEffectId.CannonFire2:shot.Kind==NavyProjectileKind.Cannonball?NavyEffectId.CannonFire1:NavyEffectId.TurretFire1;
            NavyEffectPool.Instance?.Play(id,point,direction);
        }
        void Update()
        {
            bool alive=enemy.IsAlive;
            SetStatus(ref slow,alive&&status.SlowRemaining>0&&status.SpeedMultiplier<1,NavyEffectId.SlowStatus);
            SetStatus(ref poison,alive&&status.PoisonRemaining>0,NavyEffectId.PoisonStatus);
            if(outline!=null&&flashUntil>0&&Time.time>=flashUntil){outline.Hide();flashUntil=0;}
        }
        void SetStatus(ref NavyEffect effect,bool active,NavyEffectId id)
        {
            if(active&&effect==null)effect=NavyEffectPool.Instance?.Play(id,transform.position,follow:transform);
            if(!active&&effect!=null){effect.Release();effect=null;}
        }
        void Damaged(DamageInfo damage)
        {
            if(damage.Kind==DamageKind.Poison&&damage.Source is StatusEffects)
                NavyEffectPool.Instance?.Play(NavyEffectId.PoisonTick,enemy.AimTransform.position);
            else if(!(damage.Source is TowerRuntime))
                NavyEffectPool.Instance?.Play(damage.Kind==DamageKind.Energy?NavyEffectId.SlowImpact:NavyEffectId.BulletImpact,enemy.AimTransform.position);
            else NavyEffectPool.Instance?.Play(NavyEffectId.HitFlash,enemy.AimTransform.position);
            if(outline!=null){outline.Show(new Color(1,.85f,.5f),.035f);flashUntil=Time.time+.085f;}
        }
        void Died()
        {
            var kind=enemy.Stats.EnemyType;
            var id=kind==EnemyKind.Battleship?NavyEffectId.DeathLarge:kind==EnemyKind.Gunboat||kind==EnemyKind.Destroyer?NavyEffectId.DeathMedium:NavyEffectId.DeathSmall;
            // Unparented pool instances survive immediate enemy destruction/pool return.
            NavyEffectPool.Instance?.Play(id,enemy.AimTransform.position);
        }
        void OnDisable()
        {
            health.Damaged-=Damaged;health.Died-=Died;
            if(naval!=null)naval.Attacked-=Fired;
            SetStatus(ref slow,false,NavyEffectId.SlowStatus);SetStatus(ref poison,false,NavyEffectId.PoisonStatus);
            if(outline!=null)outline.Hide();flashUntil=0;
        }
    }
}
