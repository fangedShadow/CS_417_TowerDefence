using System;
using TowerDefence.Shared;
using UnityEngine;

namespace TowerDefence.Battlefield
{
    [Serializable]
    public sealed class RankEntry
    {
        public string Name;
        [Min(0)] public int RequiredExp;
    }
    [CreateAssetMenu(menuName = "Tower Defence/Faction Ranks")]
    public sealed class FactionRankData : ScriptableObject
    {
        public Faction Faction = Faction.Navy;
        public RankEntry[] Ranks = Array.Empty<RankEntry>();
        public int IndexFor(int exp)
        {
            int result = 0;
            for (int i = 1; i < Ranks.Length; i++) if (exp >= Ranks[i].RequiredExp) result = i;
            return result;
        }
    }
}
