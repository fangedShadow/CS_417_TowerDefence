#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TowerDefence.Navy;
using TowerDefence.Shared;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TowerDefence.Tests
{
    public sealed class NavyPrefabTests
    {
        private const string Root = "Assets/Prefabs/Navy/";
        private readonly List<Object> spawned = new List<Object>();
        private static readonly Vector3 Origin = new Vector3(10000, 0, 10000);
        private GameObject Spawn(string relative, Vector3 offset = default)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + relative + ".prefab");
            Assert.That(prefab, Is.Not.Null, relative);
            var instance = Object.Instantiate(prefab, Origin + offset, Quaternion.identity);
            spawned.Add(instance);
            return instance;
        }
        private EnemyRuntime Enemy(string name = "Gunboat", Vector3 offset = default) =>
            Spawn("Enemies/" + name, offset).GetComponent<EnemyRuntime>();
        private TowerRuntime Tower(string relative, Vector3 offset = default) =>
            Spawn("Towers/" + relative, offset).GetComponent<TowerRuntime>();
        private NavyFoundation Foundation(string name, Vector3 offset = default) =>
            Spawn("Foundations/Foundation_" + name, offset).GetComponent<NavyFoundation>();

        [TearDown]
        public void Cleanup()
        {
            foreach (var projectile in Object.FindObjectsByType<NavyProjectile>())
                if (projectile != null) Object.DestroyImmediate(projectile.gameObject);
            foreach (var obj in spawned.AsEnumerable().Reverse())
                if (obj != null) Object.DestroyImmediate(obj);
            spawned.Clear();
        }

        [Test]
        public void AllRequiredAssetsHaveValidScriptsStatsAndReferences()
        {
            var paths = Directory.GetFiles(Root, "*.prefab", SearchOption.AllDirectories)
                .Where(p => !p.EndsWith("NavyRoot.prefab")).ToArray();
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
        }

        [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void EveryTurretBarrelFiresAndCooldownPreventsExtraSalvos(int level)
        {
            var tower = Tower("Attack/Turret/Turret_L" + level);
            var enemy = Enemy(offset: Vector3.forward * 5);
            var weapon = tower.GetComponent<NavyWeapon>();
            Assert.That(weapon.Muzzles.Length, Is.EqualTo(level));
            Assert.That(weapon.TryFire(enemy), Is.True);
            Assert.That(weapon.ShotsFired, Is.EqualTo(level));
            Assert.That(weapon.TryFire(enemy), Is.False);
            tower.AdvanceCooldowns(2);
            Assert.That(weapon.TryFire(enemy), Is.True);
            Assert.That(weapon.ShotsFired, Is.EqualTo(level * 2));
        }

        [Test]
        public void TargetingFiltersDomainsAndWeaponRejectsOutOfRangeTargets()
        {
            var tower = Tower("Attack/Turret/Turret_L1");
            var near = Enemy(offset: Vector3.forward * 4);
            var far = Enemy(offset: Vector3.forward * 30);
            Physics.SyncTransforms();
            Assert.That(NavyTargeting.Nearest(Origin, 8, TargetDomain.Water, ~0), Is.SameAs(near));
            Assert.That(NavyTargeting.Nearest(Origin, 8, TargetDomain.Air, ~0), Is.Null);
            Assert.That(tower.GetComponent<NavyWeapon>().TryFire(far), Is.False);
        }

        [Test]
        public void DirectRoundHitsOnlyOnceWithDuplicateEnemyColliders()
        {
            var tower = Tower("Attack/Turret/Turret_L1");
            var enemy = Enemy("PatrolBoat", Vector3.forward * 3);
            enemy.gameObject.AddComponent<BoxCollider>();
            var round = Object.Instantiate(tower.GetComponent<NavyWeapon>().ProjectilePrefab,
                Origin + Vector3.up * 0.5f, Quaternion.identity);
            Physics.SyncTransforms();
            round.Launch(tower, Vector3.forward);
            round.Advance(0.5f);
            Assert.That(enemy.CurrentHealth, Is.EqualTo(50).Within(0.01f));
            round.Advance(1);
            Assert.That(enemy.CurrentHealth, Is.EqualTo(50).Within(0.01f));
        }

        [Test]
        public void HeavyCannonHasLargerProjectilesAndDealsSplashDamage()
        {
            var tower = Tower("Attack/Cannon/Cannon_L2");
            var a = Enemy("Gunboat", Vector3.forward * 3);
            var b = Enemy("Gunboat", new Vector3(0.8f, 0, 3));
            var projectile = Object.Instantiate(tower.GetComponent<NavyWeapon>().ProjectilePrefab,
                Origin + Vector3.up * 0.5f, Quaternion.identity);
            Physics.SyncTransforms();
            projectile.Launch(tower, Vector3.forward);
            Assert.That(projectile.transform.localScale.x, Is.EqualTo(2));
            projectile.Advance(0.5f);
            Assert.That(a.CurrentHealth, Is.LessThan(a.Stats.MaxHealth));
            Assert.That(b.CurrentHealth, Is.LessThan(b.Stats.MaxHealth));
        }

        [Test]
        public void EnergyWavePiercesSeveralEnemiesButDoesNotHitThemTwice()
        {
            var tower = Tower("Attack/Cannon/Cannon_L3");
            var enemies = new[] { Enemy(offset: Vector3.forward * 3),
                Enemy(offset: Vector3.forward * 6), Enemy(offset: Vector3.forward * 9) };
            var wave = Object.Instantiate(tower.GetComponent<NavyWeapon>().ProjectilePrefab,
                Origin + Vector3.up * 0.5f, Quaternion.identity);
            Physics.SyncTransforms();
            wave.Launch(tower, Vector3.forward);
            wave.Advance(0.7f);
            foreach (var enemy in enemies) Assert.That(enemy.CurrentHealth, Is.EqualTo(40).Within(0.01f));
            wave.Advance(0.1f);
            foreach (var enemy in enemies) Assert.That(enemy.CurrentHealth, Is.EqualTo(40).Within(0.01f));
        }

        [Test]
        public void UpgradeVisualsAndProjectileTypesAreDistinct()
        {
            for (int level = 1; level <= 3; level++)
            {
                var slow = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Towers/Defense/SlowTower_L" + level + ".prefab");
                Assert.That(slow.GetComponentsInChildren<Transform>().Count(t => t.name.StartsWith("EmitterRing_")),
                    Is.EqualTo(level));
                var cannon = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Towers/Attack/Cannon/Cannon_L" + level + ".prefab");
                Assert.That(cannon.GetComponentsInChildren<Transform>().Count(t => t.name.StartsWith("LevelMarker_")),
                    Is.EqualTo(level));
                Assert.That(cannon.GetComponent<NavyWeapon>().ProjectilePrefab.Kind, Is.EqualTo(level == 1 ?
                    NavyProjectileKind.Cannonball : level == 2 ? NavyProjectileKind.HeavyCannonball : NavyProjectileKind.Wave));
            }
        }

        [Test]
        public void HealthAndBuffChangesDoNotMutateDefinitionOrAnotherInstance()
        {
            var a = Tower("Attack/Turret/Turret_L1");
            var b = Tower("Attack/Turret/Turret_L1", Vector3.right * 3);
            float maximum = a.Stats.MaxHealth;
            float baseRange = a.Stats.AttackRange;
            a.Health.TakeDamage(new DamageInfo(10));
            a.SetBuff(a, new TowerBuff(range: 0.5f));
            Assert.That(a.CurrentHealth, Is.EqualTo(maximum - 10));
            Assert.That(b.CurrentHealth, Is.EqualTo(maximum));
            Assert.That(a.Stats.MaxHealth, Is.EqualTo(maximum));
            Assert.That(b.AttackRange, Is.EqualTo(baseRange));
            Assert.That(a.Stats.AttackRange, Is.EqualTo(baseRange));
        }

        [Test]
        public void SlowUsesStrongestSourceAndRestoresBaseSpeedAfterExpiry()
        {
            var enemy = Enemy();
            var other = new GameObject("OtherSource"); spawned.Add(other);
            float speed = enemy.Stats.MovementSpeed;
            enemy.Effects.ApplySlow(enemy, 0.2f, 2);
            enemy.Effects.ApplySlow(other, 0.5f, 1);
            Assert.That(enemy.MovementSpeed, Is.EqualTo(speed * 0.5f).Within(0.001f));
            enemy.Effects.Advance(1.1f);
            Assert.That(enemy.MovementSpeed, Is.EqualTo(speed * 0.8f).Within(0.001f));
            enemy.Effects.Advance(1);
            Assert.That(enemy.MovementSpeed, Is.EqualTo(speed));
            Assert.That(enemy.Stats.MovementSpeed, Is.EqualTo(speed));
        }

        [Test]
        public void RefreshingPoisonDoesNotStarveTicksOrMultiplyStackedDamage()
        {
            var enemy = Enemy();
            var other = new GameObject("OtherSource"); spawned.Add(other);
            for (int i = 0; i < 8; i++)
            {
                enemy.Effects.ApplyPoison(enemy, 3, 3, 1);
                enemy.Effects.ApplyPoison(other, 3, 3, 1);
                enemy.Effects.Advance(0.25f);
            }
            Assert.That(enemy.CurrentHealth, Is.EqualTo(134).Within(0.001f));
            enemy.Effects.Advance(4);
            Assert.That(enemy.Effects.PoisonRemaining, Is.Zero);
        }

        [TestCase(1, 0.2f)] [TestCase(2, 0.35f)] [TestCase(3, 0.5f)]
        public void DefenseLevelsApplyTheirConfiguredSlowAndOnlyLevelThreePoisons(int level, float strength)
        {
            var tower = Tower("Defense/SlowTower_L" + level);
            var enemy = Enemy(offset: Vector3.forward * 3);
            Physics.SyncTransforms();
            var weapon = tower.GetComponent<NavyWeapon>();
            Assert.That(weapon.ProjectilePrefab.Kind, Is.EqualTo(level == 3 ? NavyProjectileKind.ToxicBolt : NavyProjectileKind.SlowBolt));
            Assert.That(tower.GetComponent<NavySlowTower>().TryFire(enemy), Is.True);
            Assert.That(enemy.MovementSpeed, Is.EqualTo(enemy.Stats.MovementSpeed));
            Assert.That(enemy.CurrentHealth, Is.EqualTo(140));
            var bolt = Object.FindObjectsByType<NavyProjectile>().Single(p => p.IsLaunched);
            bolt.Advance(0.3f);
            Assert.That(enemy.MovementSpeed, Is.EqualTo(enemy.Stats.MovementSpeed * (1 - strength)).Within(0.001f));
            float afterImpact = 140 - tower.Stats.Damage;
            Assert.That(enemy.CurrentHealth, Is.EqualTo(afterImpact).Within(0.001f));
            enemy.Effects.Advance(1);
            Assert.That(enemy.CurrentHealth, Is.EqualTo(afterImpact - (level == 3 ? 3 : 0)).Within(0.001f));
            Assert.That(tower.Stats.Damage, Is.EqualTo(2 + 2 * level));
        }

        [Test]
        public void SlowBoltsAffectOnlyHitEnemiesAndCannotDamageTwice()
        {
            var tower = Tower("Defense/SlowTower_L1");
            var hit = Enemy(offset: Vector3.forward * 4);
            var nearby = Enemy(offset: new Vector3(3, 0, 4));
            var weapon = tower.GetComponent<NavyWeapon>();
            Physics.SyncTransforms();
            Assert.That(weapon.TryFire(hit), Is.True);
            var bolt = Object.FindObjectsByType<NavyProjectile>().Single(p => p.IsLaunched);
            bolt.Advance(0.3f);
            Assert.That(hit.CurrentHealth, Is.EqualTo(136).Within(0.001f));
            Assert.That(hit.Effects.SlowRemaining, Is.GreaterThan(0));
            Assert.That(nearby.CurrentHealth, Is.EqualTo(140));
            Assert.That(nearby.MovementSpeed, Is.EqualTo(nearby.Stats.MovementSpeed));
            bolt.Advance(1);
            Assert.That(hit.CurrentHealth, Is.EqualTo(136).Within(0.001f));
        }

        [Test]
        public void MissedToxicBoltDoesNotApplySlowPoisonOrDamage()
        {
            var tower = Tower("Defense/SlowTower_L3");
            var enemy = Enemy(offset: Vector3.forward * 4);
            var weapon = tower.GetComponent<NavyWeapon>();
            Physics.SyncTransforms();
            Assert.That(weapon.TryFire(enemy), Is.True);
            var bolt = Object.FindObjectsByType<NavyProjectile>().Single(p => p.IsLaunched);
            enemy.transform.position += Vector3.right * 4;
            Physics.SyncTransforms();
            bolt.Advance(1);
            Assert.That(enemy.CurrentHealth, Is.EqualTo(140));
            Assert.That(enemy.Effects.SlowRemaining, Is.Zero);
            Assert.That(enemy.Effects.PoisonRemaining, Is.Zero);
        }

        [Test]
        public void RepeatedToxicHitsRefreshPoisonWithoutStarvingTicks()
        {
            var tower = Tower("Defense/SlowTower_L3");
            var enemy = Enemy(offset: Vector3.forward * 4);
            var weapon = tower.GetComponent<NavyWeapon>();
            Physics.SyncTransforms();
            for (int i = 0; i < 4; i++)
            {
                Assert.That(weapon.TryFire(enemy), Is.True);
                var bolt = Object.FindObjectsByType<NavyProjectile>().Single(p => p.IsLaunched);
                bolt.Advance(0.3f);
                enemy.Effects.Advance(0.5f);
                tower.AdvanceCooldowns(0.5f);
            }
            Assert.That(enemy.CurrentHealth, Is.EqualTo(140 - 4 * 8 - 2 * 3).Within(0.001f));
        }

        [Test]
        public void SupportBuffMakesSlowShooterFireMoreOften()
        {
            var support = Tower("Support/NavySupportTower", Vector3.left * 2).GetComponent<NavySupportTower>();
            var tower = Tower("Defense/SlowTower_L1");
            var enemy = Enemy(offset: Vector3.forward * 4);
            Physics.SyncTransforms(); support.RefreshBuffs();
            var weapon = tower.GetComponent<NavyWeapon>();
            Assert.That(weapon.TryFire(enemy), Is.True);
            Assert.That(tower.FireCooldown, Is.EqualTo(tower.Stats.AttackInterval / 1.25f).Within(0.001f));
            tower.AdvanceCooldowns(tower.Stats.AttackInterval / 1.25f);
            Assert.That(weapon.TryFire(enemy), Is.True);
        }

        [UnityTest]
        public IEnumerator SlowShooterAutomaticallyTargetsAndDamagesAnEnemy()
        {
            var tower = Tower("Defense/SlowTower_L3");
            var enemy = Enemy("Battleship", Vector3.forward * 5);
            float health = enemy.CurrentHealth;
            Physics.SyncTransforms();
            yield return new WaitForSeconds(0.8f);
            Assert.That(tower.GetComponent<NavyWeapon>().ShotsFired, Is.GreaterThan(0));
            Assert.That(enemy.CurrentHealth, Is.LessThan(health));
            Assert.That(enemy.MovementSpeed, Is.LessThan(enemy.Stats.MovementSpeed));
            Assert.That(enemy.Effects.PoisonRemaining, Is.GreaterThan(0));
        }

        [Test]
        public void SupportBuffsEachTowerTypeAndOverlapsDoNotStack()
        {
            var support = Tower("Support/NavySupportTower").GetComponent<NavySupportTower>();
            var second = Tower("Support/NavySupportTower", Vector3.left).GetComponent<NavySupportTower>();
            var cannon = Tower("Attack/Cannon/Cannon_L1", Vector3.right * 2);
            var turret = Tower("Attack/Turret/Turret_L1", Vector3.forward * 2);
            var slow = Tower("Defense/SlowTower_L1", Vector3.back * 2);
            Physics.SyncTransforms();
            support.RefreshBuffs(); second.RefreshBuffs();
            Assert.That(cannon.Damage, Is.EqualTo(50));
            Assert.That(turret.AttackRange, Is.EqualTo(10));
            Assert.That(slow.AttackInterval, Is.EqualTo(slow.Stats.AttackInterval / 1.25f).Within(0.001f));
            support.enabled = false;
            Assert.That(cannon.Damage, Is.EqualTo(50));
            second.enabled = false;
            Assert.That(cannon.Damage, Is.EqualTo(40));
            Assert.That(turret.AttackRange, Is.EqualTo(8));
            Assert.That(slow.AttackInterval, Is.EqualTo(slow.Stats.AttackInterval));
        }

        [Test]
        public void SupportRemovesBuffWhenTowerLeavesRangeAndExcludesOtherFactions()
        {
            var support = Tower("Support/NavySupportTower").GetComponent<NavySupportTower>();
            var cannon = Tower("Attack/Cannon/Cannon_L1", Vector3.right * 2);
            Physics.SyncTransforms(); support.RefreshBuffs();
            Assert.That(cannon.Damage, Is.EqualTo(50));
            cannon.transform.position += Vector3.right * 30;
            Physics.SyncTransforms(); support.RefreshBuffs();
            Assert.That(cannon.Damage, Is.EqualTo(40));
            var definition = Object.Instantiate(cannon.Stats); spawned.Add(definition);
            definition.Faction = Faction.Army;
            var serialized = new SerializedObject(cannon);
            serialized.FindProperty("stats").objectReferenceValue = definition;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            cannon.transform.position = Origin + Vector3.right * 2;
            Physics.SyncTransforms(); support.RefreshBuffs();
            Assert.That(cannon.Damage, Is.EqualTo(40));
        }

        [Test]
        public void OilUsesBoundSharedWalletAndDoesNotGenerateUnboundMoney()
        {
            var oil = Tower("Buildings/OilExtractor").GetComponent<OilExtractor>();
            oil.AdvanceProduction(100);
            Assert.That(oil.ProductionRemaining, Is.EqualTo(10));
            var wallet = new PlayerResources(100, 2, 3);
            oil.BindResources(wallet);
            oil.AdvanceProduction(9);
            Assert.That(wallet.Money, Is.EqualTo(100));
            oil.AdvanceProduction(1);
            Assert.That(wallet.Money, Is.EqualTo(125));
            oil.AdvanceProduction(20);
            Assert.That(wallet.Money, Is.EqualTo(175));
            Assert.That(wallet.Exp, Is.EqualTo(2));
            Assert.That(wallet.UpgradePoints, Is.EqualTo(3));
        }

        [TestCase(1, 0, 0)]
        [TestCase(2, 5, 0)]
        [TestCase(3, 5, 1)]
        public void OilUpgradeLevelsProduceIndependentResourcesAndPreserveDefinitions(int level, int exp, int points)
        {
            string name = level == 1 ? "OilExtractor" : "OilExtractor_L" + level;
            var tower = Tower("Buildings/" + name);
            var oil = tower.GetComponent<OilExtractor>();
            var wallet = new PlayerResources(100, 10, 3);
            Assert.That(tower.Stats.Faction, Is.EqualTo(Faction.Navy));
            Assert.That(tower.Stats.UpgradeLevel, Is.EqualTo(level));
            Assert.That(tower.Stats.ResourceExpGenerationAmount, Is.EqualTo(exp));
            Assert.That(tower.Stats.ResourceUpgradePointGenerationAmount, Is.EqualTo(points));
            Assert.That(tower.Upgrade.HasNextLevel, Is.EqualTo(level < 3));
            Assert.That(tower.transform.Find("Visuals/ExpCollector") != null, Is.EqualTo(level >= 2));
            Assert.That(tower.transform.Find("Visuals/UpgradePointProcessor") != null, Is.EqualTo(level == 3));
            oil.AdvanceProduction(100);
            Assert.That(oil.ProductionRemaining, Is.EqualTo(10));
            oil.BindResources(wallet);
            oil.AdvanceProduction(9);
            Assert.That(wallet.Money, Is.EqualTo(100));
            Assert.That(wallet.Exp, Is.EqualTo(10));
            Assert.That(wallet.UpgradePoints, Is.EqualTo(3));
            oil.AdvanceProduction(1);
            oil.AdvanceProduction(20);
            Assert.That(wallet.Money, Is.EqualTo(175));
            Assert.That(wallet.Exp, Is.EqualTo(10 + exp * 3));
            Assert.That(wallet.UpgradePoints, Is.EqualTo(3 + points * 3));
            Assert.That(tower.Stats.ResourceGenerationAmount, Is.EqualTo(25));
            Assert.That(tower.Stats.ResourceExpGenerationAmount, Is.EqualTo(exp));
            Assert.That(tower.Stats.ResourceUpgradePointGenerationAmount, Is.EqualTo(points));
        }

        [Test]
        public void DisabledOrDestroyedOilExtractorCannotProduceAnyResource()
        {
            var tower = Tower("Buildings/OilExtractor_L3");
            var oil = tower.GetComponent<OilExtractor>();
            var wallet = new PlayerResources();
            oil.BindResources(wallet);
            oil.enabled = false;
            oil.AdvanceProduction(20);
            Assert.That(wallet.Money + wallet.Exp + wallet.UpgradePoints, Is.Zero);
            oil.enabled = true;
            tower.Health.TakeDamage(new DamageInfo(10000));
            oil.AdvanceProduction(20);
            Assert.That(wallet.Money + wallet.Exp + wallet.UpgradePoints, Is.Zero);
        }

        [Test]
        public void ResourcesAreIndependentAndInvalidSpendingCannotCreateCurrency()
        {
            var config = AssetDatabase.LoadAssetAtPath<PlayerResourceConfig>(
                "Assets/Shared/Data/Resources/PlayerResources.asset");
            var a = config.CreateRuntime(); var b = config.CreateRuntime();
            a.AddExp(10); a.AddUpgradePoints(3);
            Assert.That(a.SpendMoney(100), Is.True);
            Assert.That(a.SpendMoney(151), Is.False);
            Assert.That(a.SpendUpgradePoints(2), Is.True);
            Assert.That(a.SpendUpgradePoints(2), Is.False);
            Assert.That(a.Money, Is.EqualTo(150)); Assert.That(a.Exp, Is.EqualTo(10));
            Assert.That(a.UpgradePoints, Is.EqualTo(1)); Assert.That(b.Money, Is.EqualTo(250));
            Assert.Throws<ArgumentOutOfRangeException>(() => a.SpendMoney(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => a.AddExp(-1));
            a.AddMoney(int.MaxValue);
            Assert.That(a.Money, Is.EqualTo(int.MaxValue));
        }

        [Test]
        public void BattleshipHasConfigurableArmorAndStatusResistances()
        {
            var enemy = Enemy("Battleship");
            enemy.TakeDamage(new DamageInfo(100));
            Assert.That(enemy.CurrentHealth, Is.EqualTo(700 - 100f / 1.8f * 0.8f).Within(0.01f));
            enemy.Effects.ApplySlow(enemy, 0.5f, 4);
            Assert.That(enemy.MovementSpeed, Is.EqualTo(0.6f).Within(0.001f));
            Assert.That(enemy.Effects.SlowRemaining, Is.EqualTo(3));
        }

        [UnityTest]
        public IEnumerator WeaponAutomaticallyFindsAndDamagesEnemyDuringPlay()
        {
            var tower = Tower("Attack/Turret/Turret_L3");
            var enemy = Enemy("Battleship", Vector3.forward * 5);
            float initial = enemy.CurrentHealth;
            Physics.SyncTransforms();
            yield return new WaitForSeconds(1.2f);
            Assert.That(tower.GetComponent<NavyWeapon>().ShotsFired, Is.GreaterThanOrEqualTo(3));
            Assert.That(enemy.CurrentHealth, Is.LessThan(initial));
        }

        [Test]
        public void ReferenceInspiredModelsKeepGameplayPivotsAndUseActualMeshAssets()
        {
            foreach (string path in Directory.GetFiles(Root + "Towers", "*.prefab", SearchOption.AllDirectories))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var meshes = prefab.GetComponentsInChildren<MeshFilter>();
                Assert.That(meshes.Length, Is.GreaterThan(15), path);
                int triangles = 0;
                foreach (var filter in meshes)
                {
                    Assert.That(filter.sharedMesh, Is.Not.Null);
                    Assert.That(AssetDatabase.GetAssetPath(filter.sharedMesh), Does.StartWith(Root + "Models/"));
                    triangles += filter.sharedMesh.triangles.Length / 3;
                }
                Assert.That(triangles, Is.LessThan(20000), path);
                if (prefab.TryGetComponent<NavyWeapon>(out var weapon))
                {
                    Assert.That(prefab.transform.Find("MountPoint"), Is.SameAs(weapon.MountPoint));
                    foreach (var muzzle in weapon.Muzzles)
                    {
                        Assert.That(muzzle.IsChildOf(prefab.transform.Find("Visuals/YawPivot")), Is.True);
                        Assert.That(muzzle.localPosition.z, Is.GreaterThan(1.6f));
                    }
                }
            }
        }

        [Test]
        public void CenterBaseIsSharedObjectiveWithEditableHealthAndIntegrationPoints()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Shared/CenterBase.prefab");
            Assert.That(prefab, Is.Not.Null);
            var obj = Object.Instantiate(prefab, Origin, Quaternion.identity);
            spawned.Add(obj);
            var center = obj.GetComponent<CenterBase>();
            Assert.That(center, Is.Not.Null);
            Assert.That(obj.GetComponent<TowerRuntime>(), Is.Null, "The common base must not be a Navy tower.");
            Assert.That(center.Health.CurrentHealth, Is.EqualTo(center.MaxHealth));
            center.Health.TakeDamage(new DamageInfo(125));
            Assert.That(center.Health.CurrentHealth, Is.EqualTo(center.MaxHealth - 100).Within(0.01f));
            foreach (string point in new[] { "EnemyArrival", "ObjectiveAim", "ArmySide", "NavySide", "SkySide" })
                Assert.That(obj.transform.Find("IntegrationPoints/" + point), Is.Not.Null, point);
        }

        [Test]
        public void StatDefinitionsExceed21DistinctNumericStatsAndExposeExactlyThreeResources()
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

        [TestCase("PatrolBoat")] [TestCase("Gunboat")] [TestCase("Destroyer")]
        [TestCase("Battleship")] [TestCase("FastAssaultBoat")]
        public void CleanEnemyModelsHaveCustomHullMeshesAndKeepTheirRuntimeWiring(string name)
        {
            var enemy = Enemy(name);
            var visuals = enemy.transform.Find("Visuals");
            Assert.That(visuals, Is.Not.Null);
            Assert.That(enemy.AimTransform, Is.SameAs(enemy.transform.Find("AimPoint")));
            Assert.That(enemy.GetComponent<Rigidbody>().isKinematic, Is.True);
            Assert.That(enemy.GetComponent<Collider>(), Is.Not.Null);
            Assert.That(enemy.GetComponent<NavalEnemy>(), Is.Not.Null);
            Assert.That(enemy.CurrentHealth, Is.EqualTo(enemy.Stats.MaxHealth));
            var filters = visuals.GetComponentsInChildren<MeshFilter>();
            Assert.That(filters.Length, Is.GreaterThan(15));
            int triangles = 0;
            foreach (var filter in filters)
            {
                Assert.That(AssetDatabase.GetAssetPath(filter.sharedMesh), Does.StartWith(Root + "Models/"));
                Assert.That(filter.GetComponent<Renderer>().sharedMaterial, Is.Not.Null);
                triangles += filter.sharedMesh.triangles.Length / 3;
            }
            Assert.That(triangles, Is.LessThan(12000), name);
            if (name == "FastAssaultBoat")
            {
                Assert.That(visuals.Find("PortHull"), Is.Not.Null);
                Assert.That(visuals.Find("StarboardHull"), Is.Not.Null);
                Assert.That(visuals.Find("LowCockpit"), Is.Not.Null);
            }
            else Assert.That(visuals.Find("MainHull"), Is.Not.Null);
            if (name == "Battleship")
                Assert.That(visuals.GetComponentsInChildren<Transform>().Count(t => t.name == "ArmorBeltPlate"), Is.EqualTo(8));
        }

        [TestCase("Normal", 0.8f, 1f)]
        [TestCase("Medium", 1.8f, 1.25f)]
        [TestCase("High", 3.2f, 1.75f)]
        public void FoundationsMountAtTheirHeightAndApplyExactRange(string name, float height, float multiplier)
        {
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

        [Test]
        public void FoundationRangeAndSupportComposeWithoutPermanentChanges()
        {
            var foundation = Foundation("High");
            var tower = Tower("Attack/Turret/Turret_L1");
            var support = Tower("Support/NavySupportTower", Vector3.left * 3).GetComponent<NavySupportTower>();
            foundation.TryMount(tower);
            Physics.SyncTransforms(); support.RefreshBuffs();
            Assert.That(tower.AttackRange, Is.EqualTo(8 * 1.75f * 1.25f).Within(0.001f));
            support.enabled = false;
            Assert.That(tower.AttackRange, Is.EqualTo(14));
            foundation.enabled = false;
            Assert.That(tower.AttackRange, Is.EqualTo(8));
            foundation.enabled = true;
            Assert.That(tower.AttackRange, Is.EqualTo(14));
            foundation.Unmount(tower);
            Assert.That(tower.AttackRange, Is.EqualTo(8));
            Assert.That(tower.Stats.AttackRange, Is.EqualTo(8));
        }

        [Test]
        public void FoundationTransfersUseOnlyTheCurrentParentAndOneTowerPerMount()
        {
            var high = Foundation("High");
            var medium = Foundation("Medium", Vector3.right * 10);
            var tower = Tower("Attack/Turret/Turret_L1");
            var second = Tower("Attack/Turret/Turret_L1", Vector3.back * 4);
            Assert.That(high.TryMount(tower), Is.True);
            Assert.That(high.TryMount(second), Is.False);
            Assert.That(medium.TryMount(tower), Is.True);
            Assert.That(tower.AttackRange, Is.EqualTo(10));
            Assert.That(high.TryMount(second), Is.True);
            Assert.That(second.AttackRange, Is.EqualTo(14));
            tower.transform.SetParent(null, true);
            Assert.That(tower.AttackRange, Is.EqualTo(8));
        }

        [Test]
        public void DirectMountParentingWorksAndNestedFoundationsDoNotMultiply()
        {
            var high = Foundation("High");
            var medium = Foundation("Medium");
            medium.transform.SetParent(high.TowerMount, true);
            var tower = Tower("Defense/SlowTower_L1");
            tower.transform.SetParent(medium.TowerMount, false);
            Assert.That(tower.AttackRange, Is.EqualTo(tower.Stats.AttackRange * 1.25f).Within(0.001f));
            Object.DestroyImmediate(medium.GetComponent<NavyFoundation>());
            Assert.That(tower.AttackRange, Is.EqualTo(tower.Stats.AttackRange * 1.75f).Within(0.001f));
            tower.transform.SetParent(null, true);
            Assert.That(tower.AttackRange, Is.EqualTo(tower.Stats.AttackRange));
        }

        [TestCase(Faction.Army)] [TestCase(Faction.Sky)]
        public void NavyFoundationRejectsOtherFactionsWithoutEditingTheirAssets(Faction faction)
        {
            var foundation = Foundation("High");
            var tower = Tower("Attack/Turret/Turret_L1");
            var stats = Object.Instantiate(tower.Stats); spawned.Add(stats);
            stats.Faction = faction;
            var serialized = new SerializedObject(tower);
            serialized.FindProperty("stats").objectReferenceValue = stats;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(foundation.TryMount(tower), Is.False);
            tower.transform.SetParent(foundation.TowerMount, false);
            Assert.That(tower.AttackRange, Is.EqualTo(stats.AttackRange));
        }

        [Test]
        public void ElevatedSupportTowerUsesItsExpandedRadius()
        {
            var foundation = Foundation("High");
            var support = Tower("Support/NavySupportTower");
            var target = Tower("Attack/Turret/Turret_L1", Vector3.right * 12);
            var component = support.GetComponent<NavySupportTower>();
            Physics.SyncTransforms(); component.RefreshBuffs();
            Assert.That(target.AttackRange, Is.EqualTo(8));
            foundation.TryMount(support);
            Physics.SyncTransforms(); component.RefreshBuffs();
            Assert.That(support.SupportRange, Is.EqualTo(14));
            Assert.That(target.AttackRange, Is.EqualTo(10));
        }

        [Test]
        public void ElevatedWaveCannonCanDamageEnemiesBeyondItsUnboostedTravelRange()
        {
            var foundation = Foundation("High");
            var cannon = Tower("Attack/Cannon/Cannon_L3");
            var enemy = Enemy(offset: Vector3.forward * 26);
            foundation.TryMount(cannon);
            Physics.SyncTransforms();
            Assert.That(cannon.AttackRange, Is.EqualTo(31.5f));
            Assert.That(cannon.WaveTravelRange, Is.EqualTo(31.5f));
            Assert.That(cannon.GetComponent<NavyWeapon>().TryFire(enemy), Is.True);
            var wave = Object.FindObjectsByType<NavyProjectile>().Single(p => p.IsLaunched);
            wave.Advance(3);
            Assert.That(enemy.CurrentHealth, Is.EqualTo(40).Within(0.01f));
            Assert.That(cannon.Stats.WaveRange, Is.EqualTo(18));
        }

        [Test]
        public void NavalMovementIsDrivenExternallyAndUsesTemporarySpeed()
        {
            var enemy = Enemy("PatrolBoat");
            enemy.Effects.ApplySlow(enemy, 0.5f, 2);
            enemy.GetComponent<NavalEnemy>().MoveTowards(Origin + Vector3.forward * 10, 1);
            Assert.That(enemy.transform.position.z, Is.EqualTo(Origin.z + 2).Within(0.01f));
            Assert.That(enemy.transform.position.y, Is.EqualTo(Origin.y));
        }
    }
}
#endif
