using System.Collections;
using System.Collections.Generic;
using Abbey.Combat;
using Abbey.Core;
using Abbey.Decrees;
using Abbey.Economy;
using Abbey.Light;
using Abbey.Nightmares;
using Abbey.Villagers;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Abbey.Tests.PlayMode
{
    /// <summary>
    /// PlayMode coverage for the emergency overdrive levers (P3-08) in fully programmatic
    /// worlds. Candle Line — carriers hold mobile <see cref="LightSource"/>s that light a
    /// route Safe at night, night work proceeds at the lit worksite, candles drain per
    /// upkeep interval, and the carriers stand down at dawn; Lantern Overburn — the light's
    /// radius climbs and its fuel drains at the multiplied rate over time; deferred debt —
    /// after a debt-heavy night a later night's director wave exceeds the no-debt baseline
    /// under the same seed. Deterministic: autoTick off, manual Tick with fixed dt; the
    /// dawn settle runs off the clock's phase event.
    /// </summary>
    public class CandleLinePlayModeTests
    {
        const float Dt = 0.1f;

        readonly List<GameObject> _spawned = new List<GameObject>();
        readonly List<ScriptableObject> _assets = new List<ScriptableObject>();
        GameClock _clock;

        [SetUp]
        public void SetUp()
        {
            ClearStatics();
        }

        [TearDown]
        public void TearDown()
        {
            if (OverdriveSystem.Instance != null)
            {
                Object.DestroyImmediate(OverdriveSystem.Instance.gameObject);
            }
            var monsters = new List<MonsterController>(MonsterController.Active);
            foreach (var m in monsters)
            {
                if (m != null)
                {
                    Object.DestroyImmediate(m.gameObject);
                }
            }
            foreach (var go in _spawned)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }
            _spawned.Clear();
            foreach (var a in _assets)
            {
                if (a != null)
                {
                    Object.DestroyImmediate(a);
                }
            }
            _assets.Clear();
            ClearStatics();
        }

        static void ClearStatics()
        {
            EventBus.ResetAll();
            GameEventLog.Clear();
            DarknessEvaluator.Clear();
            DuskRecallSystem.Clear();
            MonsterController.ClearRegistry();
            ResourceLedger.Clear();
            TrustLedger.Clear();
            BeastStatusLedger.Clear();
            OverdriveConfig.ClearCache();
            CombatConfig.ClearCache();
            PrototypeConfig.ClearCache();
            EconomyConfig.ClearCache();
        }

        // ---- World construction ------------------------------------------

        PrototypeConfig CreateConfig()
        {
            var config = ScriptableObject.CreateInstance<PrototypeConfig>();
            _assets.Add(config);
            config.edgeBandFraction = 0.3f;
            config.arrivalRadius = 0.3f;
            config.dayDurationSeconds = 1f;
            config.duskDurationSeconds = 1f;
            config.nightDurationSeconds = 1f;
            config.dawnDurationSeconds = 1f;
            config.villagerWalkSpeed = 3f;
            config.simulationSeed = 4242;
            config.phase3NightsEnabled = true;
            DarknessEvaluator.Config = config;
            DuskRecallSystem.Config = config;
            return config;
        }

        EconomyConfig CreateEcon()
        {
            var econ = ScriptableObject.CreateInstance<EconomyConfig>();
            _assets.Add(econ);
            econ.baseStorageCapacity = 1000;
            ResourceLedger.Config = econ;
            return econ;
        }

        OverdriveConfig CreateOverdrive()
        {
            var cfg = ScriptableObject.CreateInstance<OverdriveConfig>();
            _assets.Add(cfg);
            return cfg;
        }

        GameClock CreateClock(PrototypeConfig config)
        {
            var go = new GameObject("Clock");
            _spawned.Add(go);
            _clock = go.AddComponent<GameClock>();
            _clock.autoTick = false;
            _clock.Configure(config);
            return _clock;
        }

        OverdriveSystem CreateOverdriveSystem(OverdriveConfig cfg)
        {
            var go = new GameObject("Overdrive");
            _spawned.Add(go);
            var sys = go.AddComponent<OverdriveSystem>();
            sys.autoTick = false;
            sys.Configure(cfg);
            return sys;
        }

        VillagerAgent CreateVillager(Vector3 pos, PrototypeConfig config)
        {
            var go = new GameObject("Villager");
            _spawned.Add(go);
            go.transform.position = pos;
            var v = go.AddComponent<VillagerAgent>();
            v.autoTick = false;
            v.Config = config;
            return v;
        }

        LightSource CreateLantern(Vector3 pos, PrototypeConfig config, float radius, float fuel, float rate)
        {
            var go = new GameObject("Lantern");
            _spawned.Add(go);
            go.transform.position = pos;
            var l = go.AddComponent<LightSource>();
            l.autoTick = false;
            l.radius = radius;
            l.strength = 1f;
            l.fuelSeconds = fuel;
            l.fuelConsumptionPerSecond = rate;
            return l;
        }

        NightmareDirector CreateDirector(PrototypeConfig config)
        {
            var go = new GameObject("Director");
            _spawned.Add(go);
            go.transform.position = Vector3.zero;
            var d = go.AddComponent<NightmareDirector>();
            d.autoTick = false;
            d.monstersAutoTick = false;
            d.Config = config;
            return d;
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

        static bool LogContains(string type, string fragment = null)
        {
            var records = GameEventLog.Records;
            for (int i = 0; i < records.Count; i++)
            {
                if (records[i].Type == type
                    && (fragment == null || records[i].Data.Contains(fragment)))
                {
                    return true;
                }
            }
            return false;
        }

        // ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator CandleLine_LightsRoute_NightWorkProceeds_CandlesDrain_StandDownAtDawn()
        {
            var config = CreateConfig();
            CreateEcon();
            var overdriveCfg = CreateOverdrive();
            overdriveCfg.DefFor(OverdriveActionId.CandleLine).upkeepIntervalSeconds = 0.5f;
            CreateClock(config);
            ResourceLedger.Add(ResourceType.Candles, 20, "test");

            var sys = CreateOverdriveSystem(overdriveCfg);
            var worksite = new Vector3(0f, 0f, 6f);
            var villager = CreateVillager(worksite, config);
            villager.AssignWork(worksite, worksite);

            // Day: fire the Candle Line along the road (0,0,0) -> (0,0,12).
            Assert.AreEqual(DayPhase.Day, _clock.Phase);
            Assert.IsTrue(sys.Activate(OverdriveActionId.CandleLine,
                new OverdriveContext(Vector3.zero, new Vector3(0f, 0f, 12f))));
            Assert.AreEqual(4, sys.ActiveActions[0].CandleLights.Count, "one mobile light per carrier");
            Assert.AreEqual(LightZone.Safe, DarknessEvaluator.Classify(worksite),
                "the carriers light the worksite Safe");

            // Dusk: the exempt villager is not recalled off the job.
            AdvanceClockTo(DayPhase.Dusk);
            Assert.IsFalse(villager.IsRecallOrdered, "an overdriven villager ignores the dusk recall");

            // Night: work proceeds at the lit worksite; the candles drain per interval.
            AdvanceClockTo(DayPhase.Night);
            int candlesAtNight = ResourceLedger.Get(ResourceType.Candles);
            for (int i = 0; i < 40; i++)
            {
                villager.Tick(Dt);
                sys.Tick(Dt);
                if (i % 10 == 9)
                {
                    yield return null;
                }
            }
            Assert.AreEqual(LightZone.Safe, DarknessEvaluator.Classify(worksite),
                "the route stays Safe through the night");
            Assert.AreNotEqual(VillagerState.Missing, villager.State, "the worker never slips into the dark");
            Assert.AreNotEqual(VillagerState.Dead, villager.State);
            Assert.Less(villager.Fear, 0.01f, "no fear accrues in the lit worksite");
            Assert.Less(ResourceLedger.Get(ResourceType.Candles), candlesAtNight,
                "candles drain per hour while the line burns");

            // Dawn: the clock's phase event settles the overdrive — carriers gone, worker handed back.
            AdvanceClockTo(DayPhase.Dawn);
            Assert.AreEqual(0, sys.ActiveActions.Count, "the line stands down at dawn");
            Assert.IsFalse(villager.NightWorkExempt, "the worker is handed back to the recall");
            // The carriers are Destroy()d (deferred in play mode); let the frame process it.
            yield return null;
            Assert.AreEqual(LightZone.Dark, DarknessEvaluator.Classify(worksite),
                "with the carriers gone the road is dark again");
        }

        [UnityTest]
        public IEnumerator LanternOverburn_RadiusUp_FuelDrainsAtMultipliedRate()
        {
            var config = CreateConfig();
            CreateEcon();
            var overdriveCfg = CreateOverdrive();
            CreateClock(config);
            ResourceLedger.Add(ResourceType.Oil, 10, "test");

            var sys = CreateOverdriveSystem(overdriveCfg);
            var boosted = CreateLantern(new Vector3(20f, 0f, 0f), config, 5f, 100f, 1f);
            var control = CreateLantern(new Vector3(60f, 0f, 0f), config, 5f, 100f, 1f);
            var def = overdriveCfg.DefFor(OverdriveActionId.LanternOverburn);

            Assert.IsTrue(sys.Activate(OverdriveActionId.LanternOverburn,
                new OverdriveContext(Vector3.zero, Vector3.zero,
                    new List<LightSource> { boosted })));
            Assert.AreEqual(5f * def.overburnRadiusMultiplier, boosted.radius, 1e-3f,
                "overburn brightens the lantern");

            // Burn both for the same wall-clock time; the overburned one eats more fuel.
            for (int i = 0; i < 100; i++)
            {
                boosted.Tick(Dt);
                control.Tick(Dt);
                if (i % 20 == 19)
                {
                    yield return null;
                }
            }
            float boostedBurned = 100f - boosted.fuelSeconds;
            float controlBurned = 100f - control.fuelSeconds;
            Assert.Greater(boostedBurned, controlBurned * (def.overburnFuelMultiplier - 0.5f),
                "the overburned lantern drains fuel far faster than the control");

            sys.SettleAtDawn();
            Assert.IsFalse(boosted.IsOverburning, "overburn ends at dawn");
            Assert.AreEqual(5f, boosted.radius, 1e-3f, "the base radius is restored");
            yield return null;
        }

        [UnityTest]
        public IEnumerator DeferredDebt_MakesLaterNightRougher_UnderSameSeed()
        {
            var config = CreateConfig();
            CreateEcon();

            // Baseline: a Phase 3 night with no overdrive debt in play.
            var baseline = CreateDirector(config);
            baseline.BeginNight();
            int baselineCount = baseline.SpawnedMonsters.Count;
            Assert.Greater(baselineCount, 0, "the season wave spawned monsters");
            baseline.EndNight();
            baseline.enabled = false;
            yield return null;

            // An earlier night's Abbey Rite booked a heavy nightmare debt.
            var overdriveCfg = CreateOverdrive();
            var sys = CreateOverdriveSystem(overdriveCfg);
            ResourceLedger.Add(ResourceType.Candles, 10, "test");
            Assert.IsTrue(sys.Activate(OverdriveActionId.AbbeyRite));
            sys.SettleAtDawn();
            Assert.Greater(sys.PendingNightmareDebt, 0f, "the rite pooled a nightmare debt");
            int expectedExtra = overdriveCfg.DebtMonsters(sys.PendingNightmareDebt, out _);
            Assert.Greater(expectedExtra, 0);

            // The same night index + seed, now with the debt: the director spawns more.
            var debtNight = CreateDirector(config);
            debtNight.BeginNight();
            int debtCount = debtNight.SpawnedMonsters.Count;

            Assert.AreEqual(baselineCount + expectedExtra, debtCount,
                "the debt adds exactly its extra monsters on top of the season wave");
            Assert.Greater(debtCount, baselineCount, "the debt-heavy history makes a later night rougher");
            Assert.AreEqual(0f, sys.PendingNightmareDebt, 1e-4f, "the night burned off the pooled debt");
            debtNight.EndNight();
            yield return null;
        }
    }
}
