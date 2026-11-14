using System.Collections.Generic;
using TowerDefence.Shared;
using UnityEngine;

namespace TowerDefence.Navy
{
    [RequireComponent(typeof(TowerRuntime))]
    public sealed class NavySupportTower : MonoBehaviour
    {
        [SerializeField] private LayerMask towerLayers = ~0;
        [SerializeField, Min(0.05f)] private float scanInterval = 0.2f;
        private HashSet<ITowerBuffReceiver> affected = new HashSet<ITowerBuffReceiver>();
        private float remaining;
        private TowerRuntime runtime;
        private void Awake() => runtime = GetComponent<TowerRuntime>();
        private void Update()
        {
            remaining -= Time.deltaTime;
            if (remaining <= 0) { RefreshBuffs(); remaining = scanInterval; }
        }
        private static bool Exists(ITowerBuffReceiver receiver) => receiver is Component c && c != null;
        public void RefreshBuffs()
        {
            if (runtime == null) runtime = GetComponent<TowerRuntime>();
            if (!runtime.CanReceiveBuff) { RemoveAll(); return; }
            var next = new HashSet<ITowerBuffReceiver>();
            float radius = runtime.SupportRange;
            foreach (var collider in Physics.OverlapSphere(transform.position, radius,
                towerLayers, QueryTriggerInteraction.Collide))
            {
                var receiver = collider.GetComponentInParent<ITowerBuffReceiver>();
                if (!Exists(receiver) || !receiver.CanReceiveBuff || receiver.Faction != Faction.Navy ||
                    (receiver.BuffTransform.position - transform.position).sqrMagnitude >
                    radius * radius) continue;
                TowerBuff buff;
                float strength = runtime.Stats.BuffStrength;
                switch (receiver.Kind)
                {
                    case TowerKind.Cannon: buff = new TowerBuff(damage: strength); break;
                    case TowerKind.Turret: buff = new TowerBuff(range: strength); break;
                    case TowerKind.Slow: buff = new TowerBuff(defenseFrequency: strength); break;
                    default: continue;
                }
                if (next.Add(receiver)) receiver.SetBuff(this, buff);
            }
            foreach (var receiver in affected)
                if (Exists(receiver) && !next.Contains(receiver)) receiver.RemoveBuff(this);
            affected = next;
        }
        private void RemoveAll()
        {
            foreach (var receiver in affected)
                if (Exists(receiver)) receiver.RemoveBuff(this);
            affected.Clear();
        }
        private void OnDisable() => RemoveAll();
    }
}
