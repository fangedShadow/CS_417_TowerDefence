using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefence.Battlefield
{
    public sealed class TowerActionView : MonoBehaviour
    {
        public TMP_Text Heading, Stats, Costs;
        public Button Upgrade, Repair, Sell;
        private GamePanelController panel;
        public void Bind(GamePanelController owner)
        {
            panel = owner;
            Upgrade.onClick.AddListener(() => { panel.Tutorials.ShowOnce("UpgradeTower"); panel.Construction.TryIndividualUpgrade(panel.Construction.Selected, out _); });
            Repair.onClick.AddListener(() => { panel.Tutorials.ShowOnce("RepairTower"); panel.Construction.TryRepair(panel.Construction.Selected, out _); });
            Sell.onClick.AddListener(() => panel.ConfirmSale());
            Refresh();
        }
        public void Refresh()
        {
            if (panel == null) return;
            var target = panel.Construction.Selected;
            bool valid = target != null && target.Alive;
            Heading.text = valid ? target.Definition.DisplayName + " L" + (target.TierIndex + 1) : "SELECT A STRUCTURE";
            if (!valid)
            {
                Stats.text = "Point at a tower or foundation in the world or on the map.";
                Costs.text = string.Empty; Upgrade.interactable = Repair.interactable = Sell.interactable = false; return;
            }
            var runtime = target.Runtime; var stats = runtime.Stats;
            Stats.text = "Health  " + runtime.Health.CurrentHealth.ToString("0") + " / " + runtime.Health.MaxHealth.ToString("0") +
                "\nDamage  " + runtime.Damage.ToString("0.#") + "\nRange  " + runtime.AttackRange.ToString("0.#") +
                "\nFire interval  " + runtime.AttackInterval.ToString("0.##") + "s\nCooldown  " + runtime.FireCooldown.ToString("0.0") + "s" +
                "\nArmor  " + stats.Armor + "\nSlow  " + (stats.SlowStrength * 100).ToString("0") + "%" +
                "\nPoison  " + stats.PoisonDamage + " / tick";
            if (stats.Kind == TowerDefence.Shared.TowerKind.Resource)
                Stats.text += "\nProduction  " + stats.ResourceGenerationAmount + " Money / " + stats.ResourceExpGenerationAmount + " EXP / " + stats.ResourceUpgradePointGenerationAmount + " Points";
            if (target.Foundation != null) Stats.text += "\nFoundation range  ×" + target.Foundation.RangeMultiplier;
            Upgrade.interactable = panel.Construction.IndividualUpgradeReason(target, out string reason);
            int next = target.TierIndex + 1;
            string cost = next < target.Definition.Tiers.Length ? target.Definition.Tiers[next].IndividualMoneyCost + " Money + " + target.Definition.Tiers[next].IndividualPointCost + " Points" : "MAXIMUM LEVEL";
            Costs.text = "Upgrade: " + cost + "\n" + (Upgrade.interactable ? "Only this structure changes" : reason) +
                "\nRepair: " + stats.RepairCost + " Money\nSell refund: " + stats.SellValue + " Money";
            Repair.interactable = runtime.CurrentHealth < runtime.Health.MaxHealth && panel.Session.Resources.Money >= stats.RepairCost;
            Sell.interactable = !target.Definition.IsFoundation || target.Cell.Structure == null;
        }
    }
}
