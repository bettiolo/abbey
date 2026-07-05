using System.Collections.Generic;
using Abbey.Core;
using Abbey.Nightmares;
using NUnit.Framework;
using UnityEngine;

namespace Abbey.Tests.EditMode
{
    /// <summary>
    /// EditMode coverage for the threat-source exploitation store (P3-11,
    /// <see cref="ThreatSourceSystem"/>): pressure accumulates per mapped economy event,
    /// decays at day markers, drops when a source is mitigated, folds identically twice
    /// (determinism), and the seeded spawn-location draw shifts toward the higher-pressure
    /// source. Worlds are built programmatically with an injected <see cref="ThreatConfig"/>.
    /// </summary>
    public class ThreatSourceTests
    {
        readonly List<GameObject> _spawned = new List<GameObject>();
        ThreatConfig _cfg;

        [SetUp]
        public void SetUp()
        {
            ClearStatics();
            _cfg = ScriptableObject.CreateInstance<ThreatConfig>();
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
            Object.DestroyImmediate(_cfg);
            ClearStatics();
        }

        static void ClearStatics()
        {
            EventBus.ResetAll();
            GameEventLog.Clear();
            ThreatConfig.ClearCache();
        }

        ThreatSourceSystem MakeSystem()
        {
            var go = new GameObject("ThreatSources");
            _spawned.Add(go);
            var sys = go.AddComponent<ThreatSourceSystem>();
            sys.Config = _cfg;
            return sys;
        }

        static void Day() => GameEventLog.Append("RationsIssued", "law=rations_equal total=6");

        // ---- Accumulation -------------------------------------------------

        [Test]
        public void Woodcutting_RaisesForest_MiningRaisesCave()
        {
            var sys = MakeSystem();
            GameEventLog.Append("resource", "wood +5 (woodcutter)");
            GameEventLog.Append("resource", "wood +5 (woodcutter)");
            GameEventLog.Append("resource", "coal +3 (miner)");
            sys.RecomputeFromLog();

            Assert.Greater(sys.PressureFor(ThreatSourceType.Forest), 0f, "woodcutting pressures the forest");
            Assert.Greater(sys.PressureFor(ThreatSourceType.Cave), 0f, "coal pressures the cave");
            Assert.Greater(sys.PressureFor(ThreatSourceType.Forest), sys.PressureFor(ThreatSourceType.Cave),
                "two wood cuts outweigh one coal haul");
            Assert.AreEqual(0f, sys.PressureFor(ThreatSourceType.Shore),
                "an untouched source stays at zero");
        }

        [Test]
        public void SalvageAndGraves_MapToShoreAndCrypt()
        {
            var sys = MakeSystem();
            GameEventLog.Append("salvage", "Wreck stage 1->2");
            GameEventLog.Append("burial", "law=mass_graves_active deceased=V tag=grave_mass");
            sys.RecomputeFromLog();

            Assert.Greater(sys.PressureFor(ThreatSourceType.Shore), 0f, "salvage pressures the shore");
            Assert.Greater(sys.PressureFor(ThreatSourceType.Crypt), 0f, "grave handling pressures the crypt");
        }

        // ---- Decay --------------------------------------------------------

        [Test]
        public void DayMarkers_DecayPressureTowardZero()
        {
            var sys = MakeSystem();
            for (int i = 0; i < 5; i++)
            {
                GameEventLog.Append("resource", "wood +5 (woodcutter)");
            }
            sys.RecomputeFromLog();
            float peak = sys.PressureFor(ThreatSourceType.Forest);

            // Several elapsed days with no further woodcutting: the forest pressure decays.
            for (int i = 0; i < 4; i++)
            {
                Day();
            }
            sys.RecomputeFromLog();
            Assert.Less(sys.PressureFor(ThreatSourceType.Forest), peak,
                "with no fresh cutting the forest cools toward zero across days");
        }

        // ---- Mitigation ---------------------------------------------------

        [Test]
        public void Mitigation_ReducesSourcePressure()
        {
            var sys = MakeSystem();
            for (int i = 0; i < 5; i++)
            {
                GameEventLog.Append("resource", "wood +5 (woodcutter)");
            }
            sys.RecomputeFromLog();
            float before = sys.PressureFor(ThreatSourceType.Forest);
            Assert.Greater(before, 0.2f);

            sys.Mitigate(ThreatSourceType.Forest, 0.3f);
            Assert.Less(sys.PressureFor(ThreatSourceType.Forest), before,
                "resting the forest lowers its pressure");
        }

        // ---- Determinism --------------------------------------------------

        [Test]
        public void SameLog_FoldsToIdenticalPressures()
        {
            var sys = MakeSystem();
            GameEventLog.Append("resource", "wood +5 (woodcutter)");
            GameEventLog.Append("resource", "stone +4 (quarry)");
            Day();
            GameEventLog.Append("resource", "scrap_iron +2 (salvage)");

            sys.RecomputeFromLog();
            float f1 = sys.PressureFor(ThreatSourceType.Forest);
            float m1 = sys.PressureFor(ThreatSourceType.Mountain);
            sys.RecomputeFromLog();
            Assert.AreEqual(f1, sys.PressureFor(ThreatSourceType.Forest),
                "the same log always folds to the same forest pressure");
            Assert.AreEqual(m1, sys.PressureFor(ThreatSourceType.Mountain));
        }

        // ---- Weighted spawn selection -------------------------------------

        [Test]
        public void WeightedSelection_ShiftsTowardHighPressureSource_AndIsSeedDeterministic()
        {
            var sys = MakeSystem();
            var forest = sys.RegisterSource(ThreatSourceType.Forest, new Vector3(-20f, 0f, 20f));
            var shore = sys.RegisterSource(ThreatSourceType.Shore, new Vector3(20f, 0f, -20f));

            // Hammer the forest; barely touch the shore.
            for (int i = 0; i < 10; i++)
            {
                GameEventLog.Append("resource", "wood +5 (woodcutter)");
            }
            GameEventLog.Append("salvage", "Wreck stage 1->2");
            sys.RecomputeFromLog();
            Assert.Greater(sys.PressureFor(ThreatSourceType.Forest), sys.PressureFor(ThreatSourceType.Shore));

            int forestHits = 0, shoreHits = 0;
            var rng = new System.Random(12345);
            for (int i = 0; i < 400; i++)
            {
                var src = sys.SelectWeightedSource(rng, null);
                if (src == forest) { forestHits++; }
                else if (src == shore) { shoreHits++; }
            }
            Assert.Greater(forestHits, shoreHits,
                "the pressured forest is drawn far more often than the barely-used shore");

            // Determinism: the same seed reproduces the same sequence of draws.
            var a = new System.Random(999);
            var b = new System.Random(999);
            for (int i = 0; i < 50; i++)
            {
                Assert.AreSame(sys.SelectWeightedSource(a, null), sys.SelectWeightedSource(b, null),
                    "a fixed seed makes the weighted draw reproducible");
            }
        }

        [Test]
        public void PreferredSource_GetsSelectionBonus()
        {
            var sys = MakeSystem();
            var forest = sys.RegisterSource(ThreatSourceType.Forest, new Vector3(-20f, 0f, 20f));
            var crypt = sys.RegisterSource(ThreatSourceType.Crypt, new Vector3(20f, 0f, 20f));
            // No exploitation pressure anywhere — only the preferred bonus separates them.
            sys.RecomputeFromLog();

            int cryptHits = 0, forestHits = 0;
            var rng = new System.Random(7);
            for (int i = 0; i < 400; i++)
            {
                var src = sys.SelectWeightedSource(rng, ThreatSourceType.Crypt);
                if (src == crypt) { cryptHits++; }
                else if (src == forest) { forestHits++; }
            }
            Assert.Greater(cryptHits, forestHits,
                "a rule's preferred source is favoured even at equal pressure");
        }
    }
}
