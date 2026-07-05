using System.Collections.Generic;
using Abbey.Beast;
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
    /// EditMode coverage for the standing laws (P3-09). Worlds are built programmatically
    /// and the config is injected, so each law group's mechanical effect is asserted against
    /// its data: the Food ration pass draws the configured ration per class from the ledger
    /// (Workers First reduces the idle share; Fasting cuts all and applies hunger + sanity;
    /// Beast Share feeds the hound first); the Night labour law gates the overdrive levers;
    /// a scripted death under each Burial option produces the right costs/refunds + grave
    /// tag; the Hound law writes the P3-07 doctrine; the Old rites law drives the daily
    /// sanctity / old-faith pressure + tags; and a decree is cooldown-gated and event-logged.
    /// </summary>
    public class LawEffectsTests
    {
        readonly List<GameObject> _spawned = new List<GameObject>();
        readonly List<Object> _assets = new List<Object>();
        LawsConfig _laws;
        PrototypeConfig _proto;
        EconomyConfig _econ;

        [SetUp]
        public void SetUp()
        {
            ClearStatics();
            _laws = ScriptableObject.CreateInstance<LawsConfig>();
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
            if (LawSystem.Instance != null)
            {
                Object.DestroyImmediate(LawSystem.Instance.gameObject);
            }
            if (SanitySystem.Instance != null)
            {
                Object.DestroyImmediate(SanitySystem.Instance.gameObject);
            }
            if (HoundEvolutionSystem.Instance != null)
            {
                Object.DestroyImmediate(HoundEvolutionSystem.Instance.gameObject);
            }
            foreach (var go in _spawned)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }
            _spawned.Clear();
            Object.DestroyImmediate(_laws);
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
            LawsConfig.ClearCache();
            PrototypeConfig.ClearCache();
            EconomyConfig.ClearCache();
        }

        // ---- Helpers -----------------------------------------------------

        LawSystem MakeLaws()
        {
            var go = new GameObject("LawSystem");
            _spawned.Add(go);
            var sys = go.AddComponent<LawSystem>();
            sys.Configure(_laws);
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

        HoundEvolutionSystem MakeHound()
        {
            var go = new GameObject("HoundEvolution");
            _spawned.Add(go);
            var sys = go.AddComponent<HoundEvolutionSystem>();
            var cfg = ScriptableObject.CreateInstance<HoundEvolutionConfig>();
            _assets.Add(cfg);
            sys.Configure(cfg);
            return sys;
        }

        VillagerAgent MakeVillager(VillagerState state)
        {
            var go = new GameObject("Villager");
            _spawned.Add(go);
            var v = go.AddComponent<VillagerAgent>();
            v.autoTick = false;
            v.Config = _proto;
            if (state != VillagerState.Idle)
            {
                v.ForceState(state);
            }
            return v;
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

        // ---- Food ---------------------------------------------------------

        [Test]
        public void Food_Equal_IssuesFlatRationsFromLedger()
        {
            ResourceLedger.Add(ResourceType.Food, 100, "test");
            MakeVillager(VillagerState.Working);
            MakeVillager(VillagerState.Working);
            MakeVillager(VillagerState.Idle);
            var laws = MakeLaws();
            var e = _laws.FoodEffectFor(FoodLaw.Equal);
            int before = ResourceLedger.Get(ResourceType.Food);

            laws.IssueRations();

            int expected = 2 * e.workerRation + 1 * e.idleRation + e.houndRation;
            Assert.AreEqual(before - expected, ResourceLedger.Get(ResourceType.Food),
                "Equal draws the flat ration for each villager plus the hound keep");
            Assert.AreEqual(expected, laws.FoodIssuedLastPass);
            Assert.IsTrue(LogContains("RationsIssued", "law=rations_equal"));
        }

        [Test]
        public void Food_WorkersFirst_ReducesIdleRation()
        {
            ResourceLedger.Add(ResourceType.Food, 100, "test");
            MakeVillager(VillagerState.Working);
            MakeVillager(VillagerState.Working);
            MakeVillager(VillagerState.Idle);
            var laws = MakeLaws();
            Assert.IsTrue(laws.DecreeFood(FoodLaw.WorkersFirst));
            var e = _laws.FoodEffectFor(FoodLaw.WorkersFirst);
            int before = ResourceLedger.Get(ResourceType.Food);

            laws.IssueRations();

            int expected = 2 * e.workerRation + 1 * e.idleRation + e.houndRation;
            Assert.AreEqual(before - expected, ResourceLedger.Get(ResourceType.Food),
                "the idle villager draws the reduced ration exactly per config");
            Assert.Less(e.idleRation, e.workerRation, "Workers First feeds the idle less than a worker");
        }

        [Test]
        public void Food_Fasting_CutsAll_AppliesHungerAndSanity()
        {
            ResourceLedger.Add(ResourceType.Food, 100, "test");
            var sanity = MakeSanity();
            var v = MakeVillager(VillagerState.Idle);
            var record = sanity.RecordFor(v);
            var laws = MakeLaws();
            Assert.IsTrue(laws.DecreeFood(FoodLaw.Fasting));
            var equal = _laws.FoodEffectFor(FoodLaw.Equal);
            var fast = _laws.FoodEffectFor(FoodLaw.Fasting);

            laws.IssueRations();

            Assert.Less(fast.idleRation, equal.idleRation, "Fasting cuts the ration below Equal");
            Assert.AreEqual(1f - fast.sanityCostPerVillager, record.Sanity, 1e-4f,
                "each villager pays the fasting sanity cost");
            Assert.AreEqual(fast.hungerPerVillager, laws.Hunger, 1e-4f,
                "hunger pressure rises by the config amount per villager");
        }

        [Test]
        public void Food_BeastShare_FeedsHoundFirst()
        {
            // Only enough food for the hound's fuller keep — under Beast Share it eats first
            // and the villager goes without, proving the ordering.
            ResourceLedger.Add(ResourceType.Food, 3, "test");
            MakeVillager(VillagerState.Working);
            var laws = MakeLaws();
            Assert.IsTrue(laws.DecreeFood(FoodLaw.BeastShare));
            var e = _laws.FoodEffectFor(FoodLaw.BeastShare);
            Assert.IsTrue(e.feedHoundFirst);
            Assert.AreEqual(3, e.houndRation, "the default Beast Share keep matches this scenario");

            laws.IssueRations();

            Assert.AreEqual(0, ResourceLedger.Get(ResourceType.Food),
                "the hound consumed the stores before the villager");
            Assert.IsTrue(LogContains("RationsIssued", "beast +3"));
            Assert.AreEqual(3, laws.FoodIssuedLastPass);
        }

        // ---- Night labour -------------------------------------------------

        [Test]
        public void NightLabour_GateMatrix_AcrossThreeOptions()
        {
            var laws = MakeLaws();

            laws.nightLabour = NightLabourLaw.Forced;
            Assert.IsTrue(laws.IsPermittedByLaw(OverdriveActionId.ForcedNightWork));
            Assert.IsTrue(laws.IsPermittedByLaw(OverdriveActionId.VolunteerWatch));
            Assert.IsTrue(laws.IsPermittedByLaw(OverdriveActionId.BellToll),
                "a non-night-labour lever is always permitted");

            laws.nightLabour = NightLabourLaw.NoWorkAfterBell;
            Assert.IsFalse(laws.IsPermittedByLaw(OverdriveActionId.ForcedNightWork));
            Assert.IsFalse(laws.IsPermittedByLaw(OverdriveActionId.CandleLine));
            Assert.IsFalse(laws.IsPermittedByLaw(OverdriveActionId.VolunteerWatch));
            Assert.IsTrue(laws.IsPermittedByLaw(OverdriveActionId.BellToll),
                "No Work After Bell only bars the night-labour levers");

            laws.nightLabour = NightLabourLaw.PaidRisk;
            Assert.IsTrue(laws.IsPermittedByLaw(OverdriveActionId.ForcedNightWork));
            Assert.IsTrue(laws.IsPermittedByLaw(OverdriveActionId.VolunteerWatch));
        }

        [Test]
        public void NightLabour_Decree_RepointsOverdriveGate()
        {
            var overdrive = new GameObject("Overdrive").AddComponent<OverdriveSystem>();
            _spawned.Add(overdrive.gameObject);
            overdrive.autoTick = false;
            var laws = MakeLaws();

            Assert.IsTrue(laws.DecreeNightLabour(NightLabourLaw.NoWorkAfterBell));
            Assert.IsFalse(overdrive.IsPermitted(OverdriveActionId.CandleLine),
                "the overdrive system now reads the Night labour gate");
            Assert.IsTrue(overdrive.IsPermitted(OverdriveActionId.BellToll));
        }

        // ---- Burial -------------------------------------------------------

        [Test]
        public void Burial_FullRites_CostsCandles_BoostsMercy_StampsGraveTag()
        {
            ResourceLedger.Add(ResourceType.Candles, 10, "test");
            var laws = MakeLaws(); // FullRites is the default burial law
            var e = _laws.BurialEffectFor(BurialLaw.FullRites);
            int before = ResourceLedger.Get(ResourceType.Candles);

            laws.ProcessBurial("Alma");

            int candleCost = 0;
            for (int i = 0; i < e.cost.Count; i++)
            {
                if (e.cost[i].type == ResourceType.Candles) candleCost += e.cost[i].amount;
            }
            Assert.AreEqual(before - candleCost, ResourceLedger.Get(ResourceType.Candles),
                "Full Rites spend the configured candles");
            Assert.Greater(laws.Mercy, 0f, "honouring the dead lifts mercy");
            Assert.IsTrue(LogContains("burial", "tag=grave_full_rites"));
        }

        [Test]
        public void Burial_MassGraves_Cheap_FearHit_SanctityFall()
        {
            var laws = MakeLaws();
            Assert.IsTrue(laws.DecreeBurial(BurialLaw.MassGraves));

            laws.ProcessBurial("Bran");

            Assert.Greater(laws.Fear, 0f, "mass graves stoke fear");
            Assert.Less(laws.Sanctity, 0f, "mass graves cost sanctity");
            Assert.IsTrue(LogContains("burial", "tag=grave_mass"));
        }

        [Test]
        public void Burial_UseTheDead_RefundsResources_BooksNightmareTag()
        {
            var laws = MakeLaws();
            Assert.IsTrue(laws.DecreeBurial(BurialLaw.UseTheDead));
            var e = _laws.BurialEffectFor(BurialLaw.UseTheDead);

            laws.ProcessBurial("Cael");

            for (int i = 0; i < e.refund.Count; i++)
            {
                Assert.AreEqual(e.refund[i].amount, ResourceLedger.Get(e.refund[i].type),
                    "the corpse refunds the configured resources");
            }
            Assert.Greater(laws.NightmarePressure, 0f, "using the dead books a nightmare toll");
            Assert.Less(laws.Mercy, 0f, "using the dead is a heavy mercy hit");
            Assert.IsTrue(LogContains("burial", "tag=grave_used"));
        }

        [Test]
        public void Burial_DawnScan_BuriesTheNightsDead()
        {
            var laws = MakeLaws(); // default FullRites, no candles needed to stamp a grave
            GameEventLog.Append("villager_died", "Dara");
            GameEventLog.Append("villager_died", "Enid");

            laws.RunDailyUpkeep();

            Assert.IsTrue(LogContains("burial", "deceased=Dara"));
            Assert.IsTrue(LogContains("burial", "deceased=Enid"));
        }

        // ---- Hound --------------------------------------------------------

        [Test]
        public void Hound_Decree_WritesDoctrineIntoEvolutionSystem()
        {
            var houndSystem = MakeHound();
            var laws = MakeLaws();
            // Configure applied the default Family doctrine onto the P3-07 system.
            Assert.AreEqual(HoundDoctrine.Family, houndSystem.Doctrine,
                "the default Hound law seeds the doctrine on enable");

            Assert.IsTrue(laws.DecreeHound(HoundLaw.Weapon));
            Assert.AreEqual(HoundDoctrine.Weapon, houndSystem.Doctrine,
                "decreeing the Hound law feeds P3-07 its doctrine");
        }

        // ---- Old rites ----------------------------------------------------

        [Test]
        public void OldRites_TolerateOfferings_VentsPressure_EmitsOffering()
        {
            var laws = MakeLaws();
            Assert.IsTrue(laws.DecreeOldRites(OldRitesLaw.TolerateOfferings));
            var e = _laws.OldRitesEffectFor(OldRitesLaw.TolerateOfferings);

            laws.ApplyOldRitesDaily();

            Assert.AreEqual(e.sanctityDeltaPerDay, laws.Sanctity, 1e-4f);
            Assert.AreEqual(e.oldFaithPressureDeltaPerDay, laws.OldFaithPressure, 1e-4f);
            Assert.Less(laws.OldFaithPressure, 0f, "toleration vents old-faith pressure");
            Assert.IsTrue(LogContains("old_rite", LawTags.OfferingMade));
        }

        [Test]
        public void OldRites_ForbidPaganRites_BuildsPressure_EmitsSecretRite()
        {
            var laws = MakeLaws(); // ForbidPaganRites is the default old-rites law
            var e = _laws.OldRitesEffectFor(OldRitesLaw.ForbidPaganRites);

            laws.ApplyOldRitesDaily();

            Assert.AreEqual(e.oldFaithPressureDeltaPerDay, laws.OldFaithPressure, 1e-4f);
            Assert.Greater(laws.OldFaithPressure, 0f, "forbidding builds old-faith pressure");
            Assert.IsTrue(LogContains("old_rite", LawTags.SecretRite));
        }

        // ---- Decree lifecycle ---------------------------------------------

        [Test]
        public void Decree_Cooldown_BlocksImmediateRelegislation()
        {
            var laws = MakeLaws();
            Assert.IsTrue(laws.DecreeFood(FoodLaw.WorkersFirst), "the first decree passes");
            Assert.IsFalse(laws.DecreeFood(FoodLaw.BeastShare),
                "a second Food decree the same day is blocked by the cooldown");
            Assert.AreEqual(FoodLaw.WorkersFirst, laws.ActiveFood, "the blocked decree did not take");
            Assert.IsTrue(LogContains("decree_refused", "reason=cooldown"));
        }

        [Test]
        public void Decree_IsEventLogged_WithGroupAndTag()
        {
            var laws = MakeLaws();
            Assert.IsTrue(laws.DecreeFood(FoodLaw.Fasting));
            Assert.IsTrue(LogContains("decree", "group=Food"));
            Assert.IsTrue(LogContains("decree", "tag=fasting_active"));
        }

        [Test]
        public void ActiveTags_ExposesAllFiveStandingTags()
        {
            var laws = MakeLaws();
            var tags = laws.ActiveTags();
            Assert.AreEqual(5, tags.Length);
            CollectionAssert.Contains(tags, LawTags.For(laws.ActiveFood));
            CollectionAssert.Contains(tags, LawTags.For(laws.ActiveHound));
            Assert.AreEqual(LawTags.For(laws.ActiveBurial), laws.TagFor(LawGroup.Burial));
        }
    }
}
