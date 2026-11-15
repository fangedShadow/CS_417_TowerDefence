using System;
using UnityEngine;

namespace TowerDefence.Navy
{
    [Serializable] public sealed class ShipGun
    {
        public Transform Yaw;
        public Transform[] Barrels=Array.Empty<Transform>();
        public Transform[] Muzzles=Array.Empty<Transform>();
    }
    public sealed class NavyShipWeapons : MonoBehaviour
    {
        public ShipGun[] Guns=Array.Empty<ShipGun>();
        public float TurnSpeed=110,ElevationSpeed=65,RecoilDistance=.08f,RecoilDuration=.22f;
        Vector3[][] positions;
        Quaternion[][] rotations;
        float[][] barrelShotRemaining;
        NavalEnemy owner;
        int cursor;
        void Awake()
        {
            owner=GetComponent<NavalEnemy>();positions=new Vector3[Guns.Length][];rotations=new Quaternion[Guns.Length][];barrelShotRemaining=new float[Guns.Length][];
            for(int g=0;g<Guns.Length;g++)
            {
                positions[g]=new Vector3[Guns[g].Barrels.Length];rotations[g]=new Quaternion[Guns[g].Barrels.Length];barrelShotRemaining[g]=new float[Guns[g].Barrels.Length];
                for(int b=0;b<positions[g].Length;b++){positions[g][b]=Guns[g].Barrels[b].localPosition;rotations[g][b]=Guns[g].Barrels[b].localRotation;}
            }
        }
        void LateUpdate()
        {
            for(int g=0;g<Guns.Length;g++)
            {
                var gun=Guns[g];if(gun.Yaw==null)continue;
                if(owner.HasAttackTarget)
                {
                    var direction=owner.AttackAim-gun.Yaw.position;direction.y=0;
                    if(direction.sqrMagnitude>.001f)gun.Yaw.rotation=Quaternion.RotateTowards(gun.Yaw.rotation,Quaternion.LookRotation(direction),TurnSpeed*Time.deltaTime);
                }
                for(int b=0;b<gun.Barrels.Length;b++)
                {
                    var barrel=gun.Barrels[b];float pitch=0;
                    if(owner.HasAttackTarget)
                    {
                        var local=gun.Yaw.InverseTransformDirection(owner.AttackAim-barrel.position);
                        pitch=Mathf.Clamp(-Mathf.Atan2(local.y,new Vector2(local.x,local.z).magnitude)*Mathf.Rad2Deg,-35,12);
                    }
                    barrel.localRotation=Quaternion.RotateTowards(barrel.localRotation,Quaternion.Euler(pitch,0,0)*rotations[g][b],ElevationSpeed*Time.deltaTime);
                    barrelShotRemaining[g][b]=Mathf.Max(0,barrelShotRemaining[g][b]-Time.deltaTime);
                    float phase=1-barrelShotRemaining[g][b]/Mathf.Max(.04f,RecoilDuration);
                    float slide=phase<.2f?Mathf.SmoothStep(0,1,phase/.2f):Mathf.SmoothStep(0,1,(1-phase)/.8f);
                    var forward=b<gun.Muzzles.Length?gun.Muzzles[b].forward:gun.Yaw.forward;
                    barrel.localPosition=positions[g][b]+barrel.parent.InverseTransformVector(-forward*RecoilDistance*slide);
                }
            }
        }
        public Transform NextMuzzle(Vector3 aim)
        {
            int count=0;foreach(var gun in Guns)count+=gun.Muzzles.Length;
            if(count==0)return null;int index=cursor++%count;
            foreach(var gun in Guns){if(index<gun.Muzzles.Length)return gun.Muzzles[index];index-=gun.Muzzles.Length;}
            return null;
        }
        public void NotifyFired(Transform muzzle)
        {
            for(int g=0;g<Guns.Length;g++)for(int b=0;b<Guns[g].Muzzles.Length;b++)
                if(Guns[g].Muzzles[b]==muzzle)barrelShotRemaining[g][b]=Mathf.Max(.04f,RecoilDuration);
        }
        void OnDisable()
        {
            if(positions==null)return;
            for(int g=0;g<Guns.Length;g++)for(int b=0;b<Guns[g].Barrels.Length;b++)
                if(Guns[g].Barrels[b]!=null){Guns[g].Barrels[b].localPosition=positions[g][b];Guns[g].Barrels[b].localRotation=rotations[g][b];barrelShotRemaining[g][b]=0;}
        }
    }
}
