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
    /// Placement validation: footprint overlap against sites and completed
    /// buildings (touching edges allowed), the affordability gate, that placing
    /// consumes nothing, and the "build" event-log trail. Catalog and economy
    /// config are injected so no default balance value matters.
    /// </summary>
    public class BuildingPlacerTests
    {
        EconomyConfig _economyConfig;
        BuildingCatalog _catalog;

        [SetUp]
        public void SetUp()
        {
            ClearStatics();

            _economyConfig = ScriptableObject.CreateInstance<EconomyConfig>();
            _economyConfig.baseStorageCapacity = 999;
            ResourceLedger.Config = _economyConfig;

            _catalog = ScriptableObject.CreateInstance<BuildingCatalog>();
            _catalog.buildings = new List<BuildingType>
            {
                new BuildingType
                {
                    id = "hut",
                    displayName = "Test Hut",
                    footprint = new Vector2(2f, 2f),
                    cost = new List<ResourceStack> { new ResourceStack(ResourceType.Wood, 5) },
                    buildWorkSeconds = 4f,
                    function = FunctionKind.Shelter,
                },
            };
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

        void FundWood(int amount)
        {
            ResourceLedger.Add(ResourceType.Wood, amount, "test");
        }

        [Test]
        public void CanPlaceAt_UnknownId_IsRejected()
        {
            Assert.IsFalse(BuildingPlacer.CanPlaceAt("no_such_building", Vector3.zero,
                out var error));
            Assert.AreEqual(PlacementError.UnknownBuilding, error);

            Assert.IsNull(BuildingPlacer.PlaceConstructionSite("no_such_building", Vector3.zero));
            Assert.IsTrue(LogContains("build", "no_such_building placement rejected"));
        }

        [Test]
        public void CanPlaceAt_RejectsOverlap_AllowsTouchingEdges()
        {
            FundWood(20);
            Assert.IsNotNull(BuildingPlacer.PlaceConstructionSite("hut", Vector3.zero));

            // 2x2 footprint centered at origin occupies -1..1 on both axes.
            Assert.IsFalse(BuildingPlacer.CanPlaceAt("hut", new Vector3(1.9f, 0f, 0f), out var error),
                "footprints sharing ground must be rejected");
            Assert.AreEqual(PlacementError.Overlapping, error);
            Assert.IsFalse(BuildingPlacer.CanPlaceAt("hut", new Vector3(0f, 0f, 1.9f)),
                "overlap applies on the Z axis too");

            Assert.IsTrue(BuildingPlacer.CanPlaceAt("hut", new Vector3(2f, 0f, 0f)),
                "touching edges is adjacency, not overlap — snug building is allowed");
            Assert.IsTrue(BuildingPlacer.CanPlaceAt("hut", new Vector3(0f, 0f, -2f)));
        }

        [Test]
        public void CanPlaceAt_RejectsOverlap_WithCompletedBuildings()
        {
            FundWood(20);
            var site = BuildingPlacer.PlaceConstructionSite("hut", Vector3.zero);
            site.DeliverResource(ResourceType.Wood, 5);
            site.ApplyWork(4f);
            Assert.IsTrue(site.IsComplete, "sanity: the hut finished");
            Assert.AreEqual(0, ConstructionSite.Active.Count,
                "a completed site leaves the site registry");

            Assert.IsFalse(BuildingPlacer.CanPlaceAt("hut", new Vector3(1f, 0f, 1f), out var error),
                "the finished building still occupies its footprint");
            Assert.AreEqual(PlacementError.Overlapping, error);
            Assert.IsTrue(BuildingPlacer.CanPlaceAt("hut", new Vector3(2f, 0f, 0f)));
        }

        [Test]
        public void CanPlaceAt_GatesOnAffordability()
        {
            Assert.IsFalse(BuildingPlacer.CanPlaceAt("hut", Vector3.zero, out var error),
                "an empty ledger cannot afford wood x5");
            Assert.AreEqual(PlacementError.Unaffordable, error);
            Assert.IsNull(BuildingPlacer.PlaceConstructionSite("hut", Vector3.zero));
            Assert.AreEqual(0, ConstructionSite.Active.Count);

            FundWood(5);
            Assert.IsTrue(BuildingPlacer.CanPlaceAt("hut", Vector3.zero, out error));
            Assert.AreEqual(PlacementError.None, error);
        }

        [Test]
        public void PlaceConstructionSite_ConsumesNothing_RegistersSite_AndLogs()
        {
            FundWood(5);
            var site = BuildingPlacer.PlaceConstructionSite("hut", new Vector3(3f, 0f, 4f));

            Assert.IsNotNull(site);
            Assert.AreEqual("hut", site.Type.id);
            Assert.AreEqual(1, ConstructionSite.Active.Count);
            Assert.AreSame(site, ConstructionSite.Active[0]);
            Assert.AreEqual(5, ResourceLedger.Get(ResourceType.Wood),
                "placement reserves nothing — the cost is paid at delivery time");
            Assert.IsTrue(site.NeedsMaterials);
            Assert.IsTrue(LogContains("build", "hut placed at (3.0, 4.0)"));
        }
    }
}
