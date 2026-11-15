using System.Collections.Generic;
using UnityEngine;

namespace TowerDefence.Navy
{
    public sealed class NavyGhostSnapshot : MonoBehaviour
    {
        public Material FadeMaterial;
        readonly List<MeshRenderer> shells=new List<MeshRenderer>();
        MaterialPropertyBlock properties;
        public void Capture(GameObject ghost)
        {
            Clear();if(ghost==null||FadeMaterial==null)return;
            foreach(var source in ghost.GetComponentsInChildren<MeshFilter>())
            {
                if(source.sharedMesh==null||source.GetComponent<NavyOutlineShell>()!=null)continue;
                MeshRenderer renderer;
                if(active==shells.Count)
                {
                    var go=new GameObject("Pooled Preview Fade");go.layer=2;go.transform.SetParent(transform,false);
                    go.AddComponent<MeshFilter>();renderer=go.AddComponent<MeshRenderer>();renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;shells.Add(renderer);
                }
                else renderer=shells[active];
                renderer.GetComponent<MeshFilter>().sharedMesh=source.sharedMesh;
                var materials=new Material[source.sharedMesh.subMeshCount];for(int i=0;i<materials.Length;i++)materials[i]=FadeMaterial;renderer.sharedMaterials=materials;
                renderer.transform.SetPositionAndRotation(source.transform.position,source.transform.rotation);renderer.transform.localScale=source.transform.lossyScale;
                renderer.gameObject.SetActive(true);active++;
            }
            SetOpacity(1);
        }
        int active;
        public void SetOpacity(float opacity)
        {
            if(properties==null)properties=new MaterialPropertyBlock();properties.SetFloat("_Opacity",opacity*.35f);
            for(int i=0;i<active;i++)shells[i].SetPropertyBlock(properties);
        }
        public void Clear(){foreach(var shell in shells)shell.gameObject.SetActive(false);active=0;}
    }
}
