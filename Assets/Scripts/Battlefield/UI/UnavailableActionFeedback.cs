using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TowerDefence.Battlefield
{
    // Disabled Buttons ignore clicks; this companion explains the reason without running their action.
    [RequireComponent(typeof(Button))]
    public sealed class UnavailableActionFeedback : MonoBehaviour, IPointerClickHandler
    {
        public GamePanelController Panel;
        public string Reason;
        Color normalColor;
        bool styled;
        public void Configure(GamePanelController panel,string reason)
        {
            Panel=panel;Reason=reason;
            var button=GetComponent<Button>();var image=button.targetGraphic as Image;
            if(button.interactable){if(styled&&image!=null)image.color=normalColor;styled=false;return;}
            if(!styled&&image!=null){normalColor=image.color;styled=true;}
            if(image!=null)image.color=reason.Contains("Money")||reason.Contains("money")||reason.Contains("Points")||reason.Contains("resources")?
                new Color(.48f,.10f,.13f):new Color(.23f,.27f,.31f);
            var colors=button.colors;colors.disabledColor=new Color(.8f,.8f,.8f,1);button.colors=colors;
        }
        public void OnPointerClick(PointerEventData data)
        {
            if(!GetComponent<Button>().interactable&&Panel!=null&&!string.IsNullOrEmpty(Reason))
            {
                Panel.ShowError(Reason);
                var popup=GetComponentInParent<PopupView>();if(popup!=null&&popup.Progress!=null){popup.Progress.text=Reason;popup.Progress.color=new Color(1,.35f,.32f);}
            }
        }
    }
}
