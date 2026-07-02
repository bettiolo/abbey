using System.Collections;
using System.Collections.Generic;
using Abbey.Core;
using Abbey.Light;
using Abbey.Nightmares;
using Abbey.Villagers;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Abbey.Tests.PlayMode
{
    /// <summary>
    /// P2-06: the Phase 2 scripted night. Staggered pale hound spawns across a
    /// simulated night, the lantern moth draining a lantern into a darkness gap,
    /// the drowned sailor gated on a died-by-water record, and the bell as a
    /// weak-nightmare stun. Deterministic: autoTick off everywhere, manual Tick
    /// with fixed dt, bounded loops, seeded spawn selection.
    /// </summary>
    public class NightmarePhase2Tests
    {
        const float Dt = 0.1f;

        readonly List<GameObject> _spawned = new List<GameObject>();
        readonly List<ScriptableObject> _assets = new List<ScriptableObject>();

        [SetUp]
        public void SetUp()
        {
            EventBus.ResetAll();
            GameEventLog.Clear();
            DarknessEvaluator.Clear();
            DuskRecallSystem.Clear();
            MonsterController.ClearRegistry();
            NightmareDirector.ResetStaticEvents();
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
            foreach (var asset in _assets)
            {
                if (asset != null)
                {
                    Object.DestroyImmediate(asset);
                }
            }
            _assets.Clear();
            MonsterController.ClearRegistry();
            DuskRecallSystem.Clear();
            DarknessEvaluator.Clear();
            EventBus.ResetAll();
            GameEventLog.Clear();
            NightmareDirector.ResetStaticEvents();
        }

        // ------------------------------------------------------------------
        // World helpers
        // ------------------------------------------------------------------

        GameObject Track(GameObject go)
        {
            _spawned.Add(go);
            return go;
        }

        PrototypeConfig CreateConfig()
        {
            var config = ScriptableObject.CreateInstance<PrototypeConfig>();
            _assets.Add(config);
            config.phase2NightsEnabled = true;
            config.nightDurationSeconds = 30f;
            config.edgeBandFraction = 0.3f;
            config.arrivalRadius = 0.3f;
            config.simulationSeed = 777;
            config.monsterSpawnMinRadius = 20f;
            config.monsterSpawnMaxRadius = 30f;
            config.monsterSpawnAttempts = 64;
            config.monsterMoveSpeed = 2.5f;
            config.monsterFleeSpeed = 4f;
            config.monsterLightTolerance = 0.15f;
            config.monsterMaxHealth = 50f;
            config.monsterSightRange = 60f;
            config.lanternMothMoveSpeed = 6f;
            config.lanternMothFleeSpeed = 7f;
            config.lanternMothFleeRange = 5f;
            config.lanternMothDrainRange = 1.5f;
            config.lanternMothDrainPerSecond = 20f;
            config.bellNightmareStunSeconds = 2f;
            config.drownedSailorMoveSpeed = 1.2f;
            config.drownedSailorLightTolerance = 0.6f;
            config.drownedSailorSpawnRadius = 6f;
            config.waterDeathRadius = 8f;
            return config;
        }

        NightmareDirector CreateDirector(PrototypeConfig config)
        {
            DarknessEvaluator.Config = config;
            DuskRecallSystem.Config = config;
            var go = Track(new GameObject("Director"));
            var director = go.AddComponent<NightmareDirector>();
            director.monstersAutoTick = false;
            director.autoTick = false;
            director.Config = config;
            return director;
        }

        LightSource CreateLight(Vector3 position, float radius, float fuelSeconds,
            bool sacred = false)
        {
            var go = Track(new GameObject($"Light_{_spawned.Count}"));
            go.transform.position = position;
            var source = go.AddComponent<LightSource>();
            source.autoTick = false;
            source.radius = radius;
            source.strength = 1f;
            source.fuelSeconds = fuelSeconds;
            source.sacred = sacred;
            return source;
        }

        /// <summary>Steps the director and every live spawned monster.</summary>
        void StepNight(NightmareDirector director, float dt)
        {
            director.Tick(dt);
            var monsters = director.SpawnedMonsters;
            for (int i = 0; i < monsters.Count; i++)
            {
                var m = monsters[i];
                if (m != null && m.IsAlive && m.gameObject.activeSelf)
                {
                    m.Tick(dt);
                }
            }
        }

        static bool LogContains(string type, string dataFragment = null)
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

        static int CountOfType<T>(IReadOnlyList<MonsterController> monsters)
            where T : MonsterController
        {
            int count = 0;
            for (int i = 0; i < monsters.Count; i++)
            {
                if (monsters[i] is T)
                {
                    count++;
                }
            }
            return count;
        }

        // ------------------------------------------------------------------
        // 1. Staggered pale hounds across a simulated night
        // ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Phase2Night_PaleHounds_SpawnStaggered_NeverAsAWave()
        {
            var config = CreateConfig();
            config.phase2NightSchedule = new[]
            {
                "0.10:pale_hound", // 3s
                "0.40:pale_hound", // 12s
                "0.70:pale_hound", // 21s
            };
            CreateLight(Vector3.zero, radius: 10f, fuelSeconds: -1f); // camp
            var director = CreateDirector(config);

            director.BeginNight();
            Assert.AreEqual(0, director.SpawnedMonsters.Count,
                "the night boundary spawns nothing: Phase 2 is intimate, not a wave");

            var countsAt = new Dictionary<int, int>(); // tick index -> spawned count
            for (int i = 0; i < 300; i++) // 30s night
            {
                StepNight(director, Dt);
                countsAt[i] = director.SpawnedMonsters.Count;
                if (i % 60 == 59)
                {
                    yield return null;
                }
            }

            Assert.AreEqual(3, director.SpawnedMonsters.Count, "all three hounds arrived");
            Assert.AreEqual(1, countsAt[50], "only the first hound is out at t=5s");
            Assert.AreEqual(2, countsAt[150], "two hounds at t=15s");
            Assert.AreEqual(3, countsAt[250], "three hounds at t=25s");
            foreach (var monster in director.SpawnedMonsters)
            {
                Assert.AreEqual(NightmareType.PaleHound, monster.Type);
                Assert.IsTrue(LogContains("nightmare", $"spawn type=PaleHound name={monster.name}"),
                    "every spawn is event-logged with the stable 'nightmare' type");
            }

            director.EndNight();
            Assert.AreEqual(0, director.SpawnedMonsters.Count);
            Assert.IsTrue(LogContains("nightmare", "despawn"), "despawns are logged too");
            yield return null;
        }

        // ------------------------------------------------------------------
        // 2. Lantern moth drains a lantern: a Dark gap where Safe was
        // ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator LanternMoth_DrainsWeakestLantern_CreatingDarknessGap()
        {
            var config = CreateConfig();
            config.phase2NightSchedule = new[] { "0.05:lantern_moth" };
            CreateLight(Vector3.zero, radius: 10f, fuelSeconds: -1f); // strong camp, undrainable
            var lantern = CreateLight(new Vector3(14f, 0f, 0f), radius: 4f, fuelSeconds: 30f);
            var gapPoint = lantern.transform.position;
            var director = CreateDirector(config);

            Assert.AreEqual(LightZone.Safe, DarknessEvaluator.Classify(gapPoint),
                "the lantern's heart starts Safe");

            director.BeginNight();
            LanternMothController moth = null;
            for (int i = 0; i < 400 && lantern.isLit; i++) // bounded: fly in + 1.5s drain
            {
                StepNight(director, Dt);
                if (moth == null && director.SpawnedMonsters.Count > 0)
                {
                    moth = director.SpawnedMonsters[0] as LanternMothController;
                }
                if (i % 80 == 79)
                {
                    yield return null;
                }
            }

            Assert.IsNotNull(moth, "the director spawned a lantern moth");
            Assert.AreEqual(NightmareType.LanternMoth, moth.Type);
            Assert.AreSame(lantern, DarknessEvaluator.Sources[1],
                "sanity: the lantern is the registered weak source");
            Assert.IsFalse(lantern.isLit, "the moth drained the lantern dead");
            Assert.AreEqual(0f, lantern.fuelSeconds, "fuel fully drained");
            Assert.AreEqual(LightZone.Dark, DarknessEvaluator.Classify(gapPoint),
                "a darkness gap opened where Safe territory was");
            Assert.IsTrue(LogContains("LightExtinguished"), "the light logged its death");
            Assert.IsTrue(LogContains("nightmare", "moth_drained_light"),
                "the drain is event-logged");
            Assert.IsFalse(LogContains("monster_attacked_villager"),
                "the moth is harmless to people");
            yield return null;
        }

        // ------------------------------------------------------------------
        // 3. Drowned sailor: only when someone died by water
        // ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator DrownedSailor_DoesNotRise_WithoutWaterDeath()
        {
            var config = CreateConfig();
            config.phase2NightSchedule = new[] { "0.30:drowned_sailor" };
            CreateLight(Vector3.zero, radius: 10f, fuelSeconds: -1f);
            var director = CreateDirector(config);
            var wreck = Track(new GameObject("Shipwreck"));
            wreck.transform.position = new Vector3(25f, 0f, 0f);
            director.shipwreckAnchor = wreck.transform;

            director.BeginNight();
            for (int i = 0; i < 320; i++)
            {
                StepNight(director, Dt);
                if (i % 80 == 79)
                {
                    yield return null;
                }
            }

            Assert.AreEqual(0, CountOfType<DrownedSailorController>(director.SpawnedMonsters),
                "no water death, no sailor");
            Assert.IsTrue(LogContains("nightmare", "drowned_sailor_skipped"));
            director.EndNight();
            yield return null;
        }

        [UnityTest]
        public IEnumerator DrownedSailor_Rises_WhenSomeoneDiedByWater()
        {
            var config = CreateConfig();
            config.phase2NightSchedule = new[] { "0.30:drowned_sailor" };
            CreateLight(Vector3.zero, radius: 10f, fuelSeconds: -1f);
            var director = CreateDirector(config);
            var wreck = Track(new GameObject("Shipwreck"));
            wreck.transform.position = new Vector3(25f, 0f, 0f);
            director.shipwreckAnchor = wreck.transform;

            // Someone drowned by the wreck; the director's observer records it
            // with location (VillagerAgent's own record has none).
            var villagerGO = Track(new GameObject("Villager_Drowned"));
            villagerGO.transform.position = new Vector3(23f, 0f, 3f);
            var villager = villagerGO.AddComponent<VillagerAgent>();
            villager.autoTick = false;
            villager.Config = config;
            villager.ForceState(VillagerState.Dead);
            director.Tick(Dt); // pre-night observation pass
            Assert.IsTrue(NightmareDirector.HasWaterDeathRecord());
            Assert.IsTrue(LogContains("villager_died_at", "nearWater=True"));

            director.BeginNight();
            DrownedSailorController sailor = null;
            for (int i = 0; i < 320 && sailor == null; i++)
            {
                StepNight(director, Dt);
                for (int m = 0; m < director.SpawnedMonsters.Count && sailor == null; m++)
                {
                    sailor = director.SpawnedMonsters[m] as DrownedSailorController;
                }
                if (i % 80 == 79)
                {
                    yield return null;
                }
            }

            Assert.IsNotNull(sailor, "the sailor rises when the water has taken someone");
            Assert.IsTrue(LogContains("nightmare", "spawn type=DrownedSailor"));
            // It may already have taken one slow step in the spawn tick.
            Assert.LessOrEqual(
                PlanarMotion.Distance(sailor.transform.position, wreck.transform.position),
                config.drownedSailorSpawnRadius + config.drownedSailorMoveSpeed * Dt + 1e-3f,
                "it rises beside the wreck, not on the far ring");
            director.EndNight();
            yield return null;
        }

        // ------------------------------------------------------------------
        // 4. Bell = weak-nightmare stun (moth freezes; pale hound does not)
        // ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Bell_StunsLanternMoth_ButNotPaleHound()
        {
            var config = CreateConfig();
            DarknessEvaluator.Config = config;
            var lantern = CreateLight(new Vector3(20f, 0f, 0f), radius: 4f, fuelSeconds: 60f);

            var mothGO = Track(new GameObject("Moth"));
            mothGO.transform.position = new Vector3(5f, 0f, 0f);
            var moth = mothGO.AddComponent<LanternMothController>();
            moth.autoTick = false;
            moth.Configure(config);

            var houndGO = Track(new GameObject("PaleHound"));
            houndGO.transform.position = new Vector3(5f, 0f, 5f);
            var paleHound = houndGO.AddComponent<PaleHoundController>();
            paleHound.autoTick = false;
            paleHound.Configure(config);
            yield return null;

            // The moth is flying toward the lantern before the bell.
            Vector3 before = moth.transform.position;
            moth.Tick(Dt);
            Assert.Greater(PlanarMotion.Distance(before, moth.transform.position), 0f,
                "sanity: the moth moves before the bell");

            EventBus.RaiseBellRang(new Vector3(5f, 0f, 2f), 15f);
            Assert.IsTrue(moth.IsStunned, "bell pulse stuns the weak nightmare");
            Assert.IsFalse(paleHound.IsStunned, "the pale hound is not a weak nightmare");
            Assert.IsTrue(LogContains("nightmare", "stun name=Moth"),
                "the stun is event-logged");

            Vector3 frozen = moth.transform.position;
            // Hold well inside the stun window (2s): frozen solid.
            for (int i = 0; i < 15; i++)
            {
                moth.Tick(Dt);
                Assert.AreEqual(frozen, moth.transform.position,
                    $"a stunned moth holds still (tick {i})");
            }
            // Then the stun expires (float-tolerant: a few extra ticks allowed).
            bool resumed = false;
            for (int i = 0; i < 20 && !resumed; i++)
            {
                moth.Tick(Dt);
                resumed = PlanarMotion.Distance(frozen, moth.transform.position) > 0f;
            }
            Assert.IsTrue(resumed, "the moth resumes after the stun");
            Assert.IsFalse(moth.IsStunned);

            // A distant bell does not reach it.
            EventBus.RaiseBellRang(new Vector3(500f, 0f, 0f), 15f);
            Assert.IsFalse(moth.IsStunned, "a bell outside its radius does nothing");
            yield return null;
        }
    }
}
