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
        public bool Alive => !retired && Runtime != null && Runtime.Health != null && Runtime.Health.IsAlive;
        public void Bind(ConstructionController controller, ConstructionDefinition definition, int tier, GridCell cell, TowerRuntime runtime)
        {
            owner = controller; Definition = definition; TierIndex = tier; Cell = cell; Runtime = runtime;
            Foundation = runtime.GetComponent<NavyFoundation>();
            if (Runtime.Health == null) Runtime.ResetRuntime();
            Runtime.Health.Died += OnDied;
            Runtime.Health.Damaged += OnDamaged;
            var oil = Runtime.GetComponent<OilExtractor>();
            if (oil != null) oil.BindResources(owner.Session.Resources);
        }
        private void OnDied() { if (!retired && owner != null) owner.StructureDestroyed(this); }
        private void OnDamaged(DamageInfo damage) { if (owner != null) owner.Session.NotifyChanged(); }
        public void Retire()
        {
            retired = true;
            if (Runtime != null && Runtime.Health != null)
            {
                Runtime.Health.Died -= OnDied;
                Runtime.Health.Damaged -= OnDamaged;
            }
        }
        private void OnDestroy() { if (!retired && owner != null) owner.StructureDestroyed(this); Retire(); }
    }
}
