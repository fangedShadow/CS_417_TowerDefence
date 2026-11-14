using System;
using TowerDefence.Shared;
using UnityEngine;

namespace TowerDefence.Battlefield
{
    [Serializable]
    public sealed class ConstructionTier
    {
        public GameObject Prefab;
        public Sprite Thumbnail;
        [Min(1)] public int RequiredRank = 1;
        [Min(0)] public int GlobalMoneyCost;
        [Min(0)] public int GlobalPointCost;
        [Min(0)] public int IndividualMoneyCost;
        [Min(0)] public int IndividualPointCost;
        public TowerStatsData Stats => Prefab != null ? Prefab.GetComponent<TowerRuntime>()?.Stats : null;
    }

    [CreateAssetMenu(menuName = "Tower Defence/Construction Catalog Entry")]
    public sealed class ConstructionDefinition : ScriptableObject
    {
        public string Id;
        public string DisplayName;
        public Faction Faction = Faction.Navy;
        public TowerKind Kind;
        public bool RequiresFoundation = true;
        public TowerKind[] SupportedTowerKinds;
        public ConstructionTier[] Tiers = Array.Empty<ConstructionTier>();
        public bool IsFoundation => Kind == TowerKind.Foundation;
        public bool Supports(ConstructionDefinition other) => IsFoundation && other != null &&
            other.Faction == Faction && Array.IndexOf(SupportedTowerKinds, other.Kind) >= 0;
    }
}
