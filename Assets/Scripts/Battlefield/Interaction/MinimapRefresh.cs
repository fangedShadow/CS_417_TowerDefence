using UnityEngine;
using UnityEngine.Rendering;

namespace TowerDefence.Battlefield
{
    public sealed class MinimapRefresh : MonoBehaviour
    {
        public Camera MapCamera;
        public BattlefieldSession Session;
        [Range(1, 30)] public float FramesPerSecond = 8;
        public Bounds PlayableBounds=new Bounds(new Vector3(0,0,-1),new Vector3(80,8,110));
        private float next;
        private void OnEnable() { if (Session != null) Session.Changed += ForceRefresh; }
        private void OnDisable() { if (Session != null) Session.Changed -= ForceRefresh; if (MapCamera != null) MapCamera.enabled = false; }
        public void ForceRefresh() => next = 0;
        private void Start()=>FrameBattlefield();
        public void FrameBattlefield()
        {
            if(MapCamera==null)return;
            MapCamera.aspect=PlayableBounds.size.x/Mathf.Max(1,PlayableBounds.size.z);
            MapCamera.orthographicSize=PlayableBounds.size.z*.5f;
            var position=MapCamera.transform.position;position.x=PlayableBounds.center.x;position.z=PlayableBounds.center.z;
            MapCamera.transform.position=position;ClampView();ForceRefresh();
        }
        public void ClampView()
        {
            if(MapCamera==null)return;
            float max=Mathf.Min(PlayableBounds.extents.z,PlayableBounds.extents.x/MapCamera.aspect);
            MapCamera.orthographicSize=Mathf.Clamp(MapCamera.orthographicSize,Mathf.Min(14,max),max);
            var half=new Vector2(MapCamera.orthographicSize*MapCamera.aspect,MapCamera.orthographicSize);
            var position=MapCamera.transform.position;
            position.x=Mathf.Clamp(position.x,PlayableBounds.min.x+half.x,PlayableBounds.max.x-half.x);
            position.z=Mathf.Clamp(position.z,PlayableBounds.min.z+half.y,PlayableBounds.max.z-half.y);
            MapCamera.transform.position=position;
        }
        private void LateUpdate()
        {
            bool render = SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null && Time.unscaledTime >= next;
            MapCamera.enabled = render;
            if (render) next = Time.unscaledTime + 1 / Mathf.Max(1, FramesPerSecond);
        }
    }
}
