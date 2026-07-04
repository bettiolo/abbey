using System.Collections;
using System.Collections.Generic;
using Abbey.Beast;
using Abbey.Buildings;
using Abbey.Core;
using Abbey.Economy;
using Abbey.Hero;
using Abbey.Light;
using Abbey.Nightmares;
using Abbey.Reports;
using Abbey.Session;
using Abbey.Villagers;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Abbey.Tests.PlayMode
{
    /// <summary>
    /// P2-10: the FULL Phase 2 slice driven end-to-end in a programmatic world (no
    /// scene asset load — the world mirrors PrototypeSceneBuilder's wiring, but the
    /// bootstrap stays editor-only, so this constructs the same systems directly).
    ///
    /// The comprehensive loop test walks Day -> Dusk -> Night(=The First White Night)
    /// -> Dawn and asserts, in one run: a Salvager hauls wreck salvage into the
    /// ResourceLedger during the day; the dusk recall flags the one villager too far
    /// out; the White Night's monsters never enter Safe light; the fed hound answers
    /// the bell (the bond branch); a morning_report record is produced at dawn; and
    /// the verdict is the clean Win. Separate cases reach every other terminal
    /// outcome from the same integrated world: Bittersweet Survival (1..5 villagers),
    /// and each Loss (hero dead / fire out / all villagers lost).
    ///
    /// Deterministic: autoTick off everywhere, manual fixed-dt stepping, the clock
    /// advanced only by explicit phase crossings, bounded loops, seeded spawns.
    /// </summary>
    public class FirstWhiteNightSliceTests
    {
        const float Dt = 0.1f;

        readonly List<GameObject> _spawned = new List<GameObject>();
        readonly List<ScriptableObject> _assets = new List<ScriptableObject>();
        readonly List<VillagerAgent> _villagers = new List<VillagerAgent>();

        PrototypeConfig _proto;
        EconomyConfig _econ;
        JobsConfig _jobs;
        GameSessionConfig _sessionConfig;

        GameClock _clock;
        BellkeeperController _hero;
        HoundController _hound;
        LightSource _flame;
        NightmareDirector _director;
        FirstWhiteNightScenario _scenario;
        GameSession _session;
        MorningReportSystem _reports;
        VillagerJobAgent _salvager;
        VillagerAgent _farVillager;

        int _decidedCount;
        SessionSummary _lastDecided;
        int _reportCount;

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
            MonsterController.ClearRegistry();
            NightmareDirector.ResetStaticEvents();
            FirstWhiteNightScenario.ResetStaticEvents();
            GameSession.ResetStaticEvents();
            MorningReportSystem.ResetStaticEvents();
            EconomyConfig.ClearCache();
            JobsConfig.ClearCache();
            BuildingCatalog.ClearCache();
            GameSessionConfig.ClearCache();
            PrototypeConfig.ClearCache();
            _villagers.Clear();
            _decidedCount = 0;
            _reportCount = 0;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            _spawned.Clear();
            foreach (var building in Object.FindObjectsByType<Building>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(building.gameObject);
            }
            foreach (var a in _assets)
            {
                if (a != null) Object.DestroyImmediate(a);
            }
            _assets.Clear();
            _villagers.Clear();
            MonsterController.ClearRegistry();
            NightmareDirector.ResetStaticEvents();
            FirstWhiteNightScenario.ResetStaticEvents();
            GameSession.ResetStaticEvents();
            MorningReportSystem.ResetStaticEvents();
            SalvageSite.ClearRegistry();
            JobWorkPoint.ClearRegistry();
            ConstructionSite.ClearRegistry();
            Building.ClearRegistry();
            RestorationNode.ClearRegistry();
            BuildingPlacer.Clear();
            AbbeyState.Clear();
            ResourceLedger.Clear();
            DuskRecallSystem.Clear();
            DarknessEvaluator.Clear();
            EventBus.ResetAll();
            GameEventLog.Clear();
            EconomyConfig.ClearCache();
            JobsConfig.ClearCache();
            BuildingCatalog.ClearCache();
            GameSessionConfig.ClearCache();
            PrototypeConfig.ClearCache();
        }

        // ==================================================================
        // The full loop -> clean Win
        // ==================================================================

        [UnityTest]
        public IEnumerator Slice_FullLoop_SalvageRecallNightHoundReport_ReachesWin()
        {
            BuildWorld(ringVillagers: 8, includeFarVillager: true, includeSalvager: true);
            GameSession.OutcomeDecided += OnDecided;
            MorningReportSystem.ReportReady += OnReport;
            _session.Begin();

            Assert.AreEqual(DayPhase.Day, _clock.Phase);
            Assert.AreEqual(GameOutcome.Undecided, _session.Evaluate(),
                "the run is open while the White Night is still ahead");

            // ---- DAY: the hero feeds the hound; a salvager fills the ledger ----
            int startWood = ResourceLedger.Get(ResourceType.Wood);
            _hero.SetMoveTarget(_hound.transform.position);
            bool fed = false;
            for (int i = 0; i < 400 && (!fed || ResourceLedger.Get(ResourceType.Wood) <= startWood); i++)
            {
                StepActors(Dt);
                if (!fed && PlanarMotion.Distance(
                        _hero.transform.position, _hound.transform.position)
                    <= _proto.interactRange * 0.9f)
                {
                    fed = _hero.FeedHound(_hound);
                }
                if (i % 40 == 39)
                {
                    yield return null;
                }
            }
            Assert.IsTrue(fed, "the hero reached and fed the chained hound during the day");
            Assert.AreEqual(HoundState.Fed, _hound.State);
            Assert.Greater(ResourceLedger.Get(ResourceType.Wood), startWood,
                "day work: the salvager hauled wreck salvage into the ledger");
            Assert.IsTrue(LogContains("resource", "wood +"),
                "the deposit is event-logged as a resource record");
            Assert.AreEqual(DayPhase.Day, _clock.Phase, "the day work fits inside Day");

            // ---- DUSK: the scenario arms; the far villager is flagged too far ----
            var endangered = new List<GameObject>();
            EventBus.VillagerEndangered += go => endangered.Add(go);
            CrossIntoPhase(DayPhase.Dusk);
            Assert.IsTrue(_scenario.IsArmed, "the White Night is armed the dusk before");
            Assert.IsTrue(_director.Config.phase2NightsEnabled,
                "the director switched to the harder scripted mode");
            Assert.Contains(_farVillager.gameObject, endangered,
                "dusk recall flags the one villager who is too far out");

            // The bell recalls the camp and calls the hound (the bond branch).
            Assert.IsTrue(_hero.RingBell());
            Assert.IsTrue(LogContains("hound_answered_bell", null),
                "the fed hound answers the bell");
            Assert.AreEqual(HoundState.Following, _hound.State);

            // Step through dusk so the recalled far villager walks back into the light.
            for (int i = 0; i < 250 && DarknessEvaluator.Classify(
                     _farVillager.transform.position) != LightZone.Safe; i++)
            {
                StepActors(Dt);
                if (i % 40 == 39)
                {
                    yield return null;
                }
            }
            Assert.AreEqual(LightZone.Safe,
                DarknessEvaluator.Classify(_farVillager.transform.position),
                "the too-far villager is recalled into Safe light before night");

            // ---- NIGHT (The First White Night): monsters keep out of strong light ----
            CrossIntoPhase(DayPhase.Night);
            Assert.IsTrue(_scenario.HasBegun, "the White Night began");
            Assert.IsTrue(LogContains(FirstWhiteNightScenario.RecordType, "white_night_begins"));

            bool sawMonster = false;
            for (int i = 0; i < 150; i++)
            {
                StepActors(Dt);
                var monsters = _director.SpawnedMonsters;
                for (int m = 0; m < monsters.Count; m++)
                {
                    var monster = monsters[m];
                    if (monster != null && monster.IsAlive)
                    {
                        sawMonster = true;
                        Assert.AreNotEqual(LightZone.Safe,
                            DarknessEvaluator.Classify(monster.transform.position),
                            $"a White Night monster entered Safe light at step {i}");
                    }
                }
                if (i % 40 == 39)
                {
                    yield return null;
                }
            }
            Assert.IsTrue(sawMonster, "the White Night spawned at least one monster to face");
            Assert.IsFalse(LogContains("monster_attacked_villager", null),
                "everyone was tucked into Safe light: no villager was struck");

            // ---- DAWN: the morning report and the verdict ----
            CrossIntoPhase(DayPhase.Dawn);
            _session.Evaluate();

            Assert.AreEqual(1, _reportCount, "a morning_report is produced at dawn");
            Assert.IsTrue(_reports.HasReport);
            Assert.IsNotNull(FindLastRecord(MorningReportSystem.RecordType),
                "the shared log holds the dawn morning_report record");

            Assert.AreEqual(GameOutcome.Win, _session.Outcome);
            Assert.AreEqual(LossReason.None, _session.Reason);
            Assert.AreEqual(1, _decidedCount, "OutcomeDecided fires once");
            Assert.GreaterOrEqual(_lastDecided.VillagersAlive,
                _sessionConfig.villagerWinThreshold);
            Assert.IsTrue(_lastDecided.BellkeeperAlive);
            Assert.IsTrue(_lastDecided.AbbeyFireLit);
            Assert.IsTrue(LogContains(GameSession.RecordType, "outcome=Win"));
            yield return null;
        }

        // ==================================================================
        // Bittersweet Survival — cleared the White Night, but few remain
        // ==================================================================

        [UnityTest]
        public IEnumerator Slice_ReachesBittersweetSurvival_WithFewVillagers()
        {
            BuildWorld(ringVillagers: 3, includeFarVillager: false, includeSalvager: false);
            GameSession.OutcomeDecided += OnDecided;
            MorningReportSystem.ReportReady += OnReport;
            _session.Begin();

            CrossIntoPhase(DayPhase.Dusk);
            CrossIntoPhase(DayPhase.Night);
            for (int i = 0; i < 150; i++)
            {
                StepActors(Dt);
                if (i % 40 == 39)
                {
                    yield return null;
                }
            }
            foreach (var v in _villagers)
            {
                Assert.AreNotEqual(VillagerState.Dead, v.State);
                Assert.AreNotEqual(VillagerState.Missing, v.State);
            }

            CrossIntoPhase(DayPhase.Dawn);
            _session.Evaluate();

            Assert.AreEqual(1, _reportCount, "a morning_report is produced at dawn");
            Assert.AreEqual(GameOutcome.SurvivedBittersweet, _session.Outcome);
            Assert.AreEqual(LossReason.None, _session.Reason);
            Assert.AreEqual(3, _lastDecided.VillagersAlive);
            Assert.Less(_lastDecided.VillagersAlive, _sessionConfig.villagerWinThreshold);
            Assert.IsTrue(_lastDecided.BellkeeperAlive);
            Assert.IsTrue(_lastDecided.AbbeyFireLit);
            Assert.AreEqual(1, _decidedCount, "OutcomeDecided fires once");
            Assert.IsTrue(LogContains(GameSession.RecordType, "outcome=SurvivedBittersweet"));
            yield return null;
        }

        // ==================================================================
        // Loss paths (from the same integrated world)
        // ==================================================================

        [UnityTest]
        public IEnumerator Slice_LossWhenBellkeeperDies()
        {
            BuildWorld(ringVillagers: 8, includeFarVillager: false, includeSalvager: false);
            GameSession.OutcomeDecided += OnDecided;
            _session.Begin();

            _hero.TakeDamage(999f);
            Assert.IsFalse(_hero.IsAlive);
            _session.Evaluate();

            Assert.AreEqual(GameOutcome.Loss, _session.Outcome);
            Assert.AreEqual(LossReason.BellkeeperDead, _session.Reason);
            Assert.AreEqual(1, _decidedCount);
            Assert.IsTrue(LogContains(GameSession.RecordType, "outcome=Loss reason=BellkeeperDead"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Slice_LossWhenAbbeyFireGoesOut()
        {
            BuildWorld(ringVillagers: 8, includeFarVillager: false, includeSalvager: false);
            GameSession.OutcomeDecided += OnDecided;
            _session.Begin();

            _session.Evaluate();      // the flame is seen burning
            _flame.Extinguish();
            Assert.IsFalse(_flame.isLit);
            _session.Evaluate();

            Assert.AreEqual(GameOutcome.Loss, _session.Outcome);
            Assert.AreEqual(LossReason.AbbeyFireOut, _session.Reason);
            Assert.AreEqual(1, _decidedCount);
            Assert.IsTrue(LogContains(GameSession.RecordType, "outcome=Loss reason=AbbeyFireOut"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Slice_LossWhenAllVillagersLost()
        {
            BuildWorld(ringVillagers: 6, includeFarVillager: false, includeSalvager: false);
            GameSession.OutcomeDecided += OnDecided;
            _session.Begin();

            for (int i = 0; i < _villagers.Count; i++)
            {
                _villagers[i].ForceState(i % 2 == 0
                    ? VillagerState.Dead : VillagerState.Missing);
            }
            _session.Evaluate();

            Assert.AreEqual(GameOutcome.Loss, _session.Outcome);
            Assert.AreEqual(LossReason.VillagersLost, _session.Reason);
            Assert.AreEqual(1, _decidedCount);
            Assert.IsTrue(LogContains(GameSession.RecordType, "outcome=Loss reason=VillagersLost"));
            yield return null;
        }

        // ==================================================================
        // World construction (mirrors PrototypeSceneBuilder, no scene asset)
        // ==================================================================

        void BuildWorld(int ringVillagers, bool includeFarVillager, bool includeSalvager)
        {
            _proto = ScriptableObject.CreateInstance<PrototypeConfig>();
            _assets.Add(_proto);
            _proto.dayDurationSeconds = 30f;
            _proto.duskDurationSeconds = 30f;
            _proto.nightDurationSeconds = 40f;
            _proto.dawnDurationSeconds = 10f;
            _proto.edgeBandFraction = 0.3f;
            _proto.arrivalRadius = 0.3f;
            _proto.campfireRadius = 10f; // Safe within 7 of origin
            _proto.bellkeeperMaxHealth = 100f;
            _proto.bellkeeperMoveSpeed = 6f;
            _proto.interactRange = 2f;
            _proto.bellCooldownSeconds = 1f;
            _proto.feedCooldownSeconds = 0.2f;
            _proto.startingCarriedFood = 3;
            _proto.villagerWalkSpeed = 2f;
            _proto.villagerWorkDurationSeconds = 1f;
            _proto.villagerPickupDurationSeconds = 0.2f;
            // Keep the night's outcome controlled by shelter alone: no darkness harm.
            _proto.villagerFearPerSecondInDark = 0f;
            _proto.villagerFearPerSecondInEdge = 0f;
            _proto.villagerInjuredDarkSeconds = 1000f;
            _proto.villagerMissingDarkSeconds = 2000f;
            _proto.duskRecallEndangeredDistance = 12f;
            _proto.duskLateRecallDelaySeconds = 2f;
            _proto.bellRadius = 40f;              // the dusk pulse covers the far villager
            _proto.bellPulseMemorySeconds = 60f;
            _proto.bellRecallSpeedMultiplier = 1.5f;
            _proto.rescueFollowDistance = 1.5f;
            // Hound: one meal crosses Fed, and it answers the bell into Following
            // (same thresholds as FirstNightTests' proven fed branch).
            _proto.houndStartTrust = 0.1f;
            _proto.houndStartHunger = 0.9f;
            _proto.hungerStarvingThreshold = 0.8f;
            _proto.feedTrustGain = 0.5f;
            _proto.feedHungerRelief = 0.4f;
            _proto.trustFedThreshold = 0.5f;
            _proto.trustFollowThreshold = 0.95f;
            _proto.houndHungerPerSecond = 0f;
            _proto.houndMoveSpeed = 6f;
            _proto.houndAttackRange = 2f;
            _proto.houndAttackCooldownSeconds = 0.2f;
            // Monsters: born in the dark, never enter Safe.
            _proto.monsterMaxHealth = 50f;
            _proto.monsterMoveSpeed = 2.5f;
            _proto.monsterFleeSpeed = 3f;
            _proto.monsterLightTolerance = 0.15f;
            _proto.monsterSightRange = 60f;
            _proto.monsterSpawnMinRadius = 20f;
            _proto.monsterSpawnMaxRadius = 30f;
            _proto.monsterSpawnAttempts = 64;
            _proto.firstNightMonsterCount = 1;
            _proto.simulationSeed = 909;

            _econ = ScriptableObject.CreateInstance<EconomyConfig>();
            _assets.Add(_econ);
            _econ.baseStorageCapacity = 60;
            _econ.salvageSiteWood = 30;
            _econ.salvageSiteFood = 0;
            _econ.salvageSiteOil = 0;
            _econ.salvageSiteMedicine = 0;
            _econ.salvageYieldPerCycle = 2;
            _econ.salvageWorkDurationSeconds = 0.4f;

            _jobs = ScriptableObject.CreateInstance<JobsConfig>();
            _assets.Add(_jobs);
            _jobs.carryCapacity = 4;

            _sessionConfig = ScriptableObject.CreateInstance<GameSessionConfig>();
            _assets.Add(_sessionConfig);
            _sessionConfig.villagerWinThreshold = 6;
            _sessionConfig.whiteNightIndex = 1; // the first night IS the White Night here
            _sessionConfig.whiteNightSchedule = new[]
            {
                "0.15:pale_hound",
                "0.45:pale_hound",
                "0.75:whisper",
            };

            DarknessEvaluator.Config = _proto;
            DuskRecallSystem.Config = _proto;
            ResourceLedger.Config = _econ;

            var clockGO = Track(new GameObject("Clock"));
            _clock = clockGO.AddComponent<GameClock>();
            _clock.autoTick = false;
            _clock.Configure(_proto);

            var fireGO = Track(new GameObject("Campfire"));
            fireGO.transform.position = Vector3.zero;
            var fire = fireGO.AddComponent<LightSource>();
            fire.autoTick = false;
            fire.radius = _proto.campfireRadius;
            fire.strength = 1f;
            fire.fuelSeconds = -1f;

            var flameGO = Track(new GameObject("AbbeyFlame"));
            flameGO.transform.position = new Vector3(3f, 0f, 0f);
            _flame = flameGO.AddComponent<LightSource>();
            _flame.autoTick = false;
            _flame.sacred = true;
            _flame.radius = 6f;
            _flame.strength = 1f;
            _flame.fuelSeconds = -1f;
            _flame.isLit = true;

            var heroGO = Track(new GameObject("Bellkeeper"));
            heroGO.transform.position = Vector3.zero;
            _hero = heroGO.AddComponent<BellkeeperController>();
            _hero.autoTick = false;
            _hero.useDirectInput = false;
            _hero.Configure(_proto);

            var houndGO = Track(new GameObject("BlackHound"));
            houndGO.transform.position = new Vector3(8f, 0f, 0f);
            _hound = houndGO.AddComponent<HoundController>();
            _hound.autoTick = false;
            _hound.Configure(_proto);

            var directorGO = Track(new GameObject("Director"));
            directorGO.transform.position = Vector3.zero;
            _director = directorGO.AddComponent<NightmareDirector>();
            _director.autoTick = false;
            _director.monstersAutoTick = false;
            _director.Config = _proto;
            var anchorGO = Track(new GameObject("WreckAnchor"));
            anchorGO.transform.position = new Vector3(-20f, 0f, -20f);
            _director.shipwreckAnchor = anchorGO.transform;

            var scenarioGO = Track(new GameObject("Scenario"));
            _scenario = scenarioGO.AddComponent<FirstWhiteNightScenario>();
            _scenario.Config = _sessionConfig;
            _scenario.director = _director;
            _scenario.Clear();

            var reportsGO = Track(new GameObject("MorningReportSystem"));
            _reports = reportsGO.AddComponent<MorningReportSystem>();

            var sessionGO = Track(new GameObject("GameSession"));
            _session = sessionGO.AddComponent<GameSession>();
            _session.autoEvaluate = false;
            _session.Config = _sessionConfig;
            _session.bellkeeper = _hero;
            _session.abbeyFlame = _flame;
            _session.Clear();

            // Storage pile + salvage node for the economy loop.
            var pileGO = Track(new GameObject("StoragePile"));
            pileGO.transform.position = Vector3.zero;
            pileGO.AddComponent<StoragePile>();

            var siteGO = Track(new GameObject("SalvageSite"));
            siteGO.transform.position = new Vector3(0f, 0f, 4f); // inside Safe
            var site = siteGO.AddComponent<SalvageSite>();
            site.Configure(_econ);

            for (int i = 0; i < ringVillagers; i++)
            {
                float angle = i / (float)ringVillagers * Mathf.PI * 2f;
                var pos = new Vector3(Mathf.Cos(angle) * 4f, 0f, Mathf.Sin(angle) * 4f);
                _villagers.Add(CreateVillager($"Villager_{i:D2}", pos, seed: i));
            }

            if (includeSalvager)
            {
                var v = CreateVillager("Villager_Salvager", new Vector3(1f, 0f, 1f), seed: 50);
                _salvager = v.gameObject.AddComponent<VillagerJobAgent>();
                _salvager.autoTick = false;
                _salvager.Config = _jobs;
                _salvager.SetJob(VillagerJob.Salvager);
                _villagers.Add(v);
            }

            if (includeFarVillager)
            {
                _farVillager = CreateVillager("Villager_Far", new Vector3(30f, 0f, 0f), seed: 60);
                _villagers.Add(_farVillager);
            }
        }

        VillagerAgent CreateVillager(string name, Vector3 pos, int seed)
        {
            var go = Track(new GameObject(name));
            go.transform.position = pos;
            var v = go.AddComponent<VillagerAgent>();
            v.autoTick = false;
            v.Config = _proto;
            v.seed = seed;
            return v;
        }

        GameObject Track(GameObject go)
        {
            _spawned.Add(go);
            return go;
        }

        void OnDecided(SessionSummary s)
        {
            _decidedCount++;
            _lastDecided = s;
        }

        void OnReport(MorningReportData d, string prose)
        {
            _reportCount++;
        }

        // ------------------------------------------------------------------
        // Deterministic stepping (actors only; the clock is crossed explicitly)
        // ------------------------------------------------------------------

        void StepActors(float dt)
        {
            _hero.Tick(dt);
            _hound.Tick(dt);
            _director.Tick(dt);
            if (_salvager != null)
            {
                _salvager.Tick(dt);
            }
            for (int i = 0; i < _villagers.Count; i++)
            {
                if (_villagers[i] != null)
                {
                    _villagers[i].Tick(dt);
                }
            }
            var monsters = _director.SpawnedMonsters;
            for (int i = 0; i < monsters.Count; i++)
            {
                var m = monsters[i];
                if (m != null && m.IsAlive && m.gameObject.activeSelf)
                {
                    m.Tick(dt);
                }
            }
        }

        void CrossIntoPhase(DayPhase expected)
        {
            float remaining = _clock.GetPhaseDuration(_clock.Phase) - _clock.TimeInPhase;
            _clock.Tick(remaining + 0.001f);
            Assert.AreEqual(expected, _clock.Phase,
                $"expected the boundary tick to land in {expected}");
        }

        static bool LogContains(string type, string dataFragment)
        {
            var records = GameEventLog.Records;
            for (int i = 0; i < records.Count; i++)
            {
                if (records[i].Type != type)
                {
                    continue;
                }
                if (dataFragment == null || records[i].Data.Contains(dataFragment))
                {
                    return true;
                }
            }
            return false;
        }

        static GameEventLog.Record? FindLastRecord(string type)
        {
            var records = GameEventLog.Records;
            for (int i = records.Count - 1; i >= 0; i--)
            {
                if (records[i].Type == type)
                {
                    return records[i];
                }
            }
            return null;
        }
    }
}
