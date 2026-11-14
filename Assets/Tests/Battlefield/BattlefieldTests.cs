#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TowerDefence.Battlefield;
using TowerDefence.Shared;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Unity.XR.CoreUtils;
using Object = UnityEngine.Object;

namespace TowerDefence.Tests
{
    public sealed class BattlefieldTests
    {
        private ConstructionController construction;
        private BattlefieldSession session;
        private GamePanelController panel;
        private ConstructionDefinition Entry(TowerKind kind) => session.Catalog.Single(e=>e.Kind==kind);
        private GridCell FreeWater() => construction.Grid.Cells.First(c=>c.Definition.Surface==BuildSurface.Water&&c.Definition.Buildable&&!c.Occupied);
        [UnitySetUp]
        public IEnumerator Setup()
        {
            yield return SceneManager.LoadSceneAsync("Main",LoadSceneMode.Single);
            yield return null;
            construction=Object.FindAnyObjectByType<ConstructionController>();session=construction.Session;
            panel=Object.FindAnyObjectByType<GamePanelController>();panel.Tutorials.Close();
        }
        private PlacedStructure Build(ConstructionDefinition entry,GridCell cell)
        {
            Assert.That(construction.TryBuild(entry,cell,Quaternion.identity,out var reason),Is.True,reason);
            return entry.IsFoundation?cell.Foundation:cell.Structure;
        }
        private PlacedStructure Tower(GridCell cell=null)
        {
            cell=cell??FreeWater();Build(Entry(TowerKind.Foundation),cell);return Build(Entry(TowerKind.Turret),cell);
        }
        [Test]
        public void BattlefieldHasPhysicalLandCanyonsBridgesOceanAndOneSharedBase()
        {
            var root=GameObject.Find("Battlefield").transform;
            foreach(string name in new[]{"LeftLand","CenterLand","RightLand"})
                Assert.That(root.Find("Land/"+name).GetComponent<MeshCollider>(),Is.Not.Null);
            Assert.That(root.Find("Land/CanyonPaths").childCount,Is.GreaterThanOrEqualTo(3));
            Assert.That(root.Find("Land/Bridges").childCount,Is.EqualTo(2));
            Assert.That(root.Find("Ocean/WaterSurface").GetComponent<Collider>().isTrigger,Is.True);
            Assert.That(root.Find("Ocean/OceanFloor").GetComponent<Collider>(),Is.Not.Null);
            Assert.That(Object.FindObjectsByType<CenterBase>().Length,Is.EqualTo(1));
            Assert.That(root.Find("MainBase/FortifiedBasePlatform").GetComponent<MeshCollider>(),Is.Not.Null);
            Assert.That(root.Find("MainBase").GetComponent<BaseIntegration>().NavalApproach.Length,Is.GreaterThan(1));
        }
        [Test]
        public void GridHasAlignedWaterLandAndBlockedRoutes()
        {
            Assert.That(construction.Grid.Cells.Count(c=>c.Definition.Surface==BuildSurface.Water&&c.Definition.Buildable),Is.GreaterThan(50));
            Assert.That(construction.Grid.Cells.Any(c=>c.Definition.Surface==BuildSurface.Land),Is.True);
            foreach(var cell in construction.Grid.Cells)
            {
                Assert.That(construction.Grid.At(construction.Grid.World(cell)),Is.SameAs(cell));
                if(cell.Definition.Surface==BuildSurface.Water&&Mathf.Abs(cell.Definition.LocalPosition.x)<4&&cell.Definition.LocalPosition.z<-20)
                    Assert.That(cell.Definition.Buildable,Is.False);
            }
        }
        [Test]
        public void FoundationThenTowerOccupiesOneWaterCellAndChargesExactCosts()
        {
            var cell=FreeWater();int before=session.Resources.Money;
            var foundation=Build(Entry(TowerKind.Foundation),cell);var tower=Build(Entry(TowerKind.Turret),cell);
            Assert.That(session.Resources.Money,Is.EqualTo(before-foundation.Runtime.Stats.PurchaseCost-tower.Runtime.Stats.PurchaseCost));
            Assert.That(cell.Occupied,Is.True);Assert.That(tower.transform.parent,Is.SameAs(foundation.Foundation.TowerMount));
            Assert.That(tower.Runtime.FoundationRangeMultiplier,Is.EqualTo(1));
            Assert.That(construction.TryBuild(Entry(TowerKind.Turret),cell,Quaternion.identity,out _),Is.False);
        }
        [Test]
        public void InvalidLandOccupiedAndUnfoundedBuildsSpendNothing()
        {
            int money=session.Resources.Money;var cell=FreeWater();
            Assert.That(construction.TryBuild(Entry(TowerKind.Turret),cell,Quaternion.identity,out _),Is.False);
            var land=construction.Grid.Cells.First(c=>c.Definition.Surface==BuildSurface.Land);
            Assert.That(construction.TryBuild(Entry(TowerKind.Foundation),land,Quaternion.identity,out _),Is.False);
            Assert.That(construction.TryBuild(Entry(TowerKind.Foundation),null,Quaternion.identity,out _),Is.False);
            Assert.That(session.Resources.Money,Is.EqualTo(money));
            Build(Entry(TowerKind.Foundation),cell);money=session.Resources.Money;
            Assert.That(construction.TryBuild(Entry(TowerKind.Foundation),cell,Quaternion.identity,out _),Is.False);
            Assert.That(session.Resources.Money,Is.EqualTo(money));
        }
        [Test]
        public void CatalogStatesPrioritizeLockedBeforeUnaffordable()
        {
            session.Resources.SpendMoney(session.Resources.Money);
            Assert.That(construction.State(Entry(TowerKind.Turret),out _),Is.EqualTo(CatalogState.Unaffordable));
            Assert.That(construction.State(Entry(TowerKind.Cannon),out _),Is.EqualTo(CatalogState.Locked));
            session.Resources.AddExp(100);
            Assert.That(construction.State(Entry(TowerKind.Cannon),out _),Is.EqualTo(CatalogState.Unaffordable));
            session.Resources.AddMoney(500);
            Assert.That(construction.State(Entry(TowerKind.Cannon),out _),Is.EqualTo(CatalogState.Available));
            foreach(var row in panel.ConstructionRows.GetComponentsInChildren<CatalogEntryView>())Assert.That(row.Action,Is.Not.Null);
        }
        [Test]
        public void GlobalUpgradeChangesFutureConstructionOnly()
        {
            var old=Tower();var data=Entry(TowerKind.Turret);
            session.Resources.AddMoney(2000);session.Resources.AddExp(100);
            Assert.That(construction.TryGlobalUpgrade(data,out var reason),Is.True,reason);
            Assert.That(session.GlobalTier(data),Is.EqualTo(1));Assert.That(old.TierIndex,Is.Zero);
            var cell=FreeWater();Build(Entry(TowerKind.Foundation),cell);var next=Build(data,cell);
            Assert.That(next.TierIndex,Is.EqualTo(1));Assert.That(old.Runtime.Stats.UpgradeLevel,Is.EqualTo(1));
            Assert.That(next.Runtime.Stats.UpgradeLevel,Is.EqualTo(2));
        }
        [Test]
        public void IndividualUpgradePreservesHealthPercentCellAndFoundationAndDoesNotUnlockGlobalTier()
        {
            var tower=Tower();var cell=tower.Cell;var foundation=cell.Foundation;var position=tower.transform.position;
            var second=Tower();tower.Runtime.Health.TakeDamage(new DamageInfo(30));
            float fraction=tower.Runtime.CurrentHealth/tower.Runtime.Health.MaxHealth;
            session.Resources.AddMoney(1000);session.Resources.AddExp(100);
            Assert.That(construction.TryIndividualUpgrade(tower,out var reason),Is.True,reason);
            var replacement=cell.Structure;Assert.That(replacement.TierIndex,Is.EqualTo(1));
            Assert.That(replacement.Runtime.CurrentHealth/replacement.Runtime.Health.MaxHealth,Is.EqualTo(fraction).Within(0.0001f));
            Assert.That(replacement.transform.position,Is.EqualTo(position));Assert.That(cell.Foundation,Is.SameAs(foundation));
            Assert.That(replacement.transform.parent,Is.SameAs(foundation.Foundation.TowerMount));
            Assert.That(second.TierIndex,Is.Zero);Assert.That(session.GlobalTier(Entry(TowerKind.Turret)),Is.Zero);
        }
        [Test]
        public void FoundationUpgradeKeepsItsMountedTowerAndChangesRange()
        {
            var tower=Tower();var cell=tower.Cell;var previous=cell.Foundation;
            tower.Runtime.Health.TakeDamage(new DamageInfo(7));float health=tower.Runtime.CurrentHealth;
            session.Resources.AddMoney(1000);session.Resources.AddExp(100);
            Assert.That(construction.TryIndividualUpgrade(previous,out var reason),Is.True,reason);
            Assert.That(cell.Structure,Is.SameAs(tower));Assert.That(tower.Runtime.CurrentHealth,Is.EqualTo(health));
            Assert.That(tower.Runtime.AttackRange,Is.EqualTo(tower.Runtime.Stats.AttackRange*1.25f).Within(0.001f));
            Assert.That(tower.transform.parent,Is.SameAs(cell.Foundation.Foundation.TowerMount));
        }
        [Test]
        public void RepairClampsAndHealthyOrUnfundedRepairsSpendNothing()
        {
            var tower=Tower();int money=session.Resources.Money;
            Assert.That(construction.TryRepair(tower,out _),Is.False);Assert.That(session.Resources.Money,Is.EqualTo(money));
            tower.Runtime.Health.TakeDamage(new DamageInfo(20));
            Assert.That(construction.TryRepair(tower,out _),Is.True);
            Assert.That(tower.Runtime.CurrentHealth,Is.EqualTo(tower.Runtime.Health.MaxHealth));
            Assert.That(session.Resources.Money,Is.EqualTo(money-tower.Runtime.Stats.RepairCost));
            tower.Runtime.Health.TakeDamage(new DamageInfo(20));session.Resources.SpendMoney(session.Resources.Money);
            Assert.That(construction.TryRepair(tower,out _),Is.False);Assert.That(tower.Runtime.CurrentHealth,Is.LessThan(tower.Runtime.Health.MaxHealth));
        }
        [Test]
        public void SaleRefundsAndClearsCorrectSlotAndProtectsOccupiedFoundations()
        {
            var tower=Tower();var cell=tower.Cell;var foundation=cell.Foundation;
            Assert.That(construction.TrySell(foundation,out _),Is.False);
            int money=session.Resources.Money,refund=tower.Runtime.Stats.SellValue;
            Assert.That(construction.TrySell(tower,out _),Is.True);
            Assert.That(session.Resources.Money,Is.EqualTo(money+refund));Assert.That(cell.Structure,Is.Null);
            Assert.That(cell.Foundation,Is.SameAs(foundation));
            Assert.That(construction.TrySell(foundation,out _),Is.True);Assert.That(cell.Occupied,Is.False);
        }
        [Test]
        public void CombatDestructionClearsFoundationAndMountedOccupancyWithoutRefund()
        {
            var tower=Tower();var cell=tower.Cell;int money=session.Resources.Money;
            cell.Foundation.Runtime.Health.TakeDamage(new DamageInfo(100000));
            Assert.That(cell.Occupied,Is.False);Assert.That(tower.Alive,Is.False);
            Assert.That(session.Resources.Money,Is.EqualTo(money));
        }
        [Test]
        public void PreviewCancelCostsNothingAndNeverChangesAuthoredPrefab()
        {
            var data=Entry(TowerKind.Foundation);var path=AssetDatabase.GetAssetPath(data.Tiers[0].Prefab);var bytes=File.ReadAllBytes(path);
            int money=session.Resources.Money;var preview=Object.FindAnyObjectByType<ConstructionPreview>();
            preview.Begin(data);preview.Hover(construction.Grid.World(FreeWater()));Assert.That(preview.Valid,Is.True);
            preview.Rotate();Assert.That(preview.Rotation,Is.EqualTo(90));preview.Cancel();
            Assert.That(preview.Entry,Is.Null);Assert.That(session.Resources.Money,Is.EqualTo(money));
            Assert.That(File.ReadAllBytes(path),Is.EqualTo(bytes));
        }
        [Test]
        public void PhysicalAndCommanderRaysUseTheSameInstancesGridAndWallet()
        {
            var pointer=Object.FindAnyObjectByType<WorldInteraction>();var preview=Object.FindAnyObjectByType<ConstructionPreview>();
            var cell=FreeWater();var world=construction.Grid.World(cell);
            var ray=new Ray(world+Vector3.up*100,Vector3.down);
            preview.Begin(Entry(TowerKind.Foundation));Physics.SyncTransforms();pointer.HandleRay(ray,true);
            Assert.That(cell.Foundation,Is.Not.Null);int money=session.Resources.Money;
            panel.SetCommander(true);preview.Begin(Entry(TowerKind.Turret));Physics.SyncTransforms();pointer.HandleRay(ray,true);
            Assert.That(cell.Structure,Is.Not.Null);Assert.That(session.Resources.Money,Is.EqualTo(money-cell.Structure.Runtime.Stats.PurchaseCost));
            Assert.That(Object.FindObjectsByType<PlacedStructure>().Length,Is.EqualTo(2));
            panel.SetCommander(false);Assert.That(cell.Structure,Is.SameAs(construction.Selected));
        }
        [Test]
        public void MinimapAndOverviewUseOneLiveWorldCameraAndTransitionSuspendsLocomotion()
        {
            var camera=GameObject.Find("LiveMinimapCamera").GetComponent<Camera>();
            Assert.That(camera.targetTexture,Is.Not.Null);Assert.That(camera.targetTexture.width,Is.EqualTo(512));
            Assert.That(Object.FindObjectsByType<Camera>().Length,Is.EqualTo(2));
            foreach(var raw in panel.GetComponentsInChildren<RawImage>(true))Assert.That(raw.texture,Is.SameAs(camera.targetTexture));
            panel.SetCommander(true);Assert.That(panel.CommanderOpen,Is.True);
            Assert.That(panel.LocomotionProviders.All(b=>!b.enabled),Is.True);
            panel.SetCommander(false);Assert.That(panel.LocomotionProviders.All(b=>b.enabled),Is.True);
            Assert.That(Object.FindObjectsByType<XROrigin>().Length,Is.EqualTo(1));
        }
        [Test]
        public void NavyRanksResourcesAndCheaperIndividualCostsAreDataDriven()
        {
            Assert.That(session.NavyRanks.Ranks.Select(r=>r.Name),Is.EqualTo(new[]{"Seaman","Petty Officer","Lieutenant","Commander","Admiral"}));
            Assert.That(session.Catalog.All(e=>e.Faction==Faction.Navy),Is.True);
            Assert.That(session.FactionActive(Faction.Army),Is.False);Assert.That(session.FactionActive(Faction.Sky),Is.False);
            session.Resources.AddExp(1500);Assert.That(session.RankNumber,Is.EqualTo(5));Assert.That(session.NextRankExp,Is.EqualTo(-1));Assert.That(session.RankProgress,Is.EqualTo(1));
            foreach(var entry in session.Catalog)foreach(var tier in entry.Tiers.Skip(1))Assert.That(tier.IndividualMoneyCost,Is.LessThan(tier.GlobalMoneyCost));
            session.Resources.AddMoney(37);session.Resources.AddUpgradePoints(4);
            Assert.That(panel.ResourceText.text,Does.Contain(session.Resources.Money.ToString()));Assert.That(panel.ResourceText.text,Does.Contain("UPGRADE POINTS  "+session.Resources.UpgradePoints));
        }
        [Test]
        public void ResourceTransactionIsAtomicWhenUpgradePointsAreMissing()
        {
            var wallet=new PlayerResources(100,10,0);int changes=0;wallet.Changed+=()=>changes++;
            Assert.That(wallet.TrySpend(50,1),Is.False);Assert.That(wallet.Money,Is.EqualTo(100));Assert.That(changes,Is.Zero);
            wallet.AddUpgradePoints(1);changes=0;Assert.That(wallet.TrySpend(50,1),Is.True);
            Assert.That(wallet.Money,Is.EqualTo(50));Assert.That(wallet.UpgradePoints,Is.Zero);Assert.That(wallet.Exp,Is.EqualTo(10));Assert.That(changes,Is.EqualTo(1));
        }
        [Test]
        public void AllFifteenTutorialsAreSavedEditableAndOpenCloseWithTheirButtons()
        {
            var controller=panel.Tutorials;Assert.That(controller.PopupPrefabs.Length,Is.EqualTo(15));
            foreach(var prefab in controller.PopupPrefabs)
            {
                Assert.That(AssetDatabase.GetAssetPath(prefab),Does.StartWith("Assets/Prefabs/Shared/UI/Tutorials/"));
                Assert.That(prefab.Body.font,Is.Not.Null);Assert.That(prefab.Body.fontSize,Is.GreaterThanOrEqualTo(26));
                var popup=controller.Show(prefab.Definition.Id);Assert.That(popup.Title.text,Is.EqualTo(prefab.Definition.Title));
                Assert.That(popup.Body.text,Is.EqualTo(prefab.Definition.Pages[0]));
                if(prefab.Definition.Pages.Length>1){popup.Next.onClick.Invoke();Assert.That(popup.Body.text,Is.EqualTo(prefab.Definition.Pages[1]));popup.Back.onClick.Invoke();Assert.That(popup.Body.text,Is.EqualTo(prefab.Definition.Pages[0]));}
                popup.Close.onClick.Invoke();Assert.That(controller.Current,Is.Null);
            }
            Assert.That(controller.ShowOnce("WaveExplanation"),Is.True);Assert.That(controller.ShowOnce("WaveExplanation"),Is.False);
        }
        [Test]
        public void CatalogThumbnailsAreSpritesAndRankProgressRendersAtTheActualFraction()
        {
            foreach(var entry in session.Catalog)foreach(var tier in entry.Tiers)Assert.That(tier.Thumbnail,Is.Not.Null,entry.Id);
            Canvas.ForceUpdateCanvases();
            Assert.That(panel.RankProgress.value,Is.Zero);
            Assert.That(panel.RankProgress.fillRect.rect.width,Is.LessThan(0.1f));
            session.Resources.AddExp(50);Canvas.ForceUpdateCanvases();
            Assert.That(panel.RankProgress.value,Is.EqualTo(0.5f));
            Assert.That(panel.RankProgress.fillRect.rect.width,Is.EqualTo(105).Within(0.1f));
        }
        [Test]
        public void DesktopFallbackHasComfortableEyeHeightAndAllNewPrefabsUseSharedPrefabFolder()
        {
            var origin=Object.FindAnyObjectByType<XROrigin>();
            Assert.That(origin.Camera.transform.position.y-origin.transform.position.y,Is.GreaterThan(1.5f));
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Shared/Battlefield/Battlefield.prefab"),Is.Not.Null);
            Assert.That(Directory.Exists("Assets/Shared/Prefabs"),Is.False);
            foreach(string path in Directory.GetFiles("Assets/Prefabs/Shared/UI","*.prefab",SearchOption.AllDirectories))
                Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(path),Is.Not.Null);
        }

        [Test]
        public void SafetyBordersAreInvisibleSolidAndCollideOnlyWithPlayers()
        {
            var borders=GameObject.Find("Battlefield/Environment/PlayerSafetyBorders");
            Assert.That(borders,Is.Not.Null);
            var colliders=borders.GetComponentsInChildren<Collider>();
            Assert.That(colliders.Length,Is.GreaterThan(20));
            Assert.That(borders.GetComponentsInChildren<Renderer>(),Is.Empty);
            foreach(var wall in colliders)
            {
                Assert.That(wall.enabled,Is.True);Assert.That(wall.isTrigger,Is.False);
                Assert.That(wall.gameObject.layer,Is.EqualTo(LayerMask.NameToLayer("PlayerBoundary")));
            }
            for(int layer=0;layer<32;layer++)Assert.That(Physics.GetIgnoreLayerCollision(9,layer),Is.EqualTo(layer!=8));
            Assert.That(panel.Interaction.RightRay.raycastMask.value&(1<<9),Is.Zero);
        }

        private Vector3 MovePlayerProbe(Vector3 start,params Vector3[] deltas)
        {
            var probe=new GameObject("SafetyBorderMovementProbe");probe.layer=8;probe.transform.position=start;
            try
            {
                var body=probe.AddComponent<CharacterController>();body.height=1.8f;body.radius=.25f;body.center=Vector3.up*.9f;body.stepOffset=.3f;
                Physics.SyncTransforms();
                foreach(var delta in deltas)for(int step=0;step<80;step++)body.Move(delta/80);
                return probe.transform.position;
            }
            finally{Object.DestroyImmediate(probe);}
        }

        [TestCase(-38,.1f,-44,-6,0,0)]
        [TestCase(38,.1f,-44,6,0,0)]
        [TestCase(0,3.6f,52,0,0,6)]
        [TestCase(12,.1f,-54,0,0,-6)]
        [TestCase(0,.6f,-19,0,0,-7)]
        [TestCase(0,3.6f,11,0,0,-8)]
        public void PlayerCannotCrossPerimeterBaseShoreOrCanyonDrop(float x,float y,float z,float dx,float dy,float dz)
        {
            var start=new Vector3(x,y,z);var delta=new Vector3(dx,dy,dz);
            var end=MovePlayerProbe(start,delta);
            Assert.That(Vector3.Dot(end-start,delta.normalized),Is.LessThan(delta.magnitude-1),"Player crossed a protected edge");
        }

        [TestCase(-18)]
        [TestCase(18)]
        public void SafetyBordersLeaveBothBridgesWalkable(float centerX)
        {
            var start=new Vector3(centerX-4,3.8f,25);var delta=Vector3.right*8;
            Assert.That(Vector3.Distance(MovePlayerProbe(start,delta),start+delta),Is.LessThan(.4f));
        }

        [TestCase(-1)]
        [TestCase(1)]
        public void SafetyBordersLeaveBothAccessRampsWalkable(int side)
        {
            var start=new Vector3(side*8,.6f,-1);var end=new Vector3(side*28,3.6f,9);
            var bend=new Vector3(side*14,3.6f,2);
            Assert.That(Vector3.Distance(MovePlayerProbe(start,bend-start,end-bend),end),Is.LessThan(.5f));
        }

        [TestCase(-1)]
        [TestCase(1)]
        public void RampSafetyRailsEndBeforeTheOpenPlateau(int side)
        {
            var start=new Vector3(side*22,3.65f,6);var delta=new Vector3(side,0,-2).normalized*3;
            Assert.That(Vector3.Distance(MovePlayerProbe(start,delta),start+delta),Is.LessThan(.4f));
        }

        [Test]
        public void InvisibleBorderDoesNotInterceptConstructionRay()
        {
            var entry=Entry(TowerKind.Foundation);var preview=panel.Interaction.Preview;
            var ray=new Ray(new Vector3(44,4,-40),new Vector3(-12,-4.01f,0).normalized);
            Assert.That(Physics.Raycast(ray,out _,30,1<<9),Is.True,"Ray must pass through the safety wall for this regression check");
            preview.Begin(entry);panel.Interaction.HandleRay(ray,false);
            Assert.That(preview.HoveredCell,Is.Not.Null);Assert.That(preview.Valid,Is.True,preview.Reason);
            var cell=preview.HoveredCell;int money=session.Resources.Money;
            panel.Interaction.HandleRay(ray,true);
            Assert.That(cell.Foundation,Is.Not.Null);
            Assert.That(session.Resources.Money,Is.EqualTo(money-entry.Tiers[0].Stats.PurchaseCost));
        }

        [TestCase(-28,-44)]
        [TestCase(28,-44)]
        [TestCase(0,-36)]
        [TestCase(36,-52)]
        public void PlayerStaysSupportedAtWaterLevelUnderGravity(float x,float z)
        {
            var support=GameObject.Find("Battlefield/Ocean/PlayerWaterSupport");
            Assert.That(support,Is.Not.Null);Assert.That(support.GetComponent<Renderer>(),Is.Null);
            Assert.That(support.GetComponent<Collider>().isTrigger,Is.False);Assert.That(support.layer,Is.EqualTo(9));
            var probe=new GameObject("WaterStandingProbe");probe.layer=8;probe.transform.position=new Vector3(x,.65f,z);
            try
            {
                var body=probe.AddComponent<CharacterController>();body.height=1.8f;body.radius=.25f;body.center=Vector3.up*.9f;
                Physics.SyncTransforms();
                for(int i=0;i<120;i++)body.Move(Vector3.down*.05f);
                Assert.That(probe.transform.position.y,Is.InRange(.015f,.16f),"Player sank beneath the water support");
                Assert.That(body.isGrounded,Is.True);
            }
            finally{Object.DestroyImmediate(probe);}
        }

        [TestCase(-1)]
        [TestCase(1)]
        public void PlayerCanWalkFromBaseOntoWaterAndReturnThroughSideGate(int side)
        {
            var probe=new GameObject("WaterAccessWalkingProbe");probe.layer=8;probe.transform.position=new Vector3(side*12.2f,.6f,-12);
            try
            {
                var body=probe.AddComponent<CharacterController>();body.height=1.8f;body.radius=.25f;body.center=Vector3.up*.9f;body.stepOffset=.3f;
                Physics.SyncTransforms();
                for(int i=0;i<110;i++)body.Move(new Vector3(side*.08f,-.045f,0));
                Assert.That(probe.transform.position.x*side,Is.GreaterThan(20.5f),"Player could not leave the base through the water gate");
                Assert.That(probe.transform.position.y,Is.InRange(.015f,.17f));Assert.That(body.isGrounded,Is.True);
                for(int i=0;i<110;i++)body.Move(new Vector3(-side*.08f,-.045f,0));
                Assert.That(probe.transform.position.x*side,Is.LessThan(12.8f),"Player could not return up the water ramp");
                Assert.That(probe.transform.position.y,Is.InRange(.48f,.68f));Assert.That(body.isGrounded,Is.True);
            }
            finally{Object.DestroyImmediate(probe);}
        }

        [TestCase(-1)]
        [TestCase(1)]
        public void MainHeadquartersLeavesPlayerStartAndBothWaterGateApproachesClear(int side)
        {
            var start=GameObject.Find("Battlefield/PlayerRig").transform.position;
            var destination=new Vector3(side*12.2f,start.y,-12);
            var end=MovePlayerProbe(start,new Vector3(side*12.2f,0,0),new Vector3(0,0,-5));
            Assert.That(Vector3.Distance(end,destination),Is.LessThan(.4f),"Headquarters blocked the route from the spawn to a water gate");
            var shared=GameObject.Find("Battlefield/MainBase").GetComponent<BaseIntegration>().SharedBase;
            Assert.That(shared,Is.Not.Null);Assert.That(shared.Health.IsAlive,Is.True);
        }

        [Test]
        public void WaterSupportDoesNotBlockFoundationPlacementFromTheWater()
        {
            var cell=FreeWater();var point=construction.Grid.World(cell);
            var ray=new Ray(point+Vector3.up*2,Vector3.down);
            Assert.That(Physics.Raycast(ray,out var floor,4,1<<9),Is.True);
            Assert.That(floor.collider.name,Is.EqualTo("PlayerWaterSupport"));
            var preview=panel.Interaction.Preview;preview.Begin(Entry(TowerKind.Foundation));
            panel.Interaction.HandleRay(ray,false);
            Assert.That(preview.HoveredCell,Is.SameAs(cell));Assert.That(preview.Valid,Is.True,preview.Reason);
            panel.Interaction.HandleRay(ray,true);Assert.That(cell.Foundation,Is.Not.Null);
        }

        [Test]
        public void MaximumTierAndLockedUpgradesDoNotSpendResources()
        {
            var data=Entry(TowerKind.Cannon);int money=session.Resources.Money;
            Assert.That(construction.TryGlobalUpgrade(data,out _),Is.False);Assert.That(session.Resources.Money,Is.EqualTo(money));
            Assert.That(construction.TryGlobalUpgrade(Entry(TowerKind.Support),out _),Is.False);
            session.Resources.AddExp(1500);session.SetGlobalTier(data,data.Tiers.Length-1);
            Assert.That(construction.TryGlobalUpgrade(data,out _),Is.False);
            Assert.That(session.Resources.Money,Is.EqualTo(money));
        }
    }
}
#endif
