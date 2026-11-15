using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TowerDefence.Navy
{
    public sealed class NavyOutlineTarget : MonoBehaviour
    {
        readonly List<Renderer> shells=new List<Renderer>();
        MaterialPropertyBlock properties;
        public bool Visible { get; private set; }
        public Color CurrentColor { get; private set; }
        public void Show(Color color,float width=.045f)
        {
            if(shells.Count==0)CreateShells();CurrentColor=color;Visible=true;
            if(properties==null)properties=new MaterialPropertyBlock();properties.SetColor("_OutlineColor",color);properties.SetFloat("_Thickness",width);properties.SetFloat("_Opacity",color.a);
            foreach(var shell in shells)if(shell!=null){shell.enabled=true;shell.SetPropertyBlock(properties);}
        }
        void CreateShells()
        {
            var catalog=NavyEffectPool.Instance?.Catalog;if(catalog==null||catalog.OutlineMaterial==null)return;
            var filters=GetComponentsInChildren<MeshFilter>(true);
            foreach(var source in filters)
            {
                if(source.sharedMesh==null||source.GetComponent<NavyOutlineShell>()!=null||source.GetComponent<MeshRenderer>()==null)continue;
                var go=new GameObject("Navy Outline Shell");go.layer=2;go.transform.SetParent(source.transform,false);go.AddComponent<NavyOutlineShell>();
                go.AddComponent<MeshFilter>().sharedMesh=source.sharedMesh;
                var renderer=go.AddComponent<MeshRenderer>();var materials=new Material[source.sharedMesh.subMeshCount];for(int i=0;i<materials.Length;i++)materials[i]=catalog.OutlineMaterial;
                renderer.sharedMaterials=materials;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.enabled=false;shells.Add(renderer);
            }
        }
        public void Hide(){Visible=false;foreach(var shell in shells)if(shell!=null)shell.enabled=false;}
        void OnDisable()=>Hide();
    }
}
