using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace TowerDefence.Battlefield
{
    public sealed class CommanderMap : MonoBehaviour, IPointerClickHandler, IPointerMoveHandler, IScrollHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        public Camera MapCamera;
        public RawImage MapImage;
        public WorldInteraction Interaction;
        public MinimapRefresh Framing;
        private bool dragged,pressed;
        private Vector2 last;
        public void OnPointerClick(PointerEventData data)
        {
            if(pressed){pressed=false;return;}
            if (dragged) { dragged = false; return; }
            if (TryRay(data, out var ray)) SelectRay(ray);
        }
        public void OnPointerExit(PointerEventData data)=>Interaction.SetHover(null);
        public void OnPointerDown(PointerEventData data){if(data.button==PointerEventData.InputButton.Left&&TryRay(data,out var ray)){pressed=true;SelectRay(ray);}}
        public void OnPointerUp(PointerEventData data){}
        public void OnPointerMove(PointerEventData data)
        { if (!data.dragging && TryRay(data, out var ray)) Interaction.HandleRay(ray, false); }
        private bool TryRay(PointerEventData data, out Ray ray)
        {
            ray = default;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(MapImage.rectTransform, data.position,
                data.pressEventCamera != null ? data.pressEventCamera : data.enterEventCamera, out var local)) return false;
            var rect = MapImage.rectTransform.rect;
            Vector2 uv = new Vector2((local.x - rect.xMin) / rect.width, (local.y - rect.yMin) / rect.height);
            return TryViewportRay(uv,out ray);
        }
        public bool TryViewportRay(Vector2 uv,out Ray ray)
        {
            ray=default;
            if(MapCamera==null||uv.x<0||uv.x>1||uv.y<0||uv.y>1)return false;
            ray=MapCamera.ViewportPointToRay(uv);return true;
        }
        public void SelectViewport(Vector2 uv)
        { if(TryViewportRay(uv,out var ray))SelectRay(ray); }
        void SelectRay(Ray ray)
        {
            // A map click chooses the real grid slot and leaves a preview. The shared
            // Confirm control commits it once; inspecting a tower selects its real instance.
            Interaction.HandleRay(ray,true);
            Interaction.Panel.Refresh();
        }
        public void OnScroll(PointerEventData data) => Zoom(-data.scrollDelta.y * 2);
        public void Zoom(float amount)
        {
            MapCamera.orthographicSize=Mathf.Max(1,MapCamera.orthographicSize+amount);
            if(Framing!=null){Framing.ClampView();Framing.ForceRefresh();}
        }
        public void ResetView(){if(Framing!=null)Framing.FrameBattlefield();}
        bool Local(PointerEventData data,out Vector2 point)=>RectTransformUtility.ScreenPointToLocalPointInRectangle(MapImage.rectTransform,data.position,
            data.pressEventCamera!=null?data.pressEventCamera:data.enterEventCamera,out point);
        public void OnBeginDrag(PointerEventData data) { dragged = false; Local(data,out last); }
        public void OnDrag(PointerEventData data)
        {
            if(!Local(data,out var current))return;
            Vector2 delta = current - last; last = current;
            if (delta.sqrMagnitude < 0.1f) return;
            dragged = true;
            var position = MapCamera.transform.position;
            float scale = 2 * MapCamera.orthographicSize / Mathf.Max(1, MapImage.rectTransform.rect.height);
            position.x -= delta.x * scale;
            position.z -= delta.y * scale;
            MapCamera.transform.position = position;
            if(Framing!=null){Framing.ClampView();Framing.ForceRefresh();}
        }
        public void OnEndDrag(PointerEventData data) { }
    }

}
