using Abbey.Buildings;
using Abbey.Core;
using Abbey.Economy;
using Abbey.Light;
using Abbey.Villagers;
using NUnit.Framework;
using UnityEngine;

namespace Abbey.Tests.EditMode
{
    /// <summary>
    /// Abbey restoration nodes (P2-04): fixed pre-placed construction sites whose
    /// completion effects are catalog-driven — the gate flips the static
    /// path-blocked flag, the bell tower boosts every bell pulse through
    /// DuskRecallSystem, the shrine spawns a sacred light, the infirmary heals
    /// injured villagers through the public VillagerAgent API. Everything lands
    /// as "abbey" event-log records. Deterministic, no scenes, manual ticks.
    /// </summary>
    public class AbbeyRestorationTests
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
            foreach (var villager in Object.FindObjectsByType<VillagerAgent>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(villager.gameObject);
            }
            foreach (var light in Object.FindObjectsByType<LightSource>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(light.gameObject);
            }
            foreach (var clock in Object.FindObjectsByType<GameClock>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(clock.gameObject);
            }
            Object.DestroyImmediate(_catalog);
            Object.DestroyImmediate(_economyConfig);
            ClearStatics();
        }

        static void ClearStatics()
        {
            AbbeyState.Clear();
            RestorationNode.ClearRegistry();
            ConstructionSite.ClearRegistry();
            Building.ClearRegistry();
            BuildingPlacer.Clear();
            BuildingCatalog.ClearCache();
            DarknessEvaluator.Clear();
            DuskRecallSystem.Clear();
            ResourceLedger.Clear();
            EconomyConfig.ClearCache();
            PrototypeConfig.ClearCache();
            EventBus.ResetAll();
            GameEventLog.Clear();
        }

        static bool LogContains(string type, string dataFragment)
        {
            return LogCount(type, dataFragment) > 0;
        }

        static int LogCount(string type, string dataFragment)
        {
            int count = 0;
            var records = GameEventLog.Records;
            for (int i = 0; i < records.Count; i++)
            {
                if (records[i].Type == type && records[i].Data.Contains(dataFragment))
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>Funds the ledger with the node's full cost, delivers it and works the site out.</summary>
        static Building CompleteNode(RestorationNode node)
        {
            var site = node.Site;
            foreach (var stack in site.Type.cost)
            {
                ResourceLedger.Add(stack.type, stack.amount, "test");
                Assert.AreEqual(stack.amount, site.DeliverResource(stack.type, stack.amount),
                    $"the funded {stack.type} delivery must be fully accepted");
            }
            site.ApplyWork(site.Type.buildWorkSeconds);
            Assert.IsTrue(site.IsComplete, "sanity: the node site must complete");
            return site.CompletedBuilding;
        }

        [Test]
        public void DefaultCatalog_ContainsTheAbbeyRepairEntries()
        {
            var gate = _catalog.Find("abbey_gate_repair");
            Assert.IsNotNull(gate);
            Assert.AreEqual(FunctionKind.Gate, gate.function);
            Assert.IsNotEmpty(gate.cost, "gate repair must cost something (wood+stone)");

            var tower = _catalog.Find("bell_tower_repair");
            Assert.IsNotNull(tower);
            Assert.AreEqual(FunctionKind.BellTower, tower.function);
            Assert.IsNotEmpty(tower.cost, "bell tower repair must cost something (wood+iron)");
        }

        [Test]
        public void Place_BindsEachNodeToItsCatalogEntry_AndRegisters()
        {
            var node = RestorationNode.Place(RestorationNodeKind.CandleShrine, new Vector3(3f, 0f, 1f));

            Assert.IsNotNull(node);
            Assert.AreEqual("candle_shrine_t1", node.Site.Type.id, "shrine reuses the catalog entry");
            Assert.AreEqual("candle_shrine", node.NodeId);
            Assert.Contains(node, (System.Collections.ICollection)RestorationNode.Active);
            Assert.Contains(node.Site, (System.Collections.ICollection)ConstructionSite.Active,
                "the node is an ordinary construction site to the builder job");
            Assert.IsTrue(LogContains("abbey", "candle_shrine node placed at (3.0, 1.0)"));

            Assert.AreEqual("abbey_gate_repair",
                RestorationNode.CatalogId(RestorationNodeKind.AbbeyGate));
            Assert.AreEqual("bell_tower_repair",
                RestorationNode.CatalogId(RestorationNodeKind.BellTower));
            Assert.AreEqual("infirmary_corner_t1",
                RestorationNode.CatalogId(RestorationNodeKind.InfirmaryCorner));
        }

        [Test]
        public void GateCompletion_FlipsThePathBlockedFlag_AndLogs()
        {
            Assert.IsFalse(AbbeyState.GateRepaired, "the night path starts open");
            var node = RestorationNode.Place(RestorationNodeKind.AbbeyGate, Vector3.zero);

            Assert.IsFalse(AbbeyState.GateRepaired, "placing the ruin repairs nothing");
            var building = CompleteNode(node);

            Assert.IsTrue(AbbeyState.GateRepaired,
                "the flag the nightmare spawner queries must flip on completion");
            Assert.AreEqual(FunctionKind.Gate, building.Kind);
            Assert.IsTrue(LogContains("abbey", "gate_repaired"));
            Assert.IsTrue(LogContains("abbey", "abbey_gate_repair restored"));
            Assert.AreEqual(0, RestorationNode.Active.Count,
                "a completed node deactivates with its site");

            AbbeyState.MarkGateRepaired();
            Assert.AreEqual(1, LogCount("abbey", "gate_repaired"), "marks are idempotent");
        }

        [Test]
        public void ShrineCompletion_SpawnsSacredLight_WithPrototypeConfigValues()
        {
            var node = RestorationNode.Place(RestorationNodeKind.CandleShrine, new Vector3(2f, 0f, 2f));
            var building = CompleteNode(node);

            var light = building.GetComponent<LightSource>();
            Assert.IsNotNull(light, "a completed shrine must burn");
            Assert.IsTrue(light.sacred, "the shrine flame is sacred");
            Assert.IsTrue(light.isLit && light.enabled);
            Assert.IsTrue(light.HasInfiniteFuel, "sacred flames never burn out");
            var cfg = PrototypeConfig.LoadOrDefault();
            Assert.AreEqual(cfg.sacredFlameRadius, light.radius);
            Assert.AreEqual(cfg.sacredFlameStrength, light.strength);
            Assert.Contains(light, (System.Collections.ICollection)DarknessEvaluator.Sources,
                "the sacred light immediately contributes territory");
            Assert.IsTrue(AbbeyState.ShrineLit);
            Assert.IsTrue(LogContains("abbey", "shrine_lit"));
        }

        [Test]
        public void BellTowerCompletion_SetsMultiplierFromConfig_AndWidensBellPulses()
        {
            var cfg = PrototypeConfig.LoadOrDefault();
            cfg.bellRadius = 15f;
            cfg.bellTowerRangeMultiplier = 1.5f;
            cfg.duskLateRecallDelaySeconds = 999f; // only the bell may move anyone
            DarknessEvaluator.Config = cfg;
            DuskRecallSystem.Config = cfg;

            var clockGO = new GameObject("TestClock");
            var clock = clockGO.AddComponent<GameClock>();
            clock.autoTick = false;
            clock.Configure(cfg);

            var lightGO = new GameObject("Campfire");
            var light = lightGO.AddComponent<LightSource>();
            light.autoTick = false;
            light.radius = 5f;
            light.fuelSeconds = -1f;

            var villagerGO = new GameObject("FarVillager");
            villagerGO.transform.position = new Vector3(20f, 0f, 0f); // beyond 15, within 22.5
            var villager = villagerGO.AddComponent<VillagerAgent>();
            villager.autoTick = false;
            villager.Config = cfg;

            clock.Tick(cfg.dayDurationSeconds + 0.1f); // -> Dusk
            Assert.AreEqual(DayPhase.Dusk, clock.Phase);
            Assert.AreEqual(VillagerState.Idle, villager.State,
                "the delayed recall alone must not move the villager yet");

            EventBus.RaiseBellRang(Vector3.zero, cfg.bellRadius);
            Assert.AreEqual(VillagerState.Idle, villager.State,
                "an unrepaired bell (radius 15) cannot reach a villager at 20");

            var node = RestorationNode.Place(RestorationNodeKind.BellTower, new Vector3(0f, 0f, -5f));
            CompleteNode(node);

            Assert.IsTrue(AbbeyState.BellTowerRepaired);
            Assert.AreEqual(cfg.bellTowerRangeMultiplier, AbbeyState.BellRangeMultiplier,
                "the multiplier is the config value, not a hard-coded one");
            Assert.IsTrue(LogContains("abbey", "bell_tower_repaired"));

            EventBus.RaiseBellRang(Vector3.zero, cfg.bellRadius);
            Assert.AreEqual(VillagerState.ReturningToLight, villager.State,
                "the repaired bell (15 x 1.5 = 22.5) reaches the villager at 20");
        }

        [Test]
        public void InfirmaryCompletion_HealsInjuredVillagersInsideTheRadius()
        {
            var cfg = PrototypeConfig.LoadOrDefault();
            var node = RestorationNode.Place(RestorationNodeKind.InfirmaryCorner, Vector3.zero);
            var building = CompleteNode(node);

            Assert.IsTrue(AbbeyState.InfirmaryBuilt);
            Assert.IsTrue(LogContains("abbey", "infirmary_built"));
            var zone = building.GetComponent<InfirmaryZone>();
            Assert.IsNotNull(zone, "a completed infirmary must treat villagers");
            Assert.AreEqual(cfg.infirmaryRadius, zone.radius);
            Assert.AreEqual(cfg.infirmaryHealSeconds, zone.healSeconds);

            var patientGO = new GameObject("Patient");
            patientGO.transform.position = new Vector3(1f, 0f, 0f); // inside the radius
            var patient = patientGO.AddComponent<VillagerAgent>();
            patient.autoTick = false;
            patient.Config = cfg;
            patient.ForceState(VillagerState.Injured);

            var farGO = new GameObject("FarPatient");
            farGO.transform.position = new Vector3(zone.radius + 5f, 0f, 0f);
            var far = farGO.AddComponent<VillagerAgent>();
            far.autoTick = false;
            far.Config = cfg;
            far.ForceState(VillagerState.Injured);

            var healthyGO = new GameObject("Healthy");
            healthyGO.transform.position = new Vector3(0f, 0f, 1f);
            var healthy = healthyGO.AddComponent<VillagerAgent>();
            healthy.autoTick = false;
            healthy.Config = cfg;

            // Treatment is continuous exposure: half the time heals nobody...
            for (int i = 0; i < 3; i++)
            {
                zone.Tick(zone.healSeconds / 6f);
            }
            Assert.AreEqual(VillagerState.Injured, patient.State,
                "half the treatment time heals nobody");

            // ...and the full time puts the inside patient (only) back on its feet.
            for (int i = 0; i < 4; i++)
            {
                zone.Tick(zone.healSeconds / 6f);
            }
            Assert.AreEqual(VillagerState.Idle, patient.State,
                "the treated villager recovers through the public ForceState hook");
            Assert.AreEqual(VillagerState.Injured, far.State,
                "villagers outside the radius are not treated");
            Assert.AreEqual(VillagerState.Idle, healthy.State, "healthy villagers are untouched");
            Assert.AreEqual(1, LogCount("abbey", "infirmary_heal Patient"));
            Assert.IsFalse(LogContains("abbey", "infirmary_heal FarPatient"));
        }

        [Test]
        public void InfirmaryZone_InterruptedTreatment_StartsOver()
        {
            var zoneGO = new GameObject("Infirmary");
            var zone = zoneGO.AddComponent<InfirmaryZone>();
            zone.radius = 4f;
            zone.healSeconds = 2f;
            zone.autoTick = false;

            var cfg = PrototypeConfig.LoadOrDefault();
            var patientGO = new GameObject("Walkout");
            patientGO.transform.position = Vector3.zero;
            var patient = patientGO.AddComponent<VillagerAgent>();
            patient.autoTick = false;
            patient.Config = cfg;
            patient.ForceState(VillagerState.Injured);

            zone.Tick(1.5f); // almost healed...
            patientGO.transform.position = new Vector3(10f, 0f, 0f);
            zone.Tick(1f); // ...but walked out, so exposure resets
            patientGO.transform.position = Vector3.zero;
            zone.Tick(1.5f);
            Assert.AreEqual(VillagerState.Injured, patient.State,
                "exposure is continuous: 1.5s + 1.5s with a walkout is not 2s");

            zone.Tick(0.5f);
            Assert.AreEqual(VillagerState.Idle, patient.State);

            Object.DestroyImmediate(zoneGO);
            Object.DestroyImmediate(patientGO);
        }

        [Test]
        public void AbbeyState_Clear_ResetsEverything()
        {
            AbbeyState.MarkGateRepaired();
            AbbeyState.MarkBellTowerRepaired(2f);
            AbbeyState.MarkShrineLit();
            AbbeyState.MarkInfirmaryBuilt();
            Assert.AreEqual(2f, AbbeyState.BellRangeMultiplier);

            AbbeyState.Clear();

            Assert.IsFalse(AbbeyState.GateRepaired);
            Assert.IsFalse(AbbeyState.BellTowerRepaired);
            Assert.IsFalse(AbbeyState.ShrineLit);
            Assert.IsFalse(AbbeyState.InfirmaryBuilt);
            Assert.AreEqual(1f, AbbeyState.BellRangeMultiplier);

            AbbeyState.MarkBellTowerRepaired(0.5f);
            Assert.AreEqual(1f, AbbeyState.BellRangeMultiplier,
                "a repaired tower never makes the bell worse");
        }

        [Test]
        public void Place_WithoutACatalogEntry_ReturnsNullAndLogs()
        {
            _catalog.buildings.Clear();
            var node = RestorationNode.Place(RestorationNodeKind.AbbeyGate, Vector3.zero, _catalog);

            Assert.IsNull(node);
            Assert.IsTrue(LogContains("abbey", "abbey_gate_repair node rejected"));
            Assert.AreEqual(0, ConstructionSite.Active.Count, "no orphan site is left behind");
        }
    }
}
