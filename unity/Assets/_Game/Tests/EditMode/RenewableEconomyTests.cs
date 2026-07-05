using System.Collections.Generic;
using Abbey.Buildings;
using Abbey.Core;
using Abbey.Economy;
using Abbey.World;
using NUnit.Framework;
using UnityEngine;

namespace Abbey.Tests.EditMode
{
    /// <summary>
    /// EditMode coverage for the renewable seasonal economy (P3-04). Every world is
    /// built programmatically (no scene file). Covers: a recipe cycle completing after
    /// its configured worker-days and depositing outputs to the ledger (event-logged);
    /// an unstaffed building producing nothing; the seasonal yield multiplier (autumn
    /// out-yields spring); winter halting growth recipes while conversion recipes
    /// (kiln/smithy) keep running; a conversion recipe stalling with an empty larder;
    /// exact ledger accounting over a scripted multi-season run; and the
    /// Building.Construct → ProductionBuilding wiring for the Production function kind.
    /// Balance is injected via a test <see cref="EconomyConfig"/>; no default matters.
    /// </summary>
    public class RenewableEconomyTests
    {
        readonly List<GameObject> _spawned = new List<GameObject>();
        EconomyConfig _config;

        [SetUp]
        public void SetUp()
        {
            GameEventLog.Clear();
            ResourceLedger.Clear();
            ProductionBuilding.ClearRegistry();
            EconomyConfig.ClearCache();

            _config = ScriptableObject.CreateInstance<EconomyConfig>();
            _config.baseStorageCapacity = 1000; // never let storage clamp the assertions
            _config.storagePileCapacity = 0;
            _config.springGrowthYield = 1f;
            _config.summerGrowthYield = 1.5f;
            _config.autumnGrowthYield = 2f;
            _config.winterGrowthYield = 0f;
            _config.productionRecipes = new List<ProductionRecipe>
            {
                new ProductionRecipe
                {
                    buildingId = "field_plot_t1",
                    seasonal = true,
                    workersRequired = 1,
                    cycleDays = 2f,
                    inputs = new List<ResourceStack>(),
                    outputs = new List<ResourceStack> { new ResourceStack(ResourceType.Grain, 3) },
                },
                new ProductionRecipe
                {
                    buildingId = "charcoal_kiln_t1",
                    seasonal = false,
                    workersRequired = 1,
                    cycleDays = 1f,
                    inputs = new List<ResourceStack> { new ResourceStack(ResourceType.Wood, 2) },
                    outputs = new List<ResourceStack> { new ResourceStack(ResourceType.Coal, 1) },
                },
            };
            ResourceLedger.Config = _config;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }
            _spawned.Clear();
            Object.DestroyImmediate(_config);
            ProductionBuilding.ClearRegistry();
            ResourceLedger.Clear();
            EconomyConfig.ClearCache();
            GameEventLog.Clear();
        }

        ProductionBuilding SpawnProduction(string id, int staff = 0)
        {
            var go = new GameObject(id);
            _spawned.Add(go);
            var pb = go.AddComponent<ProductionBuilding>();
            pb.autoAdvanceOnDayChanged = false;
            pb.Initialize(id, _config);
            pb.SetStaff(staff);
            return pb;
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

        // ------------------------------------------------------------------
        // Cycle completion
        // ------------------------------------------------------------------

        [Test]
        public void StaffedCycle_Completes_AfterConfiguredDays_AndDepositsOutputs()
        {
            var field = SpawnProduction("field_plot_t1", staff: 1);

            Assert.IsFalse(field.AdvanceDay(Season.Spring), "day 1: half a 2-day cycle");
            Assert.AreEqual(0, ResourceLedger.Get(ResourceType.Grain));
            Assert.AreEqual(0, field.CompletedCycles);

            Assert.IsTrue(field.AdvanceDay(Season.Spring), "day 2: the cycle completes");
            Assert.AreEqual(3, ResourceLedger.Get(ResourceType.Grain), "spring yield ×1 of grain 3");
            Assert.AreEqual(1, field.CompletedCycles);
            Assert.IsTrue(LogContains("production", "field_plot_t1 harvest cycle 1"));
            Assert.IsTrue(LogContains("resource", "grain +3"));
        }

        [Test]
        public void UnstaffedBuilding_ProducesNothing()
        {
            var field = SpawnProduction("field_plot_t1", staff: 0);

            for (int day = 0; day < 6; day++)
            {
                Assert.IsFalse(field.AdvanceDay(Season.Autumn));
            }
            Assert.AreEqual(0, ResourceLedger.Get(ResourceType.Grain));
            Assert.AreEqual(0, field.CompletedCycles);
            Assert.IsFalse(field.IsStaffed);
        }

        [Test]
        public void MoreWorkers_AdvanceTheCycleFaster()
        {
            var field = SpawnProduction("field_plot_t1", staff: 2); // 2 worker-days/day

            Assert.IsTrue(field.AdvanceDay(Season.Spring), "2 workers clear a 2-day cycle in one day");
            Assert.AreEqual(3, ResourceLedger.Get(ResourceType.Grain));
        }

        // ------------------------------------------------------------------
        // Seasonal yield
        // ------------------------------------------------------------------

        [Test]
        public void SeasonalMultiplier_AutumnYieldsMoreThanSpring()
        {
            var spring = SpawnProduction("field_plot_t1", staff: 2);
            spring.AdvanceDay(Season.Spring); // completes a cycle this day (2 workers)
            int springGrain = ResourceLedger.Get(ResourceType.Grain);

            ResourceLedger.Clear();
            ResourceLedger.Config = _config;
            var autumn = SpawnProduction("field_plot_t1", staff: 2);
            autumn.AdvanceDay(Season.Autumn);
            int autumnGrain = ResourceLedger.Get(ResourceType.Grain);

            Assert.AreEqual(3, springGrain, "grain 3 × spring 1.0");
            Assert.AreEqual(6, autumnGrain, "grain 3 × autumn 2.0");
            Assert.Greater(autumnGrain, springGrain, "the same field harvests more in autumn");
        }

        // ------------------------------------------------------------------
        // Winter shutdown
        // ------------------------------------------------------------------

        [Test]
        public void Winter_HaltsGrowthRecipe_ButNotConversionRecipe()
        {
            var field = SpawnProduction("field_plot_t1", staff: 1);
            var kiln = SpawnProduction("charcoal_kiln_t1", staff: 1);
            ResourceLedger.Add(ResourceType.Wood, 10, "test seed");

            // Winter: the field makes no progress at all; the kiln converts as ever.
            for (int day = 0; day < 3; day++)
            {
                Assert.IsFalse(field.AdvanceDay(Season.Winter), "nothing grows in winter");
                kiln.AdvanceDay(Season.Winter);
            }

            Assert.AreEqual(0, ResourceLedger.Get(ResourceType.Grain), "growth is frozen");
            Assert.AreEqual(0, field.CompletedCycles);
            Assert.AreEqual(3, ResourceLedger.Get(ResourceType.Coal), "kiln runs year-round: 3 coal");
            Assert.AreEqual(4, ResourceLedger.Get(ResourceType.Wood), "10 - 3×2 wood consumed");

            // Growth resumes cleanly the instant spring returns.
            Assert.IsFalse(field.AdvanceDay(Season.Spring), "spring day 1: half a cycle");
            Assert.IsTrue(field.AdvanceDay(Season.Spring), "spring day 2: harvest");
            Assert.IsFalse(field.AdvanceDay(Season.Spring), "spring day 3: half of the next");
            Assert.AreEqual(1, field.CompletedCycles, "one 2-day cycle across three spring days");
            Assert.AreEqual(3, ResourceLedger.Get(ResourceType.Grain));
        }

        // ------------------------------------------------------------------
        // Conversion inputs
        // ------------------------------------------------------------------

        [Test]
        public void ConversionRecipe_Stalls_WithoutInputs_ThenResumes()
        {
            var kiln = SpawnProduction("charcoal_kiln_t1", staff: 1);

            // No wood in the ledger: the cycle reaches the threshold and stalls.
            Assert.IsFalse(kiln.AdvanceDay(Season.Summer));
            Assert.IsFalse(kiln.AdvanceDay(Season.Summer));
            Assert.AreEqual(0, ResourceLedger.Get(ResourceType.Coal), "no wood, no coal");

            // Provision wood: the held cycle fires immediately next advance.
            ResourceLedger.Add(ResourceType.Wood, 4, "test");
            Assert.IsTrue(kiln.AdvanceDay(Season.Summer));
            Assert.AreEqual(1, ResourceLedger.Get(ResourceType.Coal));
            Assert.AreEqual(2, ResourceLedger.Get(ResourceType.Wood), "4 - 2 consumed");
        }

        // ------------------------------------------------------------------
        // Multi-season ledger accounting
        // ------------------------------------------------------------------

        [Test]
        public void ScriptedYear_ProducesExactLedgerTotals()
        {
            var field = SpawnProduction("field_plot_t1", staff: 1);

            // Two days per season, one staffed worker → one cycle per season, scaled by
            // that season's yield; winter is barren.
            Season[] seasons = { Season.Spring, Season.Summer, Season.Autumn, Season.Winter };
            int expectedCycles = 0;
            foreach (var season in seasons)
            {
                field.AdvanceDay(season);
                field.AdvanceDay(season);
                if (season != Season.Winter)
                {
                    expectedCycles++;
                }
            }

            // spring 3×1 + summer 3×1.5(→4, RoundToInt banker's) + autumn 3×2 + winter 0
            // = 3 + 4 + 6 + 0 = 13
            Assert.AreEqual(13, ResourceLedger.Get(ResourceType.Grain));
            Assert.AreEqual(expectedCycles, field.CompletedCycles);
            Assert.AreEqual(3, field.CompletedCycles, "one harvest per growing season");
        }

        // ------------------------------------------------------------------
        // Enum round-trip through the ledger
        // ------------------------------------------------------------------

        [Test]
        public void NewResources_RoundTrip_ThroughTheLedger()
        {
            Assert.AreEqual(6, ResourceLedger.Add(ResourceType.Wool, 6, "shearing"));
            Assert.AreEqual(6, ResourceLedger.Get(ResourceType.Wool));
            Assert.IsTrue(ResourceLedger.TryConsume(ResourceType.Wool, 4, "loom"));
            Assert.AreEqual(2, ResourceLedger.Get(ResourceType.Wool));
            Assert.AreEqual(8, _config.GrainToFood(4), "grain mills to food at the config 1:2 ratio");
        }

        // ------------------------------------------------------------------
        // Construction wiring
        // ------------------------------------------------------------------

        [Test]
        public void Construct_ProductionBuilding_AttachesRunningComponent()
        {
            var catalog = ScriptableObject.CreateInstance<BuildingCatalog>();
            var type = catalog.Find("field_plot_t1");
            Assert.IsNotNull(type, "the catalog carries the field production buildable");
            Assert.AreEqual(FunctionKind.Production, type.function);

            var building = Building.Construct(type, Vector3.zero);
            _spawned.Add(building.gameObject);

            var production = building.GetComponent<ProductionBuilding>();
            Assert.IsNotNull(production, "Production kind attaches a ProductionBuilding");
            Assert.AreEqual("field_plot_t1", production.BuildingId);
            Assert.IsNotNull(production.Recipe, "it resolved its recipe from EconomyConfig");

            Object.DestroyImmediate(catalog);
            Building.ClearRegistry();
        }
    }
}
