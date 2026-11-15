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
        public NearFarInteractor RightNearFar;
        public InputActionReference SelectAction, CancelAction, MenuAction, SwitchTabAction, SecondaryMapAction;
        public GamePanelController Panel;
        public bool CommanderOpen { get; set; }
        public PlacedStructure Hovered {get;private set;}
        public event System.Action<PlacedStructure> HoverChanged;
        public void SetHover(PlacedStructure value){if(Hovered==value)return;Hovered=value;HoverChanged?.Invoke(value);}
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
                (Keyboard.current != null && (Keyboard.current.tabKey.wasPressedThisFrame || Keyboard.current.yKey.wasPressedThisFrame))) Panel.ToggleVisible();
            if ((SwitchTabAction != null && SwitchTabAction.action.WasPressedThisFrame()) ||
                (Keyboard.current != null && Keyboard.current.xKey.wasPressedThisFrame)) Panel.ToggleCommander();
            if(SecondaryMapAction!=null&&SecondaryMapAction.action.WasPressedThisFrame())Panel.Hud.ToggleCompact();
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame && Preview.Entry != null) Preview.Rotate();
            if (CommanderOpen) return;
            bool xr = XRSettings.isDeviceActive;
            if ((xr || Application.platform == RuntimePlatform.Android) && RightNearFar != null)
            {
                if (RightNearFar.TryGetCurrentUIRaycastResult(out var ui) && ui.isValid){SetHover(null);return;}
                var aim = RightNearFar.curveOrigin != null ? RightNearFar.curveOrigin : RightNearFar.transform;
                HandleRay(new Ray(aim.position, aim.forward), SelectAction != null && SelectAction.action.WasPressedThisFrame());
            }
            else if (xr && RightRay != null)
            {
                if (RightRay.TryGetCurrentUIRaycastResult(out var ui) && ui.isValid){SetHover(null);return;}
                HandleRay(new Ray(RightRay.transform.position, RightRay.transform.forward),
                    SelectAction != null && SelectAction.action.WasPressedThisFrame());
            }
            else if (Mouse.current != null && PhysicalCamera != null && !OverDesktopUI())
                HandleRay(PhysicalCamera.ScreenPointToRay(Mouse.current.position.ReadValue()), Mouse.current.leftButton.wasPressedThisFrame);
        }
        public void HandleRay(Ray ray, bool pressed)
        {
            // Player-only safety walls must not obstruct construction or selection rays.
            if (!Physics.Raycast(ray, out var hit, 250, ~(1 << 2 | 1 << 5 | 1 << 8 | 1 << 9), QueryTriggerInteraction.Collide)){SetHover(null);return;}
            SetHover(Preview.Entry==null?hit.collider.GetComponentInParent<PlacedStructure>():null);
            if (Preview.Entry != null)
            {
                Preview.Hover(hit.point);
                if (pressed) Preview.Confirm();
            }
            else if (pressed)
            {
                Construction.Select(hit.collider.GetComponentInParent<PlacedStructure>());
                if (Construction.Selected != null && !CommanderOpen) Panel.ShowPhysicalPanel(true);
                else if(Construction.Session.Game!=null&&Construction.Session.Game.isActiveAndEnabled)Construction.Session.Game.InspectRegion(hit.point);
            }
        }
    }
}
