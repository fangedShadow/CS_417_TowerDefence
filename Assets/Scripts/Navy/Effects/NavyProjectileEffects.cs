using UnityEngine;

namespace TowerDefence.Navy
{
    [RequireComponent(typeof(NavyProjectile))]
    public sealed class NavyProjectileEffects : MonoBehaviour
    {
        NavyProjectile projectile;
        NavyEffect trail;
        bool ending;
        int level=1;
        void Awake()=>projectile=GetComponent<NavyProjectile>();
        void OnEnable(){projectile.Launched+=Launch;projectile.Impacted+=Impact;projectile.Finished+=Finish;ending=false;}
        void Launch()
        {
            var pool=NavyEffectPool.Instance;if(pool==null)return;
            if(projectile.Kind==NavyProjectileKind.Wave)
                foreach(var renderer in projectile.GetComponentsInChildren<Renderer>())renderer.enabled=false;
            level=projectile.Owner!=null?Mathf.Clamp(projectile.Owner.Stats.UpgradeLevel,1,3):1;
            var id=projectile.Kind==NavyProjectileKind.Round?(NavyEffectId)((int)NavyEffectId.TurretTracer1+level-1):
                projectile.Kind==NavyProjectileKind.Cannonball?NavyEffectId.CannonTrail1:
                projectile.Kind==NavyProjectileKind.HeavyCannonball?NavyEffectId.CannonTrail2:
                projectile.Kind==NavyProjectileKind.Wave?NavyEffectId.WaveProjectile:
                projectile.Kind==NavyProjectileKind.ToxicBolt?NavyEffectId.ToxicTrail:NavyEffectId.SlowTrail;
            float scale=projectile.Kind==NavyProjectileKind.Wave&&projectile.Owner!=null?Mathf.Max(1,projectile.Owner.Stats.WaveWidth*.5f):1;
            trail=pool.Play(id,transform.position,transform.rotation,transform,scale);
        }
        void Impact(Vector3 point)
        {
            var id=projectile.Kind==NavyProjectileKind.Round?
                (level==3?NavyEffectId.HeavyBulletImpact:NavyEffectId.BulletImpact):
                projectile.Kind==NavyProjectileKind.Cannonball?NavyEffectId.CannonImpact1:
                projectile.Kind==NavyProjectileKind.HeavyCannonball?NavyEffectId.CannonImpact2:
                projectile.Kind==NavyProjectileKind.Wave?NavyEffectId.WaveImpact:
                projectile.Kind==NavyProjectileKind.ToxicBolt?NavyEffectId.ToxicImpact:NavyEffectId.SlowImpact;
            NavyEffectPool.Instance?.Play(id,point);
        }
        void Finish()
        {
            if(ending)return;ending=true;
            if(trail!=null){trail.Dissipate(.3f);trail=null;}
            if(projectile.Kind==NavyProjectileKind.Wave)NavyEffectPool.Instance?.Play(NavyEffectId.WaveDissipate,transform.position,transform.rotation);
        }
        void OnDisable()
        {
            projectile.Launched-=Launch;projectile.Impacted-=Impact;projectile.Finished-=Finish;
            if(trail!=null){trail.Dissipate(.25f);trail=null;}
        }
    }
}
