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
    }
}
