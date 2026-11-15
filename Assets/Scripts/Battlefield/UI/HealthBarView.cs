using TMPro;
using TowerDefence.Shared;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefence.Battlefield
{
    // Shared by the HUD and runtime tower wrappers; the authored tower stays untouched.
    public sealed class HealthBarView : MonoBehaviour
    {
        public Image Fill;
        public TMP_Text Value;
        public bool Floating;
        [Range(0,1)] public float MediumThreshold=.6f, CriticalThreshold=.25f;
        public Color Healthy=new Color(.23f,.91f,.53f), Medium=new Color(1,.77f,.24f), Critical=new Color(1,.29f,.29f);
        public float TransitionSpeed=3;
        public CombatHealth Health {get;private set;}
        Camera viewer;
        GamePanelController panel;
        Canvas canvas;
        float desired=1;
        public void Bind(CombatHealth health,Camera camera,GamePanelController owner=null)
        {
            if(Health!=null)Health.Changed-=Refresh;
            Health=health;viewer=camera;panel=owner;canvas=GetComponent<Canvas>();
            if(Health!=null)Health.Changed+=Refresh;
            Refresh();ApplyFill(desired);
        }
        public void Refresh()
        {
            desired=Health!=null?Mathf.Clamp01(Health.CurrentHealth/Health.MaxHealth):0;
            Fill.color=desired<=CriticalThreshold?Critical:desired<=MediumThreshold?Medium:Healthy;
            if(Value!=null)Value.text=Health!=null?$"{Health.CurrentHealth:0} / {Health.MaxHealth:0}":"0 / 0";
        }
        void LateUpdate()
        {
            ApplyFill(Mathf.MoveTowards(Fill.fillAmount,desired,Time.unscaledDeltaTime*TransitionSpeed));
            if(!Floating)return;
            if(canvas!=null)canvas.enabled=Health!=null&&Health.IsAlive&&(panel==null||!panel.CommanderOpen);
            if(viewer!=null)transform.rotation=Quaternion.LookRotation(transform.position-viewer.transform.position,Vector3.up);
        }
        public void ApplyFill(float fraction)
        {
            fraction=Mathf.Clamp01(fraction);
            // Unity draws an Image with no sprite as a simple quad, ignoring Filled mode.
            // Anchor the foreground to the left of its dedicated track instead.
            Fill.type=Image.Type.Simple;Fill.fillAmount=fraction;
            var rect=Fill.rectTransform;rect.anchorMin=Vector2.zero;rect.anchorMax=new Vector2(fraction,1);
            rect.offsetMin=rect.offsetMax=Vector2.zero;
        }
        void OnDestroy(){if(Health!=null)Health.Changed-=Refresh;}
    }
}
