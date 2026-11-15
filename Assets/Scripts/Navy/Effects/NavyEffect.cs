using UnityEngine;

namespace TowerDefence.Navy
{
    public sealed class NavyEffect : MonoBehaviour
    {
        public NavyEffectId Id;
        [Min(.05f)] public float Lifetime=.65f;
        public bool Loop;
        [Min(1)] public int MaximumConcurrent=12;
        public NavyAudioCue Audio=new NavyAudioCue();
        public bool ExpandRing, RotateRing, SolarBlast;
        public Vector2 RingScale=new Vector2(.2f,2);
        public float RotationSpeed=90;
        public Vector3 RingRotationAxis=Vector3.up;
        public Renderer[] AnimatedRenderers;
        public Transform Ring;
        public Transform SecondaryRing,ChargeCore;
        public Vector2 SecondaryRingScale=new Vector2(.88f,.96f);
        public float SecondaryRotationSpeed=-110;
        public float ChargeProgress {get;private set;}
        public void SetChargeProgress(float value){ChargeProgress=Mathf.Clamp01(value);if(IsLeased)Apply(Time.time-began);}
        public NavyEffect Source { get; private set; }
        public bool IsLeased { get; private set; }
        public uint LeaseVersion { get; private set; }
        public bool HasAudioLease=>IsLeased&&audioLease&&(Audio.Loop||Time.unscaledTime<audioEnd);
        NavyEffectPool pool;
        ParticleSystem[] particles;
        AudioSource speaker;
        MaterialPropertyBlock properties;
        Transform follow;
        float began,audioEnd,scale;
        bool audioLease;
        float releaseAt,fadeBegan;
        NavyGhostSnapshot snapshot;
        Quaternion ringRestRotation;
        internal void Prepare(NavyEffectPool owner,NavyEffect source)
        {
            pool=owner;Source=source;particles=GetComponentsInChildren<ParticleSystem>(true);speaker=GetComponent<AudioSource>();
            properties=new MaterialPropertyBlock();snapshot=GetComponent<NavyGhostSnapshot>();ringRestRotation=Ring!=null?Ring.localRotation:Quaternion.identity;
        }
        internal void Begin(Vector3 position,Quaternion rotation,Transform target,float size)
        {
            transform.SetPositionAndRotation(position,rotation);transform.localScale=Vector3.one*size;scale=size;follow=target;
            began=Time.time;ChargeProgress=0;LeaseVersion++;IsLeased=true;gameObject.SetActive(true);
            releaseAt=0;
            foreach(var p in particles){p.Clear(true);p.Play(true);}
            Apply(0);
            audioLease=speaker!=null&&pool.StartAudio(this);
            if(audioLease)
            {
                speaker.clip=Audio.Clip;speaker.volume=Audio.Volume;speaker.pitch=Random.Range(Audio.Pitch.x,Audio.Pitch.y);speaker.spatialBlend=Audio.SpatialBlend;
                speaker.minDistance=Mathf.Max(.1f,Audio.MinDistance);speaker.maxDistance=Mathf.Max(speaker.minDistance,Audio.MaxDistance);speaker.rolloffMode=AudioRolloffMode.Logarithmic;
                speaker.loop=Audio.Loop;speaker.dopplerLevel=0;speaker.Play();audioEnd=Time.unscaledTime+Audio.Clip.length/Mathf.Max(.1f,speaker.pitch);
            }
        }
        void Update()
        {
            if(!IsLeased)return;
            if(follow!=null)transform.SetPositionAndRotation(follow.position,follow.rotation);
            float age=Time.time-began;
            if(releaseAt>0&&Time.time>=releaseAt){Release();return;}
            if(!Loop&&age>=Lifetime){Release();return;}
            Apply(age);
        }
        void Apply(float age)
        {
            float fraction=Id==NavyEffectId.WaveCharge?ChargeProgress:Mathf.Clamp01(age/Mathf.Max(.05f,Lifetime));
            if(Ring!=null)
            {
                if(ExpandRing)Ring.localScale=Vector3.one*Mathf.Lerp(RingScale.x,RingScale.y,fraction);
                if(RotateRing)Ring.localRotation=ringRestRotation*Quaternion.AngleAxis(age*RotationSpeed,RingRotationAxis);
            }
            if(SecondaryRing!=null)
            {
                SecondaryRing.localScale=Vector3.one*Mathf.Lerp(SecondaryRingScale.x,SecondaryRingScale.y,fraction);
                SecondaryRing.localRotation=Quaternion.AngleAxis(age*SecondaryRotationSpeed,Vector3.forward);
            }
            if(ChargeCore!=null)ChargeCore.localScale=Vector3.one*(Mathf.Lerp(.10f,.34f,fraction)*(1+.05f*Mathf.Sin(age*18)));
            if(AnimatedRenderers!=null)foreach(var renderer in AnimatedRenderers)
            {
                if(renderer==null)continue;
                renderer.GetPropertyBlock(properties);properties.SetFloat("_Age",age);properties.SetFloat("_Duration",Lifetime);
                float opacity=releaseAt>0?Mathf.Clamp01((releaseAt-Time.time)/(releaseAt-fadeBegan)):Loop?1:1-Mathf.SmoothStep(.2f,1,fraction);
                if(Id==NavyEffectId.WaveCharge)opacity*=Mathf.Lerp(.2f,1,fraction);
                properties.SetFloat("_Opacity",opacity);renderer.SetPropertyBlock(properties);
            }
            if(snapshot!=null)snapshot.SetOpacity(1-fraction);
        }
        public void CapturePreview(GameObject ghost){if(snapshot!=null)snapshot.Capture(ghost);}
        public void Dissipate(float duration)
        {
            if(!IsLeased)return;follow=null;fadeBegan=Time.time;releaseAt=Time.time+Mathf.Max(.02f,duration);
            foreach(var p in particles)p.Stop(true,ParticleSystemStopBehavior.StopEmitting);
            if(speaker!=null)speaker.Stop();audioLease=false;
        }
        public void Release(){if(IsLeased&&pool!=null)pool.Release(this);}
        internal void End()
        {
            IsLeased=false;follow=null;audioLease=false;if(speaker!=null)speaker.Stop();
            foreach(var p in particles){p.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);p.Clear(true);}
            if(snapshot!=null)snapshot.Clear();gameObject.SetActive(false);
        }
    }
}
