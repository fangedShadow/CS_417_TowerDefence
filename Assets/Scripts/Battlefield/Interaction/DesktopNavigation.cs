using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR;
using Unity.XR.CoreUtils;

namespace TowerDefence.Battlefield
{
    public sealed class DesktopNavigation : MonoBehaviour
    {
        public Camera View;
        public WorldInteraction Interaction;
        public CharacterController Body;
        public float MoveSpeed = 5;
        private float fallSpeed, pitch;
        private TrackedPoseDriver pose;
        private UnityEngine.XR.Interaction.Toolkit.Locomotion.Gravity.GravityProvider gravity;
        public bool UsesDesktopControls => Application.platform != RuntimePlatform.Android && !XRSettings.isDeviceActive;
        private void Start()
        {
            if (View == null || Body == null || Interaction == null) { enabled = false; return; }
            pose = View.GetComponent<TrackedPoseDriver>();
            gravity=GetComponentInChildren<UnityEngine.XR.Interaction.Toolkit.Locomotion.Gravity.GravityProvider>();
            if (UsesDesktopControls)
            {
                if (pose != null) pose.enabled = false;
                var origin = GetComponent<XROrigin>();
                if (origin != null)
                {
                    origin.CameraYOffset = 1.7f;
                    origin.CameraFloorOffsetObject.transform.localPosition = Vector3.up * 1.7f;
                    View.transform.localPosition = Vector3.zero;
                }
            }
        }
        private void Update()
        {
            bool desktop = UsesDesktopControls;
            // A Quest session can begin after Start; desktop fallback must never disable its head tracking.
            if (pose != null && Application.platform != RuntimePlatform.Android) pose.enabled = !desktop;
            if (!desktop || Keyboard.current == null) return;
            Vector2 move = new Vector2((Keyboard.current.dKey.isPressed ? 1 : 0) - (Keyboard.current.aKey.isPressed ? 1 : 0),
                (Keyboard.current.wKey.isPressed ? 1 : 0) - (Keyboard.current.sKey.isPressed ? 1 : 0));
            var forward = View.transform.forward; forward.y = 0; forward.Normalize();
            var right = View.transform.right; right.y = 0; right.Normalize();
            if(gravity!=null&&gravity.isActiveAndEnabled)fallSpeed=0;
            else if (Body.isGrounded) fallSpeed = -1; else fallSpeed += Physics.gravity.y * Time.deltaTime;
            Body.Move(((forward * move.y + right * move.x).normalized * MoveSpeed + Vector3.up * fallSpeed) * Time.deltaTime);
            if (Mouse.current != null && Mouse.current.rightButton.isPressed)
            {
                var delta = Mouse.current.delta.ReadValue();
                transform.Rotate(0, delta.x * 0.15f, 0);
                pitch = Mathf.Clamp(pitch - delta.y * 0.15f, -65, 70);
                View.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
            }
        }
    }
}
