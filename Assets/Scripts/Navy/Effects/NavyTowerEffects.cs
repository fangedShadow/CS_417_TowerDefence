using TowerDefence.Shared;
using UnityEngine;

namespace TowerDefence.Navy
{
    [System.Serializable] public sealed class BarrelAccessoryGroup
    {
        public Transform[] Parts=System.Array.Empty<Transform>();
    }
    [RequireComponent(typeof(TowerRuntime))]
    public sealed class NavyTowerEffects : MonoBehaviour
    {
        public Transform[] RecoilParts;
        public BarrelAccessoryGroup[] BarrelAccessories=System.Array.Empty<BarrelAccessoryGroup>();
        Vector3[][] accessoryRest;
        // Only the authored barrel transforms slide; their tower/head parents never translate.
        [Min(0)] public float RecoilDistance=.055f;
        [Min(.04f)] public float RecoilDuration=.15f;
        [Min(1)] public float ElevationSpeed=75;
        public Transform PumpPart;
        public float ChargeMovement=.018f;
        TowerRuntime runtime;
        NavyWeapon weapon;
        OilExtractor oil;
        NavySupportTower support;
        NavyEffect idle, machinery, buff,charge,income;
        uint incomeLease;
        [Min(.1f)] public float WaveChargeWindow=1.1f;
        Vector3[] rest;
        Quaternion[] barrelRestRotations;
        float[] barrelShotRemaining;
        Vector3 pumpRest;
        bool ready;
        int currentBuff=-1;
        void Awake()
        {
            runtime=GetComponent<TowerRuntime>();weapon=GetComponent<NavyWeapon>();oil=GetComponent<OilExtractor>();support=GetComponent<NavySupportTower>();
            if(RecoilParts==null)RecoilParts=System.Array.Empty<Transform>();
            rest=new Vector3[RecoilParts.Length];barrelRestRotations=new Quaternion[rest.Length];barrelShotRemaining=new float[rest.Length];
            for(int i=0;i<rest.Length;i++)if(RecoilParts[i]!=null){rest[i]=RecoilParts[i].localPosition;barrelRestRotations[i]=RecoilParts[i].localRotation;}
            accessoryRest=new Vector3[BarrelAccessories.Length][];
            for(int i=0;i<BarrelAccessories.Length;i++)
            {
                accessoryRest[i]=new Vector3[BarrelAccessories[i].Parts.Length];
                for(int j=0;j<accessoryRest[i].Length;j++)if(BarrelAccessories[i].Parts[j]!=null)accessoryRest[i][j]=BarrelAccessories[i].Parts[j].localPosition;
            }
            if(PumpPart!=null)pumpRest=PumpPart.localPosition;
        }
        void OnEnable(){if(weapon!=null)weapon.Fired+=OnFired;if(oil!=null)oil.Produced+=OnProduced;}
        void Start()=>ready=true;
        void Update()
        {
            if(!ready)return;
            if(!runtime.CanReceiveBuff){Release(ref idle);Release(ref machinery);Release(ref buff);Release(ref charge);ReleaseIncome();currentBuff=-1;ResetMotion();return;}
            var pool=NavyEffectPool.Instance;if(pool==null)return;
            if(support!=null&&support.isActiveAndEnabled&&idle==null)idle=pool.Play(NavyEffectId.SupportRadar,transform.position,follow:transform);
            if(support!=null&&!support.isActiveAndEnabled)Release(ref idle);
            if(oil!=null)
            {
                bool operating=oil.IsOperating;
                if(operating&&idle==null)idle=pool.Play(NavyEffectId.OilSteam,transform.position,follow:transform);
                if(operating&&machinery==null)machinery=pool.Play(NavyEffectId.OilMachinery,transform.position,follow:transform);
                if(!operating){Release(ref idle);Release(ref machinery);ReleaseIncome();}
                if(PumpPart!=null)PumpPart.localPosition=pumpRest+(operating?Vector3.up*(Mathf.Sin(Time.time*7)*.018f):Vector3.zero);
            }
            var active=runtime.ActiveBuff;
            int next=runtime.Kind==TowerKind.Cannon&&active.Damage>0?(int)NavyEffectId.CannonBuff:
                runtime.Kind==TowerKind.Turret&&active.Range>0?(int)NavyEffectId.TurretBuff:
                runtime.Kind==TowerKind.Slow&&active.DefenseFrequency>0?(int)NavyEffectId.DefenseBuff:-1;
            if(next!=currentBuff){Release(ref buff);currentBuff=next;if(next>=0)buff=pool.Play((NavyEffectId)next,transform.position,follow:transform);}
            if(buff!=null&&runtime.Kind==TowerKind.Turret&&buff.Ring!=null)
                buff.Ring.localScale=new Vector3(runtime.AttackRange*(2/.9f),1,runtime.AttackRange*(2/.9f));
            if(weapon!=null&&weapon.ProjectilePrefab!=null&&weapon.ProjectilePrefab.Kind==NavyProjectileKind.Wave)
            {
                float remaining=Mathf.Max(runtime.FireCooldown,runtime.AbilityCooldown);
                bool charging=weapon.HasValidTarget&&remaining>0&&remaining<=WaveChargeWindow;
                if(charging&&(charge==null||!charge.IsLeased))charge=pool.Play(NavyEffectId.WaveCharge,weapon.Muzzles[0].position,weapon.Muzzles[0].rotation,weapon.Muzzles[0]);
                if(charging&&charge!=null)charge.SetChargeProgress(1-remaining/Mathf.Max(.1f,WaveChargeWindow));
                if(!charging)Release(ref charge);
            }
        }
        void LateUpdate()
        {
            if(!ready||!runtime.CanReceiveBuff||weapon==null)return;
            for(int i=0;i<RecoilParts.Length;i++)
            {
                var barrel=RecoilParts[i];if(barrel==null)continue;
                barrel.localRotation=barrelRestRotations[i];
                barrelShotRemaining[i]=Mathf.Max(0,barrelShotRemaining[i]-Time.deltaTime);
                float phase=1-barrelShotRemaining[i]/Mathf.Max(.04f,RecoilDuration);
                float slide=phase<.2f?Mathf.SmoothStep(0,1,phase/.2f):Mathf.SmoothStep(0,1,(1-phase)/.8f);
                Vector3 forward=i<weapon.Muzzles.Length?weapon.Muzzles[i].forward:barrel.parent.forward;
                barrel.localPosition=rest[i]+barrel.parent.InverseTransformVector(-forward*RecoilDistance*slide);
                if(i<BarrelAccessories.Length)
                    for(int j=0;j<BarrelAccessories[i].Parts.Length;j++)
                    {
                        var part=BarrelAccessories[i].Parts[j];if(part==null||part.IsChildOf(barrel))continue;
                        part.localPosition=accessoryRest[i][j]+part.parent.InverseTransformVector(-forward*RecoilDistance*slide);
                    }
            }
        }
        void OnFired(Transform muzzle,NavyProjectile projectile)
        {
            int index=System.Array.IndexOf(weapon.Muzzles,muzzle);
            if(index>=0&&index<barrelShotRemaining.Length)barrelShotRemaining[index]=Mathf.Max(.04f,RecoilDuration);
            var pool=NavyEffectPool.Instance;if(pool==null)return;
            int level=Mathf.Clamp(runtime.Stats.UpgradeLevel,1,3);
            NavyEffectId id=runtime.Kind==TowerKind.Turret?(NavyEffectId)((int)NavyEffectId.TurretFire1+level-1):
                runtime.Kind==TowerKind.Slow?level==3?NavyEffectId.ToxicPulse:level==2?NavyEffectId.SlowPulse2:NavyEffectId.SlowPulse1:
                level==3?NavyEffectId.WaveFire:level==2?NavyEffectId.CannonFire2:NavyEffectId.CannonFire1;
            // Fired is raised once for each real muzzle/projectile, including every double/triple barrel.
            pool.Play(id,muzzle.position,muzzle.rotation);
            if(projectile.Kind==NavyProjectileKind.Wave)
            {
                // An immediately ready first shot keeps its original timing; a short charge bloom overlaps its flash.
                if(charge==null){var bloom=pool.Play(NavyEffectId.WaveCharge,muzzle.position,muzzle.rotation);bloom?.SetChargeProgress(1);bloom?.Dissipate(.18f);}
                else Release(ref charge);
            }
        }
        void OnProduced(int money,int exp,int points)
        {
            if(oil==null||!oil.IsOperating||money<=0&&exp<=0&&points<=0)return;
            ReleaseIncome();income=NavyEffectPool.Instance?.Play(NavyEffectId.Income,transform.position+Vector3.up*.8f);
            if(income!=null)incomeLease=income.LeaseVersion;
        }
        void ReleaseIncome()
        {
            // A completed burst may already have been reused by another extractor.
            if(income!=null&&income.LeaseVersion==incomeLease)income.Release();income=null;
        }
        static void Release(ref NavyEffect effect){if(effect!=null)effect.Release();effect=null;}
        void ResetMotion()
        {
            if(rest!=null)for(int i=0;i<rest.Length;i++)if(RecoilParts[i]!=null){RecoilParts[i].localPosition=rest[i];RecoilParts[i].localRotation=barrelRestRotations[i];barrelShotRemaining[i]=0;}
            if(accessoryRest!=null)for(int i=0;i<BarrelAccessories.Length;i++)for(int j=0;j<BarrelAccessories[i].Parts.Length;j++)
                if(BarrelAccessories[i].Parts[j]!=null)BarrelAccessories[i].Parts[j].localPosition=accessoryRest[i][j];
            if(PumpPart!=null)PumpPart.localPosition=pumpRest;
        }
        void OnDisable()
        {
            if(weapon!=null)weapon.Fired-=OnFired;if(oil!=null)oil.Produced-=OnProduced;
            Release(ref idle);Release(ref machinery);Release(ref buff);Release(ref charge);ReleaseIncome();currentBuff=-1;
            ResetMotion();
        }
    }
}
