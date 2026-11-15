using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefence.Battlefield
{
    public sealed class PopupView : MonoBehaviour
    {
        public TutorialDefinition Definition;
        public TMP_Text Title, Body, NextLabel, CloseLabel, Progress;
        public Button Next, Back, Close;
        private int page;
        private TutorialPopupController owner;
        public void Bind(TutorialPopupController controller)
        {
            owner = controller; page = 0;
            Next.onClick.RemoveAllListeners(); Back.onClick.RemoveAllListeners(); Close.onClick.RemoveAllListeners();
            Next.onClick.AddListener(() => { if (page + 1 < Definition.Pages.Length) { page++; Refresh(); } else owner.Close(); });
            Back.onClick.AddListener(() => { page = Mathf.Max(0, page - 1); Refresh(); });
            Close.onClick.AddListener(owner.Close); Refresh();
        }
        private void Refresh()
        {
            Title.text = Definition.Title;
            if(Progress!=null)Progress.text=$"{page+1} / {Definition.Pages.Length}";
            Body.text = Definition.Pages.Length > 0 ? Definition.Pages[page] : string.Empty;
            Back.interactable = page > 0;
            NextLabel.text = page + 1 < Definition.Pages.Length ? "Next" : "Done";
            CloseLabel.text = "Close";
        }
        public void Confirmation(string title, string message, Action confirm)
        {
            Title.text = title; Body.text = message;
            Back.gameObject.SetActive(false); NextLabel.text = "Confirm sale"; CloseLabel.text = "Keep structure";
            Next.onClick.RemoveAllListeners();
            Next.onClick.AddListener(() => { owner.Close(); confirm(); });
        }
        public void Present(string title,string body,string nextLabel,Action next,string backLabel,Action back,string closeLabel,Action close)
        {
            Title.text=title;Body.text=body;
            if(Progress!=null)Progress.text=string.Empty;
            Configure(Next,nextLabel,next);Configure(Back,backLabel,back);Configure(Close,closeLabel,close);
            foreach(var button in new[]{Next,Back,Close}){var feedback=button.GetComponent<UnavailableActionFeedback>();if(feedback!=null)feedback.Configure(owner.GetComponent<GamePanelController>(),"This faction is not available yet.");}
        }
        private static void Configure(Button button,string label,Action action)
        {
            button.onClick.RemoveAllListeners();button.gameObject.SetActive(!string.IsNullOrEmpty(label));button.interactable=action!=null;
            var text=button.GetComponentInChildren<TMP_Text>();if(text!=null){text.text=label;text.enableAutoSizing=true;text.fontSizeMin=16;text.fontSizeMax=22;}
            if(action!=null)button.onClick.AddListener(()=>action());
        }
    }
}
