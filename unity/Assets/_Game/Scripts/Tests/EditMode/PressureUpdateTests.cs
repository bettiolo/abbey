using System.Collections.Generic;
using Abbey.Buildings;
using Abbey.Core;
using Abbey.Decrees;
using Abbey.Economy;
using Abbey.Light;
using Abbey.Morale;
using Abbey.Sanity;
using Abbey.Villagers;
using NUnit.Framework;
using UnityEngine;

namespace Abbey.Tests.EditMode
{
    /// <summary>
    /// EditMode coverage for the moral-pressure store (P3-10). Worlds are built
    /// programmatically and the config is injected, so the deterministic fold is asserted
    /// against its data: scripted event sequences move each channel by exactly the configured
    /// weights, the same log folds to identical pressures twice (determinism), day-markers
    /// decay channels toward baseline, overdrive trust records fold their signed delta, the
    /// trust tiers gate a fake volunteer query, and household sanity aggregates over a
    /// multi-occupant home.
    /// </summary>
    public class PressureUpdateTests
    {
        readonly List<GameObject> _spawned = new List<GameObject>();
        readonly List<Object> _assets = new List<Object>();
        PressuresConfig _cfg;
        PrototypeConfig _proto;
        EconomyConfig _econ;

        [SetUp]
        public void SetUp()
        {
            ClearStatics();
            _cfg = ScriptableObject.CreateInstance<PressuresConfig>();
            _proto = ScriptableObject.CreateInstance<PrototypeConfig>();
            _econ = ScriptableObject.CreateInstance<EconomyConfig>();
            _econ.baseStorageCapacity = 1000;
            DuskRecallSystem.Config = _proto;
            ResourceLedger.Config = _econ;
        }

        [TearDown]
        public void TearDown()
        {
            if (PressureSystem.Instance != null)
            {
                Object.DestroyImmediate(PressureSystem.Instance.gameObject);
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
            Object.DestroyImmediate(_cfg);
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
            AbbeyState.Clear();
            PressuresConfig.ClearCache();
            PrototypeConfig.ClearCache();
            EconomyConfig.ClearCache();
        }

        PressureSystem MakePressures()
        {
            var go = new GameObject("PressureSystem");
            _spawned.Add(go);
            var sys = go.AddComponent<PressureSystem>();
            sys.Configure(_cfg);
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

        VillagerAgent MakeVillager()
        {
            var go = new GameObject("Villager");
            _spawned.Add(go);
            var v = go.AddComponent<VillagerAgent>();
            v.autoTick = false;
            v.Config = _proto;
            return v;
        }

        Building MakeHome()
        {
            var go = new GameObject("Home");
            _spawned.Add(go);
            return go.AddComponent<Building>();
        }

        // ---- Fold ---------------------------------------------------------

        [Test]
        public void TwoDeathsAndMassGraves_MoveMercyFearSanctity()
        {
            GameEventLog.Append("villager_died", "Alma");
            GameEventLog.Append("villager_died", "Bran");
            GameEventLog.Append("burial", "law=mass_graves_active deceased=Alma tag=grave_mass");
            var p = MakePressures();

            p.RecomputeFromLog();

            float baseMercy = _cfg.ChannelFor(PressureId.Mercy).baseline;
            float baseFear = _cfg.ChannelFor(PressureId.Fear).baseline;
            float baseSanctity = _cfg.ChannelFor(PressureId.Sanctity).baseline;
            Assert.Less(p.Mercy, baseMercy, "deaths + mass graves cut mercy below baseline");
            Assert.Greater(p.Fear, baseFear, "deaths + mass graves raise fear above baseline");
            Assert.Less(p.Sanctity, baseSanctity, "mass graves erode sanctity");
        }

        [Test]
        public void SuccessfulRescue_RaisesTrust()
        {
            var p = MakePressures();
            float before = p.Trust;

            GameEventLog.Append("hero_rescue_released", "Cael");
            p.RecomputeFromLog();

            Assert.Greater(p.Trust, before, "a rescue rekindles faith in the bell");
        }

        [Test]
        public void SameLog_FoldsToIdenticalPressures()
        {
            GameEventLog.Append("villager_died", "Alma");
            GameEventLog.Append("home_razed", "Home occupants_killed=1");
            GameEventLog.Append("hero_rescue_released", "Bran");
            var p = MakePressures();

            p.RecomputeFromLog();
            float trust = p.Trust, sanctity = p.Sanctity, mercy = p.Mercy, fear = p.Fear;

            p.RecomputeFromLog(); // idempotent: same log ⇒ same pressures

            Assert.AreEqual(trust, p.Trust, 1e-6f);
            Assert.AreEqual(sanctity, p.Sanctity, 1e-6f);
            Assert.AreEqual(mercy, p.Mercy, 1e-6f);
            Assert.AreEqual(fear, p.Fear, 1e-6f);
        }

        [Test]
        public void DayMarkers_DecayFearTowardBaseline()
        {
            GameEventLog.Append("home_razed", "Home occupants_killed=0"); // fear jumps
            var p = MakePressures();
            p.RecomputeFromLog();
            float raised = p.Fear;

            // Three dawns pass (one RationsIssued villager-pass record each).
            for (int i = 0; i < 3; i++)
            {
                GameEventLog.Append("RationsIssued", "law=rations_equal total=6");
            }
            p.RecomputeFromLog();

            float baseline = _cfg.ChannelFor(PressureId.Fear).baseline;
            Assert.Less(p.Fear, raised, "fear decays as days pass with no new dread");
            Assert.Greater(p.Fear, baseline - 1e-4f, "fear does not overshoot its baseline");
        }

        [Test]
        public void OverdriveTrustRecord_FoldsSignedDelta()
        {
            var p = MakePressures();
            float baseline = p.Trust;

            TrustLedger.Add(0.3f, "overdrive:test"); // logs a signed "trust_pressure" record
            p.RecomputeFromLog();
            Assert.AreEqual(Mathf.Clamp01(baseline + 0.3f), p.Trust, 1e-4f);

            TrustLedger.Add(-0.5f, "overdrive:test");
            p.RecomputeFromLog();
            Assert.AreEqual(Mathf.Clamp01(baseline + 0.3f - 0.5f), p.Trust, 1e-4f);
        }

        // ---- Trust tiers gate a fake volunteer query ----------------------

        [Test]
        public void TrustTiers_GateVolunteerBehaviours()
        {
            var p = MakePressures();

            // Collapse trust: Broken tier bars even the night watch.
            TrustLedger.Add(-0.45f, "collapse");
            p.RecomputeFromLog();
            Assert.AreEqual(TrustTier.Broken, p.TrustTier);
            Assert.IsFalse(p.IsVolunteerEligible(VolunteerRole.NightWatch));
            Assert.IsFalse(p.IsVolunteerEligible(VolunteerRole.WarriorRecruitment));

            // Restore devotion: every behaviour opens up.
            GameEventLog.Clear();
            TrustLedger.Add(0.45f, "restore"); // baseline 0.5 + 0.45 -> 0.95 (Devoted)
            p.RecomputeFromLog();
            Assert.AreEqual(TrustTier.Devoted, p.TrustTier);
            Assert.IsTrue(p.IsVolunteerEligible(VolunteerRole.NightWatch));
            Assert.IsTrue(p.IsVolunteerEligible(VolunteerRole.WarriorRecruitment));
        }

        // ---- Household sanity aggregation ---------------------------------

        [Test]
        public void HouseholdSanity_AggregatesMultiOccupantHome()
        {
            var sanity = MakeSanity();
            var home = MakeHome();
            var v1 = MakeVillager();
            var v2 = MakeVillager();
            sanity.RecordFor(v1).Sanity = 0.4f;
            sanity.RecordFor(v2).Sanity = 0.8f;
            sanity.AssignHome(v1, home);
            sanity.AssignHome(v2, home);

            Assert.AreEqual(0.6f, sanity.HouseholdSanity(home), 1e-4f,
                "household sanity is the mean of its occupants");
            Assert.AreEqual(0.4f, sanity.HouseholdMinSanity(home), 1e-4f,
                "household min is the most-broken housemate");
            Assert.AreEqual(0.6f, sanity.AverageHouseholdSanity(), 1e-4f,
                "the single household's mean is the settlement household aggregate");
        }
    }
}
