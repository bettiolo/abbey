using System.Collections.Generic;
using Abbey.Nightmares;
using NUnit.Framework;
using UnityEngine;

namespace Abbey.Tests.EditMode
{
    /// <summary>
    /// EditMode coverage for the five P3-11 consequence-nightmare triggers
    /// (<see cref="ConsequenceNightmareCatalog"/>). Crafted <see cref="ConsequenceContext"/>s
    /// prove each species arms under exactly its documented condition and stays disarmed under
    /// its neighbours' conditions (fasting arms Hunger Wights, not Grave Crawlers), all against
    /// the coded-default <see cref="ThreatConfig"/> thresholds.
    /// </summary>
    public class ConsequenceNightmareTests
    {
        ThreatConfig _cfg;

        [SetUp]
        public void SetUp()
        {
            _cfg = ScriptableObject.CreateInstance<ThreatConfig>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_cfg);
        }

        static ConsequenceContext Ctx(
            string[] activeTags = null, float hunger = 0f, float oldFaith = 0f,
            float sanctity = 0.5f, bool broken = false, bool anyDeath = false,
            string[] logTags = null)
        {
            var set = new HashSet<string>();
            if (logTags != null)
            {
                foreach (var t in logTags)
                {
                    set.Add(t);
                }
            }
            return new ConsequenceContext(activeTags ?? System.Array.Empty<string>(),
                hunger, oldFaith, sanctity, broken, anyDeath, set);
        }

        bool Armed(NightmareType type, ConsequenceContext ctx)
        {
            var armed = ConsequenceNightmareCatalog.EvaluateArmed(_cfg, ctx);
            for (int i = 0; i < armed.Count; i++)
            {
                if (armed[i].type == type)
                {
                    return true;
                }
            }
            return false;
        }

        // ---- Neutral ------------------------------------------------------

        [Test]
        public void NeutralState_ArmsNothing()
        {
            Assert.AreEqual(0, ConsequenceNightmareCatalog.EvaluateArmed(_cfg, Ctx()).Count,
                "a settlement under no moral strain summons no consequence nightmares");
        }

        // ---- Hunger Wight -------------------------------------------------

        [Test]
        public void FastingTag_ArmsHungerWight_NotGraveCrawler()
        {
            var ctx = Ctx(activeTags: new[] { "fasting_active" });
            Assert.IsTrue(Armed(NightmareType.HungerWight, ctx), "fasting arms the Hunger Wights");
            Assert.IsFalse(Armed(NightmareType.GraveCrawler, ctx),
                "fasting must not arm a neighbour's nightmare");
            Assert.IsFalse(Armed(NightmareType.DeadWorker, ctx));
        }

        [Test]
        public void HighHungerPressure_ArmsHungerWight_WithoutTag()
        {
            Assert.IsTrue(Armed(NightmareType.HungerWight, Ctx(hunger: 0.8f)),
                "high Hunger pressure alone arms the Hunger Wights");
            Assert.IsFalse(Armed(NightmareType.HungerWight, Ctx(hunger: 0.2f)),
                "mild hunger below the threshold does not");
        }

        // ---- Dead Worker --------------------------------------------------

        [Test]
        public void ForcedLabour_ArmsDeadWorker_OnlyWithADeath()
        {
            Assert.IsFalse(Armed(NightmareType.DeadWorker,
                Ctx(activeTags: new[] { "forced_labour_night" }, anyDeath: false)),
                "forced night labour with no death yet does not raise the Dead Workers");
            Assert.IsTrue(Armed(NightmareType.DeadWorker,
                Ctx(activeTags: new[] { "forced_labour_night" }, anyDeath: true)),
                "a death under forced night labour raises the Dead Workers");
        }

        [Test]
        public void ADeath_WithoutForcedLabour_DoesNotArmDeadWorker()
        {
            Assert.IsFalse(Armed(NightmareType.DeadWorker, Ctx(anyDeath: true)),
                "an ordinary death without the forced-labour law does not raise Dead Workers");
        }

        // ---- Grave Crawler ------------------------------------------------

        [Test]
        public void GraveTags_ArmGraveCrawler()
        {
            Assert.IsTrue(Armed(NightmareType.GraveCrawler, Ctx(logTags: new[] { "grave_mass" })),
                "a mass grave raises the Grave Crawlers");
            Assert.IsTrue(Armed(NightmareType.GraveCrawler, Ctx(logTags: new[] { "grave_used" })),
                "a body put to use raises the Grave Crawlers");
            Assert.IsFalse(Armed(NightmareType.GraveCrawler, Ctx(logTags: new[] { "grave_full_rites" })),
                "an honoured grave does not");
        }

        // ---- Chain Hound --------------------------------------------------

        [Test]
        public void ChainedDoctrineOrBrokenAbbey_ArmChainHound()
        {
            Assert.IsTrue(Armed(NightmareType.ChainHound, Ctx(activeTags: new[] { "hound_chained" })),
                "the Chained doctrine arms the Chain Hounds");
            Assert.IsTrue(Armed(NightmareType.ChainHound, Ctx(broken: true)),
                "a Broken abbey arms the Chain Hounds");
            Assert.IsFalse(Armed(NightmareType.ChainHound, Ctx(activeTags: new[] { "hound_family" })),
                "a family hound does not");
        }

        // ---- Faceless Saint -----------------------------------------------

        [Test]
        public void ForbiddenRitesAloneDoesNotArmFacelessSaint()
        {
            Assert.IsFalse(Armed(NightmareType.FacelessSaint,
                Ctx(activeTags: new[] { "pagan_rites_forbidden" }, oldFaith: 0f, sanctity: 0.5f)),
                "forbidding rites at a healthy faith does not summon the Saints");
        }

        [Test]
        public void ForbiddenRitesPlusHighOldFaithOrLowSanctity_ArmsFacelessSaint()
        {
            Assert.IsTrue(Armed(NightmareType.FacelessSaint,
                Ctx(activeTags: new[] { "pagan_rites_forbidden" }, oldFaith: 0.6f)),
                "forbidden rites while the old faith runs high summons the Saints");
            Assert.IsTrue(Armed(NightmareType.FacelessSaint,
                Ctx(activeTags: new[] { "pagan_rites_forbidden" }, sanctity: 0.1f)),
                "forbidden rites while sanctity collapses summons the Saints");
            Assert.IsFalse(Armed(NightmareType.FacelessSaint, Ctx(oldFaith: 0.9f)),
                "high old faith without the forbidding law does not");
        }
    }
}
