using System.Collections;
using System.Collections.Generic;
using Abbey.Beast;
using Abbey.Core;
using Abbey.Hero;
using Abbey.Light;
using Abbey.Nightmares;
using Abbey.Villagers;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Abbey.Tests.PlayMode
{
    /// <summary>
    /// End-to-end actor scenarios, built programmatically (no scenes). Every
    /// component runs with autoTick = false and is stepped manually with fixed
    /// dt, so nothing depends on frame rate or real time; yields are bounded
    /// loop-counters, never realtime waits.
    /// </summary>
    public class ActorSystemsPlayModeTests
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
            MonsterController.ClearRegistry();
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
        }

        PrototypeConfig CreateConfig()
        {
            var config = ScriptableObject.CreateInstance<PrototypeConfig>();
            _assets.Add(config);
            config.edgeBandFraction = 0.3f;
            config.arrivalRadius = 0.3f;
            return config;
        }

        GameObject Track(GameObject go)
        {
            _spawned.Add(go);
            return go;
        }

        GameClock CreateClock(PrototypeConfig config)
        {
            var clock = Track(new GameObject("Clock")).AddComponent<GameClock>();
            clock.autoTick = false;
            clock.Configure(config);
            return clock;
        }

        LightSource CreateLight(Vector3 position, float radius, PrototypeConfig config)
        {
            var go = Track(new GameObject("Campfire"));
            go.transform.position = position;
            var source = go.AddComponent<LightSource>();
            source.autoTick = false;
            source.radius = radius;
            source.strength = 1f;
            source.fuelSeconds = -1f;
            DarknessEvaluator.Config = config;
            return source;
        }

        VillagerAgent CreateVillager(Vector3 position, PrototypeConfig config, int seed = 1)
        {
            var go = Track(new GameObject($"Villager_{_spawned.Count}"));
            go.transform.position = position;
            var villager = go.AddComponent<VillagerAgent>();
            villager.autoTick = false;
            villager.Config = config;
            villager.seed = seed;
            DuskRecallSystem.Config = config;
            return villager;
        }

        BellkeeperController CreateHero(Vector3 position, PrototypeConfig config)
        {
            var go = Track(new GameObject("Bellkeeper"));
            go.transform.position = position;
            var hero = go.AddComponent<BellkeeperController>();
            hero.autoTick = false;
            hero.useDirectInput = false;
            hero.Configure(config);
            return hero;
        }

        HoundController CreateHound(Vector3 position, PrototypeConfig config)
        {
            var go = Track(new GameObject("BlackHound"));
            go.transform.position = position;
            var hound = go.AddComponent<HoundController>();
            hound.autoTick = false;
            hound.Configure(config);
            return hound;
        }

        MonsterController CreateMonster(Vector3 position, PrototypeConfig config)
        {
            var go = Track(new GameObject("PaleHound"));
            go.transform.position = position;
            var monster = go.AddComponent<MonsterController>();
            monster.autoTick = false;
            monster.Configure(config);
            return monster;
        }

        static bool LogContains(string type)
        {
            var records = GameEventLog.Records;
            for (int i = 0; i < records.Count; i++)
            {
                if (records[i].Type == type)
                {
                    return true;
                }
            }
            return false;
        }

        // ------------------------------------------------------------------
        // (a) Dusk recall
        // ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator DuskRecall_FarVillagerWithoutBell_BecomesEndangered()
        {
            var config = CreateConfig();
            config.dayDurationSeconds = 1f;
            config.duskDurationSeconds = 60f;
            config.nightDurationSeconds = 60f;
            config.dawnDurationSeconds = 60f;
            config.duskRecallEndangeredDistance = 12f;
            config.villagerFearPerSecondInDark = 0f;
            config.villagerFearPerSecondInEdge = 0f;

            var clock = CreateClock(config);
            CreateLight(Vector3.zero, radius: 10f, config); // Safe within 7
            var farVillager = CreateVillager(new Vector3(40f, 0f, 0f), config);

            var endangered = new List<GameObject>();
            EventBus.VillagerEndangered += go => endangered.Add(go);

            clock.Tick(1.01f); // Day -> Dusk, DuskRecallSystem evaluates
            yield return null;

            Assert.AreEqual(DayPhase.Dusk, clock.Phase);
            Assert.AreEqual(1, endangered.Count, "the far, uncovered villager is the drama beat");
            Assert.AreSame(farVillager.gameObject, endangered[0]);
            Assert.IsTrue(LogContains("VillagerEndangered"));
        }

        [UnityTest]
        public IEnumerator DuskRecall_BellCoveredVillager_ReachesSafeLight()
        {
            var config = CreateConfig();
            config.dayDurationSeconds = 1f;
            config.duskDurationSeconds = 600f;
            config.nightDurationSeconds = 60f;
            config.dawnDurationSeconds = 60f;
            config.duskRecallEndangeredDistance = 12f;
            config.bellPulseMemorySeconds = 30f;
            config.bellRecallSpeedMultiplier = 1.5f;
            config.villagerWalkSpeed = 2f;
            config.villagerFearPerSecondInDark = 0f;
            config.villagerFearPerSecondInEdge = 0f;
            config.villagerInjuredDarkSeconds = 1000f;
            config.villagerMissingDarkSeconds = 2000f;
            config.bellRadius = 15f;

            var clock = CreateClock(config);
            CreateLight(Vector3.zero, radius: 10f, config);
            var villager = CreateVillager(new Vector3(25f, 0f, 0f), config);
            var hero = CreateHero(new Vector3(25f, 0f, 0f), config);

            var endangered = new List<GameObject>();
            EventBus.VillagerEndangered += go => endangered.Add(go);

            Assert.IsTrue(hero.RingBell(), "the hero rings the recall bell next to the villager");
            clock.Tick(1.01f); // -> Dusk; the fresh pulse covers the villager

            Assert.AreEqual(VillagerState.ReturningToLight, villager.State,
                "bell-covered villagers recall immediately");
            Assert.AreEqual(0, endangered.Count, "the bell prevented the endangered beat");

            bool reachedSafe = false;
            for (int i = 0; i < 400 && !reachedSafe; i++)
            {
                villager.Tick(0.1f);
                reachedSafe = villager.CurrentZone == LightZone.Safe;
                if (i % 25 == 24)
                {
                    yield return null;
                }
            }

            Assert.IsTrue(reachedSafe, "the recalled villager must reach Safe light");
            Assert.AreEqual(VillagerState.Idle, villager.State);
            Assert.IsTrue(LogContains("villager_reached_light"));
        }

        // ------------------------------------------------------------------
        // (b) Monster respects Safe light
        // ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Monster_NeverEntersSafeZone_VillagerInLightSurvivesTheNight()
        {
            var config = CreateConfig();
            config.dayDurationSeconds = 0.5f;
            config.duskDurationSeconds = 0.5f;
            config.nightDurationSeconds = 600f;
            config.dawnDurationSeconds = 60f;
            config.monsterMoveSpeed = 3f;
            config.monsterFleeSpeed = 4f;
            config.monsterLightTolerance = 0.15f;
            config.monsterAttackRange = 1.2f;
            config.monsterSightRange = 60f;
            config.monsterSpawnMinRadius = 20f;
            config.monsterSpawnMaxRadius = 30f;
            config.monsterSpawnAttempts = 64;
            config.firstNightMonsterCount = 1;
            config.simulationSeed = 4242;
            config.duskLateRecallDelaySeconds = 0f;

            var clock = CreateClock(config);
            var campfire = CreateLight(Vector3.zero, radius: 8f, config); // Safe within 5.6
            var villager = CreateVillager(Vector3.zero, config);
            var directorGO = Track(new GameObject("Director"));
            directorGO.transform.position = Vector3.zero;
            var director = directorGO.AddComponent<NightmareDirector>();
            director.monstersAutoTick = false;
            director.Config = config;

            GameObject spawnedMonster = null;
            EventBus.MonsterSpawned += go => spawnedMonster = go;

            clock.Tick(1.01f); // Day -> Dusk -> Night: the director spawns
            yield return null;

            Assert.AreEqual(DayPhase.Night, clock.Phase);
            Assert.IsNotNull(spawnedMonster, "night must spawn a monster");
            Assert.AreEqual(1, director.SpawnedMonsters.Count);
            var monster = director.SpawnedMonsters[0];
            Assert.AreEqual(LightZone.Dark, DarknessEvaluator.Classify(monster.transform.position));

            float safeRadius = campfire.EffectiveRadius * (1f - config.edgeBandFraction);
            for (int i = 0; i < 400; i++)
            {
                monster.Tick(Dt);
                villager.Tick(Dt);
                Assert.AreNotEqual(LightZone.Safe,
                    DarknessEvaluator.Classify(monster.transform.position),
                    $"monster stepped into Safe territory at iteration {i}");
                if (i % 40 == 39)
                {
                    yield return null;
                }
            }

            Assert.Greater(
                PlanarMotion.Distance(monster.transform.position, Vector3.zero),
                safeRadius - 1e-3f,
                "the monster is held outside the Safe radius");
            Assert.IsTrue(LogContains("monster_recoiled_from_light"),
                "the monster tried the light and recoiled");
            Assert.IsFalse(LogContains("monster_attacked_villager"));
            Assert.AreNotEqual(VillagerState.Injured, villager.State);
            Assert.AreNotEqual(VillagerState.Dead, villager.State);

            // Dawn: the director cleans up and writes the summary.
            clock.Tick(600f);
            Assert.AreEqual(DayPhase.Dawn, clock.Phase);
            yield return null; // let Destroy() land

            Assert.AreEqual(0, director.SpawnedMonsters.Count);
            bool sawSummary = false;
            foreach (var record in GameEventLog.Records)
            {
                if (record.Type == "NightSummary")
                {
                    sawSummary = true;
                    StringAssert.Contains("villagersDead=0", record.Data);
                }
            }
            Assert.IsTrue(sawSummary, "EndNight writes the NightSummary record");
        }

        // ------------------------------------------------------------------
        // (c) Hound bond behaviour
        // ------------------------------------------------------------------

        static PrototypeConfig ConfigureHoundScenario(PrototypeConfig config)
        {
            config.houndStartTrust = 0.2f;
            config.houndStartHunger = 0.9f;
            config.feedTrustGain = 0.5f;      // one meal reaches the Fed threshold
            config.feedHungerRelief = 0.5f;
            config.trustFedThreshold = 0.5f;
            config.trustFollowThreshold = 0.95f;
            config.hungerStarvingThreshold = 0.8f;
            config.houndHungerPerSecond = 0f;
            config.houndMoveSpeed = 5f;
            config.houndEngageRange = 12f;
            config.houndAttackRange = 2f;
            config.houndAttackDamage = 60f;
            config.houndAttackCooldownSeconds = 0.2f;
            config.monsterMaxHealth = 50f;
            config.monsterMoveSpeed = 1f;
            config.monsterFleeSpeed = 1f;     // slower than the hound: interception succeeds
            config.monsterFleeDistance = 15f;
            config.monsterSightRange = 60f;
            config.interactRange = 2f;
            config.bellRadius = 15f;
            config.villagerFearPerSecondInDark = 0f;
            config.villagerFearPerSecondInEdge = 0f;
            return config;
        }

        [UnityTest]
        public IEnumerator FedHound_AnswersBell_AndInterceptsTheMonster()
        {
            var config = ConfigureHoundScenario(CreateConfig());

            var hero = CreateHero(Vector3.zero, config);
            var hound = CreateHound(new Vector3(1f, 0f, 0f), config);
            var bait = CreateVillager(new Vector3(30f, 0f, 0f), config); // the monster's prey
            var monster = CreateMonster(new Vector3(8f, 0f, 0f), config);

            float fedTrust = -1f;
            EventBus.HoundFed += trust => fedTrust = trust;

            Assert.IsTrue(hero.FeedHound(hound), "feeding is the bond's first step");
            Assert.AreEqual(HoundState.Fed, hound.State);
            Assert.AreEqual(0.7f, fedTrust, 1e-5f, "HoundFed carries the new trust value");
            Assert.IsFalse(hound.IsStarving);

            Assert.IsTrue(hero.RingBell());
            Assert.IsTrue(LogContains("hound_answered_bell"), "the fed hound answers the bell");
            Assert.AreEqual(HoundState.Following, hound.State);

            for (int i = 0; i < 600 && monster.IsAlive; i++)
            {
                hound.Tick(Dt);
                monster.Tick(Dt);
                bait.Tick(Dt);
                if (i % 40 == 39)
                {
                    yield return null;
                }
            }

            Assert.IsFalse(monster.IsAlive, "the hound must run the monster down");
            Assert.IsTrue(LogContains("hound_engaged_monster"));
            Assert.IsTrue(LogContains("hound_attacked_monster"));
            Assert.IsTrue(LogContains("hound_killed_monster"));
            Assert.IsTrue(LogContains("monster_fleeing"), "the pressed monster broke off");
            Assert.AreNotEqual(VillagerState.Injured, bait.State, "the bait was never reached");
        }

        [UnityTest]
        public IEnumerator StarvingUnfedHound_IgnoresTheBell()
        {
            var config = ConfigureHoundScenario(CreateConfig());

            var hero = CreateHero(Vector3.zero, config);
            var hound = CreateHound(new Vector3(5f, 0f, 0f), config);

            Assert.IsTrue(hound.IsStarving, "unfed: start hunger 0.9 >= 0.8");
            Assert.AreEqual(HoundState.Chained, hound.State);

            Assert.IsTrue(hero.RingBell());
            for (int i = 0; i < 100; i++)
            {
                hound.Tick(Dt);
                if (i % 50 == 49)
                {
                    yield return null;
                }
            }

            Assert.IsTrue(LogContains("hound_ignored_bell"),
                "the snub must be visible in the event log");
            Assert.IsFalse(LogContains("hound_answered_bell"));
            Assert.IsFalse(hound.HasBellTarget);
            Assert.AreEqual(HoundState.Chained, hound.State);
            Assert.AreEqual(0f,
                PlanarMotion.Distance(hound.transform.position, new Vector3(5f, 0f, 0f)),
                1e-5f, "a chained, starving hound does not stir for the bell");
        }
    }
}
