using System.Collections;
using System.Collections.Generic;
using Abbey.Core;
using Abbey.Hero;
using Abbey.Light;
using Abbey.Nightmares;
using Abbey.Session;
using Abbey.Villagers;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Abbey.Tests.PlayMode
{
    /// <summary>
    /// P2-08: the win/loss authority driven end-to-end in a programmatic world
    /// (no scene). The win case runs the settlement THROUGH the First White Night —
    /// the scenario arms the director's harder schedule at dusk, the night is
    /// simulated, and the dawn verdict is Win. Each loss path is tripped and the
    /// reason asserted. Deterministic: autoTick off everywhere, manual Tick with a
    /// fixed dt, bounded loops.
    /// </summary>
    public class WinLossTests
    {
        const float Dt = 0.1f;

        readonly List<GameObject> _spawned = new List<GameObject>();
        readonly List<ScriptableObject> _assets = new List<ScriptableObject>();
        readonly List<VillagerAgent> _villagers = new List<VillagerAgent>();

        PrototypeConfig _proto;
        GameSessionConfig _sessionConfig;
        GameClock _clock;
        BellkeeperController _hero;
        LightSource _flame;
        NightmareDirector _director;
        FirstWhiteNightScenario _scenario;
        GameSession _session;
        int _decidedCount;
        SessionSummary _lastDecided;

        [SetUp]
        public void SetUp()
        {
            EventBus.ResetAll();
            GameEventLog.Clear();
            DarknessEvaluator.Clear();
            DuskRecallSystem.Clear();
            MonsterController.ClearRegistry();
            NightmareDirector.ResetStaticEvents();
            FirstWhiteNightScenario.ResetStaticEvents();
            GameSession.ResetStaticEvents();
            GameSessionConfig.ClearCache();
            PrototypeConfig.ClearCache();
            _villagers.Clear();
            _decidedCount = 0;
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
            _villagers.Clear();
            MonsterController.ClearRegistry();
            GameSession.ResetStaticEvents();
            FirstWhiteNightScenario.ResetStaticEvents();
            NightmareDirector.ResetStaticEvents();
            DuskRecallSystem.Clear();
            DarknessEvaluator.Clear();
            EventBus.ResetAll();
            GameEventLog.Clear();
            GameSessionConfig.ClearCache();
            PrototypeConfig.ClearCache();
        }

        // ------------------------------------------------------------------
        // WIN — survive the First White Night
        // ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Win_SurvivesTheFirstWhiteNight()
        {
            BuildWorld(villagerCount: 8, whiteNightIndex: 1);
            _session.autoEvaluate = false;
            GameSession.OutcomeDecided += OnDecided;

            Assert.AreEqual(DayPhase.Day, _clock.Phase);
            Assert.AreEqual(GameOutcome.Undecided, _session.Evaluate(),
                "the run is open while the White Night is still ahead");

            // ---- Dusk: the scenario arms the White Night on the director ----
            CrossIntoPhase(DayPhase.Dusk);
            Assert.IsTrue(_scenario.IsArmed, "the climax is armed the dusk before");
            Assert.IsTrue(_director.Config.phase2NightsEnabled,
                "the director is switched to the harder scripted mode");
            yield return null;

            // ---- Night: the White Night itself runs ----
            CrossIntoPhase(DayPhase.Night);
            Assert.IsTrue(_scenario.HasBegun, "the White Night began");
            Assert.IsTrue(LogContains("scenario", "white_night_begins"));

            for (int i = 0; i < 140; i++) // simulate the ~10s night without dying
            {
                StepNight(Dt);
                if (i % 40 == 39)
                {
                    yield return null;
                }
            }
            // Everyone was tucked in Safe light: nobody was struck.
            Assert.IsFalse(LogContains("monster_attacked_villager", null));
            foreach (var v in _villagers)
            {
                Assert.AreNotEqual(VillagerState.Dead, v.State);
                Assert.AreNotEqual(VillagerState.Missing, v.State);
            }

            // ---- Dawn: the verdict ----
            CrossIntoPhase(DayPhase.Dawn);
            _session.Evaluate();

            Assert.AreEqual(GameOutcome.Win, _session.Outcome);
            Assert.AreEqual(LossReason.None, _session.Reason);
            Assert.AreEqual(1, _decidedCount, "OutcomeDecided fires once");
            Assert.AreEqual(GameOutcome.Win, _lastDecided.Outcome);
            Assert.GreaterOrEqual(_lastDecided.VillagersAlive,
                _sessionConfig.villagerWinThreshold);
            Assert.IsTrue(_lastDecided.BellkeeperAlive);
            Assert.IsTrue(_lastDecided.AbbeyFireLit);
            Assert.IsTrue(LogContains("session", "outcome=Win"));
            yield return null;
        }

        // ------------------------------------------------------------------
        // LOSS paths
        // ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Loss_WhenBellkeeperDies()
        {
            BuildWorld(villagerCount: 8, whiteNightIndex: 1);
            GameSession.OutcomeDecided += OnDecided;
            _session.Begin();

            _hero.TakeDamage(999f);
            Assert.IsFalse(_hero.IsAlive);
            _session.Evaluate();

            Assert.AreEqual(GameOutcome.Loss, _session.Outcome);
            Assert.AreEqual(LossReason.BellkeeperDead, _session.Reason);
            Assert.IsTrue(LogContains("session", "outcome=Loss reason=BellkeeperDead"));
            Assert.AreEqual(1, _decidedCount);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Loss_WhenAbbeyFireGoesOut()
        {
            BuildWorld(villagerCount: 8, whiteNightIndex: 1);
            GameSession.OutcomeDecided += OnDecided;
            _session.Begin();

            _session.Evaluate();                 // the flame is seen burning
            _flame.Extinguish();
            Assert.IsFalse(_flame.isLit);
            _session.Evaluate();

            Assert.AreEqual(GameOutcome.Loss, _session.Outcome);
            Assert.AreEqual(LossReason.AbbeyFireOut, _session.Reason);
            Assert.IsTrue(LogContains("session", "outcome=Loss reason=AbbeyFireOut"));
            Assert.AreEqual(1, _decidedCount);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Loss_WhenAllVillagersDieOrFlee()
        {
            BuildWorld(villagerCount: 6, whiteNightIndex: 1);
            GameSession.OutcomeDecided += OnDecided;
            _session.Begin();

            for (int i = 0; i < _villagers.Count; i++)
            {
                // Half die, half flee (Missing) — the settlement is emptied either way.
                _villagers[i].ForceState(i % 2 == 0
                    ? VillagerState.Dead : VillagerState.Missing);
            }
            _session.Evaluate();

            Assert.AreEqual(GameOutcome.Loss, _session.Outcome);
            Assert.AreEqual(LossReason.VillagersLost, _session.Reason);
            Assert.IsTrue(LogContains("session", "outcome=Loss reason=VillagersLost"));
            Assert.AreEqual(1, _decidedCount);
            yield return null;
        }

        // ------------------------------------------------------------------
        // World construction
        // ------------------------------------------------------------------

        void BuildWorld(int villagerCount, int whiteNightIndex)
        {
            _proto = ScriptableObject.CreateInstance<PrototypeConfig>();
            _assets.Add(_proto);
            _proto.dayDurationSeconds = 10f;
            _proto.duskDurationSeconds = 10f;
            _proto.nightDurationSeconds = 10f;
            _proto.dawnDurationSeconds = 10f;
            _proto.edgeBandFraction = 0.3f;
            _proto.arrivalRadius = 0.3f;
            _proto.campfireRadius = 10f; // Safe within 7
            _proto.bellkeeperMaxHealth = 100f;
            // Keep the night's outcome controlled by shelter alone: no darkness harm.
            _proto.villagerFearPerSecondInDark = 0f;
            _proto.villagerFearPerSecondInEdge = 0f;
            _proto.villagerInjuredDarkSeconds = 1000f;
            _proto.villagerMissingDarkSeconds = 2000f;
            _proto.duskRecallEndangeredDistance = 12f;
            _proto.bellRadius = 15f;
            _proto.monsterSpawnMinRadius = 20f;
            _proto.monsterSpawnMaxRadius = 30f;
            _proto.monsterSpawnAttempts = 64;
            _proto.monsterMaxHealth = 50f;
            _proto.monsterMoveSpeed = 2.5f;
            _proto.monsterFleeSpeed = 4f;
            _proto.monsterLightTolerance = 0.15f;
            _proto.monsterSightRange = 60f;
            _proto.simulationSeed = 909;

            _sessionConfig = ScriptableObject.CreateInstance<GameSessionConfig>();
            _assets.Add(_sessionConfig);
            _sessionConfig.villagerWinThreshold = 6;
            _sessionConfig.whiteNightIndex = whiteNightIndex;
            _sessionConfig.whiteNightSchedule = new[]
            {
                "0.15:pale_hound",
                "0.50:pale_hound",
                "0.80:whisper",
            };

            DarknessEvaluator.Config = _proto;
            DuskRecallSystem.Config = _proto;

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
            flameGO.transform.position = new Vector3(2f, 0f, 0f);
            _flame = flameGO.AddComponent<LightSource>();
            _flame.autoTick = false;
            _flame.sacred = true;
            _flame.radius = 8f;
            _flame.strength = 1f;
            _flame.fuelSeconds = -1f; // sacred flames do not simply burn out
            _flame.isLit = true;

            var heroGO = Track(new GameObject("Bellkeeper"));
            heroGO.transform.position = Vector3.zero;
            _hero = heroGO.AddComponent<BellkeeperController>();
            _hero.autoTick = false;
            _hero.useDirectInput = false;
            _hero.Configure(_proto);

            var directorGO = Track(new GameObject("Director"));
            directorGO.transform.position = Vector3.zero;
            _director = directorGO.AddComponent<NightmareDirector>();
            _director.monstersAutoTick = false;
            _director.autoTick = false;
            _director.Config = _proto;

            var scenarioGO = Track(new GameObject("Scenario"));
            _scenario = scenarioGO.AddComponent<FirstWhiteNightScenario>();
            _scenario.Config = _sessionConfig;
            _scenario.director = _director;
            _scenario.Clear();

            var sessionGO = Track(new GameObject("GameSession"));
            _session = sessionGO.AddComponent<GameSession>();
            _session.autoEvaluate = false;
            _session.Config = _sessionConfig;
            _session.bellkeeper = _hero;
            _session.abbeyFlame = _flame;
            _session.Clear();

            for (int i = 0; i < villagerCount; i++)
            {
                float angle = i / (float)villagerCount * Mathf.PI * 2f;
                var pos = new Vector3(Mathf.Cos(angle) * 4f, 0f, Mathf.Sin(angle) * 4f);
                _villagers.Add(CreateVillager($"Villager_{i:D2}", pos, seed: i));
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

        // ------------------------------------------------------------------
        // Deterministic stepping
        // ------------------------------------------------------------------

        /// <summary>One night step: director + live monsters + villagers (clock held).</summary>
        void StepNight(float dt)
        {
            _director.Tick(dt);
            var monsters = _director.SpawnedMonsters;
            for (int i = 0; i < monsters.Count; i++)
            {
                var m = monsters[i];
                if (m != null && m.IsAlive && m.gameObject.activeSelf)
                {
                    m.Tick(dt);
                }
            }
            for (int i = 0; i < _villagers.Count; i++)
            {
                if (_villagers[i] != null)
                {
                    _villagers[i].Tick(dt);
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
    }
}
