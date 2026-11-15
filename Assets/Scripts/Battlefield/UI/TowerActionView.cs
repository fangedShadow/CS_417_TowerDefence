using TMPro;
using TowerDefence.Navy;
using TowerDefence.Shared;
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
        void Feedback(Button button,string reason){var feedback=button.GetComponent<UnavailableActionFeedback>();if(feedback!=null)feedback.Configure(panel,reason);}
        public void Refresh()
        {
            if (panel == null) return;
            var target = panel.Construction.Selected;
            bool valid = target != null && target.Alive && (panel.Session.Game==null||!panel.Session.Game.isActiveAndEnabled||panel.Session.Game.CanManage);
            Heading.text = valid ? target.Definition.DisplayName + " L" + (target.TierIndex + 1) : "SELECT A STRUCTURE";
            if (!valid)
            {
                Stats.text = "Point at a tower or foundation in the world or on the map.";
                Costs.text = string.Empty; Upgrade.interactable = Repair.interactable = Sell.interactable = false; return;
            }
            var runtime = target.Runtime; var stats = runtime.Stats;
            Stats.text=$"Health  {runtime.CurrentHealth:0} / {runtime.Health.MaxHealth:0}\nArmor  {stats.Armor:0.#}";
            var weapon=runtime.GetComponent<NavyWeapon>();
            if(weapon!=null)
            {
                Stats.text+=$"\nDamage  {runtime.Damage:0.#}\nRange  {runtime.AttackRange:0.#} m\nShot interval  {weapon.ShotInterval:0.##}s";
                if(stats.SlowStrength>0)Stats.text+=$"\nSlow  {stats.SlowStrength*100:0}%";
                if(stats.PoisonDamage>0)Stats.text+=$"\nPoison  {stats.PoisonDamage:0.#} / tick";
            }
            if(stats.Kind==TowerKind.Support)Stats.text+=$"\nSupport radius  {runtime.SupportRange:0.#} m\nBuff  {stats.BuffStrength*100:0}%";
            if(stats.Kind==TowerKind.Resource)Stats.text+=$"\nProduction per {stats.ResourceGenerationInterval:0.#}s\n{stats.ResourceGenerationAmount} Money\n{stats.ResourceExpGenerationAmount} EXP\n{stats.ResourceUpgradePointGenerationAmount} Points";
            if(target.Foundation!=null)Stats.text+=$"\nFoundation range  x{target.Foundation.RangeMultiplier:0.##}";
            Upgrade.interactable = panel.Construction.IndividualUpgradeReason(target, out string reason);
            int next = target.TierIndex + 1;
            string cost = next < target.Definition.Tiers.Length ? target.Definition.Tiers[next].IndividualMoneyCost + " Money + " + target.Definition.Tiers[next].IndividualPointCost + " Points" : "MAXIMUM LEVEL";
            Costs.text = "Upgrade: " + cost + "\n" + (Upgrade.interactable ? "Only this structure changes" : reason) +
                "\nRepair: " + stats.RepairCost + " Money\nSell refund: " + stats.SellValue + " Money";
            Repair.interactable = runtime.CurrentHealth < runtime.Health.MaxHealth && panel.Session.Resources.Money >= stats.RepairCost;
            Sell.interactable = !target.Definition.IsFoundation || target.Cell.Structure == null;
            Feedback(Upgrade,reason);Feedback(Repair,runtime.CurrentHealth>=runtime.Health.MaxHealth?"Health is already full":"Not enough Money to repair.");
            Feedback(Sell,"Sell the mounted tower before its foundation.");
        }
    }
}
