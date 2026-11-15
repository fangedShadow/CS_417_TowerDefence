using System.Collections.Generic;
using TowerDefence.Shared;
using UnityEngine;

namespace TowerDefence.Battlefield
{
    public sealed class ConstructionPreview : MonoBehaviour
    {
        public ConstructionController Construction;
        public Material ValidMaterial, InvalidMaterial, OccupiedMaterial;
        public ConstructionDefinition Entry { get; private set; }
        public GridCell HoveredCell { get; private set; }
        public string Reason { get; private set; }
        public bool Valid { get; private set; }
        public float Rotation { get; private set; }
        private GameObject ghost;
        public GameObject Ghost => ghost;
        public event System.Action<GameObject> Cancelled;
        private readonly Dictionary<GridCell, Renderer> markers = new Dictionary<GridCell, Renderer>();
        private Renderer[] ghostRenderers;
        public event System.Action Changed;
        private void OnEnable() { if (Construction != null) Construction.Session.Changed += Refresh; }
        private void OnDisable() { if (Construction != null) Construction.Session.Changed -= Refresh; Clear(false); }
        public void Begin(ConstructionDefinition entry)
        {
            Clear(false); Entry = entry; Rotation = 0;
            int tier = Construction.Session.GlobalTier(entry);
            ghost = Instantiate(entry.Tiers[tier].Prefab);
            ghost.name = "PlacementPreview";
            foreach (var behaviour in ghost.GetComponentsInChildren<MonoBehaviour>()) behaviour.enabled = false;
            foreach (var collider in ghost.GetComponentsInChildren<Collider>()) collider.enabled = false;
            foreach (var body in ghost.GetComponentsInChildren<Rigidbody>()) { body.isKinematic = true; body.useGravity = false; }
            foreach (var t in ghost.GetComponentsInChildren<Transform>()) t.gameObject.layer = 2;
            ghostRenderers = ghost.GetComponentsInChildren<Renderer>();
            ghost.SetActive(false);
            foreach (var cell in Construction.Grid.Cells)
            {
                if (cell.Definition.Surface != BuildSurface.Water) continue;
                var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                marker.name = "BuildCell " + cell.Definition.Coordinates;
                marker.transform.SetParent(transform, false);
                marker.transform.position = Construction.Grid.World(cell) + Vector3.up * 0.64f;
                marker.transform.localScale = new Vector3(Construction.Grid.CellSize * 0.88f, 0.025f, Construction.Grid.CellSize * 0.88f);
                marker.layer = 2; Destroy(marker.GetComponent<Collider>());
                markers[cell] = marker.GetComponent<Renderer>();
            }
            Refresh();
        }
        public void Hover(Vector3 world)
        {
            if (Entry == null) return;
            var cell = Construction.Grid.At(world);
            if (HoveredCell == cell && ghost != null && ghost.activeSelf) return;
            HoveredCell = cell; Refresh();
        }
        public void Rotate() { Rotation = (Rotation + 90) % 360; Refresh(); }
        public void Refresh()
        {
            if (Entry == null) return;
            foreach (var pair in markers)
            {
                bool allowed = Construction.Validate(Entry, pair.Key, out _);
                pair.Value.sharedMaterial = allowed ? ValidMaterial : pair.Key.Occupied ? OccupiedMaterial : InvalidMaterial;
            }
            Valid = Construction.Validate(Entry, HoveredCell, out string reason);
            Reason = Valid ? "Press right trigger to build" : reason;
            if (ghost != null)
            {
                ghost.SetActive(HoveredCell != null);
                if (HoveredCell != null)
                {
                    ghost.transform.position = !Entry.IsFoundation && HoveredCell.Foundation != null ?
                        HoveredCell.Foundation.Foundation.TowerMount.position : Construction.Grid.World(HoveredCell);
                    ghost.transform.rotation = Quaternion.Euler(0, Rotation, 0);
                    foreach (var renderer in ghostRenderers)
                    {
                        var materials = new Material[renderer.sharedMaterials.Length];
                        for (int i = 0; i < materials.Length; i++) materials[i] = Valid ? ValidMaterial : InvalidMaterial;
                        renderer.sharedMaterials = materials;
                    }
                }
            }
            Changed?.Invoke();
        }
        public bool Confirm()
        {
            if (Entry == null) return false;
            if (!Construction.TryBuild(Entry, HoveredCell, Quaternion.Euler(0, Rotation, 0), out _)) return false;
            Clear(false); return true;
        }
        public void Cancel()
        { Clear(true); }
        private void Clear(bool cancelled)
        {
            if (cancelled && Entry != null) Cancelled?.Invoke(ghost);
            if (ghost != null) Destroy(ghost);
            foreach (var renderer in markers.Values) if (renderer != null) Destroy(renderer.gameObject);
            markers.Clear(); ghost = null; Entry = null; HoveredCell = null; Valid = false; Reason = string.Empty;
            Changed?.Invoke();
        }
    }
}
