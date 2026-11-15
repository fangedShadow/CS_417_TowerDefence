using UnityEngine;
using TMPro;

namespace TowerDefence.Battlefield
{
    public sealed class CommandHud : MonoBehaviour
    {
        public GamePanelController Panel;
        public BattlefieldSession Session;
        public TMP_Text[] EconomyLabels=System.Array.Empty<TMP_Text>();
        public Camera Viewer;
        public Transform RightController;
        public Vector3 ControllerPositionOffset=new Vector3(0,.12f,.1f);
        public Vector3 ControllerRotationOffset=new Vector3(65,0,0);
        public bool CompactVisible=true;
        public GameObject Collapsed, Expanded;
        public Vector3 ViewOffset=new Vector3(.68f,-.46f,1.8f);
        public float CompactScale=.00145f, ExpandedScale=.0026f;
        void OnEnable(){if(Session!=null)Session.Changed+=RefreshEconomy;RefreshEconomy();}
        void OnDisable(){if(Session!=null)Session.Changed-=RefreshEconomy;}
        public void RefreshEconomy()
        {
            if(Session==null)return;
            string value=$"MONEY  {Session.Resources.Money:N0}\nRANK {Session.RankNumber}  {Session.RankName}";
            foreach(var label in EconomyLabels)if(label!=null)label.text=value;
        }
        public void SetExpanded(bool value)
        {
            Collapsed.SetActive(!value&&CompactVisible);Expanded.SetActive(value);
            if(value)
            {
                Vector3 forward=Vector3.ProjectOnPlane(Viewer.transform.forward,Vector3.up).normalized;
                transform.SetPositionAndRotation(Viewer.transform.position+forward*Panel.OpenDistance+Vector3.Cross(Vector3.up,forward)*-.85f-Vector3.up*.1f,Quaternion.LookRotation(forward));
                transform.localScale=Vector3.one*ExpandedScale;
            }
        }
        public void ToggleCompact(){CompactVisible=!CompactVisible;if(!Panel.CommanderOpen)Collapsed.SetActive(CompactVisible);}
        void LateUpdate()
        {
            if(Panel==null||RightController==null||Panel.CommanderOpen)return;
            transform.SetPositionAndRotation(RightController.TransformPoint(ControllerPositionOffset),RightController.rotation*Quaternion.Euler(ControllerRotationOffset));
            transform.localScale=Vector3.one*CompactScale;
        }
    }
}
