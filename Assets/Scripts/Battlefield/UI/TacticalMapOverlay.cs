using UnityEngine;
using UnityEngine.UI;

namespace TowerDefence.Battlefield
{
    // Screen-space symbols keep small units readable without adding another map camera.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class TacticalMapOverlay : MaskableGraphic
    {
        public Camera MapCamera;
        public Transform Player;
        public BaseIntegration Base;
        public ConstructionController Construction;
        public NavyWaveManager Waves;
        public ConstructionPreview Preview;
        public bool ShowConstruction;
        float nextRefresh;
        protected override void Awake(){base.Awake();raycastTarget=false;}
        void LateUpdate()
        {
            if(Time.unscaledTime<nextRefresh)return;
            nextRefresh=Time.unscaledTime+.1f;SetVerticesDirty();
        }
        public bool MapPoint(Vector3 world,out Vector2 point)
        {
            point=default;if(MapCamera==null)return false;
            var uv=MapCamera.WorldToViewportPoint(world);
            var rect=rectTransform.rect;
            point=new Vector2(rect.xMin+Mathf.Clamp(uv.x,.02f,.98f)*rect.width,rect.yMin+Mathf.Clamp(uv.y,.02f,.98f)*rect.height);
            return uv.z>0&&uv.x>=0&&uv.x<=1&&uv.y>=0&&uv.y<=1;
        }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();if(MapCamera==null)return;
            if(ShowConstruction&&Preview!=null&&Preview.Entry!=null&&Construction!=null)
                foreach(var cell in Construction.Grid.Cells)
                {
                    if(cell.Definition.Surface!=BuildSurface.Water||!MapPoint(Construction.Grid.World(cell),out var p))continue;
                    bool valid=Construction.Validate(Preview.Entry,cell,out _);
                    float size=rectTransform.rect.height*Construction.Grid.CellSize/(2*MapCamera.orthographicSize)*.42f;
                    var tint=cell==Preview.HoveredCell?new Color(1,.85f,.2f,.65f):valid?new Color(.1f,.95f,.7f,.38f):new Color(1,.2f,.2f,.08f);
                    Quad(mesh,p,new Vector2(size,size),tint);
                }
            if(Construction!=null)
                foreach(var cell in Construction.Grid.Cells)
                {
                    var tower=cell.Structure!=null?cell.Structure:cell.Foundation;
                    if(tower==null||!tower.Alive||!MapPoint(tower.transform.position,out var p))continue;
                    Diamond(mesh,p,3.5f,new Color(.025f,.08f,.1f));
                    Diamond(mesh,p,2.5f,tower==Construction.Selected?Color.white:new Color(.1f,.95f,.86f));
                    if(ShowConstruction&&cell.Structure!=null)
                    {
                        Quad(mesh,p+Vector2.up*7,new Vector2(6,1.5f),new Color(.02f,.07f,.09f));
                        float fraction=tower.Runtime.CurrentHealth/tower.Runtime.Health.MaxHealth;
                        Quad(mesh,p+new Vector2(-6+6*fraction,7),new Vector2(6*fraction,1),new Color(.2f,.95f,.5f));
                    }
                }
            if(Construction!=null)
            {
                var selected=Construction.Selected;float radius=SelectedTowerRange.EffectiveRadius(selected);
                if(radius>0)
                {
                    Vector3 center=selected.transform.position;
                    for(int i=0;i<96;i++)
                    {
                        float a=i*Mathf.PI*2/96,b=(i+1)*Mathf.PI*2/96;
                        if(!MapPoint(center+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*radius,out var from)||!MapPoint(center+new Vector3(Mathf.Cos(b),0,Mathf.Sin(b))*radius,out var to))continue;
                        var side=new Vector2(-(to-from).y,(to-from).x).normalized*.9f;
                        var tint=new Color(.25f,.95f,.95f,.65f);
                        Triangle(mesh,from-side,from+side,to+side,tint);Triangle(mesh,from-side,to+side,to-side,tint);
                    }
                }
            }
            if(Waves!=null)
                foreach(var enemy in Waves.ActiveEnemies)
                    if(enemy!=null&&MapPoint(enemy.transform.position,out var p))
                    {Diamond(mesh,p,4,new Color(.18f,.025f,.04f));Diamond(mesh,p,2.8f,new Color(1,.32f,.27f));}
            if(Base!=null&&Base.SharedBase!=null&&MapPoint(Base.SharedBase.transform.position,out var home))
            {Diamond(mesh,home,6,new Color(.08f,.06f,.02f));Diamond(mesh,home,4.5f,new Color(1,.8f,.24f));}
            if(Player!=null&&MapPoint(Player.position,out var player))
            {
                Vector3 direction=Vector3.ProjectOnPlane(Player.forward,Vector3.up);
                Vector3 mapForward=MapCamera.transform.InverseTransformDirection(direction);
                Vector2 forward=new Vector2(mapForward.x,mapForward.y).normalized;
                if(forward.sqrMagnitude<.001f)forward=Vector2.up;
                Vector2 side=new Vector2(-forward.y,forward.x);
                Triangle(mesh,player+forward*9,player-forward*6+side*6,player-forward*6-side*6,new Color(.01f,.07f,.1f));
                Triangle(mesh,player+forward*7,player-forward*4+side*4,player-forward*4-side*4,Color.white);
            }
        }
        static void Diamond(VertexHelper mesh,Vector2 p,float r,Color color)
        {
            Triangle(mesh,p+Vector2.up*r,p+Vector2.right*r,p+Vector2.down*r,color);
            Triangle(mesh,p+Vector2.up*r,p+Vector2.down*r,p+Vector2.left*r,color);
        }
        static void Quad(VertexHelper mesh,Vector2 p,Vector2 r,Color color)
        {
            Triangle(mesh,p+new Vector2(-r.x,r.y),p+r,p+new Vector2(r.x,-r.y),color);
            Triangle(mesh,p+new Vector2(-r.x,r.y),p+new Vector2(r.x,-r.y),p-r,color);
        }
        static void Triangle(VertexHelper mesh,Vector2 a,Vector2 b,Vector2 c,Color color)
        {
            int first=mesh.currentVertCount;mesh.AddVert(a,color,Vector2.zero);mesh.AddVert(b,color,Vector2.zero);mesh.AddVert(c,color,Vector2.zero);
            mesh.AddTriangle(first,first+1,first+2);
        }
    }
}
