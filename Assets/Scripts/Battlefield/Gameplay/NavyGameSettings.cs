using System;
using TowerDefence.Navy;
using UnityEngine;

namespace TowerDefence.Battlefield
{
    public enum NavyGameState { Initializing, FactionSelection, Tutorial, Preparation, WaveActive, WaveCompleted, FactionUnlock, GameOver }
    public enum BattlefieldRegion { MainBase, Ocean, LeftLand, CenterLand, RightLand }
    public enum TutorialTask { Explanation, Foundation, Turret }
    [Serializable] public sealed class NavyTutorialStep
    {
        public string PopupId, Title;
        [TextArea(3,10)] public string Instructions;
        public TutorialTask Task;
    }
    [Serializable] public sealed class NavalArchetype
    {
        public string Id;
        public NavalEnemy Prefab;
        public NavyProjectile AttackProjectile;
        public Vector3 MuzzleLocal=new Vector3(0,.9f,1);
    }
    [Serializable] public sealed class NavalWaveGroup
    {
        public int Archetype;
        [Min(1)] public int Count=3;
        [Min(.1f)] public float SpawnInterval=1.5f;
        [Min(0)] public int Route;
    }
    [Serializable] public sealed class NavalWaveDefinition
    {
        public NavalWaveGroup[] Groups=Array.Empty<NavalWaveGroup>();
        [Min(0)] public int Money=35, Exp=20, UpgradePoints=1;
    }
    [CreateAssetMenu(menuName="Tower Defence/Gameplay/Navy Game Settings")]
    public sealed class NavyGameSettings : ScriptableObject
    {
        public NavalArchetype[] Enemies=Array.Empty<NavalArchetype>();
        public NavalWaveDefinition[] Waves=Array.Empty<NavalWaveDefinition>();
        public NavyTutorialStep[] Tutorial=Array.Empty<NavyTutorialStep>();
        [Min(0)] public int UpgradePointsPerRank=1;
        [Min(1)] public int MaximumEnemiesPerWave=32;
        [Min(0)] public int ExtraEnemiesPerCycle=2;
        public bool RandomizeWaterRoutes=true;
        public bool ScaleEnemies=true;
        public AnimationCurve HealthDifficulty=AnimationCurve.Linear(3,1,20,1.65f);
        public AnimationCurve DamageDifficulty=AnimationCurve.Linear(3,1,20,1.35f);
        public AnimationCurve ArmorDifficulty=AnimationCurve.Linear(3,0,20,8);
        public AnimationCurve SpeedDifficulty=AnimationCurve.Linear(3,1,20,1.1f);
        public AnimationCurve FrequencyDifficulty=AnimationCurve.Linear(3,1,20,1.12f);
        public AnimationCurve RewardDifficulty=AnimationCurve.Linear(3,1,20,1.2f);
        [Min(0)] public float EndlessHealthStep=.04f,EndlessDamageStep=.025f,EndlessArmorStep=.5f,EndlessRewardStep=.01f;
        public void ScaleSpawn(TowerDefence.Shared.EnemyRuntime enemy,int wave)
        {
            if(!ScaleEnemies)return;float t=Mathf.Clamp(wave,3,20),endless=Mathf.Max(0,wave-20);
            float Evaluate(AnimationCurve curve,float fallback)=>curve!=null&&curve.length>0?curve.Evaluate(t):fallback;
            enemy.ApplyDifficulty(Evaluate(HealthDifficulty,1)*(1+endless*EndlessHealthStep),Evaluate(DamageDifficulty,1)*(1+endless*EndlessDamageStep),
                Evaluate(ArmorDifficulty,0)+endless*EndlessArmorStep,Evaluate(SpeedDifficulty,1)+endless*.003f,
                Evaluate(FrequencyDifficulty,1)+endless*.004f,Evaluate(RewardDifficulty,1)+endless*EndlessRewardStep);
        }
        [Range(0,.5f)] public float WaypointVariation=.3f;
        [Min(0)] public float WaveCompletionPause=.8f;
        [Range(.05f,.9f)] public float CriticalHealthFraction=.25f;
        public AudioClip WarningClip;
        public NavalWaveDefinition Wave(int number)=>Waves!=null&&Waves.Length>0?Waves[Mathf.Clamp(number-1,0,Waves.Length-1)]:null;
    }
}
