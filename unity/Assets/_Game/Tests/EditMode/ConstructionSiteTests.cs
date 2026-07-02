using System.Collections.Generic;
using Abbey.Buildings;
using Abbey.Core;
using Abbey.Economy;
using Abbey.Light;
using NUnit.Framework;
using UnityEngine;

namespace Abbey.Tests.EditMode
{
    /// <summary>
    /// Construction site lifecycle: delivery accounting against the ledger
    /// (partial deliveries, clamping, exact decrements), work gated behind
    /// complete materials, and completion swapping in the right function
    /// component with values from the injected/loaded configs.
    /// </summary>
    public class ConstructionSiteTests
    {
        EconomyConfig _economyConfig;
        BuildingCatalog _catalog;

        [SetUp]
        public void SetUp()
        {
            ClearStatics();

            _economyConfig = ScriptableObject.CreateInstance<EconomyConfig>();
            _economyConfig.baseStorageCapacity = 999;
            _economyConfig.storagePileCapacity = 7;
            ResourceLedger.Config = _economyConfig;

            // Coded default catalog (the shipped buildable set), independent of
            // any Resources asset. Individual tests may swap in a custom catalog.
            _catalog = ScriptableObject.CreateInstance<BuildingCatalog>();
            BuildingPlacer.Catalog = _catalog;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var site in Object.FindObjectsByType<ConstructionSite>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(site.gameObject);
            }
            foreach (var building in Object.FindObjectsByType<Building>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(building.gameObject);
            }
            Object.DestroyImmediate(_catalog);
            Object.DestroyImmediate(_economyConfig);
            ClearStatics();
        }

        static void ClearStatics()
        {
            ConstructionSite.ClearRegistry();
            Building.ClearRegistry();
            BuildingPlacer.Clear();
            BuildingCatalog.ClearCache();
            DarknessEvaluator.Clear();
            ResourceLedger.Clear();
            EconomyConfig.ClearCache();
            PrototypeConfig.ClearCache();
            GameEventLog.Clear();
        }

        static bool LogContains(string type, string dataFragment)
        {
            var records = GameEventLog.Records;
            for (int i = 0; i < records.Count; i++)
            {
                if (records[i].Type == type && records[i].Data.Contains(dataFragment))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Constructs a site directly (AddComponent + Initialize, bypassing the
        /// placer and its affordability planning gate — placement is
        /// BuildingPlacerTests' concern) for a test hut costing wood x5 + oil x2
        /// with 4s of work. TearDown finds and destroys the GameObject.
        /// </summary>
        ConstructionSite CreateTestHutSite()
        {
            var type = new BuildingType
            {
                id = "hut",
                displayName = "Test Hut",
                footprint = new Vector2(2f, 2f),
                cost = new List<ResourceStack>
                {
                    new ResourceStack(ResourceType.Wood, 5),
                    new ResourceStack(ResourceType.Oil, 2),
                },
                buildWorkSeconds = 4f,
                function = FunctionKind.Shelter,
            };
            var go = new GameObject("ConstructionSite_hut");
            var site = go.AddComponent<ConstructionSite>();
            site.Initialize(type);
            return site;
        }

        /// <summary>Funds, places, fully delivers and works a default-catalog entry.</summary>
        ConstructionSite CompleteDefaultBuilding(string id, Vector3 position)
        {
            ResourceLedger.Add(ResourceType.Wood, 50, "test");
            ResourceLedger.Add(ResourceType.Oil, 50, "test");
            ResourceLedger.Add(ResourceType.Candles, 50, "test");
            ResourceLedger.Add(ResourceType.Medicine, 50, "test");
            ResourceLedger.Add(ResourceType.RelicFragments, 50, "test");

            var site = BuildingPlacer.PlaceConstructionSite(id, position);
            Assert.IsNotNull(site, $"default catalog must contain {id}");
            foreach (var stack in site.Type.cost)
            {
                site.DeliverResource(stack.type, stack.amount);
            }
            Assert.IsFalse(site.NeedsMaterials, "sanity: every cost stack was delivered");
            site.ApplyWork(site.Type.buildWorkSeconds);
            Assert.IsTrue(site.IsComplete, "sanity: enough work was applied");
            return site;
        }

        [Test]
        public void DefaultCatalog_ContainsTheSpecSection5BuildableSet()
        {
            var expected = new[]
            {
                "campfire_t1", "storage_pile_t1", "shelter_t1", "woodcutter_t1",
                "lantern_post_t1", "guard_post_t1", "candle_shrine_t1", "infirmary_corner_t1",
            };
            foreach (var id in expected)
            {
                var type = _catalog.Find(id);
                Assert.IsNotNull(type, $"catalog must contain {id}");
                Assert.IsNotEmpty(type.displayName);
                Assert.Greater(type.footprint.x, 0f);
                Assert.Greater(type.footprint.y, 0f);
                Assert.IsNotEmpty(type.cost, $"{id} must cost something");
            }
            Assert.IsNull(_catalog.Find("no_such_id"));
        }

        [Test]
        public void DeliverResource_ClampsToStockAndNeed_DecrementsLedgerExactly()
        {
            ResourceLedger.Add(ResourceType.Wood, 3, "test");
            ResourceLedger.Add(ResourceType.Oil, 10, "test");
            var site = CreateTestHutSite();

            Assert.AreEqual(3, site.DeliverResource(ResourceType.Wood, 10),
                "acceptance is clamped by what the ledger actually holds");
            Assert.AreEqual(0, ResourceLedger.Get(ResourceType.Wood));
            Assert.AreEqual(3, site.Delivered(ResourceType.Wood));
            Assert.AreEqual(2, site.RemainingNeed(ResourceType.Wood));
            Assert.IsTrue(site.NeedsMaterials);
            Assert.IsFalse(site.NeedsWork, "no work is owed before materials complete");

            ResourceLedger.Add(ResourceType.Wood, 4, "test");
            Assert.AreEqual(2, site.DeliverResource(ResourceType.Wood, 10),
                "over-delivery is clamped to the remaining need");
            Assert.AreEqual(2, ResourceLedger.Get(ResourceType.Wood),
                "exactly the accepted units left the ledger");

            Assert.AreEqual(1, site.DeliverResource(ResourceType.Oil, 1), "partial delivery");
            Assert.IsTrue(site.NeedsMaterials);
            Assert.AreEqual(1, site.DeliverResource(ResourceType.Oil, 5));
            Assert.AreEqual(8, ResourceLedger.Get(ResourceType.Oil));

            Assert.IsFalse(site.NeedsMaterials);
            Assert.IsTrue(site.NeedsWork);
            Assert.AreEqual(0, site.DeliverResource(ResourceType.Oil, 1),
                "a materially complete site accepts nothing more");
            Assert.AreEqual(8, ResourceLedger.Get(ResourceType.Oil));
            Assert.IsTrue(LogContains("build", "hut materials complete"));
        }

        [Test]
        public void DeliverResource_RefusesResourcesNotInTheCost()
        {
            ResourceLedger.Add(ResourceType.Food, 5, "test");
            var site = CreateTestHutSite();

            Assert.AreEqual(0, site.DeliverResource(ResourceType.Food, 5));
            Assert.AreEqual(5, ResourceLedger.Get(ResourceType.Food), "nothing was consumed");
        }

        [Test]
        public void ApplyWork_IsGatedBehindMaterials_AndClampsToRemainingSeconds()
        {
            ResourceLedger.Add(ResourceType.Wood, 5, "test");
            ResourceLedger.Add(ResourceType.Oil, 2, "test");
            var site = CreateTestHutSite();

            Assert.AreEqual(0f, site.ApplyWork(2f),
                "hammering a site with missing materials does nothing");
            Assert.AreEqual(0f, site.Progress);

            site.DeliverResource(ResourceType.Wood, 5);
            Assert.AreEqual(0f, site.ApplyWork(2f), "one cost stack is still missing");
            site.DeliverResource(ResourceType.Oil, 2);

            Assert.AreEqual(1f, site.ApplyWork(1f), 1e-4f);
            Assert.AreEqual(0.25f, site.Progress, 1e-4f);
            Assert.IsFalse(site.IsComplete);
            Assert.IsTrue(site.NeedsWork);

            Assert.AreEqual(3f, site.ApplyWork(100f), 1e-4f,
                "only the remaining seconds are counted");
            Assert.IsTrue(site.IsComplete);
            Assert.AreEqual(1f, site.Progress);
            Assert.IsFalse(site.NeedsWork);
            Assert.AreEqual(0f, site.ApplyWork(1f), "a finished site refuses further work");
        }

        [Test]
        public void Completion_LanternPost_SpawnsEnabledLightSourceWithPrototypeConfigValues()
        {
            Building spawnedFromEvent = null;
            var site = BuildingPlacer.PlaceConstructionSite("lantern_post_t1", new Vector3(5f, 0f, 5f));
            Assert.IsNull(site, "an empty ledger cannot afford the lantern yet");

            ResourceLedger.Add(ResourceType.Wood, 50, "test");
            ResourceLedger.Add(ResourceType.Oil, 50, "test");
            site = BuildingPlacer.PlaceConstructionSite("lantern_post_t1", new Vector3(5f, 0f, 5f));
            site.Completed += (s, building) => spawnedFromEvent = building;
            foreach (var stack in site.Type.cost)
            {
                site.DeliverResource(stack.type, stack.amount);
            }
            site.ApplyWork(site.Type.buildWorkSeconds);

            Assert.IsTrue(site.IsComplete);
            Assert.IsNotNull(site.CompletedBuilding);
            Assert.AreSame(site.CompletedBuilding, spawnedFromEvent);
            Assert.IsFalse(site.gameObject.activeSelf, "the site swaps out for the building");
            Assert.AreEqual(0, ConstructionSite.Active.Count);
            Assert.AreEqual(1, Building.Active.Count);
            Assert.AreEqual(FunctionKind.LightSource, site.CompletedBuilding.Kind);
            Assert.AreEqual(new Vector3(5f, 0f, 5f), site.CompletedBuilding.transform.position);

            var light = site.CompletedBuilding.GetComponent<LightSource>();
            Assert.IsNotNull(light, "a lantern post must become a LightSource");
            Assert.IsTrue(light.enabled && light.isLit);
            var cfg = PrototypeConfig.LoadOrDefault();
            Assert.AreEqual(cfg.lanternRadius, light.radius);
            Assert.AreEqual(cfg.lanternStrength, light.strength);
            Assert.AreEqual(cfg.defaultFuelSeconds, light.fuelSeconds);
            Assert.AreEqual(cfg.fuelConsumptionPerSecond, light.fuelConsumptionPerSecond);
            Assert.IsFalse(light.sacred);
            Assert.Contains(light, (System.Collections.ICollection)DarknessEvaluator.Sources,
                "the new light immediately contributes territory");
        }

        [Test]
        public void Completion_Campfire_UsesTheCampfireLightProfile()
        {
            var site = CompleteDefaultBuilding("campfire_t1", Vector3.zero);

            var light = site.CompletedBuilding.GetComponent<LightSource>();
            Assert.IsNotNull(light);
            var cfg = PrototypeConfig.LoadOrDefault();
            Assert.AreEqual(cfg.campfireRadius, light.radius);
            Assert.AreEqual(cfg.campfireStrength, light.strength);
        }

        [Test]
        public void Completion_StoragePile_RegistersWithTheLedger()
        {
            int capacityBefore = ResourceLedger.Capacity;
            var site = CompleteDefaultBuilding("storage_pile_t1", Vector3.zero);

            Assert.IsNotNull(site.CompletedBuilding.GetComponent<StoragePile>(),
                "a storage pile building must raise the storage ceiling");
            Assert.AreEqual(1, ResourceLedger.StoragePiles.Count);
            Assert.AreEqual(capacityBefore + _economyConfig.storagePileCapacity,
                ResourceLedger.Capacity);
            Assert.AreEqual(FunctionKind.Storage, site.CompletedBuilding.Kind);
        }

        [Test]
        public void Completion_AppendsBuildRecordsToTheEventLog()
        {
            CompleteDefaultBuilding("shelter_t1", new Vector3(2f, 0f, -3f));

            Assert.IsTrue(LogContains("build", "shelter_t1 placed at (2.0, -3.0)"));
            Assert.IsTrue(LogContains("build", "shelter_t1 materials complete"));
            Assert.IsTrue(LogContains("build", "shelter_t1 complete at (2.0, -3.0)"));
        }
    }
}
