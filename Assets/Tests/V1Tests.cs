#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TowerDefence.Battlefield;
using TowerDefence.Navy;
using TowerDefence.Shared;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Unity.XR.CoreUtils;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;
using Object = UnityEngine.Object;

namespace TowerDefence.Tests
{
    // V1 integration checks use the saved scene and prefabs; no generation or builds.
    public sealed class V1Tests
    {
        const string Root="Assets/Prefabs/Navy/";
        static readonly Vector3 Origin=new Vector3(1000,0,1000);
        static readonly Vector3 origin=Origin;
        readonly List<Object> spawned=new List<Object>();
        PlayableNavyGame game;
        NavyGameSettings copy;
        ConstructionController construction=>game.Construction;
        GamePanelController panel=>game.Panel;
        NavyEffectPool pool=>NavyEffectPool.Instance;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            Time.timeScale=1;
            yield return SceneManager.LoadSceneAsync("Main",LoadSceneMode.Single);
            yield return null;
            game=Object.FindAnyObjectByType<PlayableNavyGame>();Assert.That(game,Is.Not.Null);
            copy=Object.Instantiate(game.Settings);game.Settings=copy;game.Waves.Settings=copy;
            copy.WaveCompletionPause=0;pool.Clear();
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Time.timeScale=1;if(game!=null)game.Waves.StopWave();
            foreach(var shot in Object.FindObjectsByType<NavyProjectile>(FindObjectsSortMode.None))Object.Destroy(shot.gameObject);
            foreach(var item in spawned.AsEnumerable().Reverse())if(item!=null)Object.Destroy(item);
            spawned.Clear();if(copy!=null)Object.Destroy(copy);yield return null;pool.Clear();
        }
        GameObject Actor(string path,Vector3? point=null)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Root+path+".prefab");Assert.That(prefab,Is.Not.Null,path);
            var go=Object.Instantiate(prefab,point??Origin,Quaternion.identity);spawned.Add(go);return go;
        }
        TowerRuntime Tower(string path,Vector3 offset=default)=>Actor("Towers/"+path,Origin+offset).GetComponent<TowerRuntime>();
        EnemyRuntime Enemy()=>Actor("Enemies/Battleship",Origin+Vector3.forward*5).GetComponent<EnemyRuntime>();
        NavyFoundation Foundation(string name)=>Actor("Foundations/Foundation_"+name).GetComponent<NavyFoundation>();
        ConstructionDefinition Entry(TowerKind kind)=>game.Session.Catalog.Single(x=>x.Kind==kind);
        GridCell Cell(float x=-4,float z=-32)=>construction.Grid.At(new Vector3(x,0,z));
        PlacedStructure Build(TowerKind kind,GridCell cell)
        {
            Assert.That(construction.TryBuild(Entry(kind),cell,Quaternion.identity,out string why),Is.True,why);
            return kind==TowerKind.Foundation?cell.Foundation:cell.Structure;
        }
        PlacedStructure Onboard()
        {
            Assert.That(game.ChooseStartingFaction(Faction.Navy),Is.True);PlacedStructure turret=null;
            int safety=0;
            while(game.State==NavyGameState.Tutorial&&safety++<30)
            {
                var step=game.Settings.Tutorial[game.Tutorial.StepIndex];
                if(step.Task==TutorialTask.Foundation)Build(TowerKind.Foundation,Cell());
                else if(step.Task==TutorialTask.Turret)turret=Build(TowerKind.Turret,Cell());
                else Assert.That(game.Tutorial.Next(),Is.True);
            }
            Assert.That(game.State,Is.EqualTo(NavyGameState.Preparation));return turret;
        }
        void SimpleWaves(int archetype=0,int count=1,float interval=.1f)
        {
            copy.RandomizeWaterRoutes=false;copy.ScaleEnemies=false;copy.Waves=Enumerable.Range(0,20).Select(_=>new NavalWaveDefinition{Groups=new[]{new NavalWaveGroup{Archetype=archetype,Count=count,SpawnInterval=interval}},Money=20,Exp=10,UpgradePoints=1}).ToArray();
        }
        IEnumerator FinishWave(TowerRuntime source)
        {
            Assert.That(game.StartNextWave(),Is.True);float deadline=Time.realtimeSinceStartup+8;
            while((game.State==NavyGameState.WaveActive||game.State==NavyGameState.WaveCompleted)&&Time.realtimeSinceStartup<deadline)
            {
                foreach(var enemy in game.Waves.ActiveEnemies.ToArray())if(enemy!=null)enemy.Runtime.TakeDamage(new DamageInfo(100000,DamageKind.Energy,source));
                yield return null;
            }
            Assert.That(game.State,Is.Not.EqualTo(NavyGameState.WaveActive));
        }

        [Test]
        public void SavedPrefabsStatsAndThreeResourcesRemainValid()
        {
            var paths = Directory.GetFiles(Root, "*.prefab", SearchOption.AllDirectories)
                .Where(p => !p.EndsWith("NavyRoot.prefab") && !p.Replace('\\','/').Contains("/Effects/")).ToArray();
            Assert.That(paths.Length, Is.EqualTo(27));
            foreach (string path in paths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                foreach (var transform in prefab.GetComponentsInChildren<Transform>(true))
                    Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject),
                        Is.Zero, path);
                Assert.That(prefab.GetComponent<Collider>(), Is.Not.Null, path);
                Assert.That(prefab.GetComponentsInChildren<Renderer>().Length, Is.GreaterThan(0), path);
                foreach (var renderer in prefab.GetComponentsInChildren<Renderer>())
                    Assert.That(renderer.sharedMaterial, Is.Not.Null, path);
                if (prefab.TryGetComponent<TowerRuntime>(out var tower))
                {
                    Assert.That(tower.Stats, Is.Not.Null, path);
                    Assert.That(tower.Stats.Faction, Is.EqualTo(Faction.Navy));
                    if (tower.Kind == TowerKind.Turret || tower.Kind == TowerKind.Cannon ||
                        tower.Kind == TowerKind.Slow || tower.Kind == TowerKind.Resource)
                    {
                        Assert.That(tower.Upgrade, Is.Not.Null);
                        Assert.That(tower.Upgrade.CurrentStats, Is.SameAs(tower.Stats));
                        Assert.That(tower.Upgrade.HasNextLevel, Is.EqualTo(tower.Stats.UpgradeLevel < 3));
                        if (tower.Upgrade.HasNextLevel)
                            Assert.That(tower.Upgrade.NextPrefab.GetComponent<TowerRuntime>().Stats,
                                Is.SameAs(tower.Upgrade.NextStats));
                    }
                }
                if (prefab.TryGetComponent<EnemyRuntime>(out var enemy))
                {
                    Assert.That(enemy.Stats, Is.Not.Null);
                    Assert.That(enemy.Stats.MovementType, Is.EqualTo(TargetDomain.Water));
                    Assert.That(prefab.GetComponent<StatusEffects>(), Is.Not.Null);
                    Assert.That(prefab.GetComponent<NavalEnemy>(), Is.Not.Null);
                }
                if (prefab.TryGetComponent<NavyWeapon>(out var weapon))
                {
                    Assert.That(weapon.ProjectilePrefab, Is.Not.Null);
                    Assert.That(weapon.MountPoint, Is.Not.Null);
                    Assert.That(weapon.Muzzles.All(m => m != null), Is.True);
                }
            }
            {
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public;
                var names = new[] { typeof(TowerStatsData), typeof(EnemyStatsData) }
                    .SelectMany(t => t.GetFields(flags))
                    .Where(f => f.FieldType == typeof(int) || f.FieldType == typeof(float))
                    .Select(f => f.Name == "AttackDamage" ? "Damage" : f.Name).Distinct().ToArray();
                Assert.That(names.Length, Is.GreaterThan(21));
                Assert.That(names, Does.Contain("Damage"));
                Assert.That(names, Does.Contain("MaxHealth"));
                Assert.That(names, Does.Contain("MovementSpeed"));
                Assert.That(names, Does.Contain("ResourceGenerationAmount"));
                var balances = typeof(IPlayerResources).GetProperties().Where(p => p.PropertyType == typeof(int))
                    .Select(p => p.Name).ToArray();
                Assert.That(balances, Is.EquivalentTo(new[] { "Money", "Exp", "UpgradePoints" }));
                var config = AssetDatabase.LoadAssetAtPath<PlayerResourceConfig>(
                    "Assets/Shared/Data/Resources/PlayerResources.asset");
                var resources = config.CreateRuntime();
                int money = resources.Money, exp = resources.Exp, points = resources.UpgradePoints;
                resources.AddMoney(17); resources.AddExp(11); resources.AddUpgradePoints(3);
                Assert.That(resources.Money, Is.EqualTo(money + 17));
                Assert.That(resources.Exp, Is.EqualTo(exp + 11));
                Assert.That(resources.UpgradePoints, Is.EqualTo(points + 3));
            }
            {
                Assert.That(pool.Catalog.Prefabs.Length,Is.EqualTo(Enum.GetValues(typeof(NavyEffectId)).Length));
                foreach(var effect in pool.Catalog.Prefabs)
                {
                    Assert.That(AssetDatabase.GetAssetPath(effect),Does.StartWith("Assets/Prefabs/Navy/Effects/"));
                    Assert.That(effect.GetComponent<NavyWeapon>(),Is.Null);Assert.That(effect.GetComponent<NavyProjectile>(),Is.Null);
                    Assert.That(effect.GetComponent<CombatHealth>(),Is.Null);Assert.That(effect.GetComponentsInChildren<Collider>(),Is.Empty);
                    Assert.That(effect.GetComponent<AudioSource>().playOnAwake,Is.False);
                    foreach(var p in effect.GetComponentsInChildren<ParticleSystem>())Assert.That(p.main.maxParticles,Is.LessThanOrEqualTo(48));
                }
                Assert.That(pool.Catalog.Find(NavyEffectId.Repaired).Audio.Clip.name,Is.EqualTo("towerBuilding"));
                Assert.That(pool.Catalog.Find(NavyEffectId.FactionUnlocked).Audio.Clip.name,Is.EqualTo("waveClear"));
                var wave=pool.Catalog.Find(NavyEffectId.WaveProjectile);
                Assert.That(AssetDatabase.LoadAssetAtPath<TowerStatsData>("Assets/Shared/Data/Towers/Navy/Cannon_L3.asset").WaveWidth,Is.EqualTo(3.2f));
                Assert.That(Vector3.Dot(wave.Ring.localRotation*Vector3.forward,Vector3.forward),Is.GreaterThan(.999f));
                Assert.That(wave.SecondaryRing,Is.Not.Null);
                foreach(var id in new[]{NavyEffectId.SlowPulse1,NavyEffectId.SlowPulse2,NavyEffectId.ToxicPulse})
                {
                    var audio=pool.Catalog.Find(id).Audio;
                    Assert.That(audio.Clip.name,Is.EqualTo("slowTowerShot"));
                    Assert.That(audio.Pitch,Is.EqualTo(new Vector2(.97f,1.03f)));
                    Assert.That(audio.Volume,Is.EqualTo(.4f));
                }
            }
            foreach(string guid in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Prefabs/Shared"}))
            {
                var shared=AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                foreach(var child in shared.GetComponentsInChildren<Transform>(true))
                    Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject),Is.Zero,shared.name);
            }
        }

        [Test]
        public void FoundationHeightsAndSupportComposeWithoutChangingStats()
        {
            game.enabled=false;
            foreach(var entry in new[]{("Normal",.8f,1f),("Medium",1.8f,1.25f),("High",3.2f,1.75f)})
            {
                var (name,height,multiplier)=entry;
                var foundation = Foundation(name);
                var tower = Tower("Attack/Turret/Turret_L1", Vector3.right * 5);
                float baseRange = tower.Stats.AttackRange;
                Assert.That(foundation.Height, Is.EqualTo(height));
                Assert.That(foundation.RangeMultiplier, Is.EqualTo(multiplier));
                Assert.That(foundation.TowerMount.localPosition.y, Is.EqualTo(height));
                Assert.That(foundation.GetComponent<TowerRuntime>().Kind, Is.EqualTo(TowerKind.Foundation));
                Assert.That(foundation.GetComponent<Collider>(), Is.Not.Null);
                Assert.That(foundation.GetComponentsInChildren<Renderer>().Length, Is.GreaterThan(10));
                Assert.That(foundation.TryMount(tower), Is.True);
                Assert.That(tower.transform.parent, Is.SameAs(foundation.TowerMount));
                Assert.That(tower.transform.localPosition, Is.EqualTo(Vector3.zero));
                Assert.That(tower.AttackRange, Is.EqualTo(baseRange * multiplier).Within(0.001f));
                Assert.That(tower.Stats.AttackRange, Is.EqualTo(baseRange));
                Assert.That(foundation.Unmount(tower), Is.True);
                Assert.That(tower.AttackRange, Is.EqualTo(baseRange));
            }
            {
                var foundation = Foundation("High");
                var tower = Tower("Attack/Turret/Turret_L1");
                var support = Tower("Support/NavySupportTower", Vector3.left * 3).GetComponent<NavySupportTower>();
                foundation.TryMount(tower);
                Physics.SyncTransforms(); support.RefreshBuffs();
                Assert.That(tower.AttackRange, Is.EqualTo(tower.Stats.AttackRange * 1.75f * 1.25f).Within(0.001f));
                support.enabled = false;
                Assert.That(tower.AttackRange, Is.EqualTo(tower.Stats.AttackRange * 1.75f));
                foundation.enabled = false;
                Assert.That(tower.AttackRange, Is.EqualTo(tower.Stats.AttackRange));
                foundation.enabled = true;
                Assert.That(tower.AttackRange, Is.EqualTo(tower.Stats.AttackRange * 1.75f));
                foundation.Unmount(tower);
                Assert.That(tower.AttackRange, Is.EqualTo(tower.Stats.AttackRange));
                Assert.That(tower.Stats.AttackRange, Is.EqualTo(10.4f));
            }
        }

        [UnityTest]
        public IEnumerator FriendlyShotsMoveOnlyBarrelAssembliesAndApplyDamageStatusesOnce()
        {
            game.enabled=false;
            var enemy=Enemy();enemy.Health.Initialize(100000);
            foreach(string path in Directory.GetFiles("Assets/Prefabs/Navy/Towers","*.prefab",SearchOption.AllDirectories))
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(prefab.GetComponent<NavyWeapon>()==null)continue;
                var go=Object.Instantiate(prefab,origin,Quaternion.identity);spawned.Add(go);var weapon=go.GetComponent<NavyWeapon>();var fx=go.GetComponent<NavyTowerEffects>();var runtime=go.GetComponent<TowerRuntime>();
                var rest=fx.RecoilParts.Select(t=>t.localPosition).ToArray();var accessoryRest=fx.BarrelAccessories.Select(g=>g.Parts.Select(t=>t.localPosition).ToArray()).ToArray();var rotations=fx.RecoilParts.Select(t=>t.localRotation).ToArray();var body=go.transform.position;var rotation=go.transform.rotation;var head=go.transform.Find("Visuals/YawPivot");var headPosition=head.localPosition;
                var fired=new HashSet<int>();var seen=new HashSet<int>();var emitted=new List<NavyProjectile>();
                weapon.Fired+=(m,p)=>{int index=Array.IndexOf(weapon.Muzzles,m);fired.Add(index);seen.Add(index);emitted.Add(p);};
                Physics.SyncTransforms();
                for(int step=0;seen.Count<weapon.Muzzles.Length&&step<6;step++)
                {
                    fired.Clear();emitted.Clear();float before=enemy.CurrentHealth;
                    weapon.enabled=true;runtime.AdvanceCooldowns(10000);Assert.That(weapon.TryFire(enemy),Is.True,path);weapon.enabled=false;
                    Assert.That(emitted.Count,Is.EqualTo(fired.Count));
                    yield return new WaitForSeconds(.035f);
                    for(int i=0;i<rest.Length;i++)
                    {
                        float delta=Vector3.Distance(rest[i],fx.RecoilParts[i].localPosition);
                        if(fired.Contains(i))Assert.That(delta,Is.GreaterThan(.001f),path+" barrel "+i);else Assert.That(delta,Is.LessThan(.0001f),path+" idle barrel "+i);
                        Assert.That(Quaternion.Angle(fx.RecoilParts[i].localRotation,rotations[i]),Is.LessThan(.001f));
                        Assert.That(fx.BarrelAccessories[i].Parts.Length,Is.GreaterThan(0),path+" attachments");
                        for(int j=0;j<fx.BarrelAccessories[i].Parts.Length;j++)
                        {
                            var part=fx.BarrelAccessories[i].Parts[j];var travel=part.parent.TransformVector(part.localPosition-accessoryRest[i][j]);
                            var barrelTravel=fx.RecoilParts[i].parent.TransformVector(fx.RecoilParts[i].localPosition-rest[i]);
                            Assert.That(Vector3.Distance(travel,barrelTravel),Is.LessThan(.001f),path+" accessory "+part.name);
                        }
                    }
                    // Close targets can already be hit during the recoil animation frame.
                    var projectiles=emitted.Where(p=>p!=null).ToArray();
                    foreach(var shot in projectiles)shot.Advance(1);
                    float after=enemy.CurrentHealth;Assert.That(after,Is.LessThan(before),path);
                    foreach(var shot in projectiles)shot.Advance(.01f);
                    Assert.That(enemy.CurrentHealth,Is.EqualTo(after),"A projectile must not damage the same ship twice");
                    if(runtime.Kind==TowerKind.Slow)
                    {
                        Assert.That(enemy.Effects.SlowRemaining,Is.GreaterThan(0));
                        Assert.That(enemy.Effects.PoisonRemaining>0,Is.EqualTo(runtime.Stats.UpgradeLevel==3));
                        float health=enemy.CurrentHealth;enemy.Effects.Advance(1);
                        if(runtime.Stats.UpgradeLevel==3)Assert.That(enemy.CurrentHealth,Is.LessThan(health));
                        enemy.Effects.Clear();
                    }
                    foreach(var shot in projectiles)Object.Destroy(shot.gameObject);
                    Assert.That(go.transform.position,Is.EqualTo(body));Assert.That(go.transform.rotation,Is.EqualTo(rotation));Assert.That(head.localPosition,Is.EqualTo(headPosition));
                    yield return new WaitForSeconds(fx.RecoilDuration+.04f);
                    for(int i=0;i<rest.Length;i++)Assert.That(Vector3.Distance(rest[i],fx.RecoilParts[i].localPosition),Is.LessThan(.0001f));
                }
                Assert.That(seen.Count,Is.EqualTo(weapon.Muzzles.Length));Object.Destroy(go);yield return null;
            }
            {
                var go=Actor("Towers/Attack/Cannon/Cannon_L3");var runtime=go.GetComponent<TowerRuntime>();var weapon=go.GetComponent<NavyWeapon>();Enemy();Physics.SyncTransforms();
                runtime.FireCooldown=.5f;runtime.AbilityCooldown=.5f;int shots=weapon.ShotsFired;
                yield return null;yield return null;
                Assert.That(weapon.HasValidTarget,Is.True);Assert.That(weapon.ShotsFired,Is.EqualTo(shots));
                Assert.That(Object.FindObjectsByType<NavyEffect>(FindObjectsSortMode.None).Any(e=>e.Id==NavyEffectId.WaveCharge&&e.IsLeased),Is.True);
                Assert.That(runtime.FireCooldown,Is.LessThan(.5f));
                var charge=Object.FindObjectsByType<NavyEffect>(FindObjectsSortMode.None).Single(e=>e.Id==NavyEffectId.WaveCharge&&e.IsLeased);
                Assert.That(charge.ChargeProgress,Is.GreaterThan(.5f));Assert.That(charge.Loop,Is.True);
                Assert.That(Vector3.Dot(charge.Ring.forward,charge.transform.forward),Is.GreaterThan(.999f));
                charge.SetChargeProgress(.1f);float ringSize=charge.Ring.localScale.x,coreSize=charge.ChargeCore.localScale.x;
                charge.SetChargeProgress(.9f);Assert.That(charge.Ring.localScale.x,Is.LessThan(ringSize));Assert.That(charge.ChargeCore.localScale.x,Is.GreaterThan(coreSize));
                Assert.That(charge.SecondaryRing,Is.Not.Null);
                go.SetActive(false);
                Assert.That(Object.FindObjectsByType<NavyEffect>(FindObjectsSortMode.None).Any(e=>e.Id==NavyEffectId.WaveCharge&&e.IsLeased),Is.False);
            }
            {
                int created=pool.InstancesCreated;
                for(int i=0;i<25;i++){var fx=pool.Play(NavyEffectId.BulletImpact,origin);Assert.That(fx,Is.Not.Null);fx.Release();foreach(var p in fx.GetComponentsInChildren<ParticleSystem>())Assert.That(p.particleCount,Is.Zero);}
                Assert.That(pool.InstancesCreated,Is.EqualTo(created));
                foreach(var id in new[]{NavyEffectId.TurretFire1,NavyEffectId.CannonFire1,NavyEffectId.WaveFire,NavyEffectId.DeathLarge,NavyEffectId.CannonImpact1,NavyEffectId.Upgraded,NavyEffectId.OilMachinery})
                    for(int i=0;i<18;i++)pool.Play(id,origin);
                Assert.That(pool.ActiveAudioVoices,Is.LessThanOrEqualTo(pool.Catalog.MaximumAudioVoices));Assert.That(pool.ActiveCount,Is.LessThanOrEqualTo(pool.Catalog.MaximumActiveEffects));
                pool.Clear();Assert.That(pool.ActiveCount,Is.Zero);Assert.That(pool.ActiveAudioVoices,Is.Zero);
            }
        }

        [Test]
        public void SavedWorldHasOneWorkingRigInvisibleBordersAndWalkableWater()
        {
            {
                var origin=Object.FindAnyObjectByType<XROrigin>();var interaction=game.Panel.Interaction;
                Assert.That(origin.name,Is.EqualTo("XR Origin (XR Rig)"));Assert.That(interaction.PhysicalCamera,Is.SameAs(origin.Camera));
                Assert.That(interaction.RightNearFar,Is.Not.Null);Assert.That(game.View,Is.Not.Null);
                Assert.That(game.Panel.GetComponent<Canvas>().renderMode,Is.EqualTo(RenderMode.WorldSpace));
                Assert.That(game.Panel.GetComponent<Canvas>().worldCamera,Is.SameAs(origin.Camera));
                Assert.That(origin.gameObject.layer,Is.EqualTo(LayerMask.NameToLayer("Player")));
                Assert.That(origin.GetComponent<CharacterController>().enabled,Is.True);
                var assets=Object.FindObjectsByType<InputActionManager>(FindObjectsSortMode.None).SelectMany(m=>m.actionAssets).ToArray();
                Assert.That(assets.Any(a=>a.name=="XRI Default Input Actions"),Is.True);Assert.That(assets.Any(a=>a.name=="BattlefieldControls"),Is.True);
                var move=origin.GetComponentInChildren<ContinuousMoveProvider>();
                Assert.That(move.leftHandMoveInput.inputActionReference.action.enabled,Is.True);
                Assert.That(game.Panel.GetComponent<Canvas>().transform.IsChildOf(origin.transform),Is.False);
                var quest=AssetDatabase.LoadAllAssetsAtPath("Assets/XR/Settings/OpenXR Package Settings.asset").Single(a=>a.name=="MetaQuestFeature Android");
                Assert.That(new SerializedObject(quest).FindProperty("m_enabled").boolValue,Is.True);
                Assert.That(interaction.MenuAction.action.bindings.Single().path,Is.EqualTo("<XRController>{LeftHand}/primaryButton"));
                Assert.That(interaction.SwitchTabAction.action.bindings.Single().path,Is.EqualTo("<XRController>{RightHand}/primaryButton"));
                Assert.That(interaction.CancelAction.action.bindings.All(b=>b.path!="<XRController>{RightHand}/secondaryButton"),Is.True);
                var overlays=game.Panel.Hud.GetComponentsInChildren<TacticalMapOverlay>(true);Assert.That(overlays.Length,Is.EqualTo(2));
                foreach(var overlay in overlays)
                {
                    Assert.That(overlay.GetComponent<CanvasRenderer>(),Is.Not.Null);Assert.That(overlay.raycastTarget,Is.False);
                    Assert.That(overlay.MapCamera.targetTexture.height,Is.GreaterThan(overlay.MapCamera.targetTexture.width));
                    Assert.That(overlay.Player,Is.SameAs(origin.Camera.transform));Assert.That(overlay.MapPoint(origin.Camera.transform.position,out var point),Is.True);
                    Assert.That(overlay.rectTransform.rect.Contains(point),Is.True);
                }
            }
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
                Assert.That(((UnityEngine.XR.Interaction.Toolkit.Interactors.Casters.CurveInteractionCaster)panel.Interaction.RightNearFar.farInteractionCaster).raycastMask.value&(1<<9),Is.Zero);
            }
            foreach(var point in new[]{new Vector2(-28,-44),new Vector2(28,-44),new Vector2(0,-36),new Vector2(36,-52)})
            {
                float x=point.x,z=point.y;
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
        }

        [UnityTest]
        public IEnumerator TutorialConstructionUpgradesRepairAndSaleShareOneWallet()
        {
            var tower=Onboard();var wallet=game.Session.Resources;var cell=tower.Cell;var foundation=cell.Foundation;
            int money=wallet.Money;
            Assert.That(construction.TryBuild(Entry(TowerKind.Foundation),cell,Quaternion.identity,out _),Is.False);
            Assert.That(construction.TryBuild(Entry(TowerKind.Turret),Cell(4,-32),Quaternion.identity,out _),Is.False);
            Assert.That(wallet.Money,Is.EqualTo(money));
            wallet.AddMoney(3000);wallet.AddExp(400);wallet.AddUpgradePoints(10);
            int points=wallet.UpgradePoints;money=wallet.Money;
            Assert.That(construction.TryGlobalUpgrade(Entry(TowerKind.Turret),out _),Is.True);
            Assert.That(wallet.Money,Is.EqualTo(money));Assert.That(wallet.UpgradePoints,Is.LessThan(points));Assert.That(tower.TierIndex,Is.Zero);
            var nextCell=Cell(4,-32);Build(TowerKind.Foundation,nextCell);var next=Build(TowerKind.Turret,nextCell);
            Assert.That(next.TierIndex,Is.EqualTo(1));Assert.That(tower.TierIndex,Is.Zero);
            tower.Runtime.Health.TakeDamage(new DamageInfo(30));float fraction=tower.Runtime.CurrentHealth/tower.Runtime.Health.MaxHealth;
            var old=tower;Assert.That(construction.TryIndividualUpgrade(tower,out _),Is.True);tower=cell.Structure;
            Assert.That(old.Runtime.GetComponent<NavyWeapon>().enabled,Is.False);Assert.That(tower.TierIndex,Is.EqualTo(1));
            Assert.That(cell.Foundation,Is.SameAs(foundation));Assert.That(tower.transform.parent,Is.SameAs(foundation.Foundation.TowerMount));
            Assert.That(tower.Runtime.CurrentHealth/tower.Runtime.Health.MaxHealth,Is.EqualTo(fraction).Within(.0001f));
            money=wallet.Money;Assert.That(construction.TryRepair(tower,out _),Is.True);
            Assert.That(wallet.Money,Is.EqualTo(money-tower.Runtime.Stats.RepairCost));Assert.That(tower.Runtime.CurrentHealth,Is.EqualTo(tower.Runtime.Health.MaxHealth));
            money=wallet.Money;Assert.That(construction.TryRepair(tower,out _),Is.False);Assert.That(wallet.Money,Is.EqualTo(money));
            Assert.That(construction.TryIndividualUpgrade(foundation,out _),Is.True);
            Assert.That(cell.Structure,Is.SameAs(tower));Assert.That(tower.Runtime.AttackRange,Is.EqualTo(tower.Runtime.Stats.AttackRange*1.25f).Within(.001f));
            Assert.That(construction.TrySell(cell.Foundation,out _),Is.False);
            money=wallet.Money;int refund=tower.Runtime.Stats.SellValue;Assert.That(construction.TrySell(tower,out _),Is.True);
            Assert.That(cell.Structure,Is.Null);Assert.That(wallet.Money,Is.EqualTo(money+refund));
            Assert.That(construction.TrySell(tower,out _),Is.False);Assert.That(wallet.Money,Is.EqualTo(money+refund));
            yield return null;Assert.That(old==null,Is.True);
        }

        [UnityTest]
        public IEnumerator PanelsOpenNearPlayerCloseTogetherAndKeepMapsAndMovementLive()
        {
            {
                var tower=Onboard();var panel=game.Panel;panel.SetCommander(true);panel.Tutorials.Close();panel.CloseMap.onClick.Invoke();
                yield return new WaitForSecondsRealtime(.17f);Assert.That(panel.CommanderOpen,Is.False);Assert.That(panel.PanelContent.activeSelf,Is.False);
                Assert.That(panel.Hud.Collapsed.activeInHierarchy,Is.True);
                panel.ShowPhysicalPanel();var previous=panel.transform.position;
                var origin=panel.Interaction.PhysicalCamera.GetComponentInParent<XROrigin>();origin.transform.position+=Vector3.right*6;Physics.SyncTransforms();
                panel.Interaction.HandleRay(new Ray(tower.transform.position+Vector3.up*20,Vector3.down),true);
                Assert.That(construction.Selected,Is.SameAs(tower));Assert.That(panel.PanelContent.activeSelf,Is.True);
                Assert.That(Vector3.Distance(previous,panel.transform.position),Is.GreaterThan(4));
                var offset=panel.transform.position-origin.Camera.transform.position;offset.y=0;Assert.That(offset.magnitude,Is.EqualTo(panel.OpenDistance).Within(.02f));
            }
            {
                var hud=game.Panel.Hud;Assert.That(hud.EconomyLabels.Length,Is.EqualTo(2));
                Assert.That(((RectTransform)hud.Collapsed.transform).sizeDelta,Is.EqualTo(new Vector2(350,460)));
                Assert.That(((RectTransform)hud.Expanded.transform).sizeDelta,Is.EqualTo(new Vector2(520,970)));
                game.Session.Resources.AddMoney(137);game.Session.Resources.AddExp(120);
                foreach(var label in hud.EconomyLabels)
                {Assert.That(label.text,Does.Contain(game.Session.Resources.Money.ToString("N0")));Assert.That(label.text,Does.Contain(game.Session.RankName));Assert.That(label.text,Does.Contain("RANK "+game.Session.RankNumber));}
            }
            {
                var origin=Object.FindAnyObjectByType<XROrigin>();var move=origin.GetComponentInChildren<ContinuousMoveProvider>();
                var input=move.leftHandMoveInput;var source=input.inputSourceMode;
                // Exercise the Quest path: desktop WASD/gravity does not run on Android.
                var desktop=origin.GetComponent<DesktopNavigation>();desktop.enabled=false;
                try
                {
                    input.inputSourceMode=XRInputValueReader.InputSourceMode.ManualValue;input.manualValue=Vector2.right;
                    Assert.That(move.mediator.xrOrigin,Is.SameAs(origin));
                    var start=origin.transform.position;float until=Time.time+.3f;int frames=0;while(Time.time<until||frames++<8)yield return null;
                    var body=origin.GetComponent<CharacterController>();
                    Assert.That(origin.transform.position.x-start.x,Is.GreaterThan(.2f),"start="+start+" end="+origin.transform.position+" input="+input.ReadValue()+" move="+move.enabled+" elapsed="+(Time.time-until+.3f)+" delta="+Time.deltaTime+" flags="+body.collisionFlags+" center="+body.center+" height="+body.height+" transformer="+move.mediator.bodyTransformer.isActiveAndEnabled+" colliders="+string.Join(",",Physics.OverlapCapsule(body.bounds.min+Vector3.up*.25f,body.bounds.max-Vector3.up*.25f,.3f).Select(c=>c.name)));
                    game.Panel.SetCommander(true);yield return null;start=origin.transform.position;
                    until=Time.time+.2f;while(Time.time<until)yield return null;
                    Assert.That(Vector3.Distance(origin.transform.position,start),Is.GreaterThan(.2f),"Overview must leave real XR movement running");
                    game.Panel.SetCommander(false);Assert.That(move.enabled,Is.True);
                }
                finally{input.manualValue=Vector2.zero;input.inputSourceMode=source;desktop.enabled=true;}
            }
            game.Panel.BeginConstruction(Entry(TowerKind.Foundation));game.Panel.Tutorials.Close();
            yield return new WaitForSecondsRealtime(.17f);Assert.That(game.Panel.PanelContent.activeSelf,Is.False);Assert.That(game.Panel.Preview.Entry,Is.Not.Null);
            game.Panel.Preview.Cancel();game.Panel.ShowPhysicalPanel();
            Assert.That(game.Panel.Hud.GetComponentsInChildren<TacticalMapOverlay>(true).Length,Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator DoubleJumpUsesBAndLandsOnWaterWithoutResettingInOverview()
        {
            Onboard();game.Panel.Tutorials.Close();var origin=game.Panel.Interaction.PhysicalCamera.GetComponentInParent<XROrigin>();
            var gate=origin.GetComponent<JumpInteractionGate>();var jump=gate.Jump;
            Assert.That(gate.JumpAction.action.bindings.Any(b=>b.path=="<XRController>{RightHand}/secondaryButton"),Is.True);
            var pose=origin.Camera.GetComponent<UnityEngine.InputSystem.XR.TrackedPoseDriver>();bool tracking=pose.enabled;
            var body=origin.GetComponent<CharacterController>();body.enabled=false;origin.transform.position=new Vector3(-8,2,-32);body.enabled=true;Physics.SyncTransforms();
            yield return new WaitForSeconds(1.2f);float baseline=origin.transform.position.y;
            Assert.That(baseline,Is.GreaterThan(-.5f));Assert.That(jump.CanJump(),Is.True);
            jump.Jump();yield return new WaitForSeconds(.18f);Assert.That(origin.transform.position.y,Is.GreaterThan(baseline+.08f));
            Assert.That(jump.CanJump(),Is.True,"One air jump should remain");jump.Jump();yield return new WaitForSeconds(.12f);
            Assert.That(jump.CanJump(),Is.False,"A third jump is forbidden");
            game.Panel.SetCommander(true);game.Panel.SetCommander(false);game.Panel.Tutorials.Close();Assert.That(jump.CanJump(),Is.False,"Opening a map cannot reset the air allowance");
            yield return new WaitForSeconds(1.4f);Assert.That(jump.CanJump(),Is.True);Assert.That(origin.transform.position.y,Is.EqualTo(baseline).Within(.12f));Assert.That(pose.enabled,Is.EqualTo(tracking));
            game.Panel.Preview.Begin(Entry(TowerKind.Foundation));yield return null;Assert.That(gate.Blocked,Is.True);Assert.That(jump.enabled,Is.True);Assert.That(gate.ReadIsPerformed(),Is.False);game.Panel.Preview.Cancel();
        }

        [UnityTest]
        public IEnumerator NavalRoutesSpawnFromMultipleDirectionsAndScaleOnlyRuntimeEnemies()
        {
            {
                var stats=copy.Enemies[0].Prefab.GetComponent<EnemyRuntime>().Stats;
                var bytes=File.ReadAllBytes(AssetDatabase.GetAssetPath(stats));var actor=Object.Instantiate(copy.Enemies[0].Prefab);copy.ScaleEnemies=true;
                copy.ScaleSpawn(actor.Runtime,1);float early=actor.Runtime.Health.MaxHealth,damage=actor.Runtime.AttackDamage;
                copy.ScaleSpawn(actor.Runtime,15);Assert.That(actor.Runtime.Health.MaxHealth,Is.GreaterThan(early));Assert.That(actor.Runtime.AttackDamage,Is.GreaterThan(damage));
                copy.ScaleSpawn(actor.Runtime,30);Assert.That(actor.Runtime.AttackInterval,Is.GreaterThanOrEqualTo(.1f));Assert.That(actor.Runtime.MovementSpeed,Is.LessThanOrEqualTo(stats.MovementSpeed*1.5f));
                Assert.That(File.ReadAllBytes(AssetDatabase.GetAssetPath(stats)),Is.EqualTo(bytes));Assert.That(stats.MaxHealth,Is.EqualTo(early));Object.DestroyImmediate(actor.gameObject);
            }
            var turret=Onboard();Assert.That(game.Waves.Routes.Select(r=>r.Id),Is.EquivalentTo(new[]{"South","West","East","WestCurve","EastCurve"}));
            foreach(var enemy in copy.Enemies)for(int route=0;route<game.Waves.Routes.Length;route++)Assert.That(game.Waves.SafeRoute(route,enemy.Prefab),Is.Not.Null);
            int money=game.Session.Resources.Money;
            Assert.That(construction.TryBuild(Entry(TowerKind.Foundation),Cell(-24,-44),Quaternion.identity,out var why),Is.False);
            Assert.That(why,Does.Contain("lanes"));Assert.That(game.Session.Resources.Money,Is.EqualTo(money));
            Assert.That(game.Waves.IsShippingLane(construction.Grid.World(Cell())),Is.False);
            SimpleWaves(0,24,.1f);copy.RandomizeWaterRoutes=true;
            var random=UnityEngine.Random.state;UnityEngine.Random.InitState(314159);var directions=new HashSet<int>();
            try
            {
                Assert.That(game.StartNextWave(),Is.True);float until=Time.realtimeSinceStartup+10;
                while((game.State==NavyGameState.WaveActive||game.State==NavyGameState.WaveCompleted)&&Time.realtimeSinceStartup<until)
                {
                    foreach(var ship in game.Waves.ActiveEnemies.ToArray())
                    {directions.Add(ship.transform.position.x< -10?-1:ship.transform.position.x>10?1:0);ship.Runtime.TakeDamage(new DamageInfo(10000,DamageKind.Energy,turret.Runtime));}
                    yield return null;
                }
                Assert.That(directions,Is.EquivalentTo(new[]{-1,0,1}));Assert.That(game.State,Is.EqualTo(NavyGameState.Preparation));
            }
            finally{UnityEngine.Random.state=random;}
        }

        [UnityTest]
        public IEnumerator EnemyBurstsUseRealGunsAndPassingFireCanDamageAndDestroyTowers()
        {
            var turret=Onboard();game.Panel.Tutorials.Close();Vector3 origin=new Vector3(1200,0,1200);
            foreach(var type in copy.Enemies)
            {
                var actor=Object.Instantiate(type.Prefab,origin,Quaternion.identity);actor.enabled=false;
                actor.BindAssault(new[]{origin,origin+Vector3.forward*10},game.Base.SharedBase,origin+Vector3.right,type.AttackProjectile,type.MuzzleLocal);
                var shots=new List<NavyProjectile>();var launches=new List<Vector3>();actor.Attacked+=(p,q,shot)=>{shots.Add(shot);launches.Add(p);};
                actor.AdvanceAssault(.001f);Assert.That(shots.Count,Is.EqualTo(1));
                for(int i=1;i<actor.Runtime.Stats.ShotsPerAttack;i++)actor.AdvanceAssault(actor.BurstShotInterval+.0001f);
                Assert.That(shots.Count,Is.EqualTo(actor.Runtime.Stats.ShotsPerAttack));
                Assert.That(shots.Sum(p=>p.HostileDamage),Is.EqualTo(actor.Runtime.AttackDamage).Within(.001f));
                if(actor.Runtime.Stats.EnemyType==EnemyKind.Battleship)
                {
                    Assert.That(shots.Count,Is.EqualTo(8));Assert.That(launches.Distinct().Count(),Is.EqualTo(8));
                    Assert.That(shots.All(p=>p.Kind==NavyProjectileKind.Round&&p.HostileDamage<actor.Runtime.AttackDamage/4),Is.True);
                    Assert.That(actor.Runtime.Stats.ShotVisualScale,Is.LessThan(.6f));
                }
                int count=shots.Count;actor.AdvanceAssault(.001f);Assert.That(shots.Count,Is.EqualTo(count));
                if(actor.Runtime.Stats.ShotsPerAttack>1)
                {
                    actor.AdvanceAssault(actor.AttackCycleInterval);Assert.That(actor.PendingShots,Is.GreaterThan(0));
                    count=shots.Count;actor.transform.position+=Vector3.back*100;actor.AdvanceAssault(actor.BurstShotInterval+.001f);
                    Assert.That(actor.PendingShots,Is.Zero);Assert.That(shots.Count,Is.EqualTo(count),"Leaving attack range must cancel remaining burst shots");
                }
                foreach(var shot in shots)Object.Destroy(shot.gameObject);Object.Destroy(actor.gameObject);yield return null;
            }
            {
                SimpleWaves();Assert.That(game.StartNextWave(),Is.True);
                var ship=game.Waves.ActiveEnemies.Single();ship.transform.position=new Vector3(-2,0,-32);Physics.SyncTransforms();
                float health=turret.Runtime.CurrentHealth;var position=ship.transform.position;
                ship.AdvanceAssault(.01f);var shots=Object.FindObjectsByType<NavyProjectile>(FindObjectsSortMode.None).Where(p=>p.HostileTarget==turret.Runtime.Health).ToArray();
                Assert.That(shots.Length,Is.EqualTo(1),"Nearby ship did not fire within its configured range");var shot=shots[0];
                Assert.That(ship.transform.position,Is.Not.EqualTo(position));int attacks=ship.AttackCount;shot.Advance(5);
                float after=turret.Runtime.CurrentHealth;Assert.That(after,Is.LessThan(health));shot.Advance(5);Assert.That(turret.Runtime.CurrentHealth,Is.EqualTo(after));
                ship.AdvanceAssault(.1f);Assert.That(ship.AttackCount,Is.EqualTo(attacks));
                int money=game.Session.Resources.Money;Assert.That(construction.TryRepair(turret,out _),Is.True);
                Assert.That(turret.Runtime.CurrentHealth,Is.EqualTo(health));Assert.That(game.Session.Resources.Money,Is.EqualTo(money-turret.Runtime.Stats.RepairCost));
                var cell=turret.Cell;var foundation=cell.Foundation;turret.Runtime.Health.Initialize(1,0);money=game.Session.Resources.Money;
                ship.AdvanceAssault(ship.Runtime.Stats.AttackInterval);
                foreach(var projectile in Object.FindObjectsByType<NavyProjectile>(FindObjectsSortMode.None).Where(p=>p.HostileTarget==turret.Runtime.Health))projectile.Advance(5);
                Assert.That(cell.Structure,Is.Null);Assert.That(cell.Foundation,Is.SameAs(foundation));Assert.That(game.Session.Resources.Money,Is.EqualTo(money));yield return null;
            }
        }

        [UnityTest]
        public IEnumerator RealTowerCombatRewardsWavesOnceAndUnlocksRegionsAtTenAndTwenty()
        {
            var tower=Onboard();
            foreach(var cell in new[]{Cell(4,-32),Cell(-4,-24),Cell(4,-24)}){Build(TowerKind.Foundation,cell);Build(TowerKind.Turret,cell);}
            Assert.That(game.StartNextWave(),Is.True);float combatUntil=Time.realtimeSinceStartup+30;int health=(int)game.Base.SharedBase.Health.CurrentHealth;
            while((game.State==NavyGameState.WaveActive||game.State==NavyGameState.WaveCompleted)&&Time.realtimeSinceStartup<combatUntil)yield return null;
            Assert.That(tower.Runtime.GetComponent<NavyWeapon>().ShotsFired,Is.GreaterThan(0));
            Assert.That(game.State,Is.EqualTo(NavyGameState.Preparation),"Real tower fire did not defeat the moving ships: "+string.Join("; ",game.Waves.ActiveEnemies.Select(e=>e.transform.position+" HP="+e.Runtime.CurrentHealth+" attacks="+e.AttackCount)));
            Assert.That(game.Session.Resources.Money,Is.GreaterThan(400));Assert.That(game.Base.SharedBase.Health.CurrentHealth,Is.EqualTo(health));
            {
                SimpleWaves(0,2,.35f);int money=game.Session.Resources.Money,exp=game.Session.Resources.Exp,points=game.Session.Resources.UpgradePoints;
                Assert.That(game.StartNextWave(),Is.True);Assert.That(game.StartNextWave(),Is.False);Assert.That(game.Waves.ScheduledCount,Is.EqualTo(2));
                var first=game.Waves.ActiveEnemies.Single();var stats=first.Runtime.Stats;first.Runtime.TakeDamage(new DamageInfo(10000,DamageKind.Energy,tower.Runtime));
                first.Runtime.TakeDamage(new DamageInfo(10000,DamageKind.Energy,tower.Runtime));Assert.That(game.State,Is.EqualTo(NavyGameState.WaveActive));
                float until=Time.realtimeSinceStartup+4;while(game.Waves.SpawnedCount<2&&Time.realtimeSinceStartup<until)yield return null;
                Assert.That(game.Waves.SpawnedCount,Is.EqualTo(2));game.Waves.ActiveEnemies.Single().Runtime.TakeDamage(new DamageInfo(10000,DamageKind.Energy,tower.Runtime));
                until=Time.realtimeSinceStartup+3;while(game.State!=NavyGameState.Preparation&&Time.realtimeSinceStartup<until)yield return null;
                Assert.That(game.State,Is.EqualTo(NavyGameState.Preparation));Assert.That(game.Session.Resources.Money,Is.EqualTo(money+stats.MoneyReward*2+20));
                Assert.That(game.Session.Resources.Exp,Is.EqualTo(exp+stats.ExpReward*2+10));Assert.That(game.Session.Resources.UpgradePoints,Is.EqualTo(points+1));
                yield return null;Assert.That(game.Session.Resources.Money,Is.EqualTo(money+stats.MoneyReward*2+20));
            }
            {
                SimpleWaves();
                for(int wave=game.Waves.CurrentWave+1;wave<=20;wave++)
                {
                    yield return FinishWave(tower.Runtime);
                    if(wave==10)
                    {
                        Assert.That(game.State,Is.EqualTo(NavyGameState.FactionUnlock));Assert.That(game.StartNextWave(),Is.False);
                        Assert.That(game.ChooseRegionUnlock(Faction.Army),Is.True);Assert.That(game.ChooseRegionUnlock(Faction.Army),Is.False);
                        Assert.That(game.Regions.IsUnlocked(BattlefieldRegion.LeftLand),Is.True);Assert.That(game.Regions.IsUnlocked(BattlefieldRegion.RightLand),Is.False);
                    }
                    if(wave==20)
                    {
                        Assert.That(game.Session.FactionActive(Faction.Sky),Is.True);Assert.That(game.Regions.IsUnlocked(BattlefieldRegion.RightLand),Is.True);
                        Assert.That(game.Panel.Tutorials.Current.Body.text,Does.Contain("unavailable"));game.Panel.Tutorials.Current.Next.onClick.Invoke();
                    }
                }
                Assert.That(game.Waves.CurrentWave,Is.EqualTo(20));Assert.That(game.State,Is.EqualTo(NavyGameState.Preparation));Assert.That(game.Session.Catalog.All(x=>x.Faction==Faction.Navy),Is.True);
                int points=game.Session.Resources.UpgradePoints;game.Session.Resources.AddExp(0);Assert.That(game.Session.Resources.UpgradePoints,Is.EqualTo(points));
            }
        }

        [UnityTest]
        public IEnumerator AllOilTiersProduceOnlyDuringActiveWavesAndStopTheirEffects()
        {
            Onboard();game.Session.Resources.AddExp(400);game.Session.Resources.AddMoney(3000);
            var foundation=Build(TowerKind.Foundation,Cell(8,-32));var placed=Build(TowerKind.Resource,foundation.Cell);
            Assert.That(construction.TryIndividualUpgrade(placed,out var reason),Is.True,reason);placed=foundation.Cell.Structure;
            game.Session.Resources.AddUpgradePoints(2);Assert.That(construction.TryIndividualUpgrade(placed,out reason),Is.True,reason);placed=foundation.Cell.Structure;
            var oil=placed.Runtime.GetComponent<OilExtractor>();var stats=placed.Runtime.Stats;var wallet=game.Session.Resources;
            int money=wallet.Money,exp=wallet.Exp,points=wallet.UpgradePoints;float remaining=oil.ProductionRemaining;
            oil.AdvanceProduction(100);Assert.That(oil.IsOperating,Is.False);Assert.That(oil.ProductionRemaining,Is.EqualTo(remaining));
            Assert.That(wallet.Money,Is.EqualTo(money));Assert.That(wallet.Exp,Is.EqualTo(exp));Assert.That(wallet.UpgradePoints,Is.EqualTo(points));
            SimpleWaves(4);Assert.That(game.StartNextWave(),Is.True);Assert.That(oil.IsOperating,Is.True);
            oil.AdvanceProduction(stats.ResourceGenerationInterval+.001f);
            Assert.That(wallet.Money,Is.EqualTo(money+stats.ResourceGenerationAmount));Assert.That(wallet.Exp,Is.EqualTo(exp+stats.ResourceExpGenerationAmount));
            Assert.That(wallet.UpgradePoints,Is.EqualTo(points+stats.ResourceUpgradePointGenerationAmount));
            yield return null;
            Assert.That(Object.FindObjectsByType<NavyEffect>(FindObjectsSortMode.None).Any(e=>e.IsLeased&&e.Id==NavyEffectId.OilMachinery),Is.True);
            foreach(int level in new[]{1,2,3})
            {
                var actor=Actor("Towers/Buildings/"+(level==1?"OilExtractor":"OilExtractor_L"+level));
                var extractor=actor.GetComponent<OilExtractor>();var balance=new PlayerResources();var definition=actor.GetComponent<TowerRuntime>().Stats;
                extractor.BindResources(balance,game.Session);extractor.AdvanceProduction(definition.ResourceGenerationInterval+.001f);
                Assert.That(balance.Money,Is.EqualTo(definition.ResourceGenerationAmount));Assert.That(balance.Exp,Is.EqualTo(level==1?0:5));
                Assert.That(balance.UpgradePoints,Is.EqualTo(level==3?1:0));
                actor.SetActive(false);
            }
            game.Waves.StopWave();remaining=oil.ProductionRemaining;money=wallet.Money;exp=wallet.Exp;points=wallet.UpgradePoints;
            oil.AdvanceProduction(100);yield return null;
            Assert.That(oil.IsOperating,Is.False);Assert.That(oil.ProductionRemaining,Is.EqualTo(remaining));
            Assert.That(wallet.Money,Is.EqualTo(money));Assert.That(wallet.Exp,Is.EqualTo(exp));Assert.That(wallet.UpgradePoints,Is.EqualTo(points));
            Assert.That(Object.FindObjectsByType<NavyEffect>(FindObjectsSortMode.None).Any(e=>e.IsLeased&&(e.Id==NavyEffectId.OilSteam||e.Id==NavyEffectId.OilMachinery||e.Id==NavyEffectId.Income)),Is.False);
        }

        [UnityTest]
        public IEnumerator BaseDestructionStopsTheGameAndRestartResetsTheWholeSession()
        {
            Onboard();SimpleWaves(4);game.Session.Resources.AddExp(400);game.Session.Resources.AddUpgradePoints(5);construction.TryGlobalUpgrade(Entry(TowerKind.Turret),out _);
            Assert.That(game.StartNextWave(),Is.True);game.Base.SharedBase.Health.Initialize(120,25);
            var attacker=game.Waves.ActiveEnemies.Single();attacker.transform.position=new Vector3(0,0,-30);
            void AttackCycle()
            {
                attacker.AdvanceAssault(attacker.AttackCycleInterval+.01f);
                for(int shot=1;shot<attacker.Runtime.Stats.ShotsPerAttack;shot++)attacker.AdvanceAssault(attacker.BurstShotInterval+.001f);
                foreach(var shot in Object.FindObjectsByType<NavyProjectile>(FindObjectsSortMode.None).Where(p=>p.Owner==null))shot.Advance(2);
            }
            for(int i=0;i<3;i++)AttackCycle();
            Assert.That(game.View.HealthWave.color.r,Is.GreaterThan(.8f));Assert.That(game.Panel.StatusText.text,Does.Contain("critical"));
            AttackCycle();
            Assert.That(game.State,Is.EqualTo(NavyGameState.GameOver));Assert.That(game.Waves.Running,Is.False);Assert.That(game.Waves.ActiveCount,Is.Zero);
            Assert.That(game.StartNextWave(),Is.False);int money=game.Session.Resources.Money;Assert.That(construction.TryBuild(Entry(TowerKind.Foundation),Cell(4,-40),Quaternion.identity,out _),Is.False);Assert.That(game.Session.Resources.Money,Is.EqualTo(money));
            Assert.That(game.Panel.Tutorials.Current.Title.text,Does.Contain("GAME OVER"));game.Restart();yield return null;
            float until=Time.realtimeSinceStartup+10;while((Object.FindAnyObjectByType<PlayableNavyGame>()==game||Object.FindAnyObjectByType<PlayableNavyGame>()==null)&&Time.realtimeSinceStartup<until)yield return null;
            game=Object.FindAnyObjectByType<PlayableNavyGame>();yield return null;
            Assert.That(game.State,Is.EqualTo(NavyGameState.FactionSelection));Assert.That(game.Waves.CurrentWave,Is.Zero);Assert.That(game.Session.Resources.Money,Is.EqualTo(1000));Assert.That(game.Session.Resources.Exp,Is.Zero);Assert.That(game.Session.Resources.UpgradePoints,Is.Zero);
            Assert.That(game.Construction.Grid.Cells.Any(c=>c.Occupied),Is.False);Assert.That(game.Session.GlobalTier(Entry(TowerKind.Turret)),Is.Zero);Assert.That(game.Base.SharedBase.Health.CurrentHealth,Is.EqualTo(game.Base.SharedBase.MaxHealth));Assert.That(game.Regions.IsUnlocked(BattlefieldRegion.Ocean),Is.False);
        }
    }
}
#endif
