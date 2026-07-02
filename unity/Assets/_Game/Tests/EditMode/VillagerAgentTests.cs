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
    /// Villager state-machine table: day work loop, dusk recall (delayed and
    /// bell-boosted), panic, injured, missing, rescue. All ticks are manual and
    /// deterministic; the config is injected so no default balance value matters.
    /// </summary>
    public class VillagerAgentTests
    {
        readonly List<GameObject> _spawned = new List<GameObject>();
        PrototypeConfig _config;
        GameClock _clock;

        [SetUp]
        public void SetUp()
        {
            EventBus.ResetAll();
            GameEventLog.Clear();
            DarknessEvaluator.Clear();
            DuskRecallSystem.Clear();
            MonsterController.ClearRegistry();

            _config = ScriptableObject.CreateInstance<PrototypeConfig>();
            _config.dayDurationSeconds = 10f;
            _config.duskDurationSeconds = 30f;
            _config.nightDurationSeconds = 30f;
            _config.dawnDurationSeconds = 30f;
            _config.edgeBandFraction = 0.3f;
            _config.arrivalRadius = 0.3f;
            _config.villagerWalkSpeed = 2f;
            _config.villagerPanicSpeed = 4f;
            _config.villagerFearPerSecondInDark = 0.5f;
            _config.villagerFearPerSecondInEdge = 0.1f;
            _config.villagerFearRecoveryPerSecond = 1f;
            _config.braveFearMultiplier = 0.4f;
            _config.villagerPanicFearThreshold = 0.6f;
            _config.villagerPanicBreakFearFraction = 0.5f;
            _config.villagerInjuredDarkSeconds = 3f;
            _config.villagerMissingDarkSeconds = 6f;
            _config.villagerWorkDurationSeconds = 0.5f;
            _config.villagerPickupDurationSeconds = 0.2f;
            _config.villagerPanicDirectionChangeSeconds = 0.5f;
            _config.villagerRestDurationSeconds = 1f;
            _config.rescueFollowDistance = 1.5f;
            _config.braveryFinishWorkThreshold = 0.65f;
            _config.duskRecallEndangeredDistance = 12f;
            _config.duskLateRecallDelaySeconds = 2f;
            _config.bellRecallSpeedMultiplier = 1.5f;
            _config.bellPulseMemorySeconds = 30f;
            _config.bellCalmAmount = 0.2f;

            DarknessEvaluator.Config = _config;
            DuskRecallSystem.Config = _config;

            var clockGO = new GameObject("TestClock");
            _spawned.Add(clockGO);
            _clock = clockGO.AddComponent<GameClock>();
            _clock.autoTick = false;
            _clock.Configure(_config);
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
            DuskRecallSystem.Clear();
            DarknessEvaluator.Clear();
            MonsterController.ClearRegistry();
            EventBus.ResetAll();
            GameEventLog.Clear();
        }

        VillagerAgent SpawnVillager(Vector3 position, float bravery, int seed = 1)
        {
            var go = new GameObject($"TestVillager_{_spawned.Count}");
            _spawned.Add(go);
            go.transform.position = position;
            var villager = go.AddComponent<VillagerAgent>();
            villager.autoTick = false;
            villager.Config = _config;
            villager.seed = seed;
            if (bravery >= 0f)
            {
                villager.Bravery = bravery; // negative = keep the seed-derived value
            }
            DuskRecallSystem.Register(villager); // defensive, mirrors OnEnable
            return villager;
        }

        LightSource SpawnLight(Vector3 position, float radius)
        {
            var go = new GameObject($"TestLight_{_spawned.Count}");
            _spawned.Add(go);
            go.transform.position = position;
            var source = go.AddComponent<LightSource>();
            source.radius = radius;
            source.strength = 1f;
            source.isLit = true;
            source.fuelSeconds = -1f;
            source.autoTick = false;
            DarknessEvaluator.Register(source); // defensive, mirrors OnEnable
            return source;
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

        [Test]
        public void WorkLoop_CyclesThroughTaskAndStorageStates()
        {
            var villager = SpawnVillager(Vector3.zero, bravery: 0.5f);
            villager.AssignWork(new Vector3(0f, 0f, 4f), Vector3.zero);

            var seen = new List<VillagerState> { villager.State };
            for (int i = 0; i < 200 && seen.Count < 6; i++)
            {
                villager.Tick(0.1f);
                if (villager.State != seen[seen.Count - 1])
                {
                    seen.Add(villager.State);
                }
            }

            CollectionAssert.AreEqual(new[]
            {
                VillagerState.AssignedToWork,
                VillagerState.WalkingToTask,
                VillagerState.Working,
                VillagerState.CarryingResource,
                VillagerState.ReturningToStorage,
                VillagerState.WalkingToTask, // the loop repeats
            }, seen);
            Assert.IsTrue(LogContains("villager_deposited_resource"));
        }

        [Test]
        public void DuskRecall_UncoveredVillager_RecallsAfterTheLateDelay()
        {
            _config.villagerFearPerSecondInDark = 0f; // isolate recall from fear
            _config.villagerFearPerSecondInEdge = 0f;
            _config.villagerInjuredDarkSeconds = 100f; // and from darkness injuries
            _config.villagerMissingDarkSeconds = 200f;
            SpawnLight(Vector3.zero, radius: 10f); // Safe within 7
            var villager = SpawnVillager(new Vector3(12f, 0f, 0f), bravery: 0f);

            _clock.Tick(10f); // -> Dusk, DuskRecallSystem evaluates
            Assert.AreEqual(DayPhase.Dusk, _clock.Phase);
            Assert.IsTrue(villager.IsRecallOrdered, "uncovered villager gets a pending recall");
            Assert.AreEqual(VillagerState.Idle, villager.State, "the late delay has not elapsed");

            villager.Tick(1f); // 1s of the 2s delay
            Assert.AreEqual(VillagerState.Idle, villager.State);

            villager.Tick(1.1f); // past the delay
            Assert.AreEqual(VillagerState.ReturningToLight, villager.State);

            bool reachedSafe = false;
            for (int i = 0; i < 100 && !reachedSafe; i++)
            {
                villager.Tick(0.1f);
                reachedSafe = villager.CurrentZone == LightZone.Safe;
            }
            Assert.IsTrue(reachedSafe, "recalled villager must walk into Safe light");
            Assert.AreEqual(VillagerState.Idle, villager.State);
            Assert.IsTrue(LogContains("villager_reached_light"));
        }

        [Test]
        public void DuskRecall_BellCoveredVillager_RecallsImmediately()
        {
            _config.villagerFearPerSecondInDark = 0f;
            _config.villagerFearPerSecondInEdge = 0f;
            _config.villagerInjuredDarkSeconds = 100f;
            _config.villagerMissingDarkSeconds = 200f;
            SpawnLight(Vector3.zero, radius: 10f);
            var villager = SpawnVillager(new Vector3(12f, 0f, 0f), bravery: 0f);

            int endangered = 0;
            EventBus.VillagerEndangered += _ => endangered++;

            EventBus.RaiseBellRang(new Vector3(12f, 0f, 0f), 5f); // pulse covers villager
            _clock.Tick(10f); // -> Dusk

            Assert.AreEqual(VillagerState.ReturningToLight, villager.State,
                "bell-covered villagers leave immediately, no drama delay");
            Assert.AreEqual(0, endangered);

            bool reachedSafe = false;
            for (int i = 0; i < 100 && !reachedSafe; i++)
            {
                villager.Tick(0.1f);
                reachedSafe = villager.CurrentZone == LightZone.Safe;
            }
            Assert.IsTrue(reachedSafe);
            Assert.IsTrue(LogContains("villager_reached_light"));
        }

        [Test]
        public void DuskEvaluation_FarUncoveredVillager_RaisesVillagerEndangered()
        {
            SpawnLight(Vector3.zero, radius: 10f);
            var far = SpawnVillager(new Vector3(30f, 0f, 0f), bravery: 0f);
            var near = SpawnVillager(new Vector3(9f, 0f, 0f), bravery: 0f);

            var endangered = new List<GameObject>();
            EventBus.VillagerEndangered += go => endangered.Add(go);

            _clock.Tick(10f); // -> Dusk

            Assert.AreEqual(1, endangered.Count, "only the far villager is endangered");
            Assert.AreSame(far.gameObject, endangered[0]);
            Assert.IsTrue(LogContains("VillagerEndangered"));
            Assert.AreNotEqual(VillagerState.Missing, near.State);
        }

        [Test]
        public void DarkAtDusk_PanicsThenInjuredThenMissing_InOrder()
        {
            // No lights anywhere: the villager cannot save itself.
            _config.duskLateRecallDelaySeconds = 100f; // keep auto-recall out of the way
            var villager = SpawnVillager(new Vector3(5f, 0f, 5f), bravery: 0f, seed: 7);

            _clock.Tick(10f); // -> Dusk
            Assert.AreEqual(LightZone.Dark, villager.CurrentZone);

            bool sawPanic = false;
            bool sawInjured = false;
            int guard = 0;
            while (villager.State != VillagerState.Missing && guard++ < 100)
            {
                villager.Tick(0.1f);
                sawPanic |= villager.State == VillagerState.Panicking;
                sawInjured |= villager.State == VillagerState.Injured;
            }

            Assert.IsTrue(sawPanic, "fear 0.5/s must cross the 0.6 panic threshold");
            Assert.IsTrue(sawInjured, "3s of continuous darkness must injure");
            Assert.AreEqual(VillagerState.Missing, villager.State, "6s of darkness means Missing");
            Assert.IsTrue(LogContains("villager_injured_by_darkness"));
            Assert.IsTrue(LogContains("villager_missing"));
            Assert.AreEqual(1f, villager.Fear, 1e-3f, "fear saturates in the dark");
        }

        [Test]
        public void BellOrder_CalmsFear_AndCutsThroughToReturning()
        {
            _config.duskLateRecallDelaySeconds = 100f;
            var villager = SpawnVillager(new Vector3(5f, 0f, 5f), bravery: 0f);

            _clock.Tick(10f); // -> Dusk (auto recall pending but delayed 100s)
            villager.Tick(1f); // 1s in the dark: fear 0.5, below panic threshold
            Assert.AreEqual(0.5f, villager.Fear, 1e-3f);

            villager.OrderReturnToLight(bellBoosted: true);

            Assert.AreEqual(0.3f, villager.Fear, 1e-3f, "the bell removes bellCalmAmount fear");
            Assert.AreEqual(VillagerState.ReturningToLight, villager.State);
        }

        [Test]
        public void BraveVillager_FinishesWorkBeforeRecalling()
        {
            var villager = SpawnVillager(Vector3.zero, bravery: 1f);
            villager.AssignWork(new Vector3(0f, 0f, 1f), new Vector3(0f, 0f, 5f));

            int guard = 0;
            while (villager.State != VillagerState.Working && guard++ < 50)
            {
                villager.Tick(0.1f);
            }
            Assert.AreEqual(VillagerState.Working, villager.State);

            villager.OrderReturnToLight(bellBoosted: false);
            Assert.AreEqual(VillagerState.Working, villager.State,
                "bravery >= threshold finishes the work item first");
            Assert.IsTrue(LogContains("villager_finishing_work"));

            villager.Tick(0.6f); // work duration (0.5s) elapses
            Assert.AreEqual(VillagerState.ReturningToLight, villager.State);
        }

        [Test]
        public void Rescue_FollowsEscort_AndCompletesWhenReleasedInSafeLight()
        {
            SpawnLight(Vector3.zero, radius: 10f);
            var villager = SpawnVillager(new Vector3(12f, 0f, 0f), bravery: 0f);
            var escortGO = new GameObject("TestEscort");
            _spawned.Add(escortGO);
            escortGO.transform.position = new Vector3(12f, 0f, 0f);

            GameObject rescued = null;
            EventBus.VillagerRescued += go => rescued = go;

            Assert.IsTrue(villager.BeginRescue(escortGO.transform));
            Assert.IsTrue(villager.IsEscorted);
            Assert.AreEqual(VillagerState.ReturningToLight, villager.State);

            // Walk the escort into the Safe zone; the villager trails it.
            escortGO.transform.position = new Vector3(4f, 0f, 0f);
            for (int i = 0; i < 100; i++)
            {
                villager.Tick(0.1f);
            }
            Assert.LessOrEqual(
                PlanarMotion.Distance(villager.transform.position, escortGO.transform.position),
                _config.rescueFollowDistance + 0.05f,
                "rescued-follow keeps the villager at follow distance");

            Assert.IsTrue(villager.ReleaseRescue(), "released inside Safe light completes the rescue");
            Assert.AreSame(villager.gameObject, rescued);
            Assert.AreEqual(VillagerState.Idle, villager.State);
            Assert.AreEqual(0f, villager.Fear);
        }

        [Test]
        public void Rescue_ReleasedInDark_VillagerWalksOnAlone()
        {
            SpawnLight(Vector3.zero, radius: 10f);
            var villager = SpawnVillager(new Vector3(20f, 0f, 0f), bravery: 0f);
            var escortGO = new GameObject("TestEscort");
            _spawned.Add(escortGO);
            escortGO.transform.position = new Vector3(20f, 0f, 0f);

            Assert.IsTrue(villager.BeginRescue(escortGO.transform));
            Assert.IsFalse(villager.ReleaseRescue(), "release in the dark is not a completed rescue");
            Assert.IsFalse(villager.IsEscorted);
            Assert.AreEqual(VillagerState.ReturningToLight, villager.State,
                "the villager keeps walking to the nearest light on its own");
        }

        [Test]
        public void MonsterAttack_InjuresHealthy_KillsInjured()
        {
            var villager = SpawnVillager(Vector3.zero, bravery: 0.5f);

            villager.OnMonsterAttack();
            Assert.AreEqual(VillagerState.Injured, villager.State);
            Assert.AreEqual(1f, villager.Fear);

            villager.OnMonsterAttack();
            Assert.AreEqual(VillagerState.Dead, villager.State);
            Assert.IsTrue(LogContains("villager_died"));

            villager.Tick(1f); // dead villagers do nothing
            Assert.AreEqual(VillagerState.Dead, villager.State);
        }

        [Test]
        public void Bravery_IsDeterministicFromSeed_AndInsideConfigRange()
        {
            var a = SpawnVillager(Vector3.zero, bravery: -1f, seed: 42);
            var b = SpawnVillager(Vector3.one, bravery: -1f, seed: 42);
            var c = SpawnVillager(Vector3.one * 2f, bravery: -1f, seed: 43);

            Assert.AreEqual(a.Bravery, b.Bravery, 1e-6f, "same seed, same bravery");
            Assert.That(a.Bravery, Is.InRange(_config.villagerBraveryMin, _config.villagerBraveryMax));
            Assert.That(c.Bravery, Is.InRange(_config.villagerBraveryMin, _config.villagerBraveryMax));
        }
    }
}
