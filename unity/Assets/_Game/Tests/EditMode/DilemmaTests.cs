using System.Collections.Generic;
using Abbey.Beast;
using Abbey.Core;
using Abbey.Economy;
using Abbey.Island;
using Abbey.Morale;
using NUnit.Framework;
using UnityEngine;

namespace Abbey.Tests.EditMode
{
    /// <summary>
    /// EditMode coverage for the dilemma cards (P3-13). Worlds are built programmatically and
    /// the <see cref="IslandConfig"/> deck injected. Each option is asserted to apply exactly
    /// its configured consequences: the moral-pressure deltas fold into
    /// <see cref="PressureSystem"/> through the "pressure_delta" record, law-like tags are
    /// stamped, resource compensations debit the ledger, and the Hound Bites a Child punish
    /// option writes a hound treatment/doctrine input for the P3-07 evolution. The three
    /// named cards (Missing Salvager, Food Thief, Hound Bites a Child) all ship.
    /// </summary>
    public class DilemmaTests
    {
        readonly List<GameObject> _spawned = new List<GameObject>();
        readonly List<Object> _assets = new List<Object>();
        IslandConfig _island;
        EconomyConfig _econ;
        PressuresConfig _pcfg;

        [SetUp]
        public void SetUp()
        {
            ClearStatics();
            _island = ScriptableObject.CreateInstance<IslandConfig>(); // ships the 3 named cards
            _assets.Add(_island);

            _econ = ScriptableObject.CreateInstance<EconomyConfig>();
            _econ.baseStorageCapacity = 1000;
            ResourceLedger.Config = _econ;
            _assets.Add(_econ);

            _pcfg = ScriptableObject.CreateInstance<PressuresConfig>();
            _assets.Add(_pcfg);
        }

        [TearDown]
        public void TearDown()
        {
            if (DilemmaSystem.Instance != null)
            {
                Object.DestroyImmediate(DilemmaSystem.Instance.gameObject);
            }
            if (PressureSystem.Instance != null)
            {
                Object.DestroyImmediate(PressureSystem.Instance.gameObject);
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

        DilemmaSystem MakeDilemmas()
        {
            var go = new GameObject("DilemmaSystem");
            _spawned.Add(go);
            var sys = go.AddComponent<DilemmaSystem>();
            sys.Configure(_island);
            return sys;
        }

        PressureSystem MakePressures()
        {
            var go = new GameObject("PressureSystem");
            _spawned.Add(go);
            var sys = go.AddComponent<PressureSystem>();
            sys.Configure(_pcfg);
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

        /// <summary>Sum of a card option's configured Pressure-effect deltas on one channel.</summary>
        float ExpectedDelta(string cardId, string optionId, PressureId id)
        {
            var card = _island.CardFor(cardId);
            float sum = 0f;
            for (int i = 0; i < card.options.Count; i++)
            {
                if (card.options[i].id != optionId)
                {
                    continue;
                }
                var effects = card.options[i].effects;
                for (int e = 0; e < effects.Count; e++)
                {
                    if (effects[e].kind == DilemmaEffectKind.Pressure && effects[e].pressure == id)
                    {
                        sum += effects[e].amount;
                    }
                }
            }
            return sum;
        }

        /// <summary>Asserts the folded channel equals baseline + configured delta (clamped).</summary>
        void AssertChannel(PressureSystem pressures, PressureId id, string cardId, string optionId)
        {
            var ch = _pcfg.ChannelFor(id);
            float expected = Mathf.Clamp(ch.baseline + ExpectedDelta(cardId, optionId, id), ch.min, ch.max);
            Assert.AreEqual(expected, pressures.Get(id), 1e-4f,
                $"{id} after {cardId}:{optionId} should be baseline+config delta");
        }

        // ---- Missing Salvager --------------------------------------------

        [Test]
        public void MissingSalvager_Search_AppliesPressuresAndTag()
        {
            var dilemmas = MakeDilemmas();
            var pressures = MakePressures();
            Assert.IsTrue(dilemmas.EnqueueCard("missing_salvager"));
            Assert.IsNotNull(dilemmas.PendingCard);

            Assert.IsTrue(dilemmas.ChooseById("search_at_night"));
            Assert.IsNull(dilemmas.PendingCard, "the resolved card leaves the queue");
            pressures.RecomputeFromLog();

            AssertChannel(pressures, PressureId.Fear, "missing_salvager", "search_at_night");
            AssertChannel(pressures, PressureId.Mercy, "missing_salvager", "search_at_night");
            AssertChannel(pressures, PressureId.Trust, "missing_salvager", "search_at_night");
            Assert.IsTrue(LogContains("dilemma_tag", "tag=night_search"));
        }

        [Test]
        public void MissingSalvager_WriteOff_CostsMercyAndTrust()
        {
            var dilemmas = MakeDilemmas();
            var pressures = MakePressures();
            dilemmas.EnqueueCard("missing_salvager");

            Assert.IsTrue(dilemmas.ChooseById("write_off"));
            pressures.RecomputeFromLog();

            AssertChannel(pressures, PressureId.Mercy, "missing_salvager", "write_off");
            AssertChannel(pressures, PressureId.Trust, "missing_salvager", "write_off");
            Assert.Less(ExpectedDelta("missing_salvager", "write_off", PressureId.Mercy), 0f,
                "writing off a lost salvager is a mercy failing");
            Assert.IsTrue(LogContains("dilemma_tag", "tag=wrote_off_lost"));
        }

        // ---- Food Thief ---------------------------------------------------

        [Test]
        public void FoodThief_Punish_AppliesFearAndTag()
        {
            var dilemmas = MakeDilemmas();
            var pressures = MakePressures();
            dilemmas.EnqueueCard("food_thief");

            Assert.IsTrue(dilemmas.ChooseById("punish"));
            pressures.RecomputeFromLog();

            AssertChannel(pressures, PressureId.Fear, "food_thief", "punish");
            AssertChannel(pressures, PressureId.Mercy, "food_thief", "punish");
            AssertChannel(pressures, PressureId.Reason, "food_thief", "punish");
            Assert.IsTrue(LogContains("dilemma_tag", "tag=thief_punished"));
        }

        [Test]
        public void FoodThief_Forgive_RaisesMercyAndTrust()
        {
            var dilemmas = MakeDilemmas();
            var pressures = MakePressures();
            dilemmas.EnqueueCard("food_thief");

            Assert.IsTrue(dilemmas.ChooseById("forgive"));
            pressures.RecomputeFromLog();

            AssertChannel(pressures, PressureId.Mercy, "food_thief", "forgive");
            AssertChannel(pressures, PressureId.Trust, "food_thief", "forgive");
            Assert.Greater(ExpectedDelta("food_thief", "forgive", PressureId.Mercy), 0f);
            Assert.IsTrue(LogContains("dilemma_tag", "tag=thief_forgiven"));
        }

        [Test]
        public void FoodThief_Exile_CostsTrust()
        {
            var dilemmas = MakeDilemmas();
            var pressures = MakePressures();
            dilemmas.EnqueueCard("food_thief");

            Assert.IsTrue(dilemmas.ChooseById("exile"));
            pressures.RecomputeFromLog();

            AssertChannel(pressures, PressureId.Trust, "food_thief", "exile");
            Assert.Less(ExpectedDelta("food_thief", "exile", PressureId.Trust), 0f);
            Assert.IsTrue(LogContains("dilemma_tag", "tag=thief_exiled"));
        }

        // ---- Hound Bites a Child -----------------------------------------

        [Test]
        public void HoundBitesChild_Punish_WritesHoundTreatment_AndChainsDoctrine()
        {
            var evoGO = new GameObject("HoundEvolutionSystem");
            _spawned.Add(evoGO);
            var evo = evoGO.AddComponent<HoundEvolutionSystem>();
            evo.Doctrine = HoundDoctrine.Neutral;

            var dilemmas = MakeDilemmas();
            var pressures = MakePressures();
            dilemmas.EnqueueCard("hound_bites_child");

            Assert.IsTrue(dilemmas.ChooseById("punish_hound"));
            pressures.RecomputeFromLog();

            Assert.IsTrue(LogContains("hound_treatment", "kind=punish"),
                "punishing the hound writes a treatment entry for P3-07");
            Assert.AreEqual(HoundDoctrine.Chained, evo.Doctrine,
                "punishment sets the chained hound doctrine");
            AssertChannel(pressures, PressureId.Fear, "hound_bites_child", "punish_hound");
            Assert.IsTrue(LogContains("dilemma_tag", "tag=hound_punished"));
        }

        [Test]
        public void HoundBitesChild_Compensate_SpendsSuppliesAndRaisesMercy()
        {
            ResourceLedger.Add(ResourceType.Food, 10, "test");
            ResourceLedger.Add(ResourceType.Medicine, 5, "test");
            var dilemmas = MakeDilemmas();
            var pressures = MakePressures();
            dilemmas.EnqueueCard("hound_bites_child");

            Assert.IsTrue(dilemmas.ChooseById("compensate_family"));
            pressures.RecomputeFromLog();

            Assert.AreEqual(8, ResourceLedger.Get(ResourceType.Food), "two food paid to the family");
            Assert.AreEqual(4, ResourceLedger.Get(ResourceType.Medicine), "one medicine paid to the family");
            AssertChannel(pressures, PressureId.Mercy, "hound_bites_child", "compensate_family");
            Assert.IsTrue(LogContains("dilemma_tag", "tag=family_compensated"));
        }

        // ---- Queue behaviour ---------------------------------------------

        [Test]
        public void Choose_DequeuesCard_AndGuardsBadIndex()
        {
            var dilemmas = MakeDilemmas();
            dilemmas.EnqueueCard("food_thief");
            Assert.AreEqual(1, dilemmas.PendingCount);

            Assert.IsFalse(dilemmas.Choose(99), "an out-of-range option is rejected");
            Assert.AreEqual(1, dilemmas.PendingCount, "a rejected choice keeps the card queued");

            Assert.IsTrue(dilemmas.Choose(0));
            Assert.AreEqual(0, dilemmas.PendingCount);
            Assert.IsNull(dilemmas.PendingCard);
        }

        [Test]
        public void PressureDelta_Fold_IsDeterministic()
        {
            var dilemmas = MakeDilemmas();
            var pressures = MakePressures();
            dilemmas.EnqueueCard("food_thief");
            dilemmas.ChooseById("forgive");

            pressures.RecomputeFromLog();
            float first = pressures.Get(PressureId.Mercy);
            pressures.RecomputeFromLog();
            Assert.AreEqual(first, pressures.Get(PressureId.Mercy), 1e-6f,
                "re-folding the same log yields the same pressure");
        }
    }
}
