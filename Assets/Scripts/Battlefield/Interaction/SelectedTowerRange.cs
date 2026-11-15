using TowerDefence.Shared;
using UnityEngine;

namespace TowerDefence.Battlefield
{
    public sealed class SelectedTowerRange : MonoBehaviour
    {
        public ConstructionController Construction;
        public Material RangeMaterial;
        GameObject ring;
        public float Radius {get;private set;}
        public static float EffectiveRadius(PlacedStructure target)
        {
            if(target==null||!target.Alive)return 0;
            var runtime=target.Runtime;
            return runtime.Kind==TowerKind.Support?runtime.SupportRange:
                runtime.Kind==TowerKind.Turret||runtime.Kind==TowerKind.Cannon||runtime.Kind==TowerKind.Slow?runtime.AttackRange:0;
        }
        void Start()
        {
            ring=GameObject.CreatePrimitive(PrimitiveType.Quad);ring.name="SelectedTowerRange";ring.layer=2;
            Destroy(ring.GetComponent<Collider>());ring.GetComponent<Renderer>().sharedMaterial=RangeMaterial;
            ring.transform.rotation=Quaternion.Euler(90,0,0);
        }
        void LateUpdate()
        {
            if(ring==null)return;var selected=Construction.Selected;Radius=EffectiveRadius(selected);ring.SetActive(Radius>0);
            if(Radius<=0)return;
            var position=selected.transform.position;position.y=Construction.Grid.World(selected.Cell).y+.06f;
            ring.transform.position=position;ring.transform.localScale=Vector3.one*(2*Radius/.9f);
        }
        void OnDestroy(){if(ring!=null)Destroy(ring);}
    }
}
