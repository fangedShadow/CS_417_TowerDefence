using System;
using TowerDefence.Shared;
using UnityEngine;

namespace TowerDefence.Battlefield
{
    public enum CatalogState { Locked, Unaffordable, Available }
    public sealed class ConstructionController : MonoBehaviour
    {
        public BattlefieldSession Session;
        public BattlefieldGrid Grid;
        public Transform StructureRoot;
        public PlacedStructure Selected { get; private set; }
        public event Action<PlacedStructure> SelectionChanged;
        public event Action<string, bool> Feedback;
        private bool busy;
        public CatalogState State(ConstructionDefinition entry, out string reason)
        {
            reason = string.Empty;
            if (entry == null || entry.Tiers.Length == 0 || !Session.FactionActive(entry.Faction))
            { reason = "Faction not available"; return CatalogState.Locked; }
            var tier = entry.Tiers[Session.GlobalTier(entry)];
            if (Session.RankNumberFor(entry.Faction) < tier.RequiredRank)
            { reason = "Requires rank " + tier.RequiredRank; return CatalogState.Locked; }
            if (tier.Stats == null) { reason = "Structure unavailable"; return CatalogState.Locked; }
            if (!Session.Resources.CanAfford(tier.Stats.PurchaseCost))
            { reason = "Need " + tier.Stats.PurchaseCost + " Money"; return CatalogState.Unaffordable; }
            return CatalogState.Available;
        }
        public bool Validate(ConstructionDefinition entry, GridCell cell, out string reason)
        {
            if (State(entry, out reason) != CatalogState.Available) return false;
            if (cell == null || !cell.Definition.Buildable) { reason = "Not a buildable cell"; return false; }
            if (entry.IsFoundation)
            {
                if (cell.Definition.Surface != BuildSurface.Water) { reason = "Navy foundations require water"; return false; }
                if (cell.Occupied) { reason = "Cell occupied"; return false; }
            }
            else if (entry.RequiresFoundation)
            {
                if (cell.Definition.Surface != BuildSurface.Water || !cell.HasFoundation || !cell.Foundation.Alive)
                { reason = "Build a Navy foundation first"; return false; }
                if (cell.Structure != null) { reason = "Foundation already occupied"; return false; }
                if (!cell.Foundation.Definition.Supports(entry) || cell.Foundation.Foundation == null)
                { reason = "Incompatible foundation"; return false; }
            }
            else if (cell.Definition.Surface != BuildSurface.Land || cell.Occupied)
            { reason = "Requires free land"; return false; }
            reason = string.Empty; return true;
        }
        private PlacedStructure Create(ConstructionDefinition entry, int tier, GridCell cell, Quaternion rotation)
        {
            var wrapper = new GameObject(entry.DisplayName + " L" + (tier + 1));
            wrapper.SetActive(false);
            wrapper.transform.SetParent(StructureRoot, false);
            wrapper.transform.position = Grid.World(cell);
            wrapper.transform.rotation = rotation;
            if (!entry.IsFoundation && entry.RequiresFoundation)
            {
                wrapper.transform.SetParent(cell.Foundation.Foundation.TowerMount, false);
                wrapper.transform.localPosition = Vector3.zero;
                wrapper.transform.rotation = rotation;
            }
            var model = Instantiate(entry.Tiers[tier].Prefab, wrapper.transform, false);
            var runtime = model.GetComponent<TowerRuntime>();
            if (runtime == null || runtime.Stats == null) { Destroy(wrapper); return null; }
            var instance = wrapper.AddComponent<PlacedStructure>();
            // Awake of the source components runs when activated, before event/resource binding.
            wrapper.SetActive(true);
            instance.Bind(this, entry, tier, cell, runtime);
            return instance;
        }
        public bool TryBuild(ConstructionDefinition entry, GridCell cell, Quaternion rotation, out string reason)
        {
            if (busy) { reason = "Please wait"; return false; }
            if (!Validate(entry, cell, out reason)) return Fail(reason);
            busy = true;
            try
            {
                int tier = Session.GlobalTier(entry);
                var instance = Create(entry, tier, cell, rotation);
                if (instance == null) { reason = "Structure unavailable"; return Fail(reason); }
                if (!Session.Resources.TrySpend(entry.Tiers[tier].Stats.PurchaseCost, 0))
                { instance.Retire(); Destroy(instance.gameObject); reason = "Insufficient Money"; return Fail(reason); }
                if (entry.IsFoundation) cell.Foundation = instance; else cell.Structure = instance;
                Select(instance); Session.NotifyChanged();
                reason = "Built " + entry.DisplayName; Feedback?.Invoke(reason, true); return true;
            }
            finally { busy = false; }
        }
        public bool GlobalUpgradeReason(ConstructionDefinition entry, out string reason)
        {
            if (entry == null || !Session.FactionActive(entry.Faction)) { reason = "Faction unavailable"; return false; }
            int next = Session.GlobalTier(entry) + 1;
            if (next >= entry.Tiers.Length) { reason = "Maximum global tier reached"; return false; }
            var tier = entry.Tiers[next];
            if (Session.RankNumberFor(entry.Faction) < tier.RequiredRank) { reason = "Requires rank " + tier.RequiredRank; return false; }
            if (Session.Resources.Money < tier.GlobalMoneyCost || Session.Resources.UpgradePoints < tier.GlobalPointCost)
            { reason = "Need " + tier.GlobalMoneyCost + " Money and " + tier.GlobalPointCost + " Upgrade Points"; return false; }
            reason = string.Empty; return true;
        }
        public bool TryGlobalUpgrade(ConstructionDefinition entry, out string reason)
        {
            if (busy) { reason = "Please wait"; return false; }
            if (!GlobalUpgradeReason(entry, out reason)) return Fail(reason);
            busy = true;
            try
            {
                int next = Session.GlobalTier(entry) + 1;
                var tier = entry.Tiers[next];
                if (!Session.Resources.TrySpend(tier.GlobalMoneyCost, tier.GlobalPointCost))
                { reason = "Insufficient resources"; return Fail(reason); }
                Session.SetGlobalTier(entry, next);
                reason = "Future " + entry.DisplayName + " builds now use tier " + (next + 1);
                Feedback?.Invoke(reason, true); return true;
            }
            finally { busy = false; }
        }
        public bool IndividualUpgradeReason(PlacedStructure target, out string reason)
        {
            if (target == null || !target.Alive) { reason = "Select a surviving structure"; return false; }
            int next = target.TierIndex + 1;
            if (next >= target.Definition.Tiers.Length) { reason = "Maximum upgrade reached"; return false; }
            var tier = target.Definition.Tiers[next];
            if (tier.IndividualMoneyCost >= tier.GlobalMoneyCost)
            { reason = "Individual upgrade must cost less than global upgrade"; return false; }
            if (Session.RankNumberFor(target.Definition.Faction) < tier.RequiredRank) { reason = "Requires rank " + tier.RequiredRank; return false; }
            if (Session.Resources.Money < tier.IndividualMoneyCost || Session.Resources.UpgradePoints < tier.IndividualPointCost)
            { reason = "Need " + tier.IndividualMoneyCost + " Money and " + tier.IndividualPointCost + " Upgrade Points"; return false; }
            reason = string.Empty; return true;
        }
        public bool TryIndividualUpgrade(PlacedStructure target, out string reason)
        {
            if (busy) { reason = "Please wait"; return false; }
            if (!IndividualUpgradeReason(target, out reason)) return Fail(reason);
            busy = true;
            try
            {
                var nextTier = target.Definition.Tiers[target.TierIndex + 1];
                var cell = target.Cell;
                PlacedStructure mounted = target.Definition.IsFoundation ? cell.Structure : null;
                float healthFraction = target.Runtime.Health.CurrentHealth / target.Runtime.Health.MaxHealth;
                var replacement = Create(target.Definition, target.TierIndex + 1, cell, target.transform.rotation);
                if (replacement == null) { reason = "Upgrade unavailable"; return Fail(reason); }
                if (!Session.Resources.TrySpend(nextTier.IndividualMoneyCost, nextTier.IndividualPointCost))
                { replacement.Retire(); Destroy(replacement.gameObject); reason = "Insufficient resources"; return Fail(reason); }
                replacement.Runtime.Health.PreserveReplacementHealth(healthFraction);
                if (target.Definition.IsFoundation)
                {
                    cell.Foundation = replacement;
                    if (mounted != null)
                    {
                        mounted.transform.SetParent(replacement.Foundation.TowerMount, false);
                        mounted.transform.localPosition = Vector3.zero;
                    }
                }
                else cell.Structure = replacement;
                target.Retire(); Destroy(target.gameObject);
                Select(replacement); Session.NotifyChanged();
                reason = "Upgraded this structure only"; Feedback?.Invoke(reason, true); return true;
            }
            finally { busy = false; }
        }
        public bool TryRepair(PlacedStructure target, out string reason)
        {
            if (busy) { reason = "Please wait"; return false; }
            if (target == null || !target.Alive) { reason = "Select a surviving structure"; return Fail(reason); }
            var health = target.Runtime.Health;
            if (health.CurrentHealth >= health.MaxHealth) { reason = "Health is already full"; return Fail(reason); }
            busy = true;
            try
            {
                if (!Session.Resources.TrySpend(target.Runtime.Stats.RepairCost, 0))
                { reason = "Insufficient Money"; return Fail(reason); }
                health.Repair(health.MaxHealth); Session.NotifyChanged();
                reason = "Repaired to full health"; Feedback?.Invoke(reason, true); return true;
            }
            finally { busy = false; }
        }
        public bool TrySell(PlacedStructure target, out string reason)
        {
            if (busy) { reason = "Please wait"; return false; }
            if (target == null || !target.Alive) { reason = "Select a surviving structure"; return Fail(reason); }
            if (target.Definition.IsFoundation && target.Cell.Structure != null)
            { reason = "Sell the mounted tower before its foundation"; return Fail(reason); }
            busy = true;
            try
            {
                int refund = target.Runtime.Stats.SellValue;
                Clear(target); target.Retire(); Destroy(target.gameObject);
                Session.Resources.AddMoney(refund); Select(null); Session.NotifyChanged();
                reason = "Sold for " + refund + " Money"; Feedback?.Invoke(reason, true); return true;
            }
            finally { busy = false; }
        }
        private void Clear(PlacedStructure target)
        {
            if (target.Cell == null) return;
            if (target.Cell.Foundation == target) target.Cell.Foundation = null;
            if (target.Cell.Structure == target) target.Cell.Structure = null;
        }
        public void StructureDestroyed(PlacedStructure target)
        {
            if (target == null) return;
            if (target.Definition != null && target.Definition.IsFoundation && target.Cell != null && target.Cell.Structure != null)
            {
                var mounted = target.Cell.Structure;
                mounted.Retire(); target.Cell.Structure = null; Destroy(mounted.gameObject);
            }
            Clear(target); target.Retire();
            if (Selected == target) Select(null);
            Destroy(target.gameObject); Session?.NotifyChanged();
        }
        public void Select(PlacedStructure target) { Selected = target; SelectionChanged?.Invoke(target); }
        private bool Fail(string reason) { Feedback?.Invoke(reason, false); return false; }
    }
}
