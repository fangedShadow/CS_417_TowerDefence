using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Jump;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;

namespace TowerDefence.Battlefield
{
    // Filters the existing XRI jump input. Never disable/re-enable JumpProvider in the air:
    // OnEnable resets its air-jump allowance and would permit an unlimited-jump exploit.
    [DefaultExecutionOrder(-10)]
    public sealed class JumpInteractionGate : MonoBehaviour, IXRInputButtonReader
    {
        public JumpProvider Jump;
        public WorldInteraction Interaction;
        public InputActionReference JumpAction;
        bool suppressed;
        public bool Blocked => Interaction==null||Interaction.Preview.Entry!=null||
            Interaction.Panel.Tutorials.Current!=null||UIEngaged||
            (EventSystem.current!=null&&EventSystem.current.currentSelectedGameObject!=null&&
             (EventSystem.current.currentSelectedGameObject.GetComponent<InputField>()!=null||EventSystem.current.currentSelectedGameObject.GetComponent<TMPro.TMP_InputField>()!=null));
        bool UIEngaged=>Interaction!=null&&Interaction.RightNearFar!=null&&Interaction.SelectAction!=null&&
            Interaction.SelectAction.action.IsPressed()&&Interaction.RightNearFar.TryGetCurrentUIRaycastResult(out var hit)&&hit.isValid;
        void Update()
        {
            bool held=JumpAction!=null&&JumpAction.action.IsPressed();
            if(Blocked&&held)suppressed=true;if(!held)suppressed=false;
        }
        public bool ReadIsPerformed()=>!Blocked&&!suppressed&&JumpAction!=null&&JumpAction.action.IsPressed();
        public bool ReadWasPerformedThisFrame()=>ReadIsPerformed()&&JumpAction.action.WasPressedThisFrame();
        public bool ReadWasCompletedThisFrame()=>JumpAction!=null&&JumpAction.action.WasReleasedThisFrame();
        public float ReadValue()=>ReadIsPerformed()?1:0;
        public bool TryReadValue(out float value){value=ReadValue();return JumpAction!=null;}
    }
}
