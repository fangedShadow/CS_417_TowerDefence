using System;
using System.Collections.Generic;
using TowerDefence.Shared;
using UnityEngine;

namespace TowerDefence.Battlefield
{
    public sealed class BattlefieldSession : MonoBehaviour
    {
        public PlayerResourceConfig ResourceConfig;
        public FactionRankData NavyRanks;
        public ConstructionDefinition[] Catalog = Array.Empty<ConstructionDefinition>();
        private PlayerResources resources;
        private readonly Dictionary<string, int> globalTiers = new Dictionary<string, int>();
        private readonly HashSet<Faction> activeFactions = new HashSet<Faction> { Faction.Navy };
        private readonly Dictionary<Faction, FactionRankData> additionalRanks = new Dictionary<Faction, FactionRankData>();
        public event Action Changed;
        public event Action<Faction> FactionRequested;
        public PlayerResources Resources
        {
            get
            {
                if (resources == null)
                {
                    resources = ResourceConfig != null ? ResourceConfig.CreateRuntime() : new PlayerResources();
                    resources.Changed += NotifyChanged;
                }
                return resources;
            }
        }
        public int RankIndex => NavyRanks != null ? NavyRanks.IndexFor(Resources.Exp) : 0;
        public int RankNumber => RankIndex + 1;
        public string RankName => NavyRanks != null && NavyRanks.Ranks.Length > 0 ? NavyRanks.Ranks[RankIndex].Name : "Seaman";
        public int NextRankExp => NavyRanks != null && RankIndex + 1 < NavyRanks.Ranks.Length ? NavyRanks.Ranks[RankIndex + 1].RequiredExp : -1;
        public float RankProgress
        {
            get
            {
                if (NextRankExp < 0) return 1;
                int start = NavyRanks.Ranks[RankIndex].RequiredExp;
                return Mathf.InverseLerp(start, NextRankExp, Resources.Exp);
            }
        }
        public int GlobalTier(ConstructionDefinition entry) => entry != null && globalTiers.TryGetValue(entry.Id, out int tier) ? tier : 0;
        public void SetGlobalTier(ConstructionDefinition entry, int tier) { globalTiers[entry.Id] = Mathf.Clamp(tier, 0, entry.Tiers.Length - 1); NotifyChanged(); }
        public bool FactionActive(Faction faction) => activeFactions.Contains(faction);
        public void RegisterFaction(Faction faction, FactionRankData ranks = null)
        {
            activeFactions.Add(faction);
            if (ranks != null && ranks.Faction == faction) additionalRanks[faction] = ranks;
            NotifyChanged();
        }
        public int RankNumberFor(Faction faction) => faction == Faction.Navy ? RankNumber :
            additionalRanks.TryGetValue(faction, out var data) ? data.IndexFor(Resources.Exp) + 1 : 0;
        public bool RequestFaction(Faction faction)
        {
            FactionRequested?.Invoke(faction);
            return FactionActive(faction);
        }
        public void NotifyChanged() => Changed?.Invoke();
    }
}
