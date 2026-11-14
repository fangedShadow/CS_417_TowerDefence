using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace TowerDefence.Battlefield
{
    public sealed class WorldInteraction : MonoBehaviour
    {
        public ConstructionController Construction;
        public ConstructionPreview Preview;
        public Camera PhysicalCamera;
        public XRRayInteractor RightRay;
        public InputActionReference SelectAction, CancelAction, MenuAction;
        public GamePanelController Panel;
        public bool CommanderOpen { get; set; }
        private readonly List<RaycastResult> uiHits = new List<RaycastResult>();
        private GameObject selection;
        public Material SelectionMaterial;
        private void Start()
        {
            Construction.SelectionChanged += ShowSelection;
            selection = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            selection.name = "SelectedStructureIndicator"; selection.layer = 2;
            Destroy(selection.GetComponent<Collider>());
            selection.transform.localScale = new Vector3(2.1f, 0.015f, 2.1f);
            selection.GetComponent<Renderer>().sharedMaterial = SelectionMaterial;
            selection.SetActive(false);
        }
        private void OnDestroy()
        {
            if (Construction != null) Construction.SelectionChanged -= ShowSelection;
            if (selection != null) Destroy(selection);
        }
        private void ShowSelection(PlacedStructure target)
        {
            if (selection == null) return;
            selection.SetActive(target != null);
            if (target != null)
            {
                selection.transform.position = (target.Foundation != null ? target.Foundation.TowerMount.position : target.Runtime.transform.position) + Vector3.up * 0.02f;
                selection.transform.localScale = new Vector3(target.Foundation != null ? 3.4f : 2.1f, 0.015f, target.Foundation != null ? 3.4f : 2.1f);
            }
        }
        private bool OverDesktopUI()
        {
            if (EventSystem.current == null || Mouse.current == null) return false;
            uiHits.Clear();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = Mouse.current.position.ReadValue() }, uiHits);
            return uiHits.Count > 0;
        }
        private void Update()
        {
            if ((CancelAction != null && CancelAction.action.WasPressedThisFrame()) ||
                (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)) Preview.Cancel();
            if ((MenuAction != null && MenuAction.action.WasPressedThisFrame()) ||
                (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)) Panel.ToggleVisible();
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame && Preview.Entry != null) Preview.Rotate();
            if (CommanderOpen) return;
            bool xr = XRSettings.isDeviceActive;
            if (xr && RightRay != null)
            {
                if (RightRay.TryGetCurrentUIRaycastResult(out var ui) && ui.isValid) return;
                HandleRay(new Ray(RightRay.transform.position, RightRay.transform.forward),
                    SelectAction != null && SelectAction.action.WasPressedThisFrame());
            }
            else if (Mouse.current != null && PhysicalCamera != null && !OverDesktopUI())
                HandleRay(PhysicalCamera.ScreenPointToRay(Mouse.current.position.ReadValue()), Mouse.current.leftButton.wasPressedThisFrame);
        }
        public void HandleRay(Ray ray, bool pressed)
        {
            // Player-only safety walls must not obstruct construction or selection rays.
            if (!Physics.Raycast(ray, out var hit, 250, ~(1 << 2 | 1 << 5 | 1 << 8 | 1 << 9), QueryTriggerInteraction.Collide)) return;
            if (Preview.Entry != null)
            {
                Preview.Hover(hit.point);
                if (pressed) Preview.Confirm();
            }
            else if (pressed)
            {
                Construction.Select(hit.collider.GetComponentInParent<PlacedStructure>());
                if (Construction.Selected != null) Panel.ShowPhysicalPanel();
            }
        }
    }
}
