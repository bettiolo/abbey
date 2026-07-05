using System.Collections.Generic;
using Abbey.Beast;
using Abbey.Combat;
using Abbey.Core;
using Abbey.Light;
using Abbey.Sanity;
using Abbey.Villagers;
using NUnit.Framework;
using UnityEngine;

namespace Abbey.Tests.EditMode
{
    /// <summary>
    /// EditMode coverage for hound evolution (P3-07). The path scoring is pure and
    /// data-driven, so scripted treatment histories drive each of the five paths
    /// (Guardian / War / Starved / Sacred / Broken) by building
    /// <see cref="HoundTreatmentSample"/>s directly against <see cref="HoundEvolutionConfig"/>;
    /// the doctrine input biases the scores (Chained doctrine + pain ⇒ Broken beats
    /// Guardian); the beast-status output matches config per path; and the beast stays
    /// exempt from the light-band combat penalties and absent from the sanity records on
    /// every path. Worlds are built programmatically; configs are injected.
    /// </summary>
    public class HoundEvolutionTests
    {
        readonly List<GameObject> _spawned = new List<GameObject>();
        readonly List<Object> _assets = new List<Object>();
        HoundEvolutionConfig _config;
        PrototypeConfig _proto;
        CombatConfig _combat;

        [SetUp]
        public void SetUp()
        {
            ClearStatics();
            _config = ScriptableObject.CreateInstance<HoundEvolutionConfig>();
            _proto = ScriptableObject.CreateInstance<PrototypeConfig>();
            _proto.edgeBandFraction = 0.3f;
            _proto.arrivalRadius = 0.3f;
            _combat = ScriptableObject.CreateInstance<CombatConfig>();
            DarknessEvaluator.Config = _proto;
            DuskRecallSystem.Config = _proto;
        }

        [TearDown]
        public void TearDown()
        {
            if (HoundEvolutionSystem.Instance != null)
            {
                Object.DestroyImmediate(HoundEvolutionSystem.Instance.gameObject);
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
            Object.DestroyImmediate(_combat);
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
            HoundEvolutionConfig.ClearCache();
            CombatConfig.ClearCache();
            PrototypeConfig.ClearCache();
        }

        // ---- Helpers -----------------------------------------------------

        static HoundTreatmentSample Sample(
            int feed = 0, int allied = 0, int solo = 0, int rites = 0, float chainMinutes = 0f,
            int injuries = 0, float trust = 0f, float hunger = 0f, float pain = 0f, float fear = 0f,
            float attachment = 0f)
        {
            return new HoundTreatmentSample(feed, allied, solo, rites, chainMinutes, injuries,
                trust, hunger, pain, fear, attachment);
        }

        HoundController MakeHound()
        {
            var go = new GameObject("Hound");
            _spawned.Add(go);
            var h = go.AddComponent<HoundController>();
            h.autoTick = false;
            h.Configure(_proto);
            return h;
        }

        HoundEvolutionSystem MakeSystem(HoundController hound)
        {
            var go = new GameObject("HoundEvolution");
            _spawned.Add(go);
            var sys = go.AddComponent<HoundEvolutionSystem>();
            sys.Configure(_config, hound);
            return sys;
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

        // ---- Five paths from scripted treatment histories ----------------

        [Test]
        public void Guardian_FromFeedingAndCoFighting()
        {
            var s = Sample(feed: 10, allied: 6, trust: 0.9f, attachment: 0.8f, fear: 0.1f, pain: 0.1f);
            var path = _config.DominantPath(s, HoundDoctrine.Neutral, out float score);
            Assert.AreEqual(HoundPath.Guardian, path);
            Assert.GreaterOrEqual(score, _config.pathAdoptThreshold);
        }

        [Test]
        public void War_FromConstantCombatAndWeaponDoctrine()
        {
            var s = Sample(feed: 6, allied: 12, solo: 2, trust: 0.4f, attachment: 0.2f,
                fear: 0.8f, pain: 0.4f, hunger: 0.3f);
            Assert.AreEqual(HoundPath.War, _config.DominantPath(s, HoundDoctrine.Weapon, out _));
        }

        [Test]
        public void Starved_FromNeglectAndSoloHunting()
        {
            var s = Sample(solo: 8, injuries: 1, trust: 0.15f, hunger: 0.9f, fear: 0.3f, pain: 0.3f);
            Assert.AreEqual(HoundPath.Starved, _config.DominantPath(s, HoundDoctrine.Neutral, out _));
        }

        [Test]
        public void Sacred_FromRitesAndSacredDoctrine()
        {
            var s = Sample(feed: 3, allied: 1, rites: 6, trust: 0.7f, attachment: 0.7f,
                fear: 0.1f, pain: 0.1f);
            Assert.AreEqual(HoundPath.Sacred, _config.DominantPath(s, HoundDoctrine.Sacred, out _));
        }

        [Test]
        public void Broken_FromChainingAndBeating()
        {
            var s = Sample(chainMinutes: 6f, injuries: 5, trust: 0.1f, hunger: 0.5f,
                pain: 0.9f, fear: 0.8f, attachment: 0.05f);
            Assert.AreEqual(HoundPath.Broken, _config.DominantPath(s, HoundDoctrine.Chained, out _));
        }

        // ---- Doctrine bias -----------------------------------------------

        [Test]
        public void ChainedDoctrineAndPain_MakesBrokenBeatGuardian()
        {
            // A history that is Guardian-leaning under a Neutral doctrine…
            var s = Sample(feed: 8, allied: 5, injuries: 3, chainMinutes: 4f,
                trust: 0.7f, hunger: 0.3f, pain: 0.7f, fear: 0.6f, attachment: 0.6f);
            Assert.AreEqual(HoundPath.Guardian, _config.DominantPath(s, HoundDoctrine.Neutral, out _),
                "under a neutral doctrine the fed, bonded hound reads Guardian");

            // …flips to Broken once the Chained doctrine biases the scores.
            Assert.AreEqual(HoundPath.Broken, _config.DominantPath(s, HoundDoctrine.Chained, out _),
                "the Chained doctrine + pain make Broken the dominant path");
        }

        [Test]
        public void BelowAdoptThreshold_StaysUnevolved()
        {
            var hound = MakeHound();
            var sys = MakeSystem(hound);
            // A trickle of treatment: dominant score below the adopt threshold.
            hound.Feed();
            hound.Trust = 0.15f;
            hound.Hunger = 0.3f;
            hound.Fear = 0.2f;
            hound.Pain = 0.2f;
            hound.Attachment = 0.05f;
            sys.EvaluateAtDawn();
            Assert.Less(sys.LastDominantScore, _config.pathAdoptThreshold);
            Assert.AreEqual(HoundPath.Unevolved, sys.CurrentPath);
            Assert.IsFalse(sys.PathLocked);
        }

        // ---- Beast status per path ---------------------------------------

        [Test]
        public void BeastStatus_MatchesConfigPerPath()
        {
            // Guardian is beloved (> 0); Broken is feared (< 0); Guardian outranks War.
            float guardian = _config.ProfileFor(HoundPath.Guardian).BeastStatus(0.9f, 0.1f);
            float war = _config.ProfileFor(HoundPath.War).BeastStatus(0.5f, 0.5f);
            float broken = _config.ProfileFor(HoundPath.Broken).BeastStatus(0.1f, 0.8f);
            Assert.Greater(guardian, 0f, "a Guardian is beloved");
            Assert.Less(broken, 0f, "a Broken hound is feared");
            Assert.Greater(guardian, war, "the Guardian outranks the War beast in standing");
            Assert.GreaterOrEqual(guardian, -1f);
            Assert.LessOrEqual(guardian, 1f);
        }

        // ---- System wiring: transition is event-logged + locks -----------

        [Test]
        public void EvaluateAtDawn_AdoptsGuardian_LogsTransition_AndLocks()
        {
            var hound = MakeHound();
            var sys = MakeSystem(hound);

            for (int i = 0; i < 8; i++)
            {
                hound.Feed(); // FeedEvents = 8
            }
            hound.Trust = 0.9f;
            hound.Attachment = 0.8f;
            hound.Fear = 0.05f;
            hound.Pain = 0.05f;
            hound.Hunger = 0.2f;

            sys.EvaluateAtDawn();

            Assert.AreEqual(HoundPath.Guardian, sys.CurrentPath);
            Assert.AreEqual(HoundPath.Guardian, hound.Path, "the path is pushed onto the controller");
            Assert.IsTrue(sys.PathLocked, "a strong history locks the path in");
            Assert.IsTrue(LogContains("hound_evolved", "Unevolved -> Guardian"),
                "the transition is on the event stream with from->to");
            Assert.Greater(sys.BeastStatus, 0f, "a Guardian's beast status is positive");
            Assert.Greater(hound.VillagerComfortRadius, 0f,
                "the Guardian behaviour (sit near villagers) was applied");
        }

        [Test]
        public void LockedPath_DoesNotChange_UnderContraryTreatment()
        {
            var hound = MakeHound();
            var sys = MakeSystem(hound);
            for (int i = 0; i < 8; i++)
            {
                hound.Feed();
            }
            hound.Trust = 0.9f;
            hound.Attachment = 0.8f;
            hound.Fear = 0.05f;
            sys.EvaluateAtDawn();
            Assert.IsTrue(sys.PathLocked);

            // Now beat and terrify it: without the lock this would trend Broken.
            hound.Trust = 0.05f;
            hound.Pain = 0.95f;
            hound.Fear = 0.95f;
            sys.EvaluateAtDawn();

            Assert.AreEqual(HoundPath.Guardian, sys.CurrentPath,
                "a locked path is permanent — the year already hardened it");
        }

        // ---- Beast exemptions on every path ------------------------------

        [Test]
        public void Beast_StaysBandExempt_OnEveryPath()
        {
            var hound = MakeHound();
            foreach (HoundPath path in System.Enum.GetValues(typeof(HoundPath)))
            {
                hound.ApplyEvolution(path, _config.ProfileFor(path).behaviour);
                Assert.IsTrue(hound.IsBeast, $"{path}: the hound is always the beast");

                var dark = LightBandCombatResolver.Resolve(
                    CombatSide.Friendly, hound.IsBeast, LightZone.Dark, _combat);
                Assert.IsTrue(dark.BeastExempt, $"{path}: beast is exempt in the Dark band");
                Assert.AreEqual(1f, dark.DamageMultiplier, 1e-4f, $"{path}: no damage debuff");
                Assert.AreEqual(0f, dark.SanityDrainPerSecond, 1e-4f, $"{path}: no sanity drain");
            }
        }

        [Test]
        public void Beast_NeverInSanityRecords_OnEveryPath()
        {
            var sanityGO = new GameObject("SanitySystem");
            _spawned.Add(sanityGO);
            var sanity = sanityGO.AddComponent<SanitySystem>();
            sanity.autoTick = false;
            var sanityConfig = ScriptableObject.CreateInstance<SanityConfig>();
            _assets.Add(sanityConfig);
            sanity.Configure(sanityConfig);

            var villagerGO = new GameObject("Villager");
            _spawned.Add(villagerGO);
            var villager = villagerGO.AddComponent<VillagerAgent>();
            villager.autoTick = false;
            villager.Config = _proto;
            DuskRecallSystem.Register(villager);

            var hound = MakeHound();
            Assert.IsNull(hound.GetComponent<VillagerAgent>(),
                "the hound carries no villager/sanity component");

            foreach (HoundPath path in System.Enum.GetValues(typeof(HoundPath)))
            {
                hound.ApplyEvolution(path, _config.ProfileFor(path).behaviour);
                sanity.RefreshRecords();
                Assert.AreEqual(1, sanity.Records.Count,
                    $"{path}: only the villager is tracked, never the beast");
                Assert.AreSame(villager, sanity.Records[0].Villager);
            }
        }
    }
}
