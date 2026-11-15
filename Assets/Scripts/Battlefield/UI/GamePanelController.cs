using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefence.Battlefield
{
    public sealed class GamePanelController : MonoBehaviour
    {
        public BattlefieldSession Session;
        public ConstructionController Construction;
        public ConstructionPreview Preview;
        public WorldInteraction Interaction;
        public TutorialPopupController Tutorials;
        public TMP_Text ResourceText, RankText, PlacementText, StatusText;
        public Slider RankProgress;
        public GameObject PanelContent, ConstructionTab, GlobalTab, CommanderRoot, CollapsedMinimap;
        public Transform ConstructionRows, GlobalRows;
        public CatalogEntryView ConstructionRowPrefab, GlobalRowPrefab;
        public TowerActionView Actions;
        public Button NavyTab, UpgradeTab, ExpandMap, CloseMap, ConfirmPlacement, CancelPlacement, RotatePlacement, RankHelp, TutorialHelp;
        public Behaviour[] LocomotionProviders;
        private readonly List<CatalogEntryView> rows = new List<CatalogEntryView>();
        private bool[] locomotionStates;
        private float nextStats;
        private bool initialized;
        public bool CommanderOpen => Interaction != null && Interaction.CommanderOpen;
        private void Start() => Initialize();
        public void Initialize()
        {
            if (initialized || Session == null || Construction == null) return;
            initialized = true;
            RebuildCatalog();
            NavyTab.onClick.AddListener(() => ShowTab(false));
            UpgradeTab.onClick.AddListener(() => { ShowTab(true); Tutorials.ShowOnce("UpgradeTower"); });
            ExpandMap.onClick.AddListener(() => SetCommander(true));
            CloseMap.onClick.AddListener(() => SetCommander(false));
            ConfirmPlacement.onClick.AddListener(() => Preview.Confirm());
            CancelPlacement.onClick.AddListener(Preview.Cancel);
            RotatePlacement.onClick.AddListener(Preview.Rotate);
            RankHelp.onClick.AddListener(() => Tutorials.Show("RankExplanation"));
            TutorialHelp.onClick.AddListener(Tutorials.ShowNextTopic);
            Session.Changed += Refresh;
            Construction.SelectionChanged += OnSelected;
            Construction.Feedback += OnFeedback;
            Preview.Changed += Refresh;
            Actions.Bind(this);
            ShowTab(false); Refresh(); Tutorials.ShowOnce("ChooseFaction");
        }
        public void RebuildCatalog()
        {
            foreach (var row in rows) if (row != null) { row.gameObject.SetActive(false); Destroy(row.gameObject); }
            rows.Clear();
            foreach (var entry in Session.Catalog)
            {
                if (!Session.FactionActive(entry.Faction)) continue;
                var row = Instantiate(ConstructionRowPrefab, ConstructionRows, false); row.Bind(this, entry, false); rows.Add(row);
                var upgrade = Instantiate(GlobalRowPrefab, GlobalRows, false); upgrade.Bind(this, entry, true); rows.Add(upgrade);
            }
        }
        private void OnDestroy()
        {
            if (!initialized) return;
            Session.Changed -= Refresh; Construction.SelectionChanged -= OnSelected;
            Construction.Feedback -= OnFeedback; Preview.Changed -= Refresh;
        }
        private void Update()
        {
            if (initialized && Time.unscaledTime >= nextStats && PanelContent.activeSelf)
            { nextStats = Time.unscaledTime + 0.25f; Actions.Refresh(); }
        }
        private void OnSelected(PlacedStructure instance) { Actions.Refresh(); }
        public void Refresh()
        {
            if (!initialized) return;
            int active = 0;
            foreach (var entry in Session.Catalog) if (Session.FactionActive(entry.Faction)) active++;
            if (rows.Count != active * 2) RebuildCatalog();
            ResourceText.text = "MONEY  " + Session.Resources.Money + "     EXP  " + Session.Resources.Exp + "     UPGRADE POINTS  " + Session.Resources.UpgradePoints;
            RankText.text = "NAVY  /  " + Session.RankName + "  (" + Session.RankNumber + "/5)  •  " +
                (Session.NextRankExp < 0 ? "MAXIMUM RANK" : Session.Resources.Exp + " / " + Session.NextRankExp + " EXP");
            RankProgress.SetValueWithoutNotify(Session.RankProgress);
            PlacementText.text = Preview.Entry != null ? Preview.Entry.DisplayName + "  •  " + Preview.Reason : "Point at a structure to inspect it, or choose a construction entry.";
            ConfirmPlacement.interactable = Preview.Entry != null && Preview.Valid;
            RotatePlacement.interactable = CancelPlacement.interactable = Preview.Entry != null;
            foreach (var row in rows) row.Refresh(); Actions.Refresh();
        }
        private void OnFeedback(string message, bool success)
        {
            StatusText.text = message; StatusText.color = success ? new Color(0.4f, 0.95f, 0.75f) : new Color(1, 0.48f, 0.42f);
            if (!success)
            {
                string id = message.Contains("Maximum") ? "MaximumUpgrade" : message.Contains("rank") || message.Contains("Faction") ?
                    "TowerLocked" : message.Contains("Money") || message.Contains("resources") || message.Contains("Need") ? "InsufficientMoney" : "InvalidPlacement";
                Tutorials.ShowOnce(id);
            }
            Refresh();
        }
        public void BeginConstruction(ConstructionDefinition entry)
        {
            if (Construction.State(entry, out _) != CatalogState.Available) return;
            Preview.Begin(entry); Tutorials.ShowOnce(entry.IsFoundation ? "PlaceFoundation" : "PlaceTower");
        }
        private void ShowTab(bool global)
        {
            SetCommander(false); ConstructionTab.SetActive(!global); GlobalTab.SetActive(global);
        }
        public void SetCommander(bool value)
        {
            if (value == Interaction.CommanderOpen) return;
            Interaction.CommanderOpen = value;
            if (value)
            {
                locomotionStates = new bool[LocomotionProviders.Length];
                for (int i = 0; i < LocomotionProviders.Length; i++)
                { locomotionStates[i] = LocomotionProviders[i].enabled; LocomotionProviders[i].enabled = false; }
                ConstructionTab.SetActive(false); GlobalTab.SetActive(false); Tutorials.ShowOnce("CommanderMode");
            }
            else
            {
                for (int i = 0; i < LocomotionProviders.Length; i++) if (locomotionStates != null) LocomotionProviders[i].enabled = locomotionStates[i];
                ConstructionTab.SetActive(true); GlobalTab.SetActive(false);
            }
            CommanderRoot.SetActive(value); CollapsedMinimap.SetActive(!value);
        }
        public void ToggleVisible()
        {
            if (CommanderOpen) SetCommander(false);
            PanelContent.SetActive(!PanelContent.activeSelf);
        }
        public void ShowPhysicalPanel() => PanelContent.SetActive(true);
        public void ConfirmSale()
        {
            var target = Construction.Selected;
            if (target == null || !target.Alive) return;
            var popup = Tutorials.Show("SellTower");
            if (popup != null) popup.Confirmation("Sell " + target.Definition.DisplayName + "?",
                "Refund: " + target.Runtime.Stats.SellValue + " Money.\nThe selected structure will be removed. Sell mounted towers before their foundation.",
                () => Construction.TrySell(target, out _));
        }
    }
}
