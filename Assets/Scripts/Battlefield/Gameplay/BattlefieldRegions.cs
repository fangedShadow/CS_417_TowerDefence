using System;
using System.Collections.Generic;
using TowerDefence.Shared;
using UnityEngine;

namespace TowerDefence.Battlefield
{
    [Serializable] public sealed class RegionBinding
    {
        public BattlefieldRegion Id;
        public MeshFilter Terrain;
        public GameObject LockedOverlay;
        public GameObject LockedBarrier;
        public Vector3[] TopTriangles;
    }
    public sealed class BattlefieldRegions : MonoBehaviour
    {
        public RegionBinding[] Regions=Array.Empty<RegionBinding>();
        public Collider WaterBounds;
        readonly HashSet<BattlefieldRegion> unlocked=new HashSet<BattlefieldRegion>{BattlefieldRegion.MainBase};
        public event Action Changed;
        public bool IsUnlocked(BattlefieldRegion region)=>unlocked.Contains(region);
        public bool GameplayImplemented(Faction faction)=>faction==Faction.Navy;
        public void ResetRegions(){unlocked.Clear();unlocked.Add(BattlefieldRegion.MainBase);Refresh();}
        public void Unlock(Faction faction)
        {
            unlocked.Add(BattlefieldRegion.MainBase);
            if(faction==Faction.Navy)unlocked.Add(BattlefieldRegion.Ocean);
            else
            {
                unlocked.Add(BattlefieldRegion.CenterLand);
                unlocked.Add(faction==Faction.Army?BattlefieldRegion.LeftLand:BattlefieldRegion.RightLand);
            }
            Refresh();
        }
        public void Refresh()
        {
            foreach(var region in Regions)
            {
                bool locked=!IsUnlocked(region.Id);
                if(region.LockedOverlay!=null)region.LockedOverlay.SetActive(locked);
                if(region.LockedBarrier!=null)region.LockedBarrier.SetActive(locked);
            }
            Changed?.Invoke();
        }
        public BattlefieldRegion? At(Vector3 world)
        {
            // Main base is checked first, because its footprint sits above the ocean.
            foreach(var region in Regions)
            {
                if(region.Terrain==null||region.Id==BattlefieldRegion.Ocean)continue;
                var p=region.Terrain.transform.InverseTransformPoint(world);var triangles=region.TopTriangles;
                if(triangles==null)continue;
                for(int i=0;i+2<triangles.Length;i+=3)if(Inside(p,triangles[i],triangles[i+1],triangles[i+2]))return region.Id;
            }
            if(WaterBounds!=null)
            {
                var b=WaterBounds.bounds;if(world.x>=b.min.x&&world.x<=b.max.x&&world.z>=b.min.z&&world.z<=b.max.z)return BattlefieldRegion.Ocean;
            }
            return null;
        }
        static bool Inside(Vector3 p,Vector3 a,Vector3 b,Vector3 c)
        {
            float Cross(Vector3 x,Vector3 y,Vector3 z)=>(y.x-x.x)*(z.z-x.z)-(y.z-x.z)*(z.x-x.x);
            float d=Cross(a,b,c);if(Mathf.Abs(d)<.00001f)return false;
            float s=Cross(a,b,p)/d,t=Cross(b,c,p)/d,u=Cross(c,a,p)/d;return s>=-.0001f&&t>=-.0001f&&u>=-.0001f;
        }
        public bool Allows(ConstructionDefinition entry,GridCell cell,BattlefieldGrid grid,out string reason)
        {
            if(entry==null||!GameplayImplemented(entry.Faction)){reason="This faction's towers are not implemented yet";return false;}
            var region=cell!=null?At(grid.World(cell)):null;
            if(region==null||!IsUnlocked(region.Value)){reason=region!=null?region+" region is locked":"Outside the battlefield";return false;}
            if(entry.Faction==Faction.Navy&&region!=BattlefieldRegion.Ocean){reason="Navy construction requires the ocean region";return false;}
            reason=string.Empty;return true;
        }
    }
}
