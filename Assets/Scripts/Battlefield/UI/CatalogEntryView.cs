using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefence.Battlefield
{
    public sealed class CatalogEntryView : MonoBehaviour
    {
        public TMP_Text Title, Details, StateLabel;
        public Image Background, Thumbnail;
        public Button Action;
        private ConstructionDefinition definition;
        private GamePanelController panel;
        private bool global;
        public void Bind(GamePanelController owner, ConstructionDefinition entry, bool globalUpgrade)
        {
            panel = owner; definition = entry; global = globalUpgrade;
            Action.onClick.RemoveAllListeners();
            Action.onClick.AddListener(() =>
            {
                if (global) panel.Construction.TryGlobalUpgrade(definition, out _);
                else panel.BeginConstruction(definition);
            });
            Refresh();
        }
        public void Refresh()
        {
            int current = panel.Session.GlobalTier(definition);
            var now = definition.Tiers[current];
            var state = panel.Construction.State(definition, out string reason);
            Title.text = definition.DisplayName + "  •  L" + (current + 1);
            var stats = now.Stats;
            if (!global)
                Details.text = stats.PurchaseCost + " Money  |  " + stats.MaxHealth + " HP  |  Range " + stats.AttackRange;
            else
            {
                int next = current + 1;
                bool eligible = panel.Construction.GlobalUpgradeReason(definition, out reason);
                if (next >= definition.Tiers.Length)
                { state = CatalogState.Locked; Details.text = "Maximum catalog tier"; }
                else
                {
                    var to = definition.Tiers[next];
                    var after = to.Stats;
                    Details.text = "L" + (current + 1) + " → L" + (next + 1) + "  |  " + to.GlobalMoneyCost + " Money + " + to.GlobalPointCost + " Points\n" +
                        "HP " + stats.MaxHealth + "→" + after.MaxHealth + "  Dmg " + stats.Damage + "→" + after.Damage + "  Range " + stats.AttackRange + "→" + after.AttackRange;
                    state = eligible ? CatalogState.Available : panel.Session.RankNumberFor(definition.Faction) < to.RequiredRank || !panel.Session.FactionActive(definition.Faction) ?
                        CatalogState.Locked : CatalogState.Unaffordable;
                }
            }
            StateLabel.text = state == CatalogState.Available ? global ? "Unlock next tier" : "Select to build" : reason;
            Action.interactable = state == CatalogState.Available;
            Background.color = state == CatalogState.Locked ? new Color(0.22f, 0.25f, 0.28f) :
                state == CatalogState.Unaffordable ? new Color(0.48f, 0.12f, 0.14f) : new Color(0.08f, 0.25f, 0.31f);
            Thumbnail.sprite = now.Thumbnail;
            Thumbnail.enabled = now.Thumbnail != null;
        }
    }
}
