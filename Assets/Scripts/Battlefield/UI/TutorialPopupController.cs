using System;
using System.Collections.Generic;
using UnityEngine;

namespace TowerDefence.Battlefield
{
    public sealed class TutorialPopupController : MonoBehaviour
    {
        public Transform PopupAnchor;
        public Camera Viewer;
        [Range(1.5f,2)] public float OpenDistance=1.85f;
        public PopupView[] PopupPrefabs = Array.Empty<PopupView>();
        private PopupView current;
        private readonly HashSet<string> seen = new HashSet<string>();
        private int helpIndex;
        public PopupView Current => current;
        public bool ManagedByGame { get; private set; }
        public void MarkSeen(string id)=>seen.Add(id);
        public PopupView ShowForGame(string id)
        { ManagedByGame=true;return Create(id); }
        public void ReleaseGameControl(){ManagedByGame=false;Close();}
        public void ShowNextTopic()
        {
            if (PopupPrefabs.Length == 0) return;
            var prefab = PopupPrefabs[helpIndex++ % PopupPrefabs.Length];
            if (prefab.Definition != null) Show(prefab.Definition.Id);
        }
        public PopupView Show(string id)
        { if(ManagedByGame)return null;return Create(id); }
        private PopupView Create(string id)
        {
            foreach (var prefab in PopupPrefabs)
            {
                if (prefab.Definition == null || prefab.Definition.Id != id) continue;
                Close();
                if(Viewer!=null)
                {
                    Vector3 forward=Viewer.transform.forward;
                    var eye=Viewer.transform.position;
                    var origin=Viewer.GetComponentInParent<Unity.XR.CoreUtils.XROrigin>();
                    if(origin!=null&&eye.y<origin.transform.position.y+.7f)eye.y=origin.transform.position.y+1.7f;
                    PopupAnchor.SetPositionAndRotation(eye+forward*OpenDistance,Quaternion.LookRotation(forward));
                }
                current = Instantiate(prefab, PopupAnchor, false); current.Bind(this); return current;
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
