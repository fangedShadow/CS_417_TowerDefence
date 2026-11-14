using UnityEngine;
using UnityEngine.Rendering;

namespace TowerDefence.Battlefield
{
    public sealed class MinimapRefresh : MonoBehaviour
    {
        public Camera MapCamera;
        public BattlefieldSession Session;
        [Range(1, 30)] public float FramesPerSecond = 8;
        private float next;
        private void OnEnable() { if (Session != null) Session.Changed += ForceRefresh; }
        private void OnDisable() { if (Session != null) Session.Changed -= ForceRefresh; if (MapCamera != null) MapCamera.enabled = false; }
        public void ForceRefresh() => next = 0;
        private void LateUpdate()
        {
            bool render = SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null && Time.unscaledTime >= next;
            MapCamera.enabled = render;
            if (render) next = Time.unscaledTime + 1 / Mathf.Max(1, FramesPerSecond);
        }
    }
}
