using System.Collections.Generic;
using Abbey.Buildings;
using Abbey.Core;
using Abbey.Morale;
using NUnit.Framework;
using UnityEngine;

namespace Abbey.Tests.EditMode
{
    /// <summary>
    /// EditMode coverage for the abbey transformation (P3-10). Crafted
    /// <see cref="PressureSnapshot"/>s are fed to an injected-config
    /// <see cref="AbbeyTransformationSystem"/> so each of the five forms is shown reachable
    /// from a plausible pressure/law setup, a neutral snapshot stays Balanced, hysteresis
    /// stops a single-day flip-flop, each adopted form pushes its configured modifier onto
    /// <see cref="AbbeyState"/>, and transitions are event-logged.
    /// </summary>
    public class AbbeyTransformationTests
    {
        readonly List<GameObject> _spawned = new List<GameObject>();
        PressuresConfig _cfg;

        [SetUp]
        public void SetUp()
        {
            ClearStatics();
            _cfg = ScriptableObject.CreateInstance<PressuresConfig>();
        }

        [TearDown]
        public void TearDown()
        {
            if (AbbeyTransformationSystem.Instance != null)
            {
                Object.DestroyImmediate(AbbeyTransformationSystem.Instance.gameObject);
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
            ClearStatics();
        }

        static void ClearStatics()
        {
            EventBus.ResetAll();
            GameEventLog.Clear();
            AbbeyState.Clear();
            PressuresConfig.ClearCache();
        }

        AbbeyTransformationSystem MakeSystem()
        {
            var go = new GameObject("AbbeyTransformation");
            _spawned.Add(go);
            var sys = go.AddComponent<AbbeyTransformationSystem>();
            sys.Configure(_cfg);
            return sys;
        }

        static PressureSnapshot Snap(
            float trust = 0.5f, float sanctity = 0.5f, float mercy = 0.5f, float fear = 0.1f,
            float reason = 0.5f, float hunger = 0f, float oldFaith = 0f, float beast = 0f,
            float household = 1f, string[] tags = null)
        {
            return new PressureSnapshot(trust, sanctity, mercy, fear, reason, hunger, oldFaith,
                beast, household, tags);
        }

        static bool LogHas(string type, string fragment)
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

        // ---- Each form reachable ------------------------------------------

        [Test]
        public void Baseline_StaysBalanced()
        {
            var sys = MakeSystem();
            Assert.AreEqual(AbbeyForm.Balanced, sys.Evaluate(Snap()),
                "a peaceful, well-fed settlement holds no transformation");
        }

        [Test]
        public void HighSanctityAndMercy_BecomesSanctuary()
        {
            var sys = MakeSystem();
            var form = sys.Evaluate(Snap(sanctity: 0.9f, mercy: 0.9f, trust: 0.8f, fear: 0f,
                tags: new[] { "full_rites" }));
            Assert.AreEqual(AbbeyForm.Sanctuary, form);
            Assert.Greater(AbbeyState.Modifiers.sacredLightRadiusBonus, 0f,
                "Sanctuary widens the sacred light");
        }

        [Test]
        public void HighFearAndReason_BecomesFortress()
        {
            var sys = MakeSystem();
            var form = sys.Evaluate(Snap(fear: 0.9f, reason: 0.8f, trust: 0.6f,
                sanctity: 0.3f, mercy: 0.3f, tags: new[] { "forced_labour_night" }));
            Assert.AreEqual(AbbeyForm.Fortress, form);
            Assert.Greater(AbbeyState.Modifiers.windowVolleyBonus, 0f,
                "Fortress hardens the window volley");
        }

        [Test]
        public void DominantHunger_BecomesFamine()
        {
            var sys = MakeSystem();
            var form = sys.Evaluate(Snap(hunger: 0.9f, reason: 0.3f, sanctity: 0.4f,
                mercy: 0.4f, trust: 0.4f, tags: new[] { "fasting_active" }));
            Assert.AreEqual(AbbeyForm.Famine, form);
            Assert.Less(AbbeyState.Modifiers.rationCeilingMultiplier, 1f,
                "Famine caps the daily ration");
        }

        [Test]
        public void DominantOldFaith_BecomesCult()
        {
            var sys = MakeSystem();
            var form = sys.Evaluate(Snap(oldFaith: 0.6f, sanctity: 0.3f, mercy: 0.4f,
                trust: 0.4f, fear: 0.3f, tags: new[] { "offerings_tolerated" }));
            Assert.AreEqual(AbbeyForm.Cult, form);
            Assert.IsTrue(AbbeyState.Modifiers.offeringsEnabled, "the Cult enables offerings");
        }

        [Test]
        public void CollapsedTrustAndSanity_BecomesBroken()
        {
            var sys = MakeSystem();
            var form = sys.Evaluate(Snap(trust: 0.1f, household: 0.15f, fear: 0.6f,
                sanctity: 0.2f, mercy: 0.2f, reason: 0.2f));
            Assert.AreEqual(AbbeyForm.Broken, form);
            Assert.Greater(AbbeyState.Modifiers.recallCompliancePenalty, 0f,
                "a Broken abbey struggles to recall its people");
        }

        // ---- Hysteresis + logging -----------------------------------------

        [Test]
        public void Hysteresis_PreventsSingleDayFlipFlop()
        {
            var sys = MakeSystem();
            // Settle into Fortress.
            Assert.AreEqual(AbbeyForm.Fortress,
                sys.Evaluate(Snap(fear: 0.9f, reason: 0.85f, trust: 0.7f, sanctity: 0.3f, mercy: 0.3f)));

            // Sanctuary now scores marginally higher than Fortress but within the hysteresis
            // margin — the abbey should NOT flip on a single day's swing.
            var stay = sys.Evaluate(Snap(sanctity: 0.9f, mercy: 0.9f, trust: 0.7f, fear: 0.75f,
                reason: 0.85f));
            Assert.AreEqual(AbbeyForm.Fortress, stay, "a marginal challenger does not dethrone the current form");

            // A decisive Sanctuary swing (Fortress no longer valid) does flip it.
            var flip = sys.Evaluate(Snap(sanctity: 0.95f, mercy: 0.95f, trust: 0.9f, fear: 0f,
                reason: 0.2f));
            Assert.AreEqual(AbbeyForm.Sanctuary, flip, "a decisive challenger takes over");
        }

        [Test]
        public void Transition_IsEventLogged()
        {
            var sys = MakeSystem();
            sys.Evaluate(Snap(hunger: 0.9f, reason: 0.3f, sanctity: 0.4f, mercy: 0.4f, trust: 0.4f,
                tags: new[] { "fasting_active" }));
            Assert.IsTrue(LogHas("abbey_transformation", "Famine"),
                "adopting a form is written to the event log");
            Assert.AreEqual(AbbeyForm.Famine, AbbeyState.CurrentForm,
                "AbbeyState carries the adopted form for downstream systems");
        }
    }
}
