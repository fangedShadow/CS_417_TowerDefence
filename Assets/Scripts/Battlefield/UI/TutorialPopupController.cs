using System;
using System.Collections.Generic;
using UnityEngine;

namespace TowerDefence.Battlefield
{
    public sealed class TutorialPopupController : MonoBehaviour
    {
        public Transform PopupAnchor;
        public PopupView[] PopupPrefabs = Array.Empty<PopupView>();
        private PopupView current;
        private readonly HashSet<string> seen = new HashSet<string>();
        private int helpIndex;
        public PopupView Current => current;
        public void ShowNextTopic()
        {
            if (PopupPrefabs.Length == 0) return;
            var prefab = PopupPrefabs[helpIndex++ % PopupPrefabs.Length];
            if (prefab.Definition != null) Show(prefab.Definition.Id);
        }
        public PopupView Show(string id)
        {
            foreach (var prefab in PopupPrefabs)
            {
                if (prefab.Definition == null || prefab.Definition.Id != id) continue;
                Close(); current = Instantiate(prefab, PopupAnchor, false); current.Bind(this); return current;
            }
            return null;
        }
        public bool ShowOnce(string id)
        {
            if (seen.Contains(id)) return false;
            if (Show(id) == null) return false;
            seen.Add(id); return true;
        }
        public void Close()
        {
            if (current != null) { current.gameObject.SetActive(false); Destroy(current.gameObject); }
            current = null;
        }
    }
}
