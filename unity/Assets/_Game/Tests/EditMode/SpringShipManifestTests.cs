using System.Collections.Generic;
using Abbey.Core;
using Abbey.Economy;
using Abbey.Island;
using Abbey.Morale;
using Abbey.Reports;
using Abbey.Session;
using Abbey.Villagers;
using Abbey.World;
using NUnit.Framework;
using UnityEngine;

namespace Abbey.Tests.EditMode
{
    /// <summary>
    /// EditMode coverage for the Phase 3 campaign close (P3-14): four calendar chapters,
    /// the three-part spring-ship manifest, the launch-window resolution and the
    /// end-summary chronicle. Worlds are built programmatically (no scene): the
    /// GameSessionConfig is injected, the calendar is driven by a manual GameClock, and the
    /// manifest reads the live ResourceLedger + ArrivalSystem rollups. Deterministic — same
    /// inputs, same verdict.
    /// </summary>
    public class SpringShipManifestTests
    {
        readonly List<GameObject> _spawned = new List<GameObject>();
        readonly List<Object> _assets = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            ClearStatics();
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
            ClearStatics();
        }

        static void ClearStatics()
        {
            EventBus.ResetAll();
            GameEventLog.Clear();
            ResourceLedger.Clear();
            DuskRecallSystem.Clear();
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

        EconomyConfig MakeEconomy()
        {
            var econ = ScriptableObject.CreateInstance<EconomyConfig>();
            econ.baseStorageCapacity = 1000;
            econ.grainToFoodRatio = 2;
            ResourceLedger.Config = econ;
            _assets.Add(econ);
            return econ;
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

        ChapterSystem MakeChapters(GameSessionConfig cfg)
        {
            var go = new GameObject("ChapterSystem");
            _spawned.Add(go);
            var chapters = go.AddComponent<ChapterSystem>();
            chapters.Configure(cfg);
            return chapters;
        }

        SpringShipScenario MakeShip(GameSessionConfig cfg, GameSession session = null)
        {
            var go = new GameObject("SpringShipScenario");
            _spawned.Add(go);
            var ship = go.AddComponent<SpringShipScenario>();
            ship.persistOnLaunch = false;
            ship.session = session;
            ship.Configure(cfg);
            return ship;
        }

        ArrivalSystem MakeArrivals(float trust)
        {
            var island = ScriptableObject.CreateInstance<IslandConfig>();
            island.stayMinTier = TrustTier.Wary;
            island.volunteerMinTier = TrustTier.Trusting;
            _assets.Add(island);

            var pcfg = ScriptableObject.CreateInstance<PressuresConfig>();
            for (int i = 0; i < pcfg.channels.Count; i++)
            {
                if (pcfg.channels[i].id == PressureId.Trust)
                {
                    pcfg.channels[i].baseline = trust;
                }
            }
            _assets.Add(pcfg);
            var pgo = new GameObject("PressureSystem");
            _spawned.Add(pgo);
            var pressures = pgo.AddComponent<PressureSystem>();
            pressures.Configure(pcfg);

            var go = new GameObject("ArrivalSystem");
            _spawned.Add(go);
            var arrivals = go.AddComponent<ArrivalSystem>();
            arrivals.spawnVillagers = false;
            arrivals.Configure(island);
            return arrivals;
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

        static void AdvanceToDay(GameClock clock, int targetDay)
        {
            int safety = 10000;
            while (clock.DayNumber < targetDay && safety-- > 0)
            {
                clock.Tick(0.5f);
            }
        }

        static int LogCount(string type, string dataFragment)
        {
            int n = 0;
            var records = GameEventLog.Records;
            for (int i = 0; i < records.Count; i++)
            {
                if (records[i].Type == type
                    && (dataFragment == null || records[i].Data.Contains(dataFragment)))
                {
                    n++;
                }
            }
            return n;
        }

        // ------------------------------------------------------------------
        // Chapters
        // ------------------------------------------------------------------

        [Test]
        public void Chapters_AdvanceThroughFourNames_AtSeasonBounds_AndAreLogged()
        {
            var cfg = MakeConfig();
            var clock = MakeClock();
            MakeSeason(1); // one day per season
            var chapters = MakeChapters(cfg);

            var seen = new List<string>();
            ChapterSystem.ChapterChanged += (i, name) => seen.Add(name);

            Assert.AreEqual(0, chapters.CurrentChapterIndex, "opens on The Wreck");
            Assert.AreEqual("The Wreck", chapters.CurrentChapterName);

            AdvanceToDay(clock, 2);
            Assert.AreEqual("The Meadow", chapters.CurrentChapterName);
            AdvanceToDay(clock, 3);
            Assert.AreEqual("The Long Rain", chapters.CurrentChapterName);
            AdvanceToDay(clock, 4);
            Assert.AreEqual("The First White Night", chapters.CurrentChapterName);

            // Each chapter beginning is logged with morning-report flavour.
            Assert.AreEqual(1, LogCount(ChapterSystem.RecordType, "The Wreck"));
            Assert.AreEqual(1, LogCount(ChapterSystem.RecordType, "The Meadow"));
            Assert.AreEqual(1, LogCount(ChapterSystem.RecordType, "The Long Rain"));
            Assert.AreEqual(1, LogCount(ChapterSystem.RecordType, "The First White Night"));
            CollectionAssert.Contains(seen, "The First White Night");
        }

        // ------------------------------------------------------------------
        // Manifest accounting — three independent parts
        // ------------------------------------------------------------------

        [Test]
        public void Manifest_EachPart_EvaluatesIndependently_AgainstConfig()
        {
            var cfg = MakeConfig();
            MakeEconomy();
            var arrivals = MakeArrivals(0.9f); // devoted: newcomers volunteer
            var ship = MakeShip(cfg);

            // Nothing yet: all three parts short.
            var m0 = ship.EvaluateManifest();
            Assert.IsFalse(m0.SettlersReady);
            Assert.IsFalse(m0.ProvisionsReady);
            Assert.IsFalse(m0.HullReady);
            Assert.IsFalse(m0.Complete);

            // Settlers only: three volunteers meet the settler threshold.
            arrivals.ReceiveArrivals(ArrivalClass.Survivor, 3, ArrivalChannel.Passive, Vector3.zero);
            var m1 = ship.EvaluateManifest();
            Assert.IsTrue(m1.SettlersReady, "3 willing sailors meet the settler threshold");
            Assert.AreEqual(3, m1.WillingSailors);
            Assert.IsFalse(m1.ProvisionsReady);
            Assert.IsFalse(m1.HullReady);
            Assert.IsFalse(m1.Complete);

            // Provisions only.
            ResourceLedger.Add(ResourceType.Food, 24, "test");
            ResourceLedger.Add(ResourceType.Candles, 6, "test");
            ResourceLedger.Add(ResourceType.Tools, 3, "test");
            var m2 = ship.EvaluateManifest();
            Assert.IsTrue(m2.ProvisionsReady, "the provision stockpile is met");
            Assert.IsFalse(m2.HullReady);
            Assert.IsFalse(m2.Complete);

            // Hull last: now all three, and the manifest is complete.
            ship.SetHullComplete(true);
            var m3 = ship.EvaluateManifest();
            Assert.IsTrue(m3.HullReady);
            Assert.IsTrue(m3.Complete, "all three parts satisfied ⇒ manifest complete");
        }

        [Test]
        public void Manifest_Provisions_FoldGrainIntoFood()
        {
            var cfg = MakeConfig();
            cfg.manifestProvisions = new List<ResourceStack>
            {
                new ResourceStack(ResourceType.Food, 24),
            };
            var econ = MakeEconomy(); // grainToFoodRatio = 2
            MakeArrivals(0.9f);
            var ship = MakeShip(cfg);

            // No raw Food, but 12 Grain mills into 24 Food at the 1:2 ratio.
            ResourceLedger.Add(ResourceType.Grain, 12, "harvest");
            Assert.AreEqual(24, econ.GrainToFood(12), "sanity: grain conversion");

            Assert.IsTrue(ship.EvaluateManifest().ProvisionsReady,
                "grain counts toward the food provision at the config ratio");
        }

        // ------------------------------------------------------------------
        // Launch-window resolution
        // ------------------------------------------------------------------

        [Test]
        public void IncompleteManifest_AtWindow_RecordsShipNeverSailed_NoWin()
        {
            var cfg = MakeConfig();
            MakeEconomy();
            MakeArrivals(0.1f); // broken trust: no volunteers, no willing sailors
            var session = MakeSession(cfg);
            var ship = MakeShip(cfg, session);

            var clock = MakeClock();
            MakeSeason(1); // day 5 => year 2, Spring (launch window opens)
            AdvanceToDay(clock, 5);

            Assert.IsTrue(ship.LaunchWindowOpen, "year 2 spring opens the window");
            Assert.IsFalse(ship.HasSailed, "an incomplete manifest does not sail");
            Assert.GreaterOrEqual(LogCount(SpringShipScenario.RecordType, "ship_did_not_sail"), 1,
                "the ship-did-not-sail beat is recorded");
            Assert.AreEqual(GameOutcome.Undecided, session.Outcome,
                "a missing manifest is NOT a loss — the year rolls on");
        }

        [Test]
        public void CompleteManifest_AtWindow_ShipSails_LatchesWin_AndOutcomeRoundTrips()
        {
            var cfg = MakeConfig();
            MakeEconomy();
            var arrivals = MakeArrivals(0.9f);
            var session = MakeSession(cfg);
            var ship = MakeShip(cfg, session);

            // Complete all three manifest parts.
            arrivals.ReceiveArrivals(ArrivalClass.Survivor, 4, ArrivalChannel.Passive, Vector3.zero);
            ResourceLedger.Add(ResourceType.Food, 24, "test");
            ResourceLedger.Add(ResourceType.Candles, 6, "test");
            ResourceLedger.Add(ResourceType.Tools, 3, "test");
            ship.SetHullComplete(true);

            var clock = MakeClock();
            MakeSeason(1);
            AdvanceToDay(clock, 5); // year 2 spring

            Assert.IsTrue(ship.HasSailed, "a complete manifest at the window sails the ship");
            Assert.AreEqual(GameOutcome.ShipSailed, session.Outcome, "the campaign win latches");
            Assert.GreaterOrEqual(LogCount(SpringShipScenario.RecordType, "launched result=ShipSailed"), 1);

            var outcome = session.LastSummary.Campaign;
            Assert.IsNotNull(outcome, "the summary carries the CampaignOutcome");
            Assert.AreEqual(CampaignResult.ShipSailed, outcome.Result);
            Assert.IsTrue(outcome.manifestComplete);
            Assert.AreEqual(4, outcome.sailedCount, "the willing sailors board");

            // JSON round-trips (the Phase 4 carryover payload).
            string json = outcome.ToJson();
            var restored = CampaignOutcome.FromJson(json);
            Assert.IsNotNull(restored);
            Assert.AreEqual(outcome.resultCode, restored.resultCode);
            Assert.AreEqual(outcome.sailedCount, restored.sailedCount);
            Assert.AreEqual(outcome.willingSailors, restored.willingSailors);
            Assert.AreEqual(outcome.chronicle, restored.chronicle);
        }

        [Test]
        public void WinterCollapse_IsADistinctLoss_WhenCampaignEnabled()
        {
            var cfg = MakeConfig();
            var clock = MakeClock();
            var season = MakeSeason(1);
            AdvanceToDay(clock, 4); // day 4 => Winter of year 1
            Assert.AreEqual(Season.Winter, season.CurrentSeason);

            var session = MakeSession(cfg);
            // A settlement that was present and is now wiped.
            var v = new GameObject("Villager");
            _spawned.Add(v);
            var agent = v.AddComponent<Abbey.Villagers.VillagerAgent>();
            agent.autoTick = false;
            agent.ForceState(Abbey.Villagers.VillagerState.Dead);

            Assert.AreEqual(GameOutcome.Loss, session.Evaluate());
            Assert.AreEqual(LossReason.WinterCollapse, session.Reason,
                "a winter wipe in campaign mode is the WinterCollapse loss");
        }

        // ------------------------------------------------------------------
        // End-summary chronicle over a scripted log (P2-07 style accuracy)
        // ------------------------------------------------------------------

        [Test]
        public void EndSummary_NamesTheScriptedFacts()
        {
            // A scripted run: two chapters, a food law, the hound turned Guardian, one death.
            GameEventLog.Append(ChapterSystem.RecordType, "chapter_begins index=0 name=\"The Wreck\" — x");
            GameEventLog.Append("decree", "group=Food tag=food_equal day=1");
            GameEventLog.Append("hound_evolved", "Unevolved -> Guardian");
            GameEventLog.Append("villager_died", "Aldous");
            GameEventLog.Append("burial", "law=full_rites deceased=Aldous tag=grave_marked");
            GameEventLog.Append(ChapterSystem.RecordType, "chapter_begins index=3 name=\"The First White Night\" — y");
            GameEventLog.Append("spring_ship", "launched result=ShipSailed sailed=5 stayed=8 volunteers=2 departures=3");

            var data = EndSummary.Build(GameEventLog.Records);
            Assert.Contains("food_equal", data.LawsEnacted, "the enacted law is captured");
            Assert.AreEqual(1, data.Deaths, "the death is counted");
            Assert.AreEqual("Guardian", data.HoundPath, "the hound path is captured");
            Assert.AreEqual(2, data.Chapters.Count, "both chapter beats are captured");
            Assert.IsTrue(data.ShipLaunched);
            Assert.AreEqual("ShipSailed", data.ShipResult);

            string prose = EndSummary.Compose(data);
            StringAssert.Contains("food_equal", prose, "the chronicle names the real law");
            StringAssert.Contains("Guardian", prose, "the chronicle names the hound's real path");
            StringAssert.Contains("The Wreck", prose, "the chronicle names the chapters lived");
            StringAssert.Contains("sailed", prose, "the chronicle records the launch");
        }
    }
}
