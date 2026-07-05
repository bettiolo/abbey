using System.Collections.Generic;
using Abbey.Core;
using Abbey.Economy;
using Abbey.Island;
using Abbey.Light;
using Abbey.Settlement;
using Abbey.Villagers;
using NUnit.Framework;
using UnityEngine;

namespace Abbey.Tests.EditMode
{
    /// <summary>
    /// EditMode coverage for island exploration (P3-13). Worlds are built programmatically
    /// and the <see cref="IslandConfig"/> is injected, so an expedition to a POI is asserted
    /// against its data: reaching the POI reveals it and applies its reward (ledger deposit +
    /// unlocked seed-slot count), the party is pulled off jobs while away (job accounting),
    /// travel time scales with distance, a completed expedition returns its villagers home,
    /// and an expedition caught by dusk is stranded in the field.
    /// </summary>
    public class ExplorationTests
    {
        readonly List<GameObject> _spawned = new List<GameObject>();
        readonly List<Object> _assets = new List<Object>();
        IslandConfig _island;
        PrototypeConfig _proto;
        EconomyConfig _econ;
        SettlementGrowthConfig _growth;

        [SetUp]
        public void SetUp()
        {
            ClearStatics();

            _island = ScriptableObject.CreateInstance<IslandConfig>();
            _island.expeditionTravelSpeed = 20f;
            _island.poiResolveSeconds = 0.5f;
            _island.arrivalRadius = 0.5f;
            _island.expeditionMaxParty = 3;
            _island.poiRewards = new List<PoiRewardRule>
            {
                new PoiRewardRule
                {
                    type = PoiType.ResourceCache,
                    resourceYields = new List<ResourceStack> { new ResourceStack(ResourceType.Wood, 5) },
                    seedSlotsUnlocked = 2,
                    unlockedSlotSize = SlotSizeClass.Small,
                },
            };

            _proto = ScriptableObject.CreateInstance<PrototypeConfig>();
            _proto.arrivalRadius = 0.5f;
            _assets.Add(_proto);

            _econ = ScriptableObject.CreateInstance<EconomyConfig>();
            _econ.baseStorageCapacity = 1000;
            ResourceLedger.Config = _econ;
            _assets.Add(_econ);

            _growth = ScriptableObject.CreateInstance<SettlementGrowthConfig>();
            _growth.minSlotSeparation = 1f;
            _growth.childSlotRingRadius = 2f;
            _assets.Add(_growth);

            DarknessEvaluator.Config = _proto;
            DuskRecallSystem.Config = _proto;
            _assets.Add(_island);
        }

        [TearDown]
        public void TearDown()
        {
            if (ExplorationSystem.Instance != null)
            {
                Object.DestroyImmediate(ExplorationSystem.Instance.gameObject);
            }
            if (SeedSlotSystem.Instance != null)
            {
                Object.DestroyImmediate(SeedSlotSystem.Instance.gameObject);
            }
            foreach (var go in _spawned)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }
            _spawned.Clear();
            foreach (var a in _assets)
            {
                if (a != null)
                {
                    Object.DestroyImmediate(a);
                }
            }
            _assets.Clear();
            ClearStatics();
        }

        static void ClearStatics()
        {
            EventBus.ResetAll();
            GameEventLog.Clear();
            DarknessEvaluator.Clear();
            DuskRecallSystem.Clear();
            ResourceLedger.Clear();
            IslandConfig.ClearCache();
            PrototypeConfig.ClearCache();
            EconomyConfig.ClearCache();
            SettlementGrowthConfig.ClearCache();
            JobsConfig.ClearCache();
        }

        // ---- Helpers -----------------------------------------------------

        ExplorationSystem MakeSystem()
        {
            var go = new GameObject("ExplorationSystem");
            go.transform.position = Vector3.zero; // camp / muster point
            _spawned.Add(go);
            var sys = go.AddComponent<ExplorationSystem>();
            sys.autoTick = false;
            sys.Configure(_island);
            return sys;
        }

        SeedSlotSystem MakeSeedSlots()
        {
            var go = new GameObject("SeedSlotSystem");
            _spawned.Add(go);
            var sys = go.AddComponent<SeedSlotSystem>();
            sys.Config = _growth;
            return sys;
        }

        VillagerAgent MakeVillager(Vector3 pos, int seed)
        {
            var go = new GameObject($"Villager_{seed}");
            go.transform.position = pos;
            _spawned.Add(go);
            var v = go.AddComponent<VillagerAgent>();
            v.autoTick = false;
            v.Config = _proto;
            v.seed = seed;
            return v;
        }

        static void RunToCompletion(ExplorationSystem sys, int maxSteps = 400)
        {
            for (int i = 0; i < maxSteps && sys.Expeditions.Count > 0; i++)
            {
                sys.Tick(0.2f);
            }
        }

        static bool LogContains(string type, string fragment)
        {
            var records = GameEventLog.Records;
            for (int i = 0; i < records.Count; i++)
            {
                if (records[i].Type == type && records[i].Data.Contains(fragment))
                {
                    return true;
                }
            }
            return false;
        }

        // ---- Tests -------------------------------------------------------

        [Test]
        public void Expedition_DiscoversPoi_AppliesLedgerAndSeedSlotRewards()
        {
            var seeds = MakeSeedSlots();
            int slotsBefore = seeds.CountByState(SlotState.Open);
            var sys = MakeSystem();
            var poi = sys.AddPoi(PoiType.ResourceCache, new Vector3(12f, 0f, 0f));
            var party = new List<VillagerAgent> { MakeVillager(Vector3.zero, 1), MakeVillager(Vector3.zero, 2) };

            Assert.IsNotNull(sys.LaunchExpedition(poi, party));
            RunToCompletion(sys);

            Assert.IsTrue(poi.discovered, "the POI is revealed once the party surveys it");
            Assert.AreEqual(5, ResourceLedger.Get(ResourceType.Wood), "the cache's wood is deposited");
            Assert.AreEqual(slotsBefore + 2, seeds.CountByState(SlotState.Open),
                "two seed slots are unlocked beside the POI");
            Assert.IsTrue(LogContains("poi_discovered", "ResourceCache"));
        }

        [Test]
        public void Expedition_PullsPartyOffJobs_WhileAway_AndReturnsThem()
        {
            var sys = MakeSystem();
            var poi = sys.AddPoi(PoiType.ResourceCache, new Vector3(12f, 0f, 0f));
            var away = MakeVillager(Vector3.zero, 1);
            var home = MakeVillager(Vector3.zero, 2);

            sys.LaunchExpedition(poi, new List<VillagerAgent> { away });
            Assert.IsTrue(sys.IsAway(away), "a departed villager is marked away");
            Assert.IsFalse(sys.IsAway(home), "a villager left behind is not away");
            Assert.AreEqual(1, sys.AwayCount);

            // Job accounting skips the away villager: a roster over both assigns only one.
            Assert.AreEqual(1, JobAssigner.ApplyDefaultRoster(new List<VillagerAgent> { away, home }),
                "the away villager is not counted against the job roster");

            RunToCompletion(sys);
            Assert.IsFalse(sys.IsAway(away), "the villager is handed back on return");
            Assert.AreEqual(0, sys.AwayCount);
        }

        [Test]
        public void Expedition_TravelTime_ScalesWithDistance()
        {
            var sys = MakeSystem();
            var near = sys.AddPoi(PoiType.ResourceCache, new Vector3(10f, 0f, 0f));
            var far = sys.AddPoi(PoiType.ResourceCache, new Vector3(40f, 0f, 0f));

            var nearExp = sys.LaunchExpedition(near, new List<VillagerAgent> { MakeVillager(Vector3.zero, 1) });
            var farExp = sys.LaunchExpedition(far, new List<VillagerAgent> { MakeVillager(Vector3.zero, 2) });

            Assert.Greater(farExp.OneWayTravelSeconds, nearExp.OneWayTravelSeconds,
                "a farther POI takes longer to reach at the same speed");
            Assert.AreEqual(40f / _island.expeditionTravelSpeed, farExp.OneWayTravelSeconds, 1e-3f);
        }

        [Test]
        public void Expedition_ReturnsVillagersToHome()
        {
            var sys = MakeSystem();
            var poi = sys.AddPoi(PoiType.ResourceCache, new Vector3(12f, 0f, 0f));
            var v = MakeVillager(Vector3.zero, 1);

            sys.LaunchExpedition(poi, new List<VillagerAgent> { v });
            RunToCompletion(sys);

            Assert.Less(Vector3.Distance(v.transform.position, Vector3.zero), 1f,
                "the villager walks back to the muster point");
            Assert.IsTrue(LogContains("expedition", "return"));
        }

        [Test]
        public void Expedition_CaughtByDusk_IsStrandedInTheField()
        {
            var sys = MakeSystem();
            var poi = sys.AddPoi(PoiType.ResourceCache, new Vector3(30f, 0f, 0f));
            var v = MakeVillager(Vector3.zero, 1);

            sys.LaunchExpedition(poi, new List<VillagerAgent> { v });
            sys.Tick(0.2f); // barely underway, still far from the POI

            sys.RecallForDusk();

            Assert.AreEqual(0, sys.Expeditions.Count, "the expedition is removed at dusk");
            Assert.IsFalse(sys.IsAway(v), "the villager is handed to the normal dusk recall");
            Assert.IsFalse(poi.discovered, "an outbound expedition caught at dusk found nothing");
            Assert.Greater(Vector3.Distance(v.transform.position, Vector3.zero), 1f,
                "it is stranded out in the field, not back in camp");
            Assert.IsTrue(LogContains("expedition", "caught_out"));
        }
    }
}
