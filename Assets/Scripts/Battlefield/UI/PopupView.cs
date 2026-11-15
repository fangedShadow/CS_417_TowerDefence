using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefence.Battlefield
{
    public sealed class PopupView : MonoBehaviour
    {
        public TutorialDefinition Definition;
        public TMP_Text Title, Body, NextLabel, CloseLabel;
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
    }
}
