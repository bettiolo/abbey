using System.Collections;
using System.Collections.Generic;
using System.IO;
using Abbey.Buildings;
using Abbey.Core;
using Abbey.Economy;
using Abbey.Island;
using Abbey.Morale;
using Abbey.Session;
using Abbey.Villagers;
using Abbey.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Abbey.Tests.PlayMode
{
    /// <summary>
    /// The Phase 3 campaign win under the play loop (P3-14), built programmatically (no
    /// scenes): a real ship <see cref="ConstructionSite"/> is completed through the normal
    /// delivery + work economy (the hull/rigging manifest part), provisions and willing
    /// sailors are granted, and the manual clock is fast-forwarded to the following spring's
    /// launch window — at which the ship sails, <see cref="GameSession"/> latches
    /// <see cref="GameOutcome.ShipSailed"/>, and the <see cref="CampaignOutcome"/> JSON
    /// round-trips through disk. The winter-collapse branch produces its distinct loss.
    /// </summary>
    public class SpringShipWinPlayModeTests
    {
        readonly List<GameObject> _spawned = new List<GameObject>();
        readonly List<Object> _assets = new List<Object>();
        string _tempPath;

        [SetUp]
        public void SetUp()
        {
            ClearStatics();
            _tempPath = Path.Combine(Application.temporaryCachePath, "campaign_outcome_test.json");
            if (File.Exists(_tempPath)) File.Delete(_tempPath);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            _spawned.Clear();
            foreach (var a in _assets)
            {
                if (a != null) Object.DestroyImmediate(a);
            }
            _assets.Clear();
            if (!string.IsNullOrEmpty(_tempPath) && File.Exists(_tempPath)) File.Delete(_tempPath);
            ClearStatics();
        }

        static void ClearStatics()
        {
            EventBus.ResetAll();
            GameEventLog.Clear();
            ResourceLedger.Clear();
            DuskRecallSystem.Clear();
            ConstructionSite.ClearRegistry();
            GameSession.ResetStaticEvents();
            ChapterSystem.ResetStaticEvents();
            SpringShipScenario.ResetStaticEvents();
            GameSessionConfig.ClearCache();
            SeasonConfig.ClearCache();
            PrototypeConfig.ClearCache();
            EconomyConfig.ClearCache();
            IslandConfig.ClearCache();
            PressuresConfig.ClearCache();
        }

        // ---- Builders ----------------------------------------------------

        GameSessionConfig MakeConfig()
        {
            var cfg = ScriptableObject.CreateInstance<GameSessionConfig>();
            cfg.phase3CampaignEnabled = true;
            cfg.springLaunchYear = 2;
            cfg.manifestSettlers = 3;
            cfg.manifestProvisions = new List<ResourceStack>
            {
                new ResourceStack(ResourceType.Food, 24),
                new ResourceStack(ResourceType.Candles, 6),
                new ResourceStack(ResourceType.Tools, 3),
            };
            cfg.shipBuildingId = "spring_ship_t1";
            _assets.Add(cfg);
            return cfg;
        }

        void MakeEconomy()
        {
            var econ = ScriptableObject.CreateInstance<EconomyConfig>();
            econ.baseStorageCapacity = 1000;
            econ.grainToFoodRatio = 2;
            ResourceLedger.Config = econ;
            _assets.Add(econ);
        }

        GameClock MakeClock()
        {
            var proto = ScriptableObject.CreateInstance<PrototypeConfig>();
            proto.dayDurationSeconds = 1f;
            proto.duskDurationSeconds = 1f;
            proto.nightDurationSeconds = 1f;
            proto.dawnDurationSeconds = 1f;
            _assets.Add(proto);
            var go = new GameObject("GameClock");
            _spawned.Add(go);
            var clock = go.AddComponent<GameClock>();
            clock.autoTick = false;
            clock.Configure(proto);
            return clock;
        }

        SeasonSystem MakeSeason(int daysPerSeason)
        {
            var cfg = ScriptableObject.CreateInstance<SeasonConfig>();
            cfg.daysPerSeason = daysPerSeason;
            _assets.Add(cfg);
            var go = new GameObject("SeasonSystem");
            _spawned.Add(go);
            var season = go.AddComponent<SeasonSystem>();
            season.Configure(cfg);
            return season;
        }

        PressureSystem MakeTrust(float trust)
        {
            var pcfg = ScriptableObject.CreateInstance<PressuresConfig>();
            for (int i = 0; i < pcfg.channels.Count; i++)
            {
                if (pcfg.channels[i].id == PressureId.Trust)
                {
                    pcfg.channels[i].baseline = trust;
                }
            }
            _assets.Add(pcfg);
            var go = new GameObject("PressureSystem");
            _spawned.Add(go);
            var sys = go.AddComponent<PressureSystem>();
            sys.Configure(pcfg);
            return sys;
        }

        ArrivalSystem MakeArrivals()
        {
            var island = ScriptableObject.CreateInstance<IslandConfig>();
            island.passiveArrivalIntervalDays = 0;
            island.stayMinTier = TrustTier.Wary;
            island.volunteerMinTier = TrustTier.Trusting;
            _assets.Add(island);
            var go = new GameObject("ArrivalSystem");
            _spawned.Add(go);
            var arrivals = go.AddComponent<ArrivalSystem>();
            arrivals.spawnVillagers = false;
            arrivals.Configure(island);
            return arrivals;
        }

        ConstructionSite MakeShipSite()
        {
            var type = new BuildingType
            {
                id = "spring_ship_t1",
                displayName = "Spring Ship",
                footprint = new Vector2(6f, 3f),
                cost = new List<ResourceStack>
                {
                    new ResourceStack(ResourceType.Wood, 20),
                    new ResourceStack(ResourceType.Tools, 4),
                    new ResourceStack(ResourceType.Wool, 8),
                },
                buildWorkSeconds = 5f,
                function = FunctionKind.Ship,
            };
            var go = new GameObject("SpringShipSite");
            _spawned.Add(go);
            var site = go.AddComponent<ConstructionSite>();
            site.Initialize(type);
            return site;
        }

        GameSession MakeSession(GameSessionConfig cfg)
        {
            var go = new GameObject("GameSession");
            _spawned.Add(go);
            var s = go.AddComponent<GameSession>();
            s.autoEvaluate = false;
            s.Config = cfg;
            s.Clear();
            return s;
        }

        SpringShipScenario MakeShip(GameSessionConfig cfg, GameSession session, ConstructionSite site)
        {
            var go = new GameObject("SpringShipScenario");
            _spawned.Add(go);
            var ship = go.AddComponent<SpringShipScenario>();
            ship.persistOnLaunch = false;
            ship.session = session;
            ship.shipSite = site;
            ship.Configure(cfg);
            return ship;
        }

        static void AdvanceToDay(GameClock clock, int targetDay)
        {
            int safety = 10000;
            while (clock.DayNumber < targetDay && safety-- > 0)
            {
                clock.Tick(0.5f);
            }
        }

        static bool LogHas(string type, string fragment)
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

        // ------------------------------------------------------------------
        // Full campaign win
        // ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator FullCampaign_CompletesShip_ReachesSpring_Sails_AndWritesOutcome()
        {
            var cfg = MakeConfig();
            MakeEconomy();
            var pressures = MakeTrust(0.9f); // devoted: newcomers volunteer
            var arrivals = MakeArrivals();
            var session = MakeSession(cfg);
            var site = MakeShipSite();
            var ship = MakeShip(cfg, session, site);
            yield return null;

            // --- Hull/rigging: complete the ship through the real delivery + work economy.
            ResourceLedger.Add(ResourceType.Wood, 20, "salvage");
            ResourceLedger.Add(ResourceType.Tools, 4 + 3, "smithy"); // ship + provisions
            ResourceLedger.Add(ResourceType.Wool, 8, "pasture");
            site.DeliverResource(ResourceType.Wood, 20);
            site.DeliverResource(ResourceType.Tools, 4);
            site.DeliverResource(ResourceType.Wool, 8);
            Assert.IsFalse(site.NeedsMaterials, "all ship materials delivered");
            site.ApplyWork(10f);
            Assert.IsTrue(site.IsComplete, "the ship reconstruction finished");
            Assert.IsTrue(ship.HullReady, "hull/rigging manifest part satisfied");
            yield return null;

            // --- Settlers: two volunteers, then two who intend to sail away (departures).
            arrivals.ReceiveArrivals(ArrivalClass.Survivor, 2, ArrivalChannel.Passive, Vector3.zero);
            Assert.AreEqual(2, arrivals.VolunteeredCount);
            pressures.Configure(RebaselineTrust(0.1f)); // broken trust: newcomers refuse + record departures
            arrivals.ReceiveArrivals(ArrivalClass.Survivor, 2, ArrivalChannel.Passive, Vector3.zero);
            Assert.AreEqual(2, arrivals.DepartureIntents.Count, "the roster respects departure intents");

            // --- Provisions.
            ResourceLedger.Add(ResourceType.Food, 24, "harvest");
            ResourceLedger.Add(ResourceType.Candles, 6, "chandler");

            var manifest = ship.EvaluateManifest();
            Assert.IsTrue(manifest.Complete, "all three manifest parts satisfied");
            Assert.AreEqual(4, manifest.WillingSailors, "volunteers + departures crew the ship");
            yield return null;

            // --- Fast-forward to the following spring: the ship sails.
            var clock = MakeClock();
            MakeSeason(1); // day 5 => year 2 spring
            AdvanceToDay(clock, 5);
            yield return null;

            Assert.IsTrue(ship.HasSailed, "the ship sails at the year-2 spring window");
            Assert.AreEqual(GameOutcome.ShipSailed, session.Outcome, "the campaign win latches");
            Assert.IsTrue(LogHas(SpringShipScenario.RecordType, "launched result=ShipSailed"));

            var outcome = session.LastSummary.Campaign;
            Assert.IsNotNull(outcome);
            Assert.AreEqual(4, outcome.sailedCount, "who-sails roster: volunteers + departures aboard");
            Assert.AreEqual(2, outcome.volunteeredCount);
            Assert.AreEqual(2, outcome.leftCount);

            // --- CampaignOutcome JSON round-trips through disk (Phase 4 carryover).
            string written = outcome.Save(_tempPath);
            Assert.IsNotNull(written);
            Assert.IsTrue(File.Exists(_tempPath));
            var loaded = CampaignOutcome.Load(_tempPath);
            Assert.IsNotNull(loaded);
            Assert.AreEqual(outcome.resultCode, loaded.resultCode);
            Assert.AreEqual(outcome.sailedCount, loaded.sailedCount);
            Assert.AreEqual(outcome.chronicle, loaded.chronicle);
        }

        PressuresConfig RebaselineTrust(float trust)
        {
            var pcfg = ScriptableObject.CreateInstance<PressuresConfig>();
            for (int i = 0; i < pcfg.channels.Count; i++)
            {
                if (pcfg.channels[i].id == PressureId.Trust)
                {
                    pcfg.channels[i].baseline = trust;
                }
            }
            _assets.Add(pcfg);
            return pcfg;
        }

        // ------------------------------------------------------------------
        // Winter collapse
        // ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator WinterCollapse_WipedBeforeSpring_IsALoss()
        {
            var cfg = MakeConfig();
            var session = MakeSession(cfg);
            var clock = MakeClock();
            var season = MakeSeason(1);
            AdvanceToDay(clock, 4); // Winter of year 1
            Assert.AreEqual(Season.Winter, season.CurrentSeason);
            yield return null;

            // The settlement was present and is now wiped out.
            var go = new GameObject("Villager");
            _spawned.Add(go);
            var agent = go.AddComponent<VillagerAgent>();
            agent.autoTick = false;
            agent.ForceState(VillagerState.Dead);

            Assert.AreEqual(GameOutcome.Loss, session.Evaluate());
            Assert.AreEqual(LossReason.WinterCollapse, session.Reason,
                "a winter wipe before the spring tide is the WinterCollapse loss");
            yield return null;
        }
    }
}
