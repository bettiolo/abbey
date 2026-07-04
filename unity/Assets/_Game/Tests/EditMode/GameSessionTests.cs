using System.Collections.Generic;
using Abbey.Core;
using Abbey.Hero;
using Abbey.Light;
using Abbey.Session;
using Abbey.Villagers;
using NUnit.Framework;
using UnityEngine;

namespace Abbey.Tests.EditMode
{
    /// <summary>
    /// P2-08: the pure win/loss truth table (VERTICAL_SLICE_SPEC §11). Minimal
    /// components are posed into each state and <see cref="GameSession.Evaluate"/> is
    /// asserted directly — no scene, no phase driving (the White-Night gate is set via
    /// the public flag). Also verifies the "session" record and that OutcomeDecided
    /// fires exactly once. Deterministic and allocation-light.
    /// </summary>
    public class GameSessionTests
    {
        readonly List<GameObject> _spawned = new List<GameObject>();
        readonly List<ScriptableObject> _assets = new List<ScriptableObject>();
        readonly List<VillagerAgent> _villagers = new List<VillagerAgent>();

        PrototypeConfig _proto;
        GameSessionConfig _sessionConfig;
        GameSession _session;
        BellkeeperController _hero;
        LightSource _flame;
        int _decidedCount;
        SessionSummary _lastDecided;

        [SetUp]
        public void SetUp()
        {
            EventBus.ResetAll();
            GameEventLog.Clear();
            DarknessEvaluator.Clear();
            DuskRecallSystem.Clear();
            GameSession.ResetStaticEvents();
            GameSessionConfig.ClearCache();
            PrototypeConfig.ClearCache();
            _villagers.Clear();
            _decidedCount = 0;

            _proto = ScriptableObject.CreateInstance<PrototypeConfig>();
            _proto.bellkeeperMaxHealth = 100f;
            _assets.Add(_proto);
            DarknessEvaluator.Config = _proto;
            DuskRecallSystem.Config = _proto;

            _sessionConfig = ScriptableObject.CreateInstance<GameSessionConfig>();
            _sessionConfig.villagerWinThreshold = 6;
            _sessionConfig.whiteNightIndex = 2;
            _assets.Add(_sessionConfig);

            // Sacred abbey flame (explicitly assigned to the session).
            var flameGO = Track(new GameObject("AbbeyFlame"));
            _flame = flameGO.AddComponent<LightSource>();
            _flame.autoTick = false;
            _flame.sacred = true;
            _flame.radius = 10f;
            _flame.strength = 1f;
            _flame.fuelSeconds = -1f;
            _flame.isLit = true;

            var heroGO = Track(new GameObject("Bellkeeper"));
            _hero = heroGO.AddComponent<BellkeeperController>();
            _hero.autoTick = false;
            _hero.Configure(_proto);

            var sessionGO = Track(new GameObject("GameSession"));
            _session = sessionGO.AddComponent<GameSession>();
            _session.autoEvaluate = false;
            _session.Config = _sessionConfig;
            _session.bellkeeper = _hero;
            _session.abbeyFlame = _flame;
            _session.Clear();

            GameSession.OutcomeDecided += s => { _decidedCount++; _lastDecided = s; };
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
            GameSession.ResetStaticEvents();
            GameSessionConfig.ClearCache();
            PrototypeConfig.ClearCache();
            DuskRecallSystem.Clear();
            DarknessEvaluator.Clear();
            EventBus.ResetAll();
            GameEventLog.Clear();
        }

        GameObject Track(GameObject go)
        {
            _spawned.Add(go);
            return go;
        }

        void SpawnVillagers(int count, VillagerState state = VillagerState.Idle)
        {
            for (int i = 0; i < count; i++)
            {
                var go = Track(new GameObject($"Villager_{state}_{_villagers.Count:D2}"));
                var v = go.AddComponent<VillagerAgent>();
                v.autoTick = false;
                v.Config = _proto;
                if (state != VillagerState.Idle)
                {
                    v.ForceState(state);
                }
                _villagers.Add(v);
            }
        }

        static int SessionVerdictRecords()
        {
            int n = 0;
            var records = GameEventLog.Records;
            for (int i = 0; i < records.Count; i++)
            {
                if (records[i].Type == GameSession.RecordType
                    && records[i].Data.StartsWith("outcome="))
                {
                    n++;
                }
            }
            return n;
        }

        // ------------------------------------------------------------------
        // WIN
        // ------------------------------------------------------------------

        [Test]
        public void Win_WhenWhiteNightClearedWithThresholdAliveHeroAliveAbbeyLit()
        {
            SpawnVillagers(6);
            _session.WhiteNightCleared = true;

            Assert.AreEqual(GameOutcome.Win, _session.Evaluate());
            Assert.AreEqual(LossReason.None, _session.Reason);
            Assert.AreEqual(1, SessionVerdictRecords(), "exactly one session verdict record");
            Assert.IsTrue(LogContains("outcome=Win"));
            Assert.AreEqual(1, _decidedCount, "OutcomeDecided fires once");
            Assert.AreEqual(6, _lastDecided.VillagersAlive);
            Assert.IsTrue(_lastDecided.BellkeeperAlive);
            Assert.IsTrue(_lastDecided.AbbeyFireLit);
        }

        [Test]
        public void Undecided_BeforeWhiteNightEvenWhenEverythingIsFine()
        {
            SpawnVillagers(8);
            _session.WhiteNightCleared = false;

            Assert.AreEqual(GameOutcome.Undecided, _session.Evaluate());
            Assert.AreEqual(0, SessionVerdictRecords());
            Assert.AreEqual(0, _decidedCount);
        }

        [Test]
        public void Undecided_WhenWhiteNightClearedButBelowThreshold_SoftMiddle()
        {
            SpawnVillagers(5); // survived, but not the 6 the win demands
            _session.WhiteNightCleared = true;

            Assert.AreEqual(GameOutcome.Undecided, _session.Evaluate(),
                "5 alive is neither a hard win nor a hard loss");
            Assert.AreEqual(0, _decidedCount);
        }

        // ------------------------------------------------------------------
        // LOSS
        // ------------------------------------------------------------------

        [Test]
        public void Loss_BellkeeperDead()
        {
            SpawnVillagers(8);
            _session.WhiteNightCleared = true;
            _hero.TakeDamage(999f);
            Assert.IsFalse(_hero.IsAlive);

            Assert.AreEqual(GameOutcome.Loss, _session.Evaluate());
            Assert.AreEqual(LossReason.BellkeeperDead, _session.Reason);
            Assert.IsTrue(LogContains("outcome=Loss reason=BellkeeperDead"));
            Assert.AreEqual(1, _decidedCount);
        }

        [Test]
        public void Loss_AbbeyFireOut_OnlyAfterItWasEverLit()
        {
            SpawnVillagers(8);

            // First pass: flame lit -> undecided, but the session now remembers it burned.
            Assert.AreEqual(GameOutcome.Undecided, _session.Evaluate());

            _flame.Extinguish();
            Assert.IsFalse(_flame.isLit);

            Assert.AreEqual(GameOutcome.Loss, _session.Evaluate());
            Assert.AreEqual(LossReason.AbbeyFireOut, _session.Reason);
            Assert.IsTrue(LogContains("outcome=Loss reason=AbbeyFireOut"));
            Assert.AreEqual(1, _decidedCount);
        }

        [Test]
        public void Loss_VillagersLost_WhenAllDeadOrFled()
        {
            SpawnVillagers(3, VillagerState.Dead);
            SpawnVillagers(3, VillagerState.Missing); // fled == Missing in this slice

            Assert.AreEqual(GameOutcome.Loss, _session.Evaluate());
            Assert.AreEqual(LossReason.VillagersLost, _session.Reason);
            Assert.IsTrue(LogContains("outcome=Loss reason=VillagersLost"));
            Assert.AreEqual(1, _decidedCount);
        }

        [Test]
        public void Loss_NoVillagerLoss_WhenRegistryEmpty()
        {
            // No villagers ever present: the all-dead loss must not fire on an empty world.
            _session.WhiteNightCleared = false;
            Assert.AreEqual(GameOutcome.Undecided, _session.Evaluate());
            Assert.AreEqual(0, _decidedCount);
        }

        [Test]
        public void LossPriority_BellkeeperDeath_BeatsAbbeyFireOut()
        {
            SpawnVillagers(8);
            _session.Evaluate();          // remember the flame ever burned
            _flame.Extinguish();
            _hero.TakeDamage(999f);

            Assert.AreEqual(GameOutcome.Loss, _session.Evaluate());
            Assert.AreEqual(LossReason.BellkeeperDead, _session.Reason,
                "the hero's death is reported ahead of the dead fire");
        }

        // ------------------------------------------------------------------
        // Latching
        // ------------------------------------------------------------------

        [Test]
        public void Verdict_LatchesAndOutcomeDecidedFiresExactlyOnce()
        {
            SpawnVillagers(6);
            _session.WhiteNightCleared = true;

            Assert.AreEqual(GameOutcome.Win, _session.Evaluate());
            // Repeated evaluations (and even a later catastrophe) do not re-decide.
            _hero.TakeDamage(999f);
            _flame.Extinguish();
            Assert.AreEqual(GameOutcome.Win, _session.Evaluate());
            Assert.AreEqual(GameOutcome.Win, _session.Evaluate());

            Assert.AreEqual(1, _decidedCount, "OutcomeDecided fires exactly once");
            Assert.AreEqual(1, SessionVerdictRecords(), "only one verdict is logged");
        }

        static bool LogContains(string dataFragment)
        {
            var records = GameEventLog.Records;
            for (int i = 0; i < records.Count; i++)
            {
                if (records[i].Type == GameSession.RecordType
                    && records[i].Data.Contains(dataFragment))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
