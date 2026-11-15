using UnityEngine;
using UnityEngine.EventSystems;

namespace TowerDefence.Battlefield
{
    // One small transition per panel or button, with no animator assets or allocations.
    public sealed class UiTransition : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        public CanvasGroup Group;
        public float Duration=.12f;
        Vector3 rest;
        float target=1;
        public bool IsHiding {get;private set;}
        void Awake(){rest=transform.localScale;}
        void OnEnable(){IsHiding=false;if(Group!=null){Group.alpha=0;Group.interactable=Group.blocksRaycasts=true;}}
        public void Show(){IsHiding=false;gameObject.SetActive(true);if(Group!=null)Group.interactable=Group.blocksRaycasts=true;}
        public void Hide(){IsHiding=true;if(Group!=null)Group.interactable=Group.blocksRaycasts=false;else gameObject.SetActive(false);}
        void Update()
        {
            if(Group!=null)
            {
                Group.alpha=Mathf.MoveTowards(Group.alpha,IsHiding?0:1,Time.unscaledDeltaTime/Mathf.Max(.01f,Duration));
                if(IsHiding&&Group.alpha==0)gameObject.SetActive(false);
            }
            if(Group==null&&transform.localScale!=rest*target)transform.localScale=Vector3.Lerp(transform.localScale,rest*target,Time.unscaledDeltaTime*25);
        }
        public void OnPointerEnter(PointerEventData e){target=1.015f;}
        public void OnPointerExit(PointerEventData e){target=1;}
        public void OnPointerDown(PointerEventData e){target=.98f;}
        public void OnPointerUp(PointerEventData e){target=1;}
    }
}
