using System.Collections.Generic;
using Abbey.Core;
using Abbey.Decrees;
using Abbey.Economy;
using Abbey.Light;
using Abbey.Sanity;
using Abbey.Villagers;
using NUnit.Framework;
using UnityEngine;

namespace Abbey.Tests.EditMode
{
    /// <summary>
    /// EditMode coverage for the emergency overdrive levers (P3-08). Worlds are built
    /// programmatically and the config is injected, so each lever's costs are asserted
    /// against its data: every action applies exactly its configured immediate costs
    /// (resource ledger / sanity / trust / beast-status) and books its nightmare-debt
    /// points; activation is refused when resources are short, when a cooldown is live or
    /// when the <see cref="OverdriveSystem.IsPermitted"/> hook bars it; the deferred dread
    /// + nightmare debt settle at dawn; and the accumulated debt converts to the director's
    /// extra-monster count.
    /// </summary>
    public class OverdriveCostTests
    {
        readonly List<GameObject> _spawned = new List<GameObject>();
        readonly List<Object> _assets = new List<Object>();
        OverdriveConfig _config;
        PrototypeConfig _proto;
        EconomyConfig _econ;

        [SetUp]
        public void SetUp()
        {
            ClearStatics();
            _config = ScriptableObject.CreateInstance<OverdriveConfig>();
            _proto = ScriptableObject.CreateInstance<PrototypeConfig>();
            _proto.edgeBandFraction = 0.3f;
            _proto.arrivalRadius = 0.3f;
            _econ = ScriptableObject.CreateInstance<EconomyConfig>();
            _econ.baseStorageCapacity = 1000;
            DarknessEvaluator.Config = _proto;
            DuskRecallSystem.Config = _proto;
            ResourceLedger.Config = _econ;
        }

        [TearDown]
        public void TearDown()
        {
            if (OverdriveSystem.Instance != null)
            {
                Object.DestroyImmediate(OverdriveSystem.Instance.gameObject);
            }
            if (SanitySystem.Instance != null)
            {
                Object.DestroyImmediate(SanitySystem.Instance.gameObject);
            }
            foreach (var go in _spawned)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }
            _spawned.Clear();
            Object.DestroyImmediate(_config);
            Object.DestroyImmediate(_proto);
            Object.DestroyImmediate(_econ);
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
            ResourceLedger.Clear();
            TrustLedger.Clear();
            BeastStatusLedger.Clear();
            OverdriveConfig.ClearCache();
            PrototypeConfig.ClearCache();
            EconomyConfig.ClearCache();
        }

        // ---- Helpers -----------------------------------------------------

        OverdriveSystem MakeSystem()
        {
            var go = new GameObject("Overdrive");
            _spawned.Add(go);
            var sys = go.AddComponent<OverdriveSystem>();
            sys.autoTick = false;
            sys.Configure(_config);
            return sys;
        }

        SanitySystem MakeSanity()
        {
            var go = new GameObject("SanitySystem");
            _spawned.Add(go);
            var sanity = go.AddComponent<SanitySystem>();
            sanity.autoTick = false;
            var cfg = ScriptableObject.CreateInstance<SanityConfig>();
            _assets.Add(cfg);
            sanity.Configure(cfg);
            return sanity;
        }

        VillagerAgent MakeVillager(Vector3 pos)
        {
            var go = new GameObject("Villager");
            _spawned.Add(go);
            go.transform.position = pos;
            var v = go.AddComponent<VillagerAgent>();
            v.autoTick = false;
            v.Config = _proto;
            return v;
        }

        LightSource MakeLantern(Vector3 pos, float radius, float fuel, float fuelRate)
        {
            var go = new GameObject("Lantern");
            _spawned.Add(go);
            go.transform.position = pos;
            var l = go.AddComponent<LightSource>();
            l.autoTick = false;
            l.radius = radius;
            l.strength = 1f;
            l.fuelSeconds = fuel;
            l.fuelConsumptionPerSecond = fuelRate;
            return l;
        }

        static bool LogContains(string type, string fragment)
        {
            var records = GameEventLog.Records;
            for (int i = 0; i < records.Count; i++)
            {
                if (records[i].Type == type && records[i].Data.Contains(fragment))
                {
                    return true;
                }
            }
            return false;
        }

        // ---- Per-action immediate costs + debt booking -------------------

        [Test]
        public void ForcedNightWork_AppliesSanityAndTrust_BooksDebt_ExemptsRecall()
        {
            var sanity = MakeSanity();
            var v = MakeVillager(Vector3.zero);
            var record = sanity.RecordFor(v);
            var sys = MakeSystem();
            var def = _config.DefFor(OverdriveActionId.ForcedNightWork);

            Assert.IsTrue(sys.Activate(OverdriveActionId.ForcedNightWork));

            Assert.AreEqual(1f - def.sanityCostPerVillager, record.Sanity, 1e-4f,
                "the participant paid exactly the configured sanity cost");
            Assert.AreEqual(def.trustDelta, TrustLedger.Trust, 1e-4f, "trust cost is the config delta");
            Assert.AreEqual(def.nightmareDebtPoints, sys.TonightDebtAccrual, 1e-4f,
                "the deferred nightmare debt is booked for tonight");
            Assert.IsTrue(v.NightWorkExempt, "the participant is pressed into night service");
        }

        [Test]
        public void CandleLine_ConsumesCandles_SpawnsCarriers_LightsRoute_BooksDebt()
        {
            ResourceLedger.Add(ResourceType.Candles, 10, "test");
            var sys = MakeSystem();
            sys.transform.position = Vector3.zero;
            var def = _config.DefFor(OverdriveActionId.CandleLine);
            int candlesBefore = ResourceLedger.Get(ResourceType.Candles);

            var ctx = new OverdriveContext(Vector3.zero, new Vector3(0f, 0f, 12f));
            Assert.IsTrue(sys.Activate(OverdriveActionId.CandleLine, ctx));

            int paid = 0;
            for (int i = 0; i < def.immediateCost.Count; i++)
            {
                if (def.immediateCost[i].type == ResourceType.Candles)
                {
                    paid += def.immediateCost[i].amount;
                }
            }
            Assert.AreEqual(candlesBefore - paid, ResourceLedger.Get(ResourceType.Candles),
                "exactly the configured candle cost was debited");
            Assert.AreEqual(def.candleCarriers, sys.ActiveActions[0].CandleLights.Count,
                "one mobile light per configured carrier");
            Assert.AreEqual(LightZone.Safe, DarknessEvaluator.Classify(new Vector3(0f, 0f, 6f)),
                "a probe on the route is lit Safe by the candle carriers");
            Assert.AreEqual(def.nightmareDebtPoints, sys.TonightDebtAccrual, 1e-4f);
        }

        [Test]
        public void LanternOverburn_ConsumesOil_BrightensAndBurnsFaster_RestoredAtDawn()
        {
            ResourceLedger.Add(ResourceType.Oil, 10, "test");
            var lantern = MakeLantern(new Vector3(20f, 0f, 0f), 5f, 100f, 1f);
            var sys = MakeSystem();
            var def = _config.DefFor(OverdriveActionId.LanternOverburn);
            int oilBefore = ResourceLedger.Get(ResourceType.Oil);

            Assert.IsTrue(sys.Activate(OverdriveActionId.LanternOverburn,
                new OverdriveContext(Vector3.zero, Vector3.zero,
                    new List<LightSource> { lantern })));

            int oilPaid = 0;
            for (int i = 0; i < def.immediateCost.Count; i++)
            {
                if (def.immediateCost[i].type == ResourceType.Oil)
                {
                    oilPaid += def.immediateCost[i].amount;
                }
            }
            Assert.AreEqual(oilBefore - oilPaid, ResourceLedger.Get(ResourceType.Oil));
            Assert.IsTrue(lantern.IsOverburning);
            Assert.AreEqual(5f * def.overburnRadiusMultiplier, lantern.radius, 1e-4f,
                "radius scaled up by the config multiplier");
            Assert.AreEqual(1f * def.overburnFuelMultiplier, lantern.fuelConsumptionPerSecond, 1e-4f,
                "fuel drains at the config multiplier");

            sys.SettleAtDawn();
            Assert.IsFalse(lantern.IsOverburning, "overburn ends at dawn");
            Assert.AreEqual(5f, lantern.radius, 1e-4f, "the original radius is restored");
            Assert.AreEqual(1f, lantern.fuelConsumptionPerSecond, 1e-4f);
        }

        [Test]
        public void BellToll_RingsBell_AppliesTrustCost()
        {
            var sys = MakeSystem();
            var def = _config.DefFor(OverdriveActionId.BellToll);

            Assert.IsTrue(sys.Activate(OverdriveActionId.BellToll));
            Assert.IsTrue(LogContains("BellRang", "radius"), "a bell pulse was rung");
            Assert.AreEqual(def.trustDelta, TrustLedger.Trust, 1e-4f);
        }

        [Test]
        public void AbbeyRite_ConsumesCandles_BooksHeavyDebt()
        {
            ResourceLedger.Add(ResourceType.Candles, 10, "test");
            var sys = MakeSystem();
            var def = _config.DefFor(OverdriveActionId.AbbeyRite);
            int before = ResourceLedger.Get(ResourceType.Candles);

            Assert.IsTrue(sys.Activate(OverdriveActionId.AbbeyRite));

            int paid = 0;
            for (int i = 0; i < def.immediateCost.Count; i++)
            {
                if (def.immediateCost[i].type == ResourceType.Candles)
                {
                    paid += def.immediateCost[i].amount;
                }
            }
            Assert.AreEqual(before - paid, ResourceLedger.Get(ResourceType.Candles));
            Assert.AreEqual(def.nightmareDebtPoints, sys.TonightDebtAccrual, 1e-4f);
            Assert.Greater(def.nightmareDebtPoints, 3f, "the rite books an old-faith / nightmare toll");
        }

        [Test]
        public void HoundHunt_AppliesBeastStatusCost_BooksDebt()
        {
            var sys = MakeSystem();
            var def = _config.DefFor(OverdriveActionId.HoundHunt);

            Assert.IsTrue(sys.Activate(OverdriveActionId.HoundHunt));
            Assert.AreEqual(def.beastStatusDelta, BeastStatusLedger.BeastStatus, 1e-4f,
                "the hunt spends beast standing");
            Assert.AreEqual(def.nightmareDebtPoints, sys.TonightDebtAccrual, 1e-4f);
        }

        [Test]
        public void VolunteerWatch_AppliesSanity_BooksDebt()
        {
            var sanity = MakeSanity();
            var v = MakeVillager(Vector3.zero);
            var record = sanity.RecordFor(v);
            var sys = MakeSystem();
            var def = _config.DefFor(OverdriveActionId.VolunteerWatch);

            Assert.IsTrue(sys.Activate(OverdriveActionId.VolunteerWatch));
            Assert.AreEqual(1f - def.sanityCostPerVillager, record.Sanity, 1e-4f);
            Assert.AreEqual(def.nightmareDebtPoints, sys.TonightDebtAccrual, 1e-4f);
        }

        // ---- Refusal paths -----------------------------------------------

        [Test]
        public void Activation_Refused_WhenResourcesInsufficient()
        {
            var sys = MakeSystem(); // no candles in the ledger
            Assert.IsFalse(sys.Activate(OverdriveActionId.AbbeyRite),
                "the rite cannot fire without its candles");
            Assert.IsTrue(LogContains("overdrive_refused", "reason=resources"));
            Assert.AreEqual(0f, sys.TonightDebtAccrual, 1e-4f, "a refused lever books no debt");
        }

        [Test]
        public void IsPermitted_Gate_RefusesActivation()
        {
            var sys = MakeSystem();
            sys.PermissionProvider = id => id != OverdriveActionId.ForcedNightWork;

            Assert.IsFalse(sys.IsPermitted(OverdriveActionId.ForcedNightWork));
            Assert.IsFalse(sys.Activate(OverdriveActionId.ForcedNightWork),
                "a barred lever refuses before any cost is paid");
            Assert.IsTrue(LogContains("overdrive_refused", "reason=not_permitted"));
            Assert.IsTrue(sys.Activate(OverdriveActionId.BellToll), "an unbarred lever still fires");
        }

        [Test]
        public void Cooldown_RefusesReactivation_WithinWindow()
        {
            var sys = MakeSystem();
            // HoundHunt has a one-night cooldown by default.
            Assert.IsTrue(sys.Activate(OverdriveActionId.HoundHunt));
            sys.SettleAtDawn(); // stand it down but keep the cooldown memory
            Assert.IsFalse(sys.Activate(OverdriveActionId.HoundHunt),
                "the same night (day) it is still on cooldown");
            Assert.IsTrue(LogContains("overdrive_refused", "reason=cooldown"));
        }

        // ---- Dawn settlement + deferred debt -----------------------------

        [Test]
        public void SettleAtDawn_AppliesDeferredDread_AndPoolsNightmareDebt()
        {
            var sanity = MakeSanity();
            var v = MakeVillager(Vector3.zero);
            var record = sanity.RecordFor(v);
            var sys = MakeSystem();
            var def = _config.DefFor(OverdriveActionId.ForcedNightWork);

            Assert.IsTrue(sys.Activate(OverdriveActionId.ForcedNightWork));
            Assert.AreEqual(0f, record.Dread, 1e-4f, "the dread is deferred, not paid on activation");

            sys.SettleAtDawn();
            Assert.AreEqual(def.deferredDreadPerVillager, record.Dread, 1e-4f,
                "dawn settles the deferred dread onto the participant");
            Assert.AreEqual(def.nightmareDebtPoints, sys.PendingNightmareDebt, 1e-4f,
                "the nightmare debt pools for later nights");
            Assert.IsFalse(v.NightWorkExempt, "the villager is handed back to the recall at dawn");
        }

        [Test]
        public void PendingDebt_ConvertsToDirectorExtraMonsters_AndDrains()
        {
            ResourceLedger.Add(ResourceType.Candles, 10, "test");
            var sys = MakeSystem();
            Assert.IsTrue(sys.Activate(OverdriveActionId.AbbeyRite)); // a debt-heavy lever
            sys.SettleAtDawn();
            float pending = sys.PendingNightmareDebt;
            Assert.Greater(pending, 0f);

            int expected = _config.DebtMonsters(pending, out float consumed);
            int extra = sys.ConsumeNightmareDebtForNight();
            Assert.AreEqual(expected, extra, "the debt buys the config's extra-monster count");
            Assert.Greater(extra, 0);
            Assert.AreEqual(Mathf.Max(0f, pending - consumed), sys.PendingNightmareDebt, 1e-4f,
                "the pool drains by the configured fraction");
        }

        [Test]
        public void Upkeep_DrainsCandlesPerInterval_StandsDownWhenEmpty()
        {
            ResourceLedger.Add(ResourceType.Candles, 5, "test");
            var sys = MakeSystem();
            var def = _config.DefFor(OverdriveActionId.CandleLine);
            def.upkeepIntervalSeconds = 1f; // an "hour" a second for the test

            Assert.IsTrue(sys.Activate(OverdriveActionId.CandleLine,
                new OverdriveContext(Vector3.zero, new Vector3(0f, 0f, 12f))));
            int afterActivate = ResourceLedger.Get(ResourceType.Candles);

            sys.Tick(1f); // one upkeep interval
            Assert.AreEqual(afterActivate - def.upkeepAmount, ResourceLedger.Get(ResourceType.Candles),
                "one interval drains exactly the config upkeep amount");

            // Run enough intervals to exhaust the stock; the lever then stands down.
            for (int i = 0; i < 10; i++)
            {
                sys.Tick(1f);
            }
            Assert.AreEqual(0, ResourceLedger.Get(ResourceType.Candles));
            Assert.IsTrue(LogContains("overdrive_risk", "out_of_candles"));
            Assert.IsFalse(sys.ActiveActions[0].Active, "an out-of-candles line stands down");
            Assert.AreEqual(0, sys.ActiveActions[0].CandleLights.Count, "its carriers are gone");
        }
    }
}
