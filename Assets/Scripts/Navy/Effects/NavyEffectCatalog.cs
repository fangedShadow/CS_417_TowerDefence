using System;
using UnityEngine;

namespace TowerDefence.Navy
{
    public enum NavyEffectId
    {
        TurretFire1, TurretFire2, TurretFire3, TurretTracer1, TurretTracer2, TurretTracer3,
        BulletImpact, HeavyBulletImpact, CannonFire1, CannonFire2, CannonTrail1, CannonTrail2,
        CannonImpact1, CannonImpact2, WaveCharge, WaveFire, WaveProjectile, WaveImpact, WaveDissipate,
        SlowPulse1, SlowPulse2, ToxicPulse, SlowTrail, ToxicTrail, SlowImpact, ToxicImpact,
        SlowStatus, PoisonStatus, PoisonTick, SupportRadar, CannonBuff, TurretBuff, DefenseBuff,
        OilSteam, OilMachinery, Income, DeathSmall, DeathMedium, DeathLarge, HitFlash,
        FoundationBuilt, TurretBuilt, CannonBuilt, DefenseBuilt, SupportBuilt, OilBuilt,
        Upgraded, Repaired, Sold, Cancelled, InvalidAction, WaveCleared, FactionUnlocked
    }
    public enum NavyAudioGroup { Weapon, Impact, Interaction, Ambient, Machinery }
    [Serializable]
    public sealed class NavyAudioCue
    {
        public AudioClip Clip;
        [Range(0,1)] public float Volume=.35f;
        public Vector2 Pitch=new Vector2(.97f,1.03f);
        [Range(0,1)] public float SpatialBlend=1;
        [Min(.1f)] public float MinDistance=2, MaxDistance=35;
        [Min(0)] public float RetriggerDelay=.06f;
        public NavyAudioGroup Group;
        public bool Loop;
    }
    [CreateAssetMenu(menuName="Tower Defence/Navy/Effect Catalog")]
    public sealed class NavyEffectCatalog : ScriptableObject
    {
        public NavyEffect[] Prefabs=Array.Empty<NavyEffect>();
        public Material OutlineMaterial;
        public Material PreviewFadeMaterial;
        [Min(1)] public int MaximumActiveEffects=100, MaximumAudioVoices=14, MaximumWeaponVoices=5, MaximumMachineryVoices=3;
        [Min(0)] public int PrewarmPerPrefab=2;
        public NavyEffect Find(NavyEffectId id)
        {foreach(var prefab in Prefabs)if(prefab!=null&&prefab.Id==id)return prefab;return null;}
    }
}
