using TowerDefence.Navy;
using TowerDefence.Shared;
using UnityEngine;

namespace TowerDefence.Battlefield
{
    // Added to a wrapper instance, never to the authored source prefab.
    public sealed class PlacedStructure : MonoBehaviour
    {
        public ConstructionDefinition Definition { get; private set; }
        public int TierIndex { get; private set; }
        public GridCell Cell { get; private set; }
        public TowerRuntime Runtime { get; private set; }
        public NavyFoundation Foundation { get; private set; }
        private ConstructionController owner;
        private bool retired;
        public HealthBarView HealthBar {get;private set;}
        public bool Alive => !retired && Runtime != null && Runtime.Health != null && Runtime.Health.IsAlive;
        public void Bind(ConstructionController controller, ConstructionDefinition definition, int tier, GridCell cell, TowerRuntime runtime)
        {
            owner = controller; Definition = definition; TierIndex = tier; Cell = cell; Runtime = runtime;
            Foundation = runtime.GetComponent<NavyFoundation>();
            if (Runtime.Health == null) Runtime.ResetRuntime();
            Runtime.Health.Died += OnDied;
            Runtime.Health.Damaged += OnDamaged;
            var oil = Runtime.GetComponent<OilExtractor>();
            if (oil != null) oil.BindResources(owner.Session.Resources,owner.Session);
            if (!definition.IsFoundation && owner.TowerHealthPrefab != null)
            {
                float top=Runtime.transform.position.y+2;
                foreach(var renderer in Runtime.GetComponentsInChildren<MeshRenderer>())top=Mathf.Max(top,renderer.bounds.max.y);
                HealthBar=Instantiate(owner.TowerHealthPrefab,transform,false);
                HealthBar.transform.position=new Vector3(Runtime.transform.position.x,top+.35f,Runtime.transform.position.z);
                var panel=owner.Session.Game!=null?owner.Session.Game.Panel:FindAnyObjectByType<GamePanelController>();
                HealthBar.Bind(Runtime.Health,panel!=null?panel.Interaction.PhysicalCamera:Camera.main,panel);
            }
        }
        private void OnDied() { if (!retired && owner != null) owner.StructureDestroyed(this); }
        private void OnDamaged(DamageInfo damage) { if (owner != null) owner.Session.NotifyChanged(); }
        public void Retire()
        {
            retired = true;
            if(HealthBar!=null)HealthBar.gameObject.SetActive(false);
            if (Runtime != null && Runtime.Health != null)
            {
                Runtime.Health.Died -= OnDied;
                Runtime.Health.Damaged -= OnDamaged;
                // A replacement/removal must stop gameplay immediately, before end-of-frame destruction.
                foreach(var component in Runtime.GetComponents<MonoBehaviour>())
                    if(!(component is CombatHealth))component.enabled=false;
            }
        }
        private void OnDestroy() { if (!retired && owner != null) owner.StructureDestroyed(this); Retire(); }
    }
}
