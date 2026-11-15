using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace TowerDefence.Battlefield
{
    public sealed class CommanderMap : MonoBehaviour, IPointerClickHandler, IPointerMoveHandler, IScrollHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public Camera MapCamera;
        public RawImage MapImage;
        public WorldInteraction Interaction;
        private bool dragged;
        private Vector2 last;
        public void OnPointerClick(PointerEventData data)
        {
            if (dragged) { dragged = false; return; }
            if (TryRay(data, out var ray)) Interaction.HandleRay(ray, true);
        }
        public void OnPointerMove(PointerEventData data)
        { if (!data.dragging && TryRay(data, out var ray)) Interaction.HandleRay(ray, false); }
        private bool TryRay(PointerEventData data, out Ray ray)
        {
            ray = default;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(MapImage.rectTransform, data.position,
                data.pressEventCamera != null ? data.pressEventCamera : data.enterEventCamera, out var local)) return false;
            var rect = MapImage.rectTransform.rect;
            Vector2 uv = new Vector2((local.x - rect.xMin) / rect.width, (local.y - rect.yMin) / rect.height);
            if (uv.x < 0 || uv.x > 1 || uv.y < 0 || uv.y > 1) return false;
            ray = MapCamera.ViewportPointToRay(uv); return true;
        }
        public void OnScroll(PointerEventData data) => Zoom(-data.scrollDelta.y * 2);
        public void Zoom(float amount) => MapCamera.orthographicSize = Mathf.Clamp(MapCamera.orthographicSize + amount, 14, 62);
        public void ResetView() { MapCamera.transform.position = new Vector3(0, 100, 0); MapCamera.orthographicSize = 58; }
        public void OnBeginDrag(PointerEventData data) { dragged = false; last = data.position; }
        public void OnDrag(PointerEventData data)
        {
            Vector2 delta = data.position - last; last = data.position;
            if (delta.sqrMagnitude < 0.1f) return;
            dragged = true;
            var position = MapCamera.transform.position;
            float scale = 2 * MapCamera.orthographicSize / Mathf.Max(1, MapImage.rectTransform.rect.height);
            position.x = Mathf.Clamp(position.x - delta.x * scale, -25, 25);
            position.z = Mathf.Clamp(position.z - delta.y * scale, -30, 30);
            MapCamera.transform.position = position;
        }
        public void OnEndDrag(PointerEventData data) { }
    }

}
