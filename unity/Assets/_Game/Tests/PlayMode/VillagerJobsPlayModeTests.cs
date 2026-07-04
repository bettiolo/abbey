using System.Collections;
using System.Collections.Generic;
using Abbey.Buildings;
using Abbey.Core;
using Abbey.Economy;
using Abbey.Light;
using Abbey.Villagers;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Abbey.Tests.PlayMode
{
    /// <summary>
    /// Visible logistics end-to-end, built programmatically (no scenes): a
    /// salvager walks to the wreck, works, hauls a visible load to storage and
    /// the ledger increments; a guard takes its post at night. Every component
    /// runs with autoTick = false and is stepped manually with fixed dt; yields
    /// are bounded loop-counters, never realtime waits.
    /// </summary>
    public class VillagerJobsPlayModeTests
    {
        const float Dt = 0.05f;

        readonly List<GameObject> _spawned = new List<GameObject>();
        readonly List<ScriptableObject> _assets = new List<ScriptableObject>();

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
            foreach (var asset in _assets)
            {
                if (asset != null)
                {
                    Object.DestroyImmediate(asset);
                }
            }
            _assets.Clear();
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

        GameObject Track(GameObject go)
        {
            _spawned.Add(go);
            return go;
        }

        PrototypeConfig CreatePrototypeConfig()
        {
            var config = ScriptableObject.CreateInstance<PrototypeConfig>();
            _assets.Add(config);
            config.dayDurationSeconds = 10f;
            config.duskDurationSeconds = 5f;
            config.nightDurationSeconds = 30f;
            config.dawnDurationSeconds = 5f;
            config.edgeBandFraction = 0.3f;
            config.arrivalRadius = 0.3f;
            config.villagerWalkSpeed = 2f;
            config.villagerFearPerSecondInDark = 0f;
            config.villagerFearPerSecondInEdge = 0f;
            config.villagerInjuredDarkSeconds = 1000f;
            config.villagerMissingDarkSeconds = 2000f;
            config.villagerPickupDurationSeconds = 0.1f;
            config.duskLateRecallDelaySeconds = 0.5f;
            DarknessEvaluator.Config = config;
            DuskRecallSystem.Config = config;
            return config;
        }

        EconomyConfig CreateEconomyConfig()
        {
            var config = ScriptableObject.CreateInstance<EconomyConfig>();
            _assets.Add(config);
            config.baseStorageCapacity = 50;
            config.salvageSiteWood = 4;
            config.salvageSiteFood = 0;
            config.salvageSiteOil = 0;
            config.salvageSiteMedicine = 0;
            config.salvageYieldPerCycle = 2;
            config.salvageWorkDurationSeconds = 0.4f;
            ResourceLedger.Config = config;
            return config;
        }

        JobsConfig CreateJobsConfig()
        {
            var config = ScriptableObject.CreateInstance<JobsConfig>();
            _assets.Add(config);
            config.carryCapacity = 4;
            config.guardPostRadius = 1.5f;
            return config;
        }

        GameClock CreateClock(PrototypeConfig config)
        {
            var clock = Track(new GameObject("Clock")).AddComponent<GameClock>();
            clock.autoTick = false;
            clock.Configure(config);
            return clock;
        }

        VillagerJobAgent CreateWorker(
            Vector3 position, VillagerJob job, PrototypeConfig proto, JobsConfig jobs)
        {
            var go = Track(new GameObject($"Villager_{_spawned.Count}"));
            go.transform.position = position;
            var villager = go.AddComponent<VillagerAgent>();
            villager.autoTick = false;
            villager.Config = proto;
            villager.seed = 1;
            villager.Bravery = 0.5f;
            var agent = go.AddComponent<VillagerJobAgent>();
            agent.autoTick = false;
            agent.Config = jobs;
            agent.SetJob(job);
            return agent;
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

        [UnityTest]
        public IEnumerator Salvager_VisiblyCompletesRoundTrip_AndLedgerIncrements()
        {
            var proto = CreatePrototypeConfig();
            var econ = CreateEconomyConfig();
            var jobs = CreateJobsConfig();
            CreateClock(proto); // Day, never ticked

            var pileGO = Track(new GameObject("StoragePile"));
            pileGO.transform.position = Vector3.zero;
            pileGO.AddComponent<StoragePile>();

            var siteGO = Track(new GameObject("WreckSite"));
            siteGO.transform.position = new Vector3(0f, 0f, 6f);
            var site = siteGO.AddComponent<SalvageSite>();
            site.Configure(econ);

            var agent = CreateWorker(Vector3.zero, VillagerJob.Salvager, proto, jobs);
            var villager = agent.Villager;

            float maxDistanceFromStorage = 0f;
            bool sawWorking = false;
            bool sawVisibleCarry = false;
            int startWood = ResourceLedger.Get(ResourceType.Wood);

            for (int i = 0; i < 1000 && ResourceLedger.Get(ResourceType.Wood) < startWood + 2; i++)
            {
                villager.Tick(Dt);
                agent.Tick(Dt);
                maxDistanceFromStorage = Mathf.Max(maxDistanceFromStorage,
                    PlanarMotion.Distance(villager.transform.position, Vector3.zero));
                sawWorking |= villager.State == VillagerState.Working;
                sawVisibleCarry |= villager.State == VillagerState.ReturningToStorage
                                   && agent.IsCarrying
                                   && agent.CarriedPropInstance != null
                                   && agent.CarriedPropInstance.activeInHierarchy;
                if (i % 25 == 0)
                {
                    yield return null; // let Unity breathe between simulated bursts
                }
            }

            Assert.AreEqual(startWood + 2, ResourceLedger.Get(ResourceType.Wood),
                "one completed round trip deposits one carry of wood into the ledger");
            Assert.AreEqual(2, econ.salvageSiteWood - site.Remaining(ResourceType.Wood),
                "the deposited wood came out of the site pool");
            Assert.Greater(maxDistanceFromStorage, 5f,
                "the salvager visibly travelled to the wreck site");
            Assert.LessOrEqual(
                PlanarMotion.Distance(villager.transform.position, Vector3.zero), 1f,
                "the trip ends back at the storage pile");
            Assert.IsTrue(sawWorking, "the salvager visibly worked the site");
            Assert.IsTrue(sawVisibleCarry,
                "the carried prop must be visible while hauling to storage");
            Assert.IsTrue(LogContains("resource", "wood +2 (salvager)"));
        }

        [UnityTest]
        public IEnumerator Builder_HaulsAndBuildsASmallHut_EndToEnd()
        {
            var proto = CreatePrototypeConfig();
            CreateEconomyConfig();
            var jobs = CreateJobsConfig();
            CreateClock(proto); // Day, never ticked

            var pileGO = Track(new GameObject("StoragePile"));
            pileGO.transform.position = Vector3.zero;
            pileGO.AddComponent<StoragePile>();
            ResourceLedger.Add(ResourceType.Wood, 10, "test");

            var hutType = new BuildingType
            {
                id = "small_hut",
                displayName = "Small Hut",
                footprint = new Vector2(2f, 2f),
                cost = new List<ResourceStack> { new ResourceStack(ResourceType.Wood, 6) },
                buildWorkSeconds = 1f,
                function = FunctionKind.Shelter,
            };
            var siteGO = Track(new GameObject("ConstructionSite_small_hut"));
            siteGO.transform.position = new Vector3(0f, 0f, 6f);
            var site = siteGO.AddComponent<ConstructionSite>();
            site.Initialize(hutType);

            var agent = CreateWorker(Vector3.zero, VillagerJob.Builder, proto, jobs);
            var villager = agent.Villager;

            float maxDistanceFromStorage = 0f;
            bool sawVisibleHaul = false;
            for (int i = 0; i < 3000 && Building.Active.Count == 0; i++)
            {
                villager.Tick(Dt);
                agent.Tick(Dt);
                maxDistanceFromStorage = Mathf.Max(maxDistanceFromStorage,
                    PlanarMotion.Distance(villager.transform.position, Vector3.zero));
                sawVisibleHaul |= agent.CarriedPropInstance != null
                                  && agent.CarriedPropInstance.activeInHierarchy;
                if (i % 25 == 0)
                {
                    yield return null; // let Unity breathe between simulated bursts
                }
            }

            Assert.IsTrue(site.IsComplete, "the hut must be finished");
            Assert.AreEqual(1, Building.Active.Count);
            Assert.AreEqual("small_hut", Building.Active[0].Id);
            Assert.AreEqual(4, ResourceLedger.Get(ResourceType.Wood),
                "exactly the 6-wood cost was consumed, paid at delivery time");
            Assert.Greater(maxDistanceFromStorage, 5f,
                "the builder visibly travelled to the site");
            Assert.IsTrue(sawVisibleHaul, "the haul must be visible via the carried prop");
            Assert.IsTrue(LogContains("job", "delivered wood x4 -> small_hut"));
            Assert.IsTrue(LogContains("job", "delivered wood x2 -> small_hut"));
            Assert.IsTrue(LogContains("job", "built small_hut"));
            Assert.IsTrue(LogContains("build", "small_hut complete"));
        }

        [UnityTest]
        public IEnumerator Guard_TakesPostAtNight()
        {
            var proto = CreatePrototypeConfig();
            CreateEconomyConfig();
            var jobs = CreateJobsConfig();
            var clock = CreateClock(proto);

            var lightGO = Track(new GameObject("Campfire"));
            lightGO.transform.position = Vector3.zero;
            var light = lightGO.AddComponent<LightSource>();
            light.autoTick = false;
            light.radius = 10f;
            light.fuelSeconds = -1f;

            var postGO = Track(new GameObject("GuardPost"));
            postGO.transform.position = new Vector3(5f, 0f, 0f);
            var post = postGO.AddComponent<JobWorkPoint>();
            post.job = VillagerJob.Guard;

            var agent = CreateWorker(new Vector3(1f, 0f, 0f), VillagerJob.Guard, proto, jobs);
            var villager = agent.Villager;

            // Day + Dusk: the guard has no post duty (dusk recall resolves inside Safe).
            clock.Tick(10f); // -> Dusk
            for (int i = 0; i < 60; i++)
            {
                villager.Tick(Dt);
                agent.Tick(Dt);
                if (i % 20 == 0)
                {
                    yield return null;
                }
            }
            Assert.IsFalse(agent.IsAtGuardPost, "no post duty before Night");

            clock.Tick(5f); // -> Night
            Assert.AreEqual(DayPhase.Night, clock.Phase);
            for (int i = 0; i < 400 && !agent.IsAtGuardPost; i++)
            {
                villager.Tick(Dt);
                agent.Tick(Dt);
                if (i % 50 == 0)
                {
                    yield return null;
                }
            }

            Assert.IsTrue(agent.IsAtGuardPost, "the guard stands its post during Night");
            Assert.LessOrEqual(
                PlanarMotion.Distance(villager.transform.position, postGO.transform.position),
                jobs.guardPostRadius + 0.05f);
            Assert.IsTrue(LogContains("job", "guard_took_post"));
        }
    }
}
