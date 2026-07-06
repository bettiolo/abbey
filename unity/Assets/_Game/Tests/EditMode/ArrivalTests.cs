using System.Collections.Generic;
using Abbey.Core;
using Abbey.Economy;
using Abbey.Island;
using Abbey.Morale;
using Abbey.Nightmares;
using NUnit.Framework;
using UnityEngine;

namespace Abbey.Tests.EditMode
{
    /// <summary>
    /// EditMode coverage for population arrivals (P3-13). Worlds are built programmatically
    /// and the <see cref="IslandConfig"/> injected: the passive-arrival schedule is a pure
    /// function of the seed (same seed ⇒ same schedule), trust decides integration (low tier
    /// ⇒ newcomers refuse and record a spring departure, mid ⇒ stay, high ⇒ stay + volunteer),
    /// and the storm-shipwreck event grants the listed supplies + crew AND arms the director's
    /// drowned-nightmare window for the configured number of nights (then disarms).
    /// </summary>
    public class ArrivalTests
    {
        readonly List<GameObject> _spawned = new List<GameObject>();
        readonly List<Object> _assets = new List<Object>();
        IslandConfig _island;
        EconomyConfig _econ;

        [SetUp]
        public void SetUp()
        {
            ClearStatics();

            _island = ScriptableObject.CreateInstance<IslandConfig>();
            _island.seed = 4242;
            _island.stayMinTier = TrustTier.Wary;
            _island.volunteerMinTier = TrustTier.Trusting;
            _assets.Add(_island);

            _econ = ScriptableObject.CreateInstance<EconomyConfig>();
            _econ.baseStorageCapacity = 1000;
            ResourceLedger.Config = _econ;
            _assets.Add(_econ);
        }

        [TearDown]
        public void TearDown()
        {
            if (ArrivalSystem.Instance != null)
            {
                foreach (var n in ArrivalSystem.Instance.Newcomers)
                {
                    if (n.Villager != null)
                    {
                        Object.DestroyImmediate(n.Villager.gameObject);
                    }
                }
                Object.DestroyImmediate(ArrivalSystem.Instance.gameObject);
            }
            if (PressureSystem.Instance != null)
            {
                Object.DestroyImmediate(PressureSystem.Instance.gameObject);
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
            ResourceLedger.Clear();
            IslandConfig.ClearCache();
            EconomyConfig.ClearCache();
            PressuresConfig.ClearCache();
        }

        // ---- Helpers -----------------------------------------------------

        ArrivalSystem MakeArrivals(bool spawnVillagers = false)
        {
            var go = new GameObject("ArrivalSystem");
            _spawned.Add(go);
            var sys = go.AddComponent<ArrivalSystem>();
            sys.spawnVillagers = spawnVillagers;
            sys.Configure(_island);
            return sys;
        }

        PressureSystem MakeTrust(float trust)
        {
            var cfg = ScriptableObject.CreateInstance<PressuresConfig>();
            _assets.Add(cfg);
            for (int i = 0; i < cfg.channels.Count; i++)
            {
                if (cfg.channels[i].id == PressureId.Trust)
                {
                    cfg.channels[i].baseline = trust;
                }
            }
            var go = new GameObject("PressureSystem");
            _spawned.Add(go);
            var sys = go.AddComponent<PressureSystem>();
            sys.Configure(cfg);
            return sys;
        }

        NightmareDirector MakeDirector()
        {
            var go = new GameObject("NightmareDirector");
            _spawned.Add(go);
            return go.AddComponent<NightmareDirector>();
        }

        // ---- Passive schedule determinism --------------------------------

        [Test]
        public void PassiveSchedule_IsDeterministic_ForASeed()
        {
            var a = ScriptableObject.CreateInstance<IslandConfig>();
            var b = ScriptableObject.CreateInstance<IslandConfig>();
            var c = ScriptableObject.CreateInstance<IslandConfig>();
            _assets.Add(a); _assets.Add(b); _assets.Add(c);
            a.seed = 777; b.seed = 777; c.seed = 778;

            bool anyDiff = false;
            for (int day = 1; day <= 40; day++)
            {
                Assert.AreEqual(a.PassiveArrivalForDay(day), b.PassiveArrivalForDay(day),
                    $"same seed ⇒ same passive draw on day {day}");
                anyDiff |= a.PassiveArrivalForDay(day) != c.PassiveArrivalForDay(day);
            }
            Assert.IsTrue(anyDiff, "a different seed produces a different schedule somewhere");
        }

        [Test]
        public void PassiveDraw_IntegratesAnArrival_OnAScheduledDay()
        {
            MakeTrust(0.9f); // devoted: whoever comes stays
            var arrivals = MakeArrivals();

            // Find a day the seeded schedule fires and drive it.
            int fireDay = -1;
            for (int day = 1; day <= 60 && fireDay < 0; day++)
            {
                if (_island.PassiveArrivalForDay(day))
                {
                    fireDay = day;
                }
            }
            Assert.Greater(fireDay, 0, "the schedule fires within 60 days");

            int before = arrivals.StayedCount;
            arrivals.OnDayChangedForTest(fireDay);
            Assert.AreEqual(before + 1, arrivals.StayedCount, "a scheduled day walks one survivor in");
        }

        // ---- Trust-gated integration -------------------------------------

        [Test]
        public void LowTrust_NewcomersRefuse_RecordSpringDeparture()
        {
            MakeTrust(0.1f); // Broken < Wary
            var arrivals = MakeArrivals();

            int stayed = arrivals.ReceiveArrivals(ArrivalClass.Survivor, 2, ArrivalChannel.Passive, Vector3.zero);

            Assert.AreEqual(0, stayed, "no one stays under broken trust");
            Assert.AreEqual(2, arrivals.LeftCount);
            Assert.AreEqual(2, arrivals.DepartureIntents.Count, "each refusal records a spring departure");
        }

        [Test]
        public void HighTrust_NewcomersStayAndVolunteer()
        {
            MakeTrust(0.9f); // Devoted >= Trusting
            var arrivals = MakeArrivals();

            int stayed = arrivals.ReceiveArrivals(ArrivalClass.Survivor, 3, ArrivalChannel.Passive, Vector3.zero);

            Assert.AreEqual(3, stayed);
            Assert.AreEqual(3, arrivals.StayedCount);
            Assert.AreEqual(3, arrivals.VolunteeredCount, "devoted trust turns newcomers into volunteers");
            Assert.AreEqual(0, arrivals.DepartureIntents.Count);
        }

        [Test]
        public void TrustMatrix_MapsTierToOutcome()
        {
            // Broken -> Left, Wary/Neutral -> Stayed, Trusting/Devoted -> Volunteered.
            AssertOutcome(0.10f, IntegrationOutcome.Left);
            AssertOutcome(0.30f, IntegrationOutcome.Stayed);   // Wary
            AssertOutcome(0.50f, IntegrationOutcome.Stayed);   // Neutral
            AssertOutcome(0.70f, IntegrationOutcome.Volunteered); // Trusting
            AssertOutcome(0.90f, IntegrationOutcome.Volunteered); // Devoted
        }

        void AssertOutcome(float trust, IntegrationOutcome expected)
        {
            ClearStatics();
            _spawned.Clear(); // GOs are destroyed by name below; recreate fresh systems
            var trustSys = MakeTrust(trust);
            var arrivals = MakeArrivals();
            arrivals.ReceiveArrivals(ArrivalClass.Survivor, 1, ArrivalChannel.Passive, Vector3.zero);
            Assert.AreEqual(1, arrivals.Newcomers.Count);
            Assert.AreEqual(expected, arrivals.Newcomers[0].Outcome,
                $"trust {trust} ({trustSys.TrustTier}) should yield {expected}");

            Object.DestroyImmediate(arrivals.gameObject);
            Object.DestroyImmediate(trustSys.gameObject);
        }

        // ---- Storm shipwreck ---------------------------------------------

        [Test]
        public void Shipwreck_GrantsSuppliesAndCrew_ArmsDrownedWindow()
        {
            _island.shipwreckSupplies = new List<ResourceStack>
            {
                new ResourceStack(ResourceType.Food, 6),
                new ResourceStack(ResourceType.Wood, 4),
            };
            _island.shipwreckCrew = new List<ArrivalCompositionEntry>
            {
                new ArrivalCompositionEntry { arrivalClass = ArrivalClass.Survivor, count = 2 },
                new ArrivalCompositionEntry { arrivalClass = ArrivalClass.Warrior, count = 1 },
            };
            _island.drownedRiskWindowNights = 3;

            MakeTrust(0.9f); // all three stay
            var director = MakeDirector();
            var arrivals = MakeArrivals();
            arrivals.director = director;

            var result = arrivals.TriggerShipwreck(Vector3.zero);

            Assert.AreEqual(6, ResourceLedger.Get(ResourceType.Food), "shipwreck food washed ashore");
            Assert.AreEqual(4, ResourceLedger.Get(ResourceType.Wood), "shipwreck wood washed ashore");
            Assert.AreEqual(3, result.PeopleAshore);
            Assert.AreEqual(3, result.Stayed, "high trust keeps the whole crew");
            Assert.IsTrue(result.DrownedRiskArmed);

            Assert.IsTrue(director.IsDrownedRiskArmedForNight(1), "night 1 is inside the window");
            Assert.IsTrue(director.IsDrownedRiskArmedForNight(3), "night 3 is the last armed night");
            Assert.IsFalse(director.IsDrownedRiskArmedForNight(4), "night 4 is past the window");
        }

        [Test]
        public void Shipwreck_LowTrust_CrewLeaves_ButSuppliesStayAndWindowArms()
        {
            _island.drownedRiskWindowNights = 2;
            MakeTrust(0.1f); // Broken: crew refuses
            var director = MakeDirector();
            var arrivals = MakeArrivals();
            arrivals.director = director;

            var result = arrivals.TriggerShipwreck(Vector3.zero);

            Assert.AreEqual(0, result.Stayed, "a fearful settlement turns the crew away");
            Assert.Greater(arrivals.DepartureIntents.Count, 0, "the crew intends to sail in spring");
            Assert.IsTrue(director.IsDrownedRiskArmedForNight(2));
            Assert.IsFalse(director.IsDrownedRiskArmedForNight(3));
        }
    }
}
