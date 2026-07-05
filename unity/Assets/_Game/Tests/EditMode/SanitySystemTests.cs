using System.Collections.Generic;
using Abbey.Beast;
using Abbey.Buildings;
using Abbey.Core;
using Abbey.Economy;
using Abbey.Light;
using Abbey.Sanity;
using Abbey.Villagers;
using NUnit.Framework;
using UnityEngine;

namespace Abbey.Tests.EditMode
{
    /// <summary>
    /// The sanity/dread pipeline (P3-03): dread accumulates only in the Dark at night,
    /// sustained dread drives sanity down until the villager goes Insane (logging the
    /// event), the work-efficiency multiplier follows the config curve and an insane
    /// villager downs tools, morning resets a wounded villager's health but never its
    /// sanity, and the beast (no VillagerAgent) is never tracked. Deterministic manual
    /// ticks; the world is built programmatically and every config is injected.
    /// </summary>
    public class SanitySystemTests
    {
        readonly List<GameObject> _spawned = new List<GameObject>();
        PrototypeConfig _proto;
        EconomyConfig _econ;
        JobsConfig _jobs;
        SanityConfig _sanity;
        GameClock _clock;
        SanitySystem _system;

        [SetUp]
        public void SetUp()
        {
            ClearStatics();

            _proto = ScriptableObject.CreateInstance<PrototypeConfig>();
            _proto.dayDurationSeconds = 1f;
            _proto.duskDurationSeconds = 1f;
            _proto.nightDurationSeconds = 1f;
            _proto.dawnDurationSeconds = 1f;
            _proto.edgeBandFraction = 0.3f;
            _proto.arrivalRadius = 0.3f;
            _proto.villagerWalkSpeed = 2f;
            _proto.villagerFearPerSecondInDark = 0f;
            _proto.villagerFearPerSecondInEdge = 0f;
            _proto.villagerInjuredDarkSeconds = 10000f;
            _proto.villagerMissingDarkSeconds = 20000f;
            _proto.villagerWorkDurationSeconds = 0.5f;
            _proto.villagerPickupDurationSeconds = 0.1f;

            _econ = ScriptableObject.CreateInstance<EconomyConfig>();
            _econ.baseStorageCapacity = 100;
            _econ.storagePileCapacity = 100;

            _jobs = ScriptableObject.CreateInstance<JobsConfig>();
            _jobs.carryCapacity = 4;
            _jobs.woodcutterWorkDurationSeconds = 0.3f;
            _jobs.woodcutterYieldPerCycle = 2;

            _sanity = ScriptableObject.CreateInstance<SanityConfig>();
            _sanity.shakenThreshold = 0.7f;
            _sanity.breakingThreshold = 0.4f;
            _sanity.insanityThreshold = 0.2f;
            _sanity.releaseThreshold = 0.5f;
            _sanity.dreadGainPerSecondInDark = 0.5f;
            _sanity.dreadDecayPerSecond = 0.25f;
            _sanity.dreadDamageThreshold = 0.5f;
            _sanity.sanityDamagePerSecondAtHighDread = 0.4f;
            _sanity.asylumRecoveryPerSecond = 0.2f;
            _sanity.homeRecoveryPerSecond = 0.05f;
            _sanity.asylumCooldownDays = 1;
            _sanity.dreadSpillPerNight = 0.2f;
            _sanity.workEfficiencyFloor = 0.3f;
            _sanity.workFullSanity = 0.8f;

            DarknessEvaluator.Config = _proto;
            DuskRecallSystem.Config = _proto;
            ResourceLedger.Config = _econ;

            var clockGO = new GameObject("TestClock");
            _spawned.Add(clockGO);
            _clock = clockGO.AddComponent<GameClock>();
            _clock.autoTick = false;
            _clock.Configure(_proto);

            var sysGO = new GameObject("SanitySystem");
            _spawned.Add(sysGO);
            _system = sysGO.AddComponent<SanitySystem>();
            _system.autoTick = false;
            _system.Config = _sanity;
        }

        [TearDown]
        public void TearDown()
        {
            if (_system != null)
            {
                Object.DestroyImmediate(_system.gameObject);
                _system = null;
            }
            foreach (var go in _spawned)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }
            _spawned.Clear();
            Object.DestroyImmediate(_proto);
            Object.DestroyImmediate(_econ);
            Object.DestroyImmediate(_jobs);
            Object.DestroyImmediate(_sanity);
            ClearStatics();
        }

        static void ClearStatics()
        {
            EventBus.ResetAll();
            GameEventLog.Clear();
            DarknessEvaluator.Clear();
            DuskRecallSystem.Clear();
            ResourceLedger.Clear();
            JobWorkPoint.ClearRegistry();
            EconomyConfig.ClearCache();
            JobsConfig.ClearCache();
            SanityConfig.ClearCache();
            PrototypeConfig.ClearCache();
        }

        // ---- Programmatic world helpers ----------------------------------

        VillagerAgent SpawnVillager(Vector3 position, int seed = 1)
        {
            var go = new GameObject($"TestVillager_{_spawned.Count}");
            _spawned.Add(go);
            go.transform.position = position;
            var villager = go.AddComponent<VillagerAgent>();
            villager.autoTick = false;
            villager.Config = _proto;
            villager.seed = seed;
            villager.Bravery = 0.5f;
            DuskRecallSystem.Register(villager);
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
            DarknessEvaluator.Register(source);
            return source;
        }

        void AdvanceClockTo(DayPhase target)
        {
            int guard = 10000;
            while (_clock.Phase != target && guard-- > 0)
            {
                _clock.Tick(0.25f);
            }
            Assert.Greater(guard, 0, $"clock never reached {target}");
        }

        static bool LogContains(string type, string dataFragment)
        {
            var records = GameEventLog.Records;
            for (int i = 0; i < records.Count; i++)
            {
                if (records[i].Type == type && records[i].Data.Contains(dataFragment))
                {
                    return true;
                }
            }
            return false;
        }

        // ------------------------------------------------------------------

        [Test]
        public void Dread_Accumulates_OnlyInDark_AtNight()
        {
            var darkV = SpawnVillager(new Vector3(100f, 0f, 100f)); // no light: Dark
            SpawnLight(Vector3.zero, 10f);
            var safeV = SpawnVillager(Vector3.zero); // inside the light: Safe

            AdvanceClockTo(DayPhase.Night);
            _system.Tick(1f);

            var darkRec = _system.RecordFor(darkV);
            var safeRec = _system.RecordFor(safeV);
            Assert.Greater(darkRec.Dread, 0f, "a villager caught in the dark at night gains dread");
            Assert.AreEqual(0f, safeRec.Dread, 1e-4f, "a villager in Safe light gains no dread");

            // Daytime: the dark villager's dread must not climb (it eases).
            AdvanceClockTo(DayPhase.Day);
            float before = darkRec.Dread;
            _system.Tick(1f);
            Assert.LessOrEqual(darkRec.Dread, before, "dread does not accumulate by day");
        }

        [Test]
        public void SustainedDark_CrossesThreshold_FlipsInsane_AndLogs()
        {
            var v = SpawnVillager(new Vector3(100f, 0f, 100f));
            GameObject insaneGo = null;
            EventBus.VillagerWentInsane += go => insaneGo = go;

            AdvanceClockTo(DayPhase.Night);
            var rec = _system.RecordFor(v);
            int guard = 0;
            while (!rec.IsInsane && guard++ < 2000)
            {
                _system.Tick(0.2f);
            }

            Assert.IsTrue(rec.IsInsane, "sustained dark exposure must drive the villager insane");
            Assert.Less(rec.Sanity, _sanity.insanityThreshold, "sanity fell below the insanity threshold");
            Assert.AreSame(v.gameObject, insaneGo, "VillagerWentInsane fired for the villager");
            Assert.IsTrue(LogContains("sanity_state", "->Insane"));
            Assert.AreEqual(0f, v.SanityWorkEfficiency, 1e-4f, "an insane villager has zero work efficiency");
        }

        [Test]
        public void WorkEfficiency_FollowsConfigCurve()
        {
            Assert.AreEqual(0f, _sanity.WorkEfficiency(_sanity.insanityThreshold), 1e-4f,
                "at/below the insanity threshold work stops");
            Assert.AreEqual(0f, _sanity.WorkEfficiency(0.1f), 1e-4f);
            Assert.AreEqual(1f, _sanity.WorkEfficiency(_sanity.workFullSanity), 1e-4f,
                "at full-sanity the multiplier is 1");
            Assert.AreEqual(1f, _sanity.WorkEfficiency(0.95f), 1e-4f);

            // Midpoint sanity 0.5: t = (0.5-0.2)/(0.8-0.2) = 0.5 -> Lerp(0.3, 1, 0.5) = 0.65.
            Assert.AreEqual(0.65f, _sanity.WorkEfficiency(0.5f), 1e-3f);
        }

        [Test]
        public void ReducedSanity_ScalesWork_InsanityStopsWork()
        {
            var pile = SpawnPile(Vector3.zero);
            SpawnWorkPoint(new Vector3(0f, 0f, 3f), VillagerJob.Woodcutter);
            var agent = SpawnWorker(Vector3.zero, VillagerJob.Woodcutter);

            // Half efficiency: yield 2 * 0.5 = 1 wood per felling trip.
            agent.Villager.SanityWorkEfficiency = 0.5f;
            int guard = 0;
            while (ResourceLedger.Get(ResourceType.Wood) < 1 && guard++ < 4000)
            {
                Step(agent);
            }
            Assert.GreaterOrEqual(ResourceLedger.Get(ResourceType.Wood), 1);
            Assert.IsTrue(LogContains("resource", "wood +1 (woodcutter)"),
                "a shaken woodcutter deposits the scaled-down yield");

            // Zero efficiency (insane): the woodcutter downs tools entirely.
            float woodBefore = ResourceLedger.Get(ResourceType.Wood);
            agent.Villager.SanityWorkEfficiency = 0f;
            for (int i = 0; i < 500; i++)
            {
                Step(agent);
            }
            Assert.AreEqual(woodBefore, ResourceLedger.Get(ResourceType.Wood),
                "an insane villager produces nothing");
            Assert.AreNotEqual(VillagerState.Working, agent.Villager.State,
                "an insane villager is not working");
            Assert.IsNotNull(pile);
        }

        [Test]
        public void MorningReset_RestoresHealth_NotSanity()
        {
            var v = SpawnVillager(Vector3.zero);
            var rec = _system.RecordFor(v);
            rec.Sanity = 0.55f; // damaged but not insane
            v.ForceState(VillagerState.Injured);

            _system.MorningReset();

            Assert.AreEqual(VillagerState.Idle, v.State, "morning heals the wound (health resets)");
            Assert.AreEqual(0.55f, rec.Sanity, 1e-4f, "sanity is the persistent track: it does not reset");
        }

        [Test]
        public void Beast_IsImmune_NeverTracked()
        {
            var v = SpawnVillager(new Vector3(100f, 0f, 100f));
            var houndGo = new GameObject("Hound");
            _spawned.Add(houndGo);
            houndGo.AddComponent<HoundController>().autoTick = false;

            Assert.IsNull(houndGo.GetComponent<VillagerAgent>(),
                "the hound is not a villager — it carries no sanity component");

            AdvanceClockTo(DayPhase.Night);
            for (int i = 0; i < 20; i++)
            {
                _system.Tick(0.2f);
            }

            Assert.AreEqual(1, _system.Records.Count, "only the villager is tracked, never the hound");
            Assert.AreSame(v, _system.Records[0].Villager);
        }

        // ---- Job helpers (efficiency scaling) ----------------------------

        StoragePile SpawnPile(Vector3 position)
        {
            var go = new GameObject($"TestPile_{_spawned.Count}");
            _spawned.Add(go);
            go.transform.position = position;
            return go.AddComponent<StoragePile>();
        }

        JobWorkPoint SpawnWorkPoint(Vector3 position, VillagerJob job)
        {
            var go = new GameObject($"TestWorkPoint_{_spawned.Count}");
            _spawned.Add(go);
            go.transform.position = position;
            var point = go.AddComponent<JobWorkPoint>();
            point.job = job;
            return point;
        }

        VillagerJobAgent SpawnWorker(Vector3 position, VillagerJob job)
        {
            var villager = SpawnVillager(position);
            var agent = villager.gameObject.AddComponent<VillagerJobAgent>();
            agent.autoTick = false;
            agent.Config = _jobs;
            agent.SetJob(job);
            return agent;
        }

        static void Step(VillagerJobAgent agent, float dt = 0.05f)
        {
            agent.Villager.Tick(dt);
            agent.Tick(dt);
        }
    }
}
