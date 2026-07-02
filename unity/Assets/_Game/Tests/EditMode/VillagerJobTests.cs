using System.Collections.Generic;
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
    /// The job layer: roster assignment, the salvager's full site → storage loop
    /// accounting into the ledger, depletion repathing (including the 0-yield
    /// trap), tender refuelling paid in ledger wood, the guard's night post, and
    /// injury suspending/resuming a job. All ticks are manual and deterministic;
    /// every config is injected so no default balance value matters.
    /// </summary>
    public class VillagerJobTests
    {
        const float Dt = 0.05f;

        readonly List<GameObject> _spawned = new List<GameObject>();
        PrototypeConfig _proto;
        EconomyConfig _econ;
        JobsConfig _jobs;
        GameClock _clock;

        [SetUp]
        public void SetUp()
        {
            EventBus.ResetAll();
            GameEventLog.Clear();
            DarknessEvaluator.Clear();
            DuskRecallSystem.Clear();
            ResourceLedger.Clear();
            SalvageSite.ClearRegistry();
            JobWorkPoint.ClearRegistry();
            ConstructionSite.ClearRegistry();
            Building.ClearRegistry();
            RestorationNode.ClearRegistry();
            BuildingPlacer.Clear();
            AbbeyState.Clear();
            EconomyConfig.ClearCache();
            JobsConfig.ClearCache();
            BuildingCatalog.ClearCache();
            PrototypeConfig.ClearCache();

            _proto = ScriptableObject.CreateInstance<PrototypeConfig>();
            _proto.dayDurationSeconds = 10f;
            _proto.duskDurationSeconds = 30f;
            _proto.nightDurationSeconds = 30f;
            _proto.dawnDurationSeconds = 30f;
            _proto.edgeBandFraction = 0.3f;
            _proto.arrivalRadius = 0.3f;
            _proto.villagerWalkSpeed = 2f;
            _proto.villagerFearPerSecondInDark = 0f; // isolate jobs from fear
            _proto.villagerFearPerSecondInEdge = 0f;
            _proto.villagerFearRecoveryPerSecond = 1f;
            _proto.villagerPanicFearThreshold = 0.6f;
            _proto.villagerInjuredDarkSeconds = 1000f;
            _proto.villagerMissingDarkSeconds = 2000f;
            _proto.villagerWorkDurationSeconds = 5f; // overridden per job
            _proto.villagerPickupDurationSeconds = 0.1f;
            _proto.villagerRestDurationSeconds = 1f;
            _proto.braveryFinishWorkThreshold = 0.65f;
            _proto.duskRecallEndangeredDistance = 12f;
            _proto.duskLateRecallDelaySeconds = 2f;

            _econ = ScriptableObject.CreateInstance<EconomyConfig>();
            _econ.baseStorageCapacity = 50;
            _econ.storagePileCapacity = 50;
            _econ.salvageSiteWood = 6;
            _econ.salvageSiteFood = 0;
            _econ.salvageSiteOil = 0;
            _econ.salvageSiteMedicine = 0;
            _econ.salvageYieldPerCycle = 2;
            _econ.salvageWorkDurationSeconds = 0.5f;

            _jobs = ScriptableObject.CreateInstance<JobsConfig>();
            _jobs.carryCapacity = 4;
            _jobs.woodcutterWorkDurationSeconds = 0.5f;
            _jobs.woodcutterYieldPerCycle = 2;
            _jobs.tenderRefuelThresholdFraction = 0.3f;
            _jobs.tenderTargetFuelSeconds = 100f;
            _jobs.tenderRefuelFuelSeconds = 60f;
            _jobs.tenderWoodCostPerRefuel = 1;
            _jobs.tenderRefuelWorkSeconds = 0.2f;
            _jobs.guardPostRadius = 1.5f;

            DarknessEvaluator.Config = _proto;
            DuskRecallSystem.Config = _proto;
            ResourceLedger.Config = _econ;

            var clockGO = new GameObject("TestClock");
            _spawned.Add(clockGO);
            _clock = clockGO.AddComponent<GameClock>();
            _clock.autoTick = false;
            _clock.Configure(_proto);
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
            foreach (var building in Object.FindObjectsByType<Building>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(building.gameObject); // spawned by Construct
            }
            Object.DestroyImmediate(_proto);
            Object.DestroyImmediate(_econ);
            Object.DestroyImmediate(_jobs);
            JobWorkPoint.ClearRegistry();
            SalvageSite.ClearRegistry();
            ConstructionSite.ClearRegistry();
            Building.ClearRegistry();
            RestorationNode.ClearRegistry();
            BuildingPlacer.Clear();
            AbbeyState.Clear();
            ResourceLedger.Clear();
            DuskRecallSystem.Clear();
            DarknessEvaluator.Clear();
            EconomyConfig.ClearCache();
            JobsConfig.ClearCache();
            BuildingCatalog.ClearCache();
            PrototypeConfig.ClearCache();
            EventBus.ResetAll();
            GameEventLog.Clear();
        }

        // ---- Programmatic world helpers ----------------------------------

        VillagerAgent SpawnVillager(Vector3 position, int seed = 1)
        {
            var go = new GameObject($"TestVillager_{_spawned.Count}");
            _spawned.Add(go);
            go.transform.position = position;
            var villager = go.AddComponent<VillagerAgent>();
            villager.autoTick = false;
            villager.Config = _proto;
            villager.seed = seed;
            villager.Bravery = 0.5f;
            return villager;
        }

        VillagerJobAgent SpawnWorker(Vector3 position, VillagerJob job)
        {
            var villager = SpawnVillager(position);
            var agent = villager.gameObject.AddComponent<VillagerJobAgent>();
            agent.autoTick = false;
            agent.Config = _jobs;
            agent.SetJob(job);
            return agent;
        }

        SalvageSite SpawnSite(Vector3 position)
        {
            var go = new GameObject($"TestSite_{_spawned.Count}");
            _spawned.Add(go);
            go.transform.position = position;
            var site = go.AddComponent<SalvageSite>();
            site.Configure(_econ);
            return site;
        }

        StoragePile SpawnPile(Vector3 position)
        {
            var go = new GameObject($"TestPile_{_spawned.Count}");
            _spawned.Add(go);
            go.transform.position = position;
            return go.AddComponent<StoragePile>();
        }

        LightSource SpawnLight(Vector3 position, float radius, float fuel = -1f)
        {
            var go = new GameObject($"TestLight_{_spawned.Count}");
            _spawned.Add(go);
            go.transform.position = position;
            var source = go.AddComponent<LightSource>();
            source.radius = radius;
            source.strength = 1f;
            source.isLit = true;
            source.fuelSeconds = fuel;
            source.autoTick = false;
            return source;
        }

        JobWorkPoint SpawnWorkPoint(Vector3 position, VillagerJob job)
        {
            var go = new GameObject($"TestWorkPoint_{_spawned.Count}");
            _spawned.Add(go);
            go.transform.position = position;
            var point = go.AddComponent<JobWorkPoint>();
            point.job = job;
            return point;
        }

        /// <summary>
        /// Programmatic construction site (bypasses the placer and its planning
        /// gate — placement is BuildingPlacerTests' concern): wood-only cost with
        /// injectable amounts so builder haul counts stay exact.
        /// </summary>
        ConstructionSite SpawnConstructionSite(Vector3 position, int woodCost, float workSeconds)
        {
            var type = new BuildingType
            {
                id = "test_hut",
                displayName = "Test Hut",
                footprint = new Vector2(2f, 2f),
                cost = new List<ResourceStack> { new ResourceStack(ResourceType.Wood, woodCost) },
                buildWorkSeconds = workSeconds,
                function = FunctionKind.Shelter,
            };
            var go = new GameObject($"TestConstructionSite_{_spawned.Count}");
            _spawned.Add(go);
            go.transform.position = position;
            var site = go.AddComponent<ConstructionSite>();
            site.Initialize(type);
            return site;
        }

        static void Step(VillagerJobAgent agent, float dt = Dt)
        {
            agent.Villager.Tick(dt);
            agent.Tick(dt);
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

        // ---- Tests --------------------------------------------------------

        [Test]
        public void JobAssigner_AppliesDefaultRoster_InRegistrationOrder()
        {
            _jobs.defaultSalvagers = 3;
            _jobs.defaultBuilders = 1;
            _jobs.defaultWoodcutters = 2;
            _jobs.defaultTenders = 1;
            _jobs.defaultGuards = 1;

            var villagers = new List<VillagerAgent>();
            for (int i = 0; i < 10; i++)
            {
                villagers.Add(SpawnVillager(new Vector3(i, 0f, 0f), seed: i));
            }

            int assigned = JobAssigner.ApplyDefaultRoster(villagers, _jobs);

            Assert.AreEqual(8, assigned, "3+1+2+1+1 quotas over 10 villagers");
            var counts = new Dictionary<VillagerJob, int>();
            int jobless = 0;
            foreach (var v in villagers)
            {
                var agent = v.GetComponent<VillagerJobAgent>();
                if (agent == null || agent.Job == VillagerJob.None)
                {
                    jobless++;
                    continue;
                }
                counts.TryGetValue(agent.Job, out int c);
                counts[agent.Job] = c + 1;
            }
            Assert.AreEqual(3, counts[VillagerJob.Salvager]);
            Assert.AreEqual(1, counts[VillagerJob.Builder]);
            Assert.AreEqual(2, counts[VillagerJob.Woodcutter]);
            Assert.AreEqual(1, counts[VillagerJob.Tender]);
            Assert.AreEqual(1, counts[VillagerJob.Guard]);
            Assert.AreEqual(2, jobless, "leftover villagers stay jobless");
            Assert.IsTrue(LogContains("job", "assigned salvager"));
            Assert.IsTrue(LogContains("job", "assigned guard"));
            Assert.IsTrue(LogContains("job", "roster applied (8 villagers)"));

            // Assign/Unassign round-trips and logs.
            JobAssigner.Unassign(villagers[0]);
            Assert.AreEqual(VillagerJob.None, villagers[0].GetComponent<VillagerJobAgent>().Job);
            Assert.IsTrue(LogContains("job", "assigned none"));
        }

        [Test]
        public void Salvager_FullLoop_HaulsSiteIntoLedger_ThenIdlesWhenStripped()
        {
            SpawnPile(Vector3.zero);
            var site = SpawnSite(new Vector3(0f, 0f, 4f));
            var agent = SpawnWorker(Vector3.zero, VillagerJob.Salvager);
            var villager = agent.Villager;

            var seenStates = new HashSet<VillagerState>();
            bool sawProp = false;
            int guard = 0;
            while (villager.State != VillagerState.Idle || guard == 0)
            {
                Step(agent);
                seenStates.Add(villager.State);
                sawProp |= agent.CarriedPropInstance != null && agent.CarriedPropInstance.activeSelf;
                if (guard++ > 2000)
                {
                    break;
                }
            }

            Assert.AreEqual(6, ResourceLedger.Get(ResourceType.Wood),
                "the whole 6-wood pool must end up in the ledger, 2 per trip");
            Assert.IsTrue(site.IsExhausted);
            Assert.AreEqual(VillagerState.Idle, villager.State, "no salvage left: idle");
            Assert.IsNull(agent.TargetSite);
            Assert.IsFalse(agent.IsCarrying, "deposited loads are no longer carried");

            Assert.IsTrue(seenStates.Contains(VillagerState.WalkingToTask));
            Assert.IsTrue(seenStates.Contains(VillagerState.Working));
            Assert.IsTrue(seenStates.Contains(VillagerState.CarryingResource));
            Assert.IsTrue(seenStates.Contains(VillagerState.ReturningToStorage));
            Assert.IsTrue(sawProp, "carrying must be visible via the carried prop");
            Assert.IsTrue(LogContains("resource", "wood +2 (salvager)"));
            Assert.IsTrue(LogContains("job", "idle (no salvage left)"));
        }

        [Test]
        public void Salvager_SiteDepletedMidRoute_RepathsToNextSite()
        {
            SpawnPile(Vector3.zero);
            var near = SpawnSite(new Vector3(0f, 0f, 4f));
            var far = SpawnSite(new Vector3(0f, 0f, 10f));
            var agent = SpawnWorker(Vector3.zero, VillagerJob.Salvager);
            var villager = agent.Villager;

            int guard = 0;
            while (villager.State != VillagerState.WalkingToTask && guard++ < 100)
            {
                Step(agent);
            }
            Assert.AreSame(near, agent.TargetSite, "starts toward the nearest site");

            near.Harvest(ResourceType.Wood, 6); // someone strips it mid-route
            Assert.IsTrue(near.IsExhausted);

            Step(agent);
            Assert.AreSame(far, agent.TargetSite, "depleted mid-route: repath to the next site");
            Assert.IsTrue(LogContains("job", "repath"));

            far.Harvest(ResourceType.Wood, 6); // and the last site dies too
            Step(agent);
            Assert.AreEqual(VillagerState.Idle, villager.State, "no sites left: idle");
            Assert.IsNull(agent.TargetSite);
        }

        [Test]
        public void Salvager_ZeroYieldConfig_IsTreatedAsDepleted()
        {
            // Economy-reviewer trap: TryHarvestCycle can report success with
            // amount == 0 when salvageYieldPerCycle is 0 at runtime. The job layer
            // must not spin on such sites.
            _econ.salvageYieldPerCycle = 0;
            SpawnPile(Vector3.zero);
            var site = SpawnSite(new Vector3(0f, 0f, 4f));
            var agent = SpawnWorker(Vector3.zero, VillagerJob.Salvager);
            var villager = agent.Villager;

            for (int i = 0; i < 100; i++)
            {
                Step(agent);
            }

            Assert.AreEqual(VillagerState.Idle, villager.State,
                "0-yield salvage is depleted salvage: stay idle, never loop");
            Assert.AreEqual(6, site.TotalRemaining, "nothing was extracted");
            Assert.AreEqual(0, ResourceLedger.Get(ResourceType.Wood));
        }

        [Test]
        public void Woodcutter_WorksAssignedPoint_AndDepositsWood()
        {
            SpawnPile(Vector3.zero);
            SpawnWorkPoint(new Vector3(0f, 0f, 5f), VillagerJob.Woodcutter);
            var agent = SpawnWorker(Vector3.zero, VillagerJob.Woodcutter);

            int guard = 0;
            while (ResourceLedger.Get(ResourceType.Wood) < 4 && guard++ < 2000)
            {
                Step(agent);
            }

            Assert.GreaterOrEqual(ResourceLedger.Get(ResourceType.Wood), 4,
                "two felling trips of woodcutterYieldPerCycle=2 each");
            Assert.IsTrue(LogContains("resource", "wood +2 (woodcutter)"));
        }

        [Test]
        public void Tender_RefuelsLowLight_PayingWoodFromTheLedger()
        {
            SpawnPile(new Vector3(2f, 0f, 0f));
            var light = SpawnLight(new Vector3(0f, 0f, 6f), radius: 3f, fuel: 10f);
            ResourceLedger.Add(ResourceType.Wood, 5, "test");
            var agent = SpawnWorker(Vector3.zero, VillagerJob.Tender);

            int guard = 0;
            while (!LogContains("job", "refueled") && guard++ < 2000)
            {
                Step(agent);
            }

            Assert.IsTrue(LogContains("job", "refueled"), "the refuel must be a job log record");
            Assert.AreEqual(70f, light.fuelSeconds, 1e-3f,
                "10s remaining + 60s refuel amount from JobsConfig");
            Assert.IsTrue(light.isLit);
            Assert.AreEqual(4, ResourceLedger.Get(ResourceType.Wood),
                "each refuel consumes tenderWoodCostPerRefuel from the ledger");
            Assert.IsTrue(LogContains("resource", "wood -1 (tender refuel)"));
            Assert.IsFalse(agent.IsCarrying);

            // Fuel fraction is now 0.7 >= threshold: the tender goes back to watching.
            float wood = ResourceLedger.Get(ResourceType.Wood);
            for (int i = 0; i < 100; i++)
            {
                Step(agent);
            }
            Assert.AreEqual(wood, ResourceLedger.Get(ResourceType.Wood),
                "a topped-up light is not refuelled again");
        }

        [Test]
        public void Tender_RelightsBurnedOutLight()
        {
            SpawnPile(Vector3.zero);
            var light = SpawnLight(new Vector3(0f, 0f, 4f), radius: 3f, fuel: 1f);
            light.Tick(2f); // burns out and extinguishes
            Assert.IsFalse(light.isLit);
            ResourceLedger.Add(ResourceType.Wood, 2, "test");
            var agent = SpawnWorker(Vector3.zero, VillagerJob.Tender);

            int guard = 0;
            while (!light.isLit && guard++ < 2000)
            {
                Step(agent);
            }

            Assert.IsTrue(light.isLit, "refuelling a dead light relights it (Ignite with fuel)");
            Assert.AreEqual(60f, light.fuelSeconds, 1e-3f);
        }

        [Test]
        public void Guard_TakesPostAtNight_AndLogsIt()
        {
            SpawnLight(Vector3.zero, radius: 10f); // Safe within 7 covers post at 5
            var post = SpawnWorkPoint(new Vector3(5f, 0f, 0f), VillagerJob.Guard);
            var agent = SpawnWorker(new Vector3(1f, 0f, 0f), VillagerJob.Guard);
            var villager = agent.Villager;

            // Day: a guard just idles near camp.
            for (int i = 0; i < 20; i++)
            {
                Step(agent);
            }
            Assert.IsFalse(agent.IsAtGuardPost);
            Assert.AreEqual(VillagerState.Idle, villager.State);

            _clock.Tick(10f); // -> Dusk (recall fires; villager is already Safe)
            for (int i = 0; i < 100; i++)
            {
                Step(agent);
            }
            Assert.IsFalse(agent.IsAtGuardPost, "not Night yet: no post duty");

            _clock.Tick(30f); // -> Night
            int guard = 0;
            while (!agent.IsAtGuardPost && guard++ < 400)
            {
                Step(agent);
            }

            Assert.IsTrue(agent.IsAtGuardPost, "the guard must reach the post during Night");
            Assert.LessOrEqual(
                PlanarMotion.Distance(villager.transform.position, post.transform.position),
                _jobs.guardPostRadius + 0.05f);
            Assert.IsTrue(LogContains("job", "guard_took_post"));
        }

        [Test]
        public void InjuredVillager_DropsOutOfJob_AndResumesOnRecovery()
        {
            SpawnLight(Vector3.zero, radius: 10f); // Safe everywhere the loop runs
            SpawnPile(Vector3.zero);
            SpawnSite(new Vector3(0f, 0f, 4f));
            var agent = SpawnWorker(Vector3.zero, VillagerJob.Salvager);
            var villager = agent.Villager;

            int guard = 0;
            while (!(villager.State == VillagerState.ReturningToStorage && agent.IsCarrying)
                   && guard++ < 500)
            {
                Step(agent);
            }
            Assert.IsTrue(agent.IsCarrying, "mid-haul before the attack");

            villager.OnMonsterAttack();
            Assert.AreEqual(VillagerState.Injured, villager.State);

            Step(agent);
            Assert.IsFalse(agent.IsCarrying, "an injured hauler drops its load");
            Assert.IsTrue(LogContains("job", "dropped"));
            Assert.AreNotEqual(VillagerState.WalkingToTask, villager.State,
                "the job stays suspended while injured");

            // Safe zone: Injured -> Resting -> Idle, then the job resumes.
            guard = 0;
            while (villager.State != VillagerState.WalkingToTask && guard++ < 500)
            {
                Step(agent);
            }
            Assert.AreEqual(VillagerState.WalkingToTask, villager.State,
                "recovered villagers pick their job back up");
            Assert.IsNotNull(agent.TargetSite);
        }

        [Test]
        public void DuskRecall_OverridesJob_AndJobResumesNextDay()
        {
            SpawnLight(Vector3.zero, radius: 10f);
            SpawnPile(Vector3.zero);
            var site = SpawnSite(new Vector3(0f, 0f, 4f));
            var agent = SpawnWorker(Vector3.zero, VillagerJob.Salvager);
            var villager = agent.Villager;

            int guard = 0;
            while (villager.State != VillagerState.WalkingToTask && guard++ < 100)
            {
                Step(agent);
            }

            _clock.Tick(10f); // -> Dusk: recall ordered (delayed, uncovered)
            Assert.IsTrue(villager.IsRecallOrdered);
            guard = 0;
            while (villager.State != VillagerState.Idle && guard++ < 500)
            {
                Step(agent); // walks to light, arrives, idles; job must NOT restart
            }
            Assert.AreEqual(VillagerState.Idle, villager.State);

            for (int i = 0; i < 50; i++)
            {
                Step(agent);
            }
            Assert.AreEqual(VillagerState.Idle, villager.State,
                "Dusk is not a work phase: the salvager stays home");

            _clock.Tick(30f + 30f + 30f); // Night + Dawn... -> next Day
            Assert.AreEqual(DayPhase.Day, _clock.Phase);
            guard = 0;
            while (villager.State != VillagerState.WalkingToTask && guard++ < 100)
            {
                Step(agent);
            }
            Assert.AreEqual(VillagerState.WalkingToTask, villager.State,
                "the job resumes the next day");
            Assert.AreSame(site, agent.TargetSite);
        }

        [Test]
        public void Builder_HaulsMaterialsPerTrip_AndWorksSiteToCompletion()
        {
            SpawnPile(Vector3.zero);
            var site = SpawnConstructionSite(new Vector3(0f, 0f, 5f), woodCost: 6, workSeconds: 0.5f);
            ResourceLedger.Add(ResourceType.Wood, 10, "test");
            var agent = SpawnWorker(Vector3.zero, VillagerJob.Builder);

            bool sawHaulProp = false;
            int guard = 0;
            while (Building.Active.Count == 0 && guard++ < 4000)
            {
                Step(agent);
                sawHaulProp |= agent.CarriedPropInstance != null
                               && agent.CarriedPropInstance.activeSelf;
            }

            Assert.AreEqual(1, Building.Active.Count, "the hut must stand");
            Assert.IsTrue(site.IsComplete);
            Assert.AreEqual(4, ResourceLedger.Get(ResourceType.Wood),
                "exactly the 6-wood cost left the ledger — delivery pays once, "
                + "the fetch leg reserves nothing");
            Assert.IsTrue(LogContains("job", "delivered wood x4 -> test_hut"),
                "first haul is a full carry");
            Assert.IsTrue(LogContains("job", "delivered wood x2 -> test_hut"),
                "second haul tops the site up");
            Assert.IsTrue(LogContains("job", "built test_hut"));
            Assert.IsTrue(LogContains("build", "test_hut complete"));
            Assert.IsTrue(sawHaulProp, "the builder's haul must be visible");
            Assert.IsNull(agent.BuildTarget, "the finished site is released");
            Assert.IsFalse(agent.IsCarrying, "the builder never holds ledger goods");

            for (int i = 0; i < 50; i++)
            {
                Step(agent);
            }
            Assert.AreEqual(VillagerState.Idle, agent.Villager.State,
                "no sites left: the builder idles");
        }

        [Test]
        public void Builder_WithNoLedgerStock_Waits_ThenServesWhenStockArrives()
        {
            SpawnPile(Vector3.zero);
            var site = SpawnConstructionSite(new Vector3(0f, 0f, 5f), woodCost: 4, workSeconds: 0.2f);
            var agent = SpawnWorker(Vector3.zero, VillagerJob.Builder);

            for (int i = 0; i < 100; i++)
            {
                Step(agent);
            }
            Assert.IsNull(agent.BuildTarget,
                "a site whose materials the ledger cannot supply is not served");
            Assert.AreEqual(0, site.Delivered(ResourceType.Wood));

            ResourceLedger.Add(ResourceType.Wood, 4, "test");
            int guard = 0;
            while (!site.IsComplete && guard++ < 2000)
            {
                Step(agent);
            }
            Assert.IsTrue(site.IsComplete, "stock arrived: the builder finishes the job");
            Assert.AreEqual(0, ResourceLedger.Get(ResourceType.Wood));
        }

        [Test]
        public void Builder_ServesAWorkOnlySite_WithoutAStorageTrip()
        {
            SpawnPile(Vector3.zero);
            var site = SpawnConstructionSite(new Vector3(0f, 0f, 4f), woodCost: 2, workSeconds: 1f);
            ResourceLedger.Add(ResourceType.Wood, 2, "test");
            site.DeliverResource(ResourceType.Wood, 2); // materials already on site
            Assert.IsTrue(site.NeedsWork);
            var agent = SpawnWorker(Vector3.zero, VillagerJob.Builder);

            int guard = 0;
            while (!site.IsComplete && guard++ < 2000)
            {
                Step(agent);
            }

            Assert.IsTrue(site.IsComplete);
            Assert.IsFalse(LogContains("job", "delivered"), "no haul was needed");
            Assert.IsTrue(LogContains("job", "built test_hut"));
        }

        [Test]
        public void Builder_RecalledMidHaul_DropsThePlan_AndNoWoodIsLostOrDoubleSpent()
        {
            SpawnLight(Vector3.zero, radius: 10f);
            SpawnPile(Vector3.zero);
            var site = SpawnConstructionSite(new Vector3(0f, 0f, 8f), woodCost: 4, workSeconds: 1f);
            ResourceLedger.Add(ResourceType.Wood, 4, "test");
            var agent = SpawnWorker(Vector3.zero, VillagerJob.Builder);
            var villager = agent.Villager;

            // Walk the fetch leg until the site-bound haul is visibly under way.
            int guard = 0;
            while (!(agent.BuildTarget != null
                     && agent.CarriedPropInstance != null
                     && agent.CarriedPropInstance.activeSelf)
                   && guard++ < 500)
            {
                Step(agent);
            }
            Assert.IsNotNull(agent.BuildTarget, "sanity: mid-haul before the recall");
            Assert.AreEqual(4, ResourceLedger.Get(ResourceType.Wood),
                "the fetch leg must not touch the ledger");

            villager.OrderReturnToLight(bellBoosted: true);
            // Agent tick only: the villager is Safe, so its own tick would already
            // resolve the recall — the interrupt must fire while it is in effect.
            agent.Tick(Dt);

            Assert.IsNull(agent.BuildTarget, "the recalled builder drops the plan");
            Assert.AreEqual(4, ResourceLedger.Get(ResourceType.Wood),
                "nothing was consumed, so nothing is lost and nothing needs refunding");
            Assert.AreEqual(0, site.Delivered(ResourceType.Wood));
            Assert.IsFalse(LogContains("job", "dropped"),
                "no physical load existed to drop");
            Assert.IsFalse(agent.CarriedPropInstance != null
                           && agent.CarriedPropInstance.activeSelf,
                "the haul visual clears with the plan");
        }

        [Test]
        public void Tender_RecalledMidErrand_RefundsTheFetchedWood_Once()
        {
            SpawnLight(Vector3.zero, radius: 10f); // safe point for the recall
            SpawnPile(new Vector3(1f, 0f, 0f));
            var light = SpawnLight(new Vector3(0f, 0f, 8f), radius: 3f, fuel: 10f);
            ResourceLedger.Add(ResourceType.Wood, 5, "test");
            var agent = SpawnWorker(Vector3.zero, VillagerJob.Tender);
            var villager = agent.Villager;

            int guard = 0;
            while (!agent.IsCarrying && guard++ < 1000)
            {
                Step(agent); // fetches at storage: ledger pays 1 wood, tender holds it
            }
            Assert.IsTrue(agent.IsCarrying, "sanity: mid-errand toward the light");
            Assert.AreEqual(4, ResourceLedger.Get(ResourceType.Wood));

            villager.OrderReturnToLight(bellBoosted: true); // recall interrupts ToLight
            agent.Tick(Dt); // agent tick only: the interrupt must fire while in effect

            Assert.IsFalse(agent.IsCarrying);
            Assert.AreEqual(5, ResourceLedger.Get(ResourceType.Wood),
                "the fetched wood goes back to the ledger, not into the void");
            Assert.AreEqual(1, LogCount("resource", "wood +1 (tender errand aborted)"));
            Assert.AreEqual(10f, light.fuelSeconds, 1e-3f, "the light was never fed");

            for (int i = 0; i < 200; i++)
            {
                Step(agent); // reach the light, calm down — no second refund may appear
            }
            Assert.AreEqual(1, LogCount("resource", "wood +1 (tender errand aborted)"),
                "the refund happens exactly once");
        }

        [Test]
        public void Tender_SuccessfulRefuel_ThenRecall_ProducesNoRefund()
        {
            SpawnLight(Vector3.zero, radius: 20f); // everything is Safe
            SpawnPile(new Vector3(1f, 0f, 0f));
            SpawnLight(new Vector3(0f, 0f, 6f), radius: 3f, fuel: 10f);
            ResourceLedger.Add(ResourceType.Wood, 5, "test");
            var agent = SpawnWorker(Vector3.zero, VillagerJob.Tender);

            int guard = 0;
            while (!LogContains("job", "refueled") && guard++ < 2000)
            {
                Step(agent);
            }
            Assert.IsTrue(LogContains("job", "refueled"), "sanity: the errand completed");
            Assert.AreEqual(4, ResourceLedger.Get(ResourceType.Wood));

            agent.Villager.OrderReturnToLight(bellBoosted: true);
            Step(agent);

            Assert.AreEqual(4, ResourceLedger.Get(ResourceType.Wood),
                "the delivered wood stays spent");
            Assert.IsFalse(LogContains("resource", "tender errand aborted"),
                "a completed refuel leaves nothing to refund");
        }

        [Test]
        public void Tender_TargetDestroyedMidRefuel_RefundsInsteadOfVoidingTheWood()
        {
            SpawnPile(new Vector3(1f, 0f, 0f));
            var light = SpawnLight(new Vector3(0f, 0f, 6f), radius: 3f, fuel: 10f);
            Vector3 lightPos = light.transform.position;
            ResourceLedger.Add(ResourceType.Wood, 5, "test");
            var agent = SpawnWorker(Vector3.zero, VillagerJob.Tender);

            // Walk the errand until the tender stands at the light, mid-refuel.
            int guard = 0;
            while (!(agent.IsCarrying
                     && PlanarMotion.Distance(agent.transform.position, lightPos)
                        <= _proto.arrivalRadius + 0.05f)
                   && guard++ < 2000)
            {
                Step(agent);
            }
            Assert.IsTrue(agent.IsCarrying, "sanity: refueling with wood in hand");

            Object.DestroyImmediate(light.gameObject); // a nightmare smashes the lantern
            Step(agent);

            Assert.IsFalse(agent.IsCarrying);
            Assert.AreEqual(5, ResourceLedger.Get(ResourceType.Wood),
                "the held wood is refunded through AbortTenderErrand, not zeroed away");
            Assert.AreEqual(1, LogCount("resource", "wood +1 (tender errand aborted)"));
            Assert.IsFalse(LogContains("job", "refueled"), "nothing was refueled");
        }

        [Test]
        public void JobsConfig_LoadOrDefault_NeverNull_AndSpeedHelperCovers()
        {
            var cfg = JobsConfig.LoadOrDefault();
            Assert.IsNotNull(cfg);
            Assert.AreSame(cfg, JobsConfig.LoadOrDefault(), "cached");
            Assert.AreEqual(1f, cfg.SpeedMultiplier(VillagerJob.None));
            Assert.Greater(cfg.SpeedMultiplier(VillagerJob.Tender), 0f);
            Assert.AreEqual(3, cfg.DefaultCount(VillagerJob.Salvager));
            Assert.AreEqual("salvager", VillagerJobs.Id(VillagerJob.Salvager));
            Assert.AreEqual("none", VillagerJobs.Id(VillagerJob.None));
        }
    }
}
