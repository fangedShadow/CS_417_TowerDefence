using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefence.Battlefield
{
    public sealed class GamePanelController : MonoBehaviour
    {
        public BattlefieldSession Session;
        public CommandHud Hud;
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
        public GameObject CatalogBody;
        public Button BackToCatalog;
        public Button NavyTab, UpgradeTab, ExpandMap, CloseMap, ConfirmPlacement, CancelPlacement, RotatePlacement, RankHelp, TutorialHelp;
        public Behaviour[] LocomotionProviders;
        private readonly List<CatalogEntryView> rows = new List<CatalogEntryView>();
        private float nextStats,errorUntil;
        private bool initialized;
        private bool globalTabOpen, positioned;
        [Range(1.5f,4)] public float OpenDistance = 2.6f;
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
            if(ConfirmPlacement!=null)ConfirmPlacement.onClick.AddListener(() => Preview.Confirm());
            if(CancelPlacement!=null)CancelPlacement.onClick.AddListener(Preview.Cancel);
            if(RotatePlacement!=null)RotatePlacement.onClick.AddListener(Preview.Rotate);
            RankHelp.onClick.AddListener(() => Tutorials.Show("RankExplanation"));
            TutorialHelp.onClick.AddListener(Tutorials.ShowNextTopic);
            Session.Changed += Refresh;
            Construction.SelectionChanged += OnSelected;
            Construction.Feedback += OnFeedback;
            Preview.Changed += Refresh;
            Actions.Bind(this);
            if(BackToCatalog!=null)BackToCatalog.onClick.AddListener(()=>Construction.Select(null));
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
                if(entry.Tiers.Length>1){var upgrade = Instantiate(GlobalRowPrefab, GlobalRows, false); upgrade.Bind(this, entry, true); rows.Add(upgrade);}
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
            if(initialized&&errorUntil>0&&Time.unscaledTime>=errorUntil){errorUntil=0;Refresh();}
            if (initialized && Time.unscaledTime >= nextStats && PanelContent.activeSelf)
            { nextStats = Time.unscaledTime + 0.25f; Actions.Refresh(); }
        }
        private void OnSelected(PlacedStructure instance)
        {
            bool selected=instance!=null&&instance.Alive;
            if(CatalogBody!=null){CatalogBody.SetActive(!selected);Actions.gameObject.SetActive(selected);}
            Actions.Refresh();
        }
        public void Refresh()
        {
            if (!initialized) return;
            int active = 0;
            foreach (var entry in Session.Catalog) if (Session.FactionActive(entry.Faction)) active+=entry.Tiers.Length>1?2:1;
            if (rows.Count != active) RebuildCatalog();
            ResourceText.text = "MONEY  " + Session.Resources.Money + "     EXP  " + Session.Resources.Exp + "     UPGRADE POINTS  " + Session.Resources.UpgradePoints;
            RankText.text = "NAVY  /  " + Session.RankName + "  (" + Session.RankNumber + "/5)  •  " +
                (Session.NextRankExp < 0 ? "MAXIMUM RANK" : Session.Resources.Exp + " / " + Session.NextRankExp + " EXP");
            RankProgress.SetValueWithoutNotify(Session.RankProgress);
            PlacementText.text = Preview.Entry != null ? Preview.Entry.DisplayName + "  •  " + Preview.Reason : string.IsNullOrEmpty(StatusText.text)?"Select a build card or inspect a tower.":StatusText.text;
            if(ConfirmPlacement!=null)ConfirmPlacement.interactable = Preview.Entry != null && Preview.Valid;
            if(RotatePlacement!=null)RotatePlacement.interactable = Preview.Entry != null;
            if(CancelPlacement!=null)CancelPlacement.interactable = Preview.Entry != null;
            if(errorUntil>Time.unscaledTime)PlacementText.text=StatusText.text;
            PlacementText.color=errorUntil>Time.unscaledTime?StatusText.color:Preview.Entry!=null?(Preview.Valid?new Color(.4f,.95f,.75f):new Color(1,.35f,.32f)):StatusText.color;
            foreach (var row in rows) row.Refresh(); Actions.Refresh();
        }
        public void ShowError(string message)=>OnFeedback(message,false);
        private void OnFeedback(string message, bool success)
        {
            errorUntil=success?0:Time.unscaledTime+3;
            StatusText.text = message; StatusText.color = success ? new Color(0.4f, 0.95f, 0.75f) : new Color(1, 0.48f, 0.42f);
            Refresh();
        }
        public void BeginConstruction(ConstructionDefinition entry)
        {
            if (Construction.State(entry, out var reason) != CatalogState.Available){ShowError(reason);return;}
            errorUntil=0;Construction.Select(null);
            Preview.Begin(entry); Tutorials.ShowOnce(entry.IsFoundation ? "PlaceFoundation" : "PlaceTower");
            if(!CommanderOpen)HideForPlacement();
        }
        public void HideForPlacement()
        {
            // Closing only the physical panel keeps the preview and movement live.
            var animation=PanelContent.GetComponent<UiTransition>();
            if(animation!=null)animation.Hide();else PanelContent.SetActive(false);
        }
        private void ShowTab(bool global)
        {
            globalTabOpen=global;
            Construction.Select(null);
            ConstructionTab.SetActive(!global); GlobalTab.SetActive(global);
            NavyTab.targetGraphic.color=global?new Color(.07f,.18f,.23f):new Color(.08f,.47f,.5f);
            UpgradeTab.targetGraphic.color=global?new Color(.08f,.47f,.5f):new Color(.07f,.18f,.23f);
        }
        public void CycleTab()
        {
            if(!PanelContent.activeSelf)return;
            bool next=CommanderOpen?false:!globalTabOpen;
            ShowTab(next);if(next)Tutorials.ShowOnce("UpgradeTower");
        }
        public void ToggleCommander()=>SetCommander(!CommanderOpen);
        public void SetCommander(bool value)
        {
            if (value == Interaction.CommanderOpen) return;
            Interaction.CommanderOpen = value;
            if (value)
            {
                ShowPhysicalPanel();PositionNearPlayer();
                var forward=Vector3.ProjectOnPlane(Interaction.PhysicalCamera.transform.forward,Vector3.up).normalized;
                transform.position+=Vector3.Cross(Vector3.up,forward)*.90f;
                Tutorials.ShowOnce("CommanderMode");
            }
            else
            {
                HideForPlacement();
            }
            ConstructionTab.SetActive(!globalTabOpen); GlobalTab.SetActive(globalTabOpen);
            CommanderRoot.SetActive(value); CollapsedMinimap.SetActive(!value);
            if(Hud!=null)Hud.SetExpanded(value);
        }
        public void ToggleVisible()
        {
            var animation=PanelContent.GetComponent<UiTransition>();
            if(PanelContent.activeSelf&&(animation==null||!animation.IsHiding))
            {
                if(CommanderOpen)SetCommander(false);
                if(animation!=null)animation.Hide();else PanelContent.SetActive(false);return;
            }
            ShowPhysicalPanel();
            if(Session.Game!=null&&Session.Game.isActiveAndEnabled&&!Session.Game.CanManage&&Tutorials.Current==null)Session.Game.Resume();
        }
        public void ShowPhysicalPanel(bool reposition=false)
        {
            var animation=PanelContent.GetComponent<UiTransition>();
            if(reposition||!PanelContent.activeSelf||!positioned||(animation!=null&&animation.IsHiding))PositionNearPlayer();
            if(animation!=null)animation.Show();else PanelContent.SetActive(true);
        }
        public void PositionNearPlayer()
        {
            if(Interaction==null||Interaction.PhysicalCamera==null)return;
            var camera=Interaction.PhysicalCamera.transform;
            Vector3 forward=Vector3.ProjectOnPlane(camera.forward,Vector3.up);
            if(forward.sqrMagnitude<.001f)forward=Quaternion.Euler(0,camera.eulerAngles.y,0)*Vector3.forward;
            forward.Normalize();
            Vector3 eye=camera.position;
            // Tracking may initialize after the first popup. Use a standing height until a valid pose arrives.
            var origin=camera.GetComponentInParent<Unity.XR.CoreUtils.XROrigin>();
            if(origin!=null&&eye.y<origin.transform.position.y+.7f)eye.y=origin.transform.position.y+1.7f;
            transform.SetPositionAndRotation(eye+forward*OpenDistance-Vector3.Cross(Vector3.up,forward)*.14f-Vector3.up*.1f,Quaternion.LookRotation(forward,Vector3.up));
            positioned=true;
        }
        public void ConfirmSale()
        {
            var target = Construction.Selected;
            if (target == null || !target.Alive) return;
            var popup = Session.Game!=null&&Session.Game.isActiveAndEnabled&&Session.Game.State==NavyGameState.Tutorial?
                Tutorials.ShowForGame("SellTower"):Tutorials.Show("SellTower");
            if (popup != null) popup.Confirmation("Sell " + target.Definition.DisplayName + "?",
                "Refund: " + target.Runtime.Stats.SellValue + " Money.\nThe selected structure will be removed. Sell mounted towers before their foundation.",
                () => Construction.TrySell(target, out _));
        }
    }
}
