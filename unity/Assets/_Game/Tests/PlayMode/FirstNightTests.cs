using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
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
    /// P01-10: the whole first night as one integration test, in a fully
    /// programmatic world (no scene load). Day work -> dusk (far villager
    /// endangered, bell + hero rescue) -> night (director spawns a monster in the
    /// dark, the monster never enters Safe light, the hound branch depends on
    /// whether it was fed) -> dawn (the director's NightSummary matches the
    /// actual villager states). Deterministic: autoTick off everywhere, manual
    /// Tick with fixed dt, bounded loops, seeded spawn selection.
    /// </summary>
    public class FirstNightTests
    {
        const float Dt = 0.1f;

        readonly List<GameObject> _spawned = new List<GameObject>();
        readonly List<ScriptableObject> _assets = new List<ScriptableObject>();

        GameClock _clock;
        BellkeeperController _hero;
        HoundController _hound;
        NightmareDirector _director;
        VillagerAgent _farWorker;
        readonly List<VillagerAgent> _villagers = new List<VillagerAgent>();

        [SetUp]
        public void SetUp()
        {
            EventBus.ResetAll();
            GameEventLog.Clear();
            DarknessEvaluator.Clear();
            DuskRecallSystem.Clear();
            MonsterController.ClearRegistry();
            _villagers.Clear();
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
            _villagers.Clear();
            MonsterController.ClearRegistry();
            DuskRecallSystem.Clear();
            DarknessEvaluator.Clear();
            EventBus.ResetAll();
            GameEventLog.Clear();
        }

        // ------------------------------------------------------------------
        // The two hound branches share one scripted night.
        // ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator FirstNight_FedHoundBranch_HoundHelpsAndSummaryIsConsistent()
        {
            return RunFirstNight(feedHound: true);
        }

        [UnityTest]
        public IEnumerator FirstNight_UnfedHoundBranch_HoundIgnoresBellAndSummaryIsConsistent()
        {
            return RunFirstNight(feedHound: false);
        }

        IEnumerator RunFirstNight(bool feedHound)
        {
            var config = CreateConfig();
            BuildWorld(config);

            var endangered = new List<GameObject>();
            var rescued = new List<GameObject>();
            GameObject spawnedMonster = null;
            EventBus.VillagerEndangered += go => endangered.Add(go);
            EventBus.VillagerRescued += go => rescued.Add(go);
            EventBus.MonsterSpawned += go => spawnedMonster = go;

            // ------------------------------------------------- DAY: work + (maybe) feed
            Assert.AreEqual(DayPhase.Day, _clock.Phase);

            if (feedHound)
            {
                _hero.SetMoveTarget(_hound.transform.position);
                bool fed = false;
                for (int i = 0; i < 100 && !fed; i++)
                {
                    StepWorld(Dt);
                    if (PlanarMotion.Distance(_hero.transform.position, _hound.transform.position)
                        <= config.interactRange * 0.9f)
                    {
                        fed = _hero.FeedHound(_hound);
                    }
                }
                Assert.IsTrue(fed, "the hero reaches the chained hound and feeds it during the day");
                Assert.AreEqual(HoundState.Fed, _hound.State);
                Assert.IsFalse(_hound.IsStarving);
            }
            else
            {
                Assert.AreEqual(HoundState.Chained, _hound.State);
                Assert.IsTrue(_hound.IsStarving, "the unfed hound starts starving");
            }
            yield return null;

            // Let the assigned worker run its day loop until it deposits.
            for (int i = 0; i < 200 && !LogContains("villager_deposited_resource"); i++)
            {
                StepWorld(Dt);
                if (i % 50 == 49)
                {
                    yield return null;
                }
            }
            Assert.IsTrue(LogContains("villager_deposited_resource"),
                "day work: the assigned villager completes at least one haul");
            Assert.AreEqual(DayPhase.Day, _clock.Phase, "the day loop must fit inside the Day phase");

            // ------------------------------------------------- DUSK: endangered + rescue
            CrossIntoPhase(DayPhase.Dusk);
            Assert.AreEqual(1, endangered.Count, "exactly the far villager is endangered at dusk");
            Assert.AreSame(_farWorker.gameObject, endangered[0]);

            // The bell covers the camp (and calls the hound — the branch point).
            Assert.IsTrue(_hero.RingBell());
            if (feedHound)
            {
                Assert.IsTrue(LogContains("hound_answered_bell"), "the fed hound answers the bell");
                Assert.AreEqual(HoundState.Following, _hound.State);
            }
            else
            {
                Assert.IsTrue(LogContains("hound_ignored_bell"), "the starving hound snubs the bell");
                Assert.AreEqual(HoundState.Chained, _hound.State);
            }

            // Hero rides out and escorts the far villager back into the light.
            _hero.SetMoveTarget(_farWorker.transform.position);
            bool rescueStarted = false;
            for (int i = 0; i < 120 && !rescueStarted; i++)
            {
                StepWorld(Dt);
                if (PlanarMotion.Distance(_hero.transform.position, _farWorker.transform.position)
                    <= config.interactRange * 0.75f)
                {
                    rescueStarted = _hero.Rescue(_farWorker);
                }
            }
            Assert.IsTrue(rescueStarted, "the hero reaches and attaches the endangered villager");
            Assert.AreEqual(VillagerState.ReturningToLight, _farWorker.State);
            Assert.IsTrue(_farWorker.IsEscorted);
            yield return null;

            _hero.SetMoveTarget(Vector3.zero); // back to the campfire
            for (int i = 0; i < 200 && _hero.HasMoveTarget; i++)
            {
                StepWorld(Dt);
                if (i % 50 == 49)
                {
                    yield return null;
                }
            }
            Assert.IsFalse(_hero.HasMoveTarget, "the hero made it back to camp within dusk");
            Assert.IsTrue(_hero.ReleaseRescued(), "released inside Safe light completes the rescue");
            Assert.AreEqual(1, rescued.Count);
            Assert.AreSame(_farWorker.gameObject, rescued[0]);
            Assert.AreEqual(DayPhase.Dusk, _clock.Phase, "the rescue must fit inside the Dusk phase");

            // ------------------------------------------------- NIGHT: monster + branch
            CrossIntoPhase(DayPhase.Night);
            Assert.IsNotNull(spawnedMonster, "the director spawns the night's monster");
            Assert.AreEqual(1, _director.SpawnedMonsters.Count);
            var monster = _director.SpawnedMonsters[0];
            Assert.AreEqual(LightZone.Dark,
                DarknessEvaluator.Classify(monster.transform.position),
                "monsters are born outside all light");

            Vector3 houndStart = _hound.transform.position;
            int nightSteps = 350; // 35s of the 40s night, leaving the boundary tick exact
            for (int i = 0; i < nightSteps; i++)
            {
                StepWorld(Dt);
                if (monster != null && monster.IsAlive)
                {
                    Assert.AreNotEqual(LightZone.Safe,
                        DarknessEvaluator.Classify(monster.transform.position),
                        $"the monster entered Safe territory at night step {i}");
                }
                if (i % 50 == 49)
                {
                    yield return null;
                }
            }
            Assert.AreEqual(DayPhase.Night, _clock.Phase);

            if (feedHound)
            {
                Assert.IsTrue(LogContains("hound_engaged_monster"), "the fed hound intercepts");
                Assert.IsTrue(LogContains("hound_killed_monster"), "the fed hound kills the monster");
                Assert.IsFalse(monster.IsAlive);
            }
            else
            {
                Assert.IsFalse(LogContains("hound_engaged_monster"), "the chained hound never engages");
                Assert.IsTrue(monster.IsAlive, "nobody kills the monster in the unfed branch");
                Assert.AreEqual(0f,
                    PlanarMotion.Distance(_hound.transform.position, houndStart), 1e-4f,
                    "the chained hound does not stir all night");
            }
            Assert.IsFalse(LogContains("monster_attacked_villager"),
                "everyone was inside Safe light: the monster never got a strike in");

            // ------------------------------------------------- DAWN: the summary
            CrossIntoPhase(DayPhase.Dawn);
            // No actor ticked since the boundary: villager states are exactly what
            // EndNight snapshotted, so the summary must match a fresh recount.
            int dead = 0, injured = 0, missing = 0;
            foreach (var v in _villagers)
            {
                switch (v.State)
                {
                    case VillagerState.Dead: dead++; break;
                    case VillagerState.Injured: injured++; break;
                    case VillagerState.Missing: missing++; break;
                }
            }

            var summary = FindLastRecord("NightSummary");
            Assert.IsNotNull(summary, "dawn writes the NightSummary record");
            Assert.AreEqual(dead, ParseCount(summary.Value.Data, "villagersDead"), "dead count");
            Assert.AreEqual(injured, ParseCount(summary.Value.Data, "villagersInjured"), "injured count");
            Assert.AreEqual(missing, ParseCount(summary.Value.Data, "villagersMissing"), "missing count");
            int survivors = _villagers.Count - dead - missing;
            Assert.AreEqual(12, _villagers.Count);
            Assert.AreEqual(12, survivors, "the guarded first night loses nobody");
            Assert.AreEqual(0, injured);

            StringAssert.Contains(feedHound ? "houndHelped=True" : "houndHelped=False",
                summary.Value.Data, "the summary records whether the hound helped");
            Assert.AreEqual(feedHound ? 1 : 0, ParseCount(summary.Value.Data, "monstersKilled"));
            Assert.AreEqual(0, _director.SpawnedMonsters.Count, "dawn despawns the night's monsters");
            yield return null;
        }

        // ------------------------------------------------------------------
        // World construction (programmatic, no scene)
        // ------------------------------------------------------------------

        PrototypeConfig CreateConfig()
        {
            var config = ScriptableObject.CreateInstance<PrototypeConfig>();
            _assets.Add(config);

            config.dayDurationSeconds = 30f;
            config.duskDurationSeconds = 30f;
            config.nightDurationSeconds = 40f;
            config.dawnDurationSeconds = 10f;

            config.edgeBandFraction = 0.3f;
            config.arrivalRadius = 0.3f;
            config.campfireRadius = 10f; // Safe within 7

            config.bellkeeperMoveSpeed = 6f;
            config.interactRange = 2f;
            config.bellCooldownSeconds = 1f;
            config.rescueCooldownSeconds = 0.2f;
            config.feedCooldownSeconds = 0.2f;
            config.startingCarriedFood = 3;

            config.villagerWalkSpeed = 2f;
            config.villagerWorkDurationSeconds = 1f;
            config.villagerPickupDurationSeconds = 0.2f;
            // Darkness fear/harm is exercised elsewhere; keep this night's outcome
            // controlled by the monster + rescue mechanics alone.
            config.villagerFearPerSecondInDark = 0f;
            config.villagerFearPerSecondInEdge = 0f;
            config.villagerInjuredDarkSeconds = 1000f;
            config.villagerMissingDarkSeconds = 2000f;

            config.duskRecallEndangeredDistance = 12f;
            config.duskLateRecallDelaySeconds = 5f;
            config.bellRadius = 15f;
            config.bellPulseMemorySeconds = 30f;
            config.bellRecallSpeedMultiplier = 1.5f;
            config.rescueFollowDistance = 1.5f;

            config.houndStartTrust = 0.1f;
            config.houndStartHunger = 0.9f;
            config.hungerStarvingThreshold = 0.8f;
            config.feedTrustGain = 0.5f;      // one meal crosses the Fed threshold
            config.feedHungerRelief = 0.4f;
            config.trustFedThreshold = 0.5f;
            config.trustFollowThreshold = 0.95f;
            config.houndHungerPerSecond = 0f;
            config.houndMoveSpeed = 6f;
            config.houndEngageRange = 100f;   // the whole map: the payoff must land
            config.houndAttackRange = 2f;
            config.houndAttackDamage = 60f;   // one bite kills a 50hp monster
            config.houndAttackCooldownSeconds = 0.2f;

            config.monsterMaxHealth = 50f;
            config.monsterMoveSpeed = 2.5f;
            config.monsterFleeSpeed = 3f;     // slower than the hound: interception wins
            config.monsterLightTolerance = 0.15f;
            config.monsterAttackRange = 1.2f;
            config.monsterAttackCooldownSeconds = 2f;
            config.monsterFleeDistance = 15f;
            config.monsterSightRange = 60f;

            config.firstNightMonsterCount = 1;
            config.monsterSpawnMinRadius = 20f;
            config.monsterSpawnMaxRadius = 30f;
            config.monsterSpawnAttempts = 64;
            config.simulationSeed = 4242;

            return config;
        }

        void BuildWorld(PrototypeConfig config)
        {
            DarknessEvaluator.Config = config;
            DuskRecallSystem.Config = config;

            var clockGO = Track(new GameObject("Clock"));
            _clock = clockGO.AddComponent<GameClock>();
            _clock.autoTick = false;
            _clock.Configure(config);

            var fireGO = Track(new GameObject("Campfire"));
            fireGO.transform.position = Vector3.zero;
            var fire = fireGO.AddComponent<LightSource>();
            fire.autoTick = false;
            fire.radius = config.campfireRadius;
            fire.strength = 1f;
            fire.fuelSeconds = -1f;

            var heroGO = Track(new GameObject("Bellkeeper"));
            heroGO.transform.position = Vector3.zero;
            _hero = heroGO.AddComponent<BellkeeperController>();
            _hero.autoTick = false;
            _hero.useDirectInput = false;
            _hero.Configure(config);

            var houndGO = Track(new GameObject("BlackHound"));
            houndGO.transform.position = new Vector3(8f, 0f, 0f); // the tower, off-camp
            _hound = houndGO.AddComponent<HoundController>();
            _hound.autoTick = false;
            _hound.Configure(config);

            var directorGO = Track(new GameObject("Director"));
            directorGO.transform.position = Vector3.zero;
            _director = directorGO.AddComponent<NightmareDirector>();
            _director.monstersAutoTick = false;
            _director.Config = config;

            // 11 villagers around the fire, all inside Safe (radius 4 < 7).
            for (int i = 0; i < 11; i++)
            {
                float angle = i / 11f * Mathf.PI * 2f;
                var pos = new Vector3(Mathf.Cos(angle) * 4f, 0f, Mathf.Sin(angle) * 4f);
                _villagers.Add(CreateVillager($"Villager_{i:D2}", pos, config, seed: i));
            }

            // The drama beat: one villager far out in the fields (Dark, > endangered
            // distance from the nearest Safe point).
            _farWorker = CreateVillager("Villager_Far", new Vector3(30f, 0f, 0f), config, seed: 11);
            _villagers.Add(_farWorker);

            // One camp villager gets a day-work assignment inside the light.
            _villagers[0].AssignWork(new Vector3(5f, 0f, 5f), new Vector3(2f, 0f, 2f));
        }

        VillagerAgent CreateVillager(string name, Vector3 pos, PrototypeConfig config, int seed)
        {
            var go = Track(new GameObject(name));
            go.transform.position = pos;
            var villager = go.AddComponent<VillagerAgent>();
            villager.autoTick = false;
            villager.Config = config;
            villager.seed = seed;
            return villager;
        }

        GameObject Track(GameObject go)
        {
            _spawned.Add(go);
            return go;
        }

        // ------------------------------------------------------------------
        // Deterministic stepping
        // ------------------------------------------------------------------

        /// <summary>One simulation step: clock first, then every actor.</summary>
        void StepWorld(float dt)
        {
            _clock.Tick(dt);
            _hero.Tick(dt);
            _hound.Tick(dt);
            for (int i = 0; i < _villagers.Count; i++)
            {
                var v = _villagers[i];
                if (v != null)
                {
                    v.Tick(dt);
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

        /// <summary>
        /// Advances the clock (only the clock — no actor moves during the jump)
        /// exactly past the current phase boundary into <paramref name="expected"/>.
        /// </summary>
        void CrossIntoPhase(DayPhase expected)
        {
            float remaining = _clock.GetPhaseDuration(_clock.Phase) - _clock.TimeInPhase;
            _clock.Tick(remaining + 0.001f);
            Assert.AreEqual(expected, _clock.Phase,
                $"expected the boundary tick to land in {expected}");
        }

        // ------------------------------------------------------------------
        // Log helpers
        // ------------------------------------------------------------------

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

        static int ParseCount(string data, string key)
        {
            var match = Regex.Match(data, key + @"=(\d+)");
            Assert.IsTrue(match.Success, $"NightSummary contains {key}: '{data}'");
            return int.Parse(match.Groups[1].Value);
        }
    }
}
