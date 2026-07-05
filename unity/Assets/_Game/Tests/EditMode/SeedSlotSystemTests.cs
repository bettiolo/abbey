using System.Collections.Generic;
using Abbey.Buildings;
using Abbey.Core;
using Abbey.Economy;
using Abbey.Light;
using Abbey.Settlement;
using NUnit.Framework;
using UnityEngine;

namespace Abbey.Tests.EditMode
{
    /// <summary>
    /// Seed-slot settlement growth (P3-02): authored slots start Open, a placement
    /// occupies one and is refused off-slot, completing a building opens child slots
    /// beside it only where the hug rule holds (adjacent to a building or on lit
    /// ground), and light debt sums the area of slots/buildings sitting outside Safe
    /// light. Deterministic, no scenes — the slot graph is built programmatically.
    /// </summary>
    public class SeedSlotSystemTests
    {
        EconomyConfig _economyConfig;
        BuildingCatalog _catalog;
        SettlementGrowthConfig _growthConfig;
        SeedSlotSystem _system;

        [SetUp]
        public void SetUp()
        {
            ClearStatics();

            _economyConfig = ScriptableObject.CreateInstance<EconomyConfig>();
            _economyConfig.baseStorageCapacity = 9999;
            ResourceLedger.Config = _economyConfig;

            _catalog = ScriptableObject.CreateInstance<BuildingCatalog>();
            _catalog.buildings = new List<BuildingType>
            {
                new BuildingType
                {
                    id = "hut",
                    displayName = "Test Hut",
                    footprint = new Vector2(2f, 2f),
                    cost = new List<ResourceStack> { new ResourceStack(ResourceType.Wood, 2) },
                    buildWorkSeconds = 1f,
                    function = FunctionKind.Shelter,
                },
            };
            BuildingPlacer.Catalog = _catalog;

            _growthConfig = ScriptableObject.CreateInstance<SettlementGrowthConfig>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_system != null)
            {
                Object.DestroyImmediate(_system.gameObject);
                _system = null;
            }
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
            foreach (var light in Object.FindObjectsByType<LightSource>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(light.gameObject);
            }
            Object.DestroyImmediate(_catalog);
            Object.DestroyImmediate(_economyConfig);
            Object.DestroyImmediate(_growthConfig);
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
            SettlementGrowthConfig.ClearCache();
            GameEventLog.Clear();
        }

        SeedSlotSystem CreateSystem(SettlementGrowthConfig config = null)
        {
            var go = new GameObject("SeedSlotSystem");
            _system = go.AddComponent<SeedSlotSystem>();
            _system.Config = config != null ? config : _growthConfig;
            return _system;
        }

        static void FundWood(int amount)
        {
            ResourceLedger.Add(ResourceType.Wood, amount, "test");
        }

        Building PlaceAndComplete(string id, Vector3 position)
        {
            FundWood(50);
            var site = BuildingPlacer.PlaceConstructionSite(id, position);
            Assert.IsNotNull(site, "placement on an open slot must succeed");
            foreach (var stack in site.Type.cost)
            {
                site.DeliverResource(stack.type, stack.amount);
            }
            site.ApplyWork(site.Type.buildWorkSeconds);
            Assert.IsTrue(site.IsComplete, "sanity: the building must complete");
            return site.CompletedBuilding;
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

        [Test]
        public void AuthoredSlots_StartOpen()
        {
            var system = CreateSystem();
            system.AddAuthoredSlot(new Vector3(1f, 0f, 0f), SlotSizeClass.Small);
            system.AddAuthoredSlot(new Vector3(4f, 0f, 0f), SlotSizeClass.Medium);
            system.AddAuthoredSlot(new Vector3(8f, 0f, 0f), SlotSizeClass.Large);

            Assert.AreEqual(3, system.Slots.Count);
            Assert.AreEqual(3, system.CountByState(SlotState.Open));
            foreach (var slot in system.Slots)
            {
                Assert.IsTrue(slot.IsOpen);
                Assert.IsFalse(slot.IsChild, "authored slots have no parent");
            }
        }

        [Test]
        public void FindOpenSlotNear_RespectsTolerance()
        {
            var system = CreateSystem();
            var slot = system.AddAuthoredSlot(Vector3.zero, SlotSizeClass.Medium);

            Assert.AreSame(slot, system.FindOpenSlotNear(new Vector3(0.5f, 0f, 0f), 1f));
            Assert.IsNull(system.FindOpenSlotNear(new Vector3(20f, 0f, 0f), 1f),
                "a far position matches no slot");
        }

        [Test]
        public void Placement_OnOpenSlot_SucceedsAndOccupiesSlot()
        {
            var system = CreateSystem();
            var slot = system.AddAuthoredSlot(Vector3.zero, SlotSizeClass.Medium);
            FundWood(10);

            Assert.IsTrue(BuildingPlacer.CanPlaceAt("hut", Vector3.zero, out var error));
            Assert.AreEqual(PlacementError.None, error);

            var site = BuildingPlacer.PlaceConstructionSite("hut", Vector3.zero);
            Assert.IsNotNull(site);
            Assert.AreEqual(SlotState.Occupied, slot.state, "the placement occupies the slot");
            Assert.AreEqual("hut", slot.occupantBuildingId);
            Assert.AreEqual(0, system.CountByState(SlotState.Open));
            Assert.IsTrue(LogContains("settlement", "slot_occupied hut"));
        }

        [Test]
        public void Placement_OffSlot_IsRejected()
        {
            var system = CreateSystem();
            system.AddAuthoredSlot(Vector3.zero, SlotSizeClass.Medium);
            FundWood(10);

            Assert.IsFalse(BuildingPlacer.CanPlaceAt("hut", new Vector3(20f, 0f, 20f),
                out var error), "no open slot near the off-slot position");
            Assert.AreEqual(PlacementError.NoOpenSlot, error);
            Assert.IsNull(BuildingPlacer.PlaceConstructionSite("hut", new Vector3(20f, 0f, 20f)));
            Assert.AreEqual(0, ConstructionSite.Active.Count, "no orphan site is placed off-slot");
        }

        [Test]
        public void Placement_WithNoSeedSlotSystem_IsUnconstrained()
        {
            // No system created — Phase 2 free placement is preserved.
            Assert.IsNull(SeedSlotSystem.Instance);
            FundWood(10);
            Assert.IsTrue(BuildingPlacer.CanPlaceAt("hut", new Vector3(20f, 0f, 20f), out var error),
                "without a SeedSlotSystem, any affordable non-overlapping spot is valid");
            Assert.AreEqual(PlacementError.None, error);
        }

        [Test]
        public void Completion_OpensChildSlots_OnLitGround()
        {
            _growthConfig.childSlotsPerBuilding = 3;
            _growthConfig.childSlotRingRadius = 3.5f;
            _growthConfig.childSlotSize = SlotSizeClass.Medium;
            _growthConfig.requireHug = true;

            var system = CreateSystem();
            system.AddAuthoredSlot(Vector3.zero, SlotSizeClass.Medium);

            // A broad lantern so every ring position sits on lit ground (hug rule).
            var lightGO = new GameObject("BroadLight");
            var light = lightGO.AddComponent<LightSource>();
            light.radius = 12f;
            light.strength = 1f;
            light.fuelSeconds = -1f;
            light.isLit = true;

            var building = PlaceAndComplete("hut", Vector3.zero);
            Assert.IsNotNull(building);

            int children = system.CountByState(SlotState.Open);
            Assert.AreEqual(3, children, "completing the building opens 3 child slots");
            foreach (var slot in system.Slots)
            {
                if (slot.IsChild)
                {
                    Assert.AreEqual(SlotState.Open, slot.state);
                    Assert.AreEqual(building.Id, slot.parentBuildingId);
                }
            }
            Assert.IsTrue(LogContains("settlement", "slot_opened parent=hut"));
        }

        [Test]
        public void ChildSlots_HugRule_SkipsIsolatedDark()
        {
            // Ring far from the parent AND no light: neither adjacency nor lit ground,
            // so the hug rule refuses every candidate.
            _growthConfig.childSlotsPerBuilding = 4;
            _growthConfig.childSlotRingRadius = 8f;
            _growthConfig.requireHug = true;

            var system = CreateSystem();
            system.AddAuthoredSlot(Vector3.zero, SlotSizeClass.Medium);

            var building = PlaceAndComplete("hut", Vector3.zero);
            Assert.IsNotNull(building);

            Assert.AreEqual(0, system.CountByState(SlotState.Open),
                "isolated dark plots do not hug — no child slots open");
            Assert.AreEqual(1, system.CountByState(SlotState.Occupied),
                "only the authored slot the building sits on is occupied");
        }

        [Test]
        public void LightDebt_RisesInDark_FallsWhenLit()
        {
            _growthConfig.lightDebtDarkWeight = 1f;
            _growthConfig.lightDebtEdgeWeight = 0.4f;

            var system = CreateSystem();
            var slotPos = new Vector3(30f, 0f, 30f);
            system.AddAuthoredSlot(slotPos, SlotSizeClass.Medium); // 2x2 = area 4

            float darkDebt = system.ComputeLightDebt();
            Assert.Greater(darkDebt, 0f, "an open slot in the dark carries light debt");
            Assert.AreEqual(4f * _growthConfig.lightDebtDarkWeight, darkDebt, 0.001f);

            // Drop a lantern over the slot: it becomes Safe, so the debt clears.
            var lightGO = new GameObject("Lantern");
            lightGO.transform.position = slotPos;
            var light = lightGO.AddComponent<LightSource>();
            light.radius = 10f;
            light.strength = 1f;
            light.fuelSeconds = -1f;
            light.isLit = true;

            float litDebt = system.ComputeLightDebt();
            Assert.Less(litDebt, darkDebt, "covering the slot with light lowers the debt");
            Assert.AreEqual(0f, litDebt, 0.001f, "a Safe slot contributes no debt");
        }

        [Test]
        public void DefaultCatalog_HasAsylumCorner_NotInfirmary()
        {
            var catalog = ScriptableObject.CreateInstance<BuildingCatalog>();
            try
            {
                var asylum = catalog.Find("asylum_corner_t1");
                Assert.IsNotNull(asylum, "the renamed asylum corner must exist");
                Assert.AreEqual(FunctionKind.Asylum, asylum.function);
                Assert.IsNull(catalog.Find("infirmary_corner_t1"),
                    "the legacy infirmary id must be gone");
            }
            finally
            {
                Object.DestroyImmediate(catalog);
            }
        }
    }
}
