using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TowerDefence.Navy;
using TowerDefence.Shared;
using UnityEngine;

namespace TowerDefence.Battlefield
{
    [Serializable] public sealed class NavalWaterRoute
    {
        public string Id="Main ocean approach";
        public Transform[] Waypoints=Array.Empty<Transform>();
    }
    public sealed class NavyWaveManager : MonoBehaviour
    {
        public NavyGameSettings Settings;
        public PlayableNavyGame Game;
        public BaseIntegration Base;
        public BattlefieldRegions Regions;
        public NavalWaterRoute[] Routes=Array.Empty<NavalWaterRoute>();
        public Transform EnemyRoot;
        readonly HashSet<NavalEnemy> active=new HashSet<NavalEnemy>();
        readonly HashSet<int> completed=new HashSet<int>();
        Coroutine spawning;
        bool allSpawned;
        public bool Running {get;private set;}
        public int CurrentWave {get;private set;}
        public int ScheduledCount {get;private set;}
        public int SpawnedCount {get;private set;}
        public int ActiveCount=>active.Count;
        public IEnumerable<NavalEnemy> ActiveEnemies=>active;
        public event Action<int> WaveCompleted;
        public event Action Changed;
        public bool TryStart()
        {
            if(Running||Game==null||Game.State!=NavyGameState.Preparation||Base.SharedBase==null||!Base.SharedBase.Health.IsAlive)return false;
            var wave=Settings.Wave(CurrentWave+1);if(wave==null||wave.Groups==null||wave.Groups.Length==0)return false;
            foreach(var group in wave.Groups)
            {
                if(group.Count<1||group.Archetype<0||group.Archetype>=Settings.Enemies.Length)return false;
                var enemy=Settings.Enemies[group.Archetype];
                // Reject broken configurable routes before changing state or starting a coroutine.
                if(enemy.Prefab==null||enemy.AttackProjectile==null||SafeRoute(group.Route,enemy.Prefab)==null)return false;
                if(Settings.RandomizeWaterRoutes)for(int r=0;r<Routes.Length;r++)if(SafeRoute(r,enemy.Prefab)==null)return false;
            }
            CurrentWave++;Running=true;allSpawned=false;SpawnedCount=0;ScheduledCount=0;
            int cycle=(CurrentWave-1)/Settings.Waves.Length,remaining=Settings.MaximumEnemiesPerWave;
            foreach(var group in wave.Groups){int n=Mathf.Min(remaining,group.Count+cycle*Settings.ExtraEnemiesPerCycle);ScheduledCount+=n;remaining-=n;}
            Game.EnterWave();spawning=StartCoroutine(Spawn(wave,cycle));Changed?.Invoke();return true;
        }
        IEnumerator Spawn(NavalWaveDefinition wave,int cycle)
        {
            int remaining=Settings.MaximumEnemiesPerWave;
            foreach(var group in wave.Groups)
            {
                int count=Mathf.Min(remaining,group.Count+cycle*Settings.ExtraEnemiesPerCycle);remaining-=count;
                var archetype=Settings.Enemies[group.Archetype];
                for(int i=0;i<count&&Running;i++)
                {
                    int routeIndex=Settings.RandomizeWaterRoutes?UnityEngine.Random.Range(0,Routes.Length):group.Route;
                    var route=SafeRoute(routeIndex,archetype.Prefab);if(route!=null&&Settings.RandomizeWaterRoutes)route=VaryRoute(route,archetype.Prefab);if(route==null){Debug.LogError("Invalid naval route configuration");StopWave();yield break;}
                    while(Running&&!SpawnClear(route[0],archetype.Prefab))yield return null;
                    if(!Running)yield break;
                    var enemy=Instantiate(archetype.Prefab,route[0],Quaternion.identity,EnemyRoot);
                    Settings.ScaleSpawn(enemy.Runtime,CurrentWave);
                    active.Add(enemy);enemy.Defeated+=Defeated;enemy.Removed+=Removed;
                    enemy.BindAssault(route,Base.SharedBase,Base.NavalEntry.position+Vector3.up*.9f,archetype.AttackProjectile,archetype.MuzzleLocal);
                    SpawnedCount++;Changed?.Invoke();
                    yield return new WaitForSeconds(Mathf.Max(.1f,group.SpawnInterval));
                }
            }
            spawning=null;allSpawned=true;TryComplete();
        }
        public Vector3[] SafeRoute(int index,NavalEnemy prefab)
        {
            if(index<0||index>=Routes.Length||Regions.WaterBounds==null||Base.NavalEntry==null)return null;
            var route=Routes[index].Waypoints;if(route==null||route.Length<2||route.Any(t=>t==null))return null;
            var result=route.Where(t=>t!=null).Select(t=>t.position).ToList();if(result.Count<2)return null;
            var bounds=Regions.WaterBounds.bounds;var box=prefab.GetComponent<BoxCollider>();
            float halfZ=box!=null?box.size.z*.5f:1.5f,halfX=box!=null?box.size.x*.5f:.8f;
            Vector3 spawn=result[0];spawn.y=0;spawn.z=Mathf.Clamp(spawn.z,bounds.min.z+halfZ+.5f,bounds.max.z-halfZ-.5f);spawn.x=Mathf.Clamp(spawn.x,bounds.min.x+halfX+.5f,bounds.max.x-halfX-.5f);result[0]=spawn;
            for(int i=0;i<result.Count;i++)
            {
                var point=result[i];point.y=0;result[i]=point;
                if(Regions.At(point)!=BattlefieldRegion.Ocean)return null;
                if(i==0)continue;
                int samples=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(result[i-1],point)/2));
                for(int s=1;s<samples;s++)if(Regions.At(Vector3.Lerp(result[i-1],point,(float)s/samples))!=BattlefieldRegion.Ocean)return null;
            }
            // The final aim point belongs to the base perimeter. Ships stop at their configured attack range.
            result.Add(Base.NavalEntry.position);return result.ToArray();
        }
        public Vector3[] VaryRoute(Vector3[] source,NavalEnemy prefab)
        {
            var varied=(Vector3[])source.Clone();
            float variation=Mathf.Clamp(Settings.WaypointVariation,0,.5f);
            // Only the offshore points vary. The final approach and base aim stay exact.
            for(int i=0;i<varied.Length-2;i++)
                if(varied[i].z<=-40)varied[i]+=new Vector3(UnityEngine.Random.Range(-variation,variation),0,UnityEngine.Random.Range(-variation,variation));
            var hull=prefab.GetComponent<BoxCollider>();float radius=hull!=null?Mathf.Max(hull.size.x,hull.size.z)*.5f:2;
            for(int i=0;i<varied.Length-2;i++)
            {
                Vector3 from=varied[i],to=varied[i+1];
                for(int j=0;j<=20;j++)
                {
                    var p=Vector3.Lerp(from,to,j/20f);
                    foreach(var offset in new[]{Vector3.left,Vector3.right,Vector3.forward,Vector3.back})
                        if(Regions.At(p+offset*radius)!=BattlefieldRegion.Ocean)return source;
                }
            }
            return varied;
        }
        bool SpawnClear(Vector3 point,NavalEnemy prefab)
        {
            var box=prefab.GetComponent<BoxCollider>();float radius=box!=null?box.size.z*.5f:1.5f;
            foreach(var enemy in active)
            {
                if(enemy==null)continue;var other=enemy.GetComponent<BoxCollider>();float spacing=radius+(other!=null?other.size.z*.5f:1.5f)+.6f;
                if((enemy.transform.position-point).sqrMagnitude<spacing*spacing)return false;
            }
            // Placement reserves the route; also reject any manually placed structure at the spawn point.
            foreach(var hit in Physics.OverlapBox(point+Vector3.up*.4f,new Vector3(box!=null?box.size.x*.5f:.8f,.4f,radius),Quaternion.identity,~0,QueryTriggerInteraction.Ignore))
                if(hit.GetComponentInParent<PlacedStructure>()!=null)return false;
            return true;
        }
        public bool IsShippingLane(Vector3 point,float foundationHalfWidth=1.7f)
        {
            float hullHalfWidth=1;
            foreach(var enemy in Settings.Enemies)
                if(enemy.Prefab!=null&&enemy.Prefab.TryGetComponent<BoxCollider>(out var box))hullHalfWidth=Mathf.Max(hullHalfWidth,box.size.x*.5f);
            foreach(var route in Routes)
            {
                for(int i=1;i<route.Waypoints.Length;i++)
                {
                    if(route.Waypoints[i-1]==null||route.Waypoints[i]==null)continue;
                    Vector2 a=new Vector2(route.Waypoints[i-1].position.x,route.Waypoints[i-1].position.z);
                    Vector2 b=new Vector2(route.Waypoints[i].position.x,route.Waypoints[i].position.z);
                    Vector2 line=b-a,p=new Vector2(point.x,point.z);
                    if(line.sqrMagnitude<.001f)continue;
                    Vector2 normal=new Vector2(-line.y,line.x).normalized;
                    float width=hullHalfWidth+Mathf.Clamp(Settings.WaypointVariation,0,.5f)+foundationHalfWidth*(Mathf.Abs(normal.x)+Mathf.Abs(normal.y));
                    var closest=a+line*Mathf.Clamp01(Vector2.Dot(p-a,line)/line.sqrMagnitude);
                    if(Vector2.Distance(p,closest)<width)return true;
                }
            }
            return false;
        }
        void Defeated(NavalEnemy enemy,bool byPlayer)
        {
            if(!active.Remove(enemy))return;
            enemy.Defeated-=Defeated;enemy.Removed-=Removed;
            if(Running&&Game.State==NavyGameState.WaveActive&&byPlayer&&Base.SharedBase!=null&&Base.SharedBase.Health.IsAlive)
            {
                Game.Session.Resources.AddMoney(enemy.Runtime.MoneyReward);Game.Session.Resources.AddExp(enemy.Runtime.ExpReward);
                NavyEffectPool.Instance?.Play(NavyEffectId.Income,enemy.transform.position+Vector3.up*.5f);
            }
            Changed?.Invoke();TryComplete();
        }
        void Removed(NavalEnemy enemy)
        {
            if(!active.Remove(enemy))return;enemy.Defeated-=Defeated;enemy.Removed-=Removed;Changed?.Invoke();TryComplete();
        }
        void TryComplete()
        {
            if(!Running||!allSpawned||active.Count!=0||Game.State!=NavyGameState.WaveActive||Base.SharedBase==null||!Base.SharedBase.Health.IsAlive||!completed.Add(CurrentWave))return;
            Running=false;WaveCompleted?.Invoke(CurrentWave);Changed?.Invoke();
        }
        public void StopWave()
        {
            Running=false;if(spawning!=null){StopCoroutine(spawning);spawning=null;}
            foreach(var enemy in active.ToArray())if(enemy!=null){enemy.Defeated-=Defeated;enemy.Removed-=Removed;enemy.StopAssault();Destroy(enemy.gameObject);}
            active.Clear();Changed?.Invoke();
        }
        void OnDisable()=>StopWave();
    }
}
