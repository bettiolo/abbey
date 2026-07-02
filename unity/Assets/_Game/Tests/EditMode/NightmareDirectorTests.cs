using System.Collections.Generic;
using Abbey.Core;
using Abbey.Light;
using Abbey.Nightmares;
using Abbey.Villagers;
using NUnit.Framework;
using UnityEngine;

namespace Abbey.Tests.EditMode
{
    /// <summary>
    /// Nightmare director: deterministic dark-ring spawn point selection (monsters
    /// are born outside all light) and the BeginNight/EndNight bookkeeping.
    /// </summary>
    public class NightmareDirectorTests
    {
        readonly List<GameObject> _spawned = new List<GameObject>();
        PrototypeConfig _config;

        [SetUp]
        public void SetUp()
        {
            EventBus.ResetAll();
            GameEventLog.Clear();
            DarknessEvaluator.Clear();
            DuskRecallSystem.Clear();
            MonsterController.ClearRegistry();
            NightmareDirector.ResetStaticEvents();

            _config = ScriptableObject.CreateInstance<PrototypeConfig>();
            _config.edgeBandFraction = 0.3f;
            _config.monsterSpawnMinRadius = 20f;
            _config.monsterSpawnMaxRadius = 30f;
            _config.monsterSpawnAttempts = 64;
            _config.simulationSeed = 999;
            _config.firstNightMonsterCount = 2;
            _config.monsterMaxHealth = 55f; // non-default on purpose: proves config injection
            DarknessEvaluator.Config = _config;
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
            MonsterController.ClearRegistry();
            DuskRecallSystem.Clear();
            DarknessEvaluator.Clear();
            EventBus.ResetAll();
            GameEventLog.Clear();
            NightmareDirector.ResetStaticEvents();
        }

        LightSource SpawnLight(Vector3 position, float radius)
        {
            var go = new GameObject($"TestLight_{_spawned.Count}");
            _spawned.Add(go);
            go.transform.position = position;
            var source = go.AddComponent<LightSource>();
            source.radius = radius;
            source.strength = 1f;
            source.fuelSeconds = -1f;
            source.autoTick = false;
            DarknessEvaluator.Register(source); // defensive, mirrors OnEnable
            return source;
        }

        [Test]
        public void FindDarkSpawnPoint_LandsOutsideLight_OnTheRing()
        {
            SpawnLight(Vector3.zero, radius: 10f);

            Vector3? point = NightmareDirector.FindDarkSpawnPoint(
                Vector3.zero, 20f, 30f, seed: 999, attempts: 64);

            Assert.IsTrue(point.HasValue);
            Assert.AreEqual(LightZone.Dark, DarknessEvaluator.Classify(point.Value),
                "monsters are born outside all light");
            float dist = PlanarMotion.Distance(Vector3.zero, point.Value);
            Assert.That(dist, Is.InRange(20f - 1e-3f, 30f + 1e-3f), "point sits on the spawn ring");
        }

        [Test]
        public void FindDarkSpawnPoint_IsDeterministicForASeed()
        {
            SpawnLight(new Vector3(5f, 0f, 0f), radius: 8f);

            Vector3? a = NightmareDirector.FindDarkSpawnPoint(Vector3.zero, 20f, 30f, 1234, 64);
            Vector3? b = NightmareDirector.FindDarkSpawnPoint(Vector3.zero, 20f, 30f, 1234, 64);
            Vector3? c = NightmareDirector.FindDarkSpawnPoint(Vector3.zero, 20f, 30f, 4321, 64);

            Assert.IsTrue(a.HasValue && b.HasValue && c.HasValue);
            Assert.AreEqual(a.Value, b.Value, "same seed, same point");
            Assert.AreNotEqual(a.Value, c.Value, "different seed, different draw");
        }

        [Test]
        public void FindDarkSpawnPoint_WhenTheWholeRingIsLit_ReturnsNull()
        {
            SpawnLight(Vector3.zero, radius: 100f); // Safe out to 70, Edge to 100

            Vector3? point = NightmareDirector.FindDarkSpawnPoint(
                Vector3.zero, 20f, 30f, seed: 999, attempts: 64);

            Assert.IsFalse(point.HasValue, "no dark point exists on the ring");
        }

        [Test]
        public void BeginNight_SpawnsConfiguredMonsters_InTheDark_AndEndNightSummarizes()
        {
            var directorGO = new GameObject("TestDirector");
            _spawned.Add(directorGO);
            var director = directorGO.AddComponent<NightmareDirector>();
            director.monstersAutoTick = false;
            director.Config = _config;

            var spawnedEvents = new List<GameObject>();
            EventBus.MonsterSpawned += go => spawnedEvents.Add(go);

            director.BeginNight();

            Assert.AreEqual(2, director.SpawnedMonsters.Count);
            Assert.AreEqual(2, spawnedEvents.Count, "MonsterSpawned raised per monster");
            foreach (var monster in director.SpawnedMonsters)
            {
                Assert.AreEqual(LightZone.Dark,
                    DarknessEvaluator.Classify(monster.transform.position));
                Assert.AreEqual(_config.monsterMaxHealth, monster.Health,
                    "director must inject its config before health initializes");
                float dist = PlanarMotion.Distance(
                    directorGO.transform.position, monster.transform.position);
                Assert.That(dist, Is.InRange(20f - 1e-3f, 30f + 1e-3f));
            }

            // Kill one, then end the night.
            director.SpawnedMonsters[0].TakeDamage(1000f);
            director.EndNight();

            string summary = null;
            foreach (var record in GameEventLog.Records)
            {
                if (record.Type == "NightSummary")
                {
                    summary = record.Data;
                }
            }
            Assert.IsNotNull(summary, "EndNight writes a NightSummary record");
            StringAssert.Contains("monstersKilled=1", summary);
            StringAssert.Contains("villagersDead=0", summary);
            Assert.AreEqual(0, director.SpawnedMonsters.Count, "the night is cleaned up");
            Assert.AreEqual(0, MonsterController.Active.Count);
        }

        // ------------------------------------------------------------------
        // Phase 2 (P2-06): schedule parsing, seeded determinism, sailor gating
        // ------------------------------------------------------------------

        NightmareDirector CreateDirector(Vector3 position)
        {
            var go = new GameObject("TestDirector");
            _spawned.Add(go);
            go.transform.position = position;
            var director = go.AddComponent<NightmareDirector>();
            director.monstersAutoTick = false;
            director.autoTick = false;
            director.Config = _config;
            return director;
        }

        void TrackDirectorMonsters(NightmareDirector director)
        {
            foreach (var monster in director.SpawnedMonsters)
            {
                if (monster != null && !_spawned.Contains(monster.gameObject))
                {
                    _spawned.Add(monster.gameObject);
                }
            }
        }

        [Test]
        public void ScheduleParse_SortsByFraction_AndSkipsInvalidEntries()
        {
            var errors = new List<string>();
            var entries = NightmareSchedule.Parse(new[]
            {
                "0.75:pale_hound",
                "0.10:whisper",
                "not a line",
                "0.30:lantern_moth",
                "0.30:drowned_sailor", // same fraction: authored order must hold
                "1.5:panic",           // clamped to 1
                "0.2:bogus_kind",
                null,
                "0.40:shadow",
            }, errors);

            Assert.AreEqual(6, entries.Count, "valid entries parsed");
            Assert.AreEqual(3, errors.Count, "invalid entries reported");
            Assert.AreEqual(NightmareEventKind.Whisper, entries[0].Kind);
            Assert.AreEqual(NightmareEventKind.SpawnLanternMoth, entries[1].Kind,
                "stable sort: lantern moth authored before drowned sailor at 0.30");
            Assert.AreEqual(NightmareEventKind.SpawnDrownedSailor, entries[2].Kind);
            Assert.AreEqual(NightmareEventKind.Shadow, entries[3].Kind);
            Assert.AreEqual(NightmareEventKind.SpawnPaleHound, entries[4].Kind);
            Assert.AreEqual(NightmareEventKind.Panic, entries[5].Kind);
            Assert.AreEqual(1f, entries[5].Fraction, "fractions clamp to 0..1");
            for (int i = 1; i < entries.Count; i++)
            {
                Assert.LessOrEqual(entries[i - 1].Fraction, entries[i].Fraction);
            }
        }

        [Test]
        public void Phase2_BeginNight_SpawnsNothingImmediately_TickStaggersSpawns()
        {
            SpawnLight(Vector3.zero, radius: 10f);
            _config.phase2NightsEnabled = true;
            _config.nightDurationSeconds = 100f;
            _config.phase2NightSchedule = new[]
            {
                "0.10:pale_hound",
                "0.50:pale_hound",
            };
            var director = CreateDirector(Vector3.zero);

            director.BeginNight();
            Assert.AreEqual(0, director.SpawnedMonsters.Count,
                "Phase 2 nights stagger spawns; the boundary itself spawns nothing");

            director.Tick(9f); // t = 9 < 10
            Assert.AreEqual(0, director.SpawnedMonsters.Count);

            director.Tick(1.5f); // t = 10.5 crosses 0.10
            TrackDirectorMonsters(director);
            Assert.AreEqual(1, director.SpawnedMonsters.Count, "first hound at its cue");
            Assert.AreEqual(NightmareType.PaleHound, director.SpawnedMonsters[0].Type);

            director.Tick(39f); // t = 49.5 < 50
            Assert.AreEqual(1, director.SpawnedMonsters.Count, "never a simultaneous wave");

            director.Tick(1f); // t = 50.5 crosses 0.50
            TrackDirectorMonsters(director);
            Assert.AreEqual(2, director.SpawnedMonsters.Count);

            director.EndNight();
        }

        [Test]
        public void Phase2_SameSeed_ProducesIdenticalNights()
        {
            SpawnLight(Vector3.zero, radius: 10f);
            _config.phase2NightsEnabled = true;
            _config.nightDurationSeconds = 60f;
            _config.phase2NightSchedule = new[]
            {
                "0.10:pale_hound",
                "0.30:lantern_moth",
                "0.60:pale_hound",
            };

            List<(NightmareType type, Vector3 pos)> RunNight()
            {
                var director = CreateDirector(Vector3.zero);
                director.BeginNight();
                for (int i = 0; i < 600; i++)
                {
                    director.Tick(0.1f);
                }
                TrackDirectorMonsters(director);
                var snapshot = new List<(NightmareType, Vector3)>();
                foreach (var monster in director.SpawnedMonsters)
                {
                    snapshot.Add((monster.Type, monster.transform.position));
                }
                director.EndNight();
                return snapshot;
            }

            var first = RunNight();
            var second = RunNight();

            Assert.AreEqual(3, first.Count, "the whole script fired");
            Assert.AreEqual(first.Count, second.Count);
            for (int i = 0; i < first.Count; i++)
            {
                Assert.AreEqual(first[i].type, second[i].type, $"type of spawn {i}");
                Assert.AreEqual(first[i].pos, second[i].pos,
                    $"identical seed must give identical spawn point for spawn {i}");
            }
        }

        [Test]
        public void Phase2_DrownedSailor_SkippedWithoutWaterDeathRecord()
        {
            SpawnLight(Vector3.zero, radius: 10f);
            _config.phase2NightsEnabled = true;
            _config.nightDurationSeconds = 10f;
            _config.phase2NightSchedule = new[] { "0.50:drowned_sailor" };
            var director = CreateDirector(Vector3.zero);

            director.BeginNight();
            for (int i = 0; i < 110; i++)
            {
                director.Tick(0.1f);
            }
            TrackDirectorMonsters(director);

            Assert.AreEqual(0, director.SpawnedMonsters.Count,
                "no one died by water: the sailor does not rise");
            bool skipped = false;
            foreach (var record in GameEventLog.Records)
            {
                if (record.Type == "nightmare" && record.Data.Contains("drowned_sailor_skipped"))
                {
                    skipped = true;
                }
            }
            Assert.IsTrue(skipped, "the skip is logged for the morning report");
            director.EndNight();
        }

        [Test]
        public void Phase2_DrownedSailor_RisesNearWreck_AfterObservedWaterDeath()
        {
            SpawnLight(Vector3.zero, radius: 10f);
            _config.phase2NightsEnabled = true;
            _config.nightDurationSeconds = 10f;
            _config.waterDeathRadius = 8f;
            _config.drownedSailorSpawnRadius = 6f;
            _config.phase2NightSchedule = new[] { "0.50:drowned_sailor" };

            var wreckGO = new GameObject("Shipwreck");
            _spawned.Add(wreckGO);
            wreckGO.transform.position = new Vector3(25f, 0f, 0f);

            var director = CreateDirector(Vector3.zero);
            director.shipwreckAnchor = wreckGO.transform;

            // A villager dies beside the wreck; the director's observer must
            // write the located death record (VillagerAgent logs no location).
            var villagerGO = new GameObject("Villager_Drowned");
            _spawned.Add(villagerGO);
            villagerGO.transform.position = new Vector3(24f, 0f, 2f);
            var villager = villagerGO.AddComponent<VillagerAgent>();
            villager.autoTick = false;
            villager.Config = _config;
            villager.ForceState(VillagerState.Dead);
            director.Tick(0.01f); // observation pass, before the night begins
            Assert.IsTrue(NightmareDirector.HasWaterDeathRecord(),
                "the director records the death as died-by-water");

            director.BeginNight();
            for (int i = 0; i < 110; i++)
            {
                director.Tick(0.1f);
            }
            TrackDirectorMonsters(director);

            Assert.AreEqual(1, director.SpawnedMonsters.Count, "the sailor rises");
            var sailor = director.SpawnedMonsters[0];
            Assert.AreEqual(NightmareType.DrownedSailor, sailor.Type);
            Assert.IsInstanceOf<DrownedSailorController>(sailor);
            Assert.LessOrEqual(
                PlanarMotion.Distance(sailor.transform.position, wreckGO.transform.position),
                _config.drownedSailorSpawnRadius + 1e-3f,
                "it rises beside the wreck");
            director.EndNight();
        }

        [Test]
        public void Phase2_WhisperAndPanic_FireAndAreLogged()
        {
            SpawnLight(Vector3.zero, radius: 10f);
            _config.phase2NightsEnabled = true;
            _config.nightDurationSeconds = 10f;
            _config.phase2NightSchedule = new[] { "0.20:whisper", "0.60:panic" };
            var director = CreateDirector(Vector3.zero);

            Vector3? whisperPos = null;
            NightmareDirector.WhisperEmitted += pos => whisperPos = pos;

            // One exposed villager out in the dark: the panic beat's victim.
            var vGO = new GameObject("Villager_Afraid");
            _spawned.Add(vGO);
            vGO.transform.position = new Vector3(15f, 0f, 0f); // beyond the camp light
            var villager = vGO.AddComponent<VillagerAgent>();
            villager.autoTick = false;
            villager.Config = _config;

            director.BeginNight();
            for (int i = 0; i < 110; i++)
            {
                director.Tick(0.1f);
            }
            TrackDirectorMonsters(director);

            Assert.IsTrue(whisperPos.HasValue, "the static whisper event fired (future audio hook)");
            Assert.AreEqual(LightZone.Dark, DarknessEvaluator.Classify(whisperPos.Value),
                "whispers rise from the dark near camp");

            bool whisperLogged = false, panicLogged = false;
            foreach (var record in GameEventLog.Records)
            {
                if (record.Type == "whisper")
                {
                    whisperLogged = true;
                }
                if (record.Type == "panic_event" && record.Data.Contains("Villager_Afraid"))
                {
                    panicLogged = true;
                }
            }
            Assert.IsTrue(whisperLogged, "whispers use the stable 'whisper' log type");
            Assert.IsTrue(panicLogged, "the exposed villager is named in the 'panic_event' record");
            Assert.AreEqual(VillagerState.Panicking, villager.State,
                "the panic beat drives the villager through the public ForceState API");
            director.EndNight();
        }

        [Test]
        public void LegacyDefault_Phase2Disabled_KeepsBoundarySpawnBehaviour()
        {
            SpawnLight(Vector3.zero, radius: 10f);
            Assert.IsFalse(_config.phase2NightsEnabled, "Phase 2 must be opt-in");
            var director = CreateDirector(Vector3.zero);

            director.BeginNight();
            TrackDirectorMonsters(director);
            Assert.AreEqual(_config.firstNightMonsterCount, director.SpawnedMonsters.Count,
                "legacy nights still spawn everything at the boundary");
            Assert.IsNull(director.Schedule, "no schedule in legacy mode");
            director.EndNight();
        }
    }
}
