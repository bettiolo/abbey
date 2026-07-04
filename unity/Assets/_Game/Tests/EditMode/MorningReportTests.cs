using System.Collections.Generic;
using Abbey.Core;
using Abbey.Reports;
using NUnit.Framework;

namespace Abbey.Tests.EditMode
{
    /// <summary>
    /// P2-07: the dawn consequence report is a pure function of the night's log
    /// window. These tests feed synthetic <see cref="GameEventLog.Record"/> streams —
    /// using the REAL event vocabulary emitted across the codebase — for several night
    /// shapes and assert the derived counts/flags, deterministic worst-first prose, and
    /// that the hound line matches the bond records. No scene, no MonoBehaviours.
    /// </summary>
    public class MorningReportTests
    {
        [SetUp]
        public void SetUp()
        {
            GameEventLog.Clear();
            MorningReportSystem.ResetStaticEvents();
        }

        [TearDown]
        public void TearDown()
        {
            GameEventLog.Clear();
            MorningReportSystem.ResetStaticEvents();
        }

        static GameEventLog.Record Rec(string type, string data)
        {
            return new GameEventLog.Record(0f, type, data);
        }

        // ------------------------------------------------------------------
        // Scenario builders (each returns one night's window)
        // ------------------------------------------------------------------

        static List<GameEventLog.Record> QuietNight()
        {
            return new List<GameEventLog.Record>
            {
                Rec("night_begins", "night=1"),
                Rec("hound_state", "BlackHound start Chained trust=0.10 hunger=0.90"),
                Rec("hero_fed_hound", "foodLeft=2"),
                Rec("hound_fed", "trust=0.60 hunger=0.50"),
                Rec("hound_state", "BlackHound Chained->Fed"),
                Rec("hero_rang_bell", "pos=(0.0,0.0)"),
                Rec("hound_answered_bell", "BlackHound pos=(0,0,0)"),
                Rec("hound_state", "BlackHound Fed->Following"),
                Rec("hero_raised_flame", "Bellkeeper"),
                Rec("VillagerState", "Villager_00 Working->Idle"),
                Rec("VillagerState", "Villager_01 WalkingToTask->Working"),
            };
        }

        static List<GameEventLog.Record> DeadlyNight()
        {
            return new List<GameEventLog.Record>
            {
                Rec("night_begins", "night=2"),
                Rec("MonsterSpawned", "PaleHound_0"),
                Rec("MonsterSpawned", "PaleHound_1"),
                Rec("nightmare", "shadow night=2 pos=(20.0,4.0)"),
                Rec("whisper", "night=2 pos=(8.0,2.0)"),
                Rec("panic_event", "night=2 villager=Villager_03 fear=0.90"),
                // One death logged explicitly, one only via a state transition.
                Rec("villager_died", "Villager_02"),
                Rec("VillagerState", "Villager_04 Injured->Dead"),
                Rec("villager_missing", "Villager_05"),
                Rec("villager_injured_by_darkness", "Villager_06"),
                Rec("LightExtinguished", "LanternPost_1 sacred=False"),
                Rec("resource", "wood -2 (tender refuel)"),
                Rec("resource", "food -1 (night meal)"),
                Rec("hound_state", "BlackHound start Chained trust=0.10 hunger=0.90"),
                Rec("hound_ignored_bell", "BlackHound state=Chained trust=0.05 hunger=0.90"),
            };
        }

        static List<GameEventLog.Record> HoundSavedHeroNight()
        {
            return new List<GameEventLog.Record>
            {
                Rec("night_begins", "night=3"),
                Rec("MonsterSpawned", "PaleHound_0"),
                Rec("hound_state", "BlackHound start Fed trust=0.60 hunger=0.30"),
                Rec("hound_intervention", "protect_hero BlackHound -> PaleHound_0"),
                Rec("hound_state", "BlackHound Following->Protective"),
                Rec("hound_intervention", "broke_chain reason=save_hero BlackHound"),
                Rec("hound_attacked_monster", "BlackHound -> PaleHound_0"),
                Rec("hound_killed_monster", "BlackHound -> PaleHound_0"),
                Rec("monster_killed", "PaleHound_0"),
                Rec("hero_rescue_started", "Villager_07"),
                Rec("hero_rescue_released", "Villager_07 safe=True"),
                Rec("VillagerRescued", "Villager_07"),
                Rec("hound_state", "BlackHound Protective->Following"),
            };
        }

        static List<GameEventLog.Record> FireDiedNight()
        {
            return new List<GameEventLog.Record>
            {
                Rec("night_begins", "night=4"),
                Rec("LightExtinguished", "AbbeyShrine sacred=True"),
                Rec("LightIgniteFailed", "AbbeyShrine no fuel"),
                Rec("hound_state", "BlackHound start Chained trust=0.10 hunger=0.90"),
            };
        }

        // ------------------------------------------------------------------
        // Counts / flags
        // ------------------------------------------------------------------

        [Test]
        public void QuietNight_NoCasualties_HoundBondedFlagsSet()
        {
            var d = MorningReport.Build(QuietNight());

            Assert.AreEqual(1, d.NightNumber);
            Assert.AreEqual(0, d.Dead);
            Assert.AreEqual(0, d.Missing);
            Assert.AreEqual(0, d.Injured);
            Assert.AreEqual(0, d.FiresLost);
            Assert.IsTrue(d.WasQuietNight);

            Assert.IsTrue(d.HoundPresent);
            Assert.IsTrue(d.HoundFedByHero);
            Assert.IsTrue(d.HoundAnsweredBell);
            Assert.AreEqual("Following", d.HoundDisposition);
            Assert.AreEqual(1, d.HoundTrustDirection, "feeding raises trust across the night");

            Assert.IsTrue(d.HeroCarriedFireIntoDark);
            Assert.AreEqual(1, d.BellRangCount);
            Assert.AreEqual(2, d.KnownVillagers);
            Assert.AreEqual(2, d.Survivors);
        }

        [Test]
        public void DeadlyNight_CountsDeathsFromRecordsAndStateTransitions()
        {
            var d = MorningReport.Build(DeadlyNight());

            Assert.AreEqual(2, d.Dead, "one explicit villager_died + one Injured->Dead transition");
            Assert.AreEqual(1, d.Missing);
            Assert.AreEqual(1, d.Injured);
            Assert.AreEqual(2, d.MonstersFaced);
            Assert.IsTrue(d.ShadowSeen);
            Assert.AreEqual(1, d.Whispers);
            Assert.AreEqual(1, d.PanicEvents);
            Assert.AreEqual(2, d.WoodConsumed);
            Assert.AreEqual(2, d.WoodBurnedIntoFires);
            Assert.AreEqual(1, d.FoodConsumed);
            Assert.AreEqual(1, d.FiresLost);
            Assert.IsFalse(d.SacredFlameLost);
            Assert.IsTrue(d.HoundIgnoredBell);
            Assert.IsFalse(d.WasQuietNight);
        }

        [Test]
        public void DeadlyNight_PrecedenceNoDoubleCounting()
        {
            // A villager cannot be both dead and missing/injured; sets are disjoint.
            var records = DeadlyNight();
            records.Add(Rec("villager_injured_by_darkness", "Villager_02")); // already dead
            records.Add(Rec("villager_missing", "Villager_04"));             // already dead
            var d = MorningReport.Build(records);

            Assert.AreEqual(2, d.Dead);
            Assert.AreEqual(1, d.Missing, "the dead villager is not also counted missing");
            Assert.AreEqual(1, d.Injured, "the dead villager is not also counted injured");
        }

        [Test]
        public void HoundSavedHeroNight_ProtectiveAndRescueFlags()
        {
            var d = MorningReport.Build(HoundSavedHeroNight());

            Assert.IsTrue(d.HoundProtectedHero);
            Assert.IsTrue(d.HoundKilledMonster);
            Assert.AreEqual(1, d.MonstersKilled);
            Assert.AreEqual(1, d.Rescued);
            Assert.IsTrue(d.HeroRescuedSomeone);
            Assert.AreEqual("Following", d.HoundDisposition);
            Assert.IsFalse(d.HoundWentMissing);
        }

        [Test]
        public void FireDiedNight_SacredFlameLost()
        {
            var d = MorningReport.Build(FireDiedNight());

            Assert.AreEqual(1, d.FiresLost);
            Assert.IsTrue(d.SacredFlameLost);
            Assert.AreEqual(0, d.FiresRelit);
            Assert.IsTrue(d.HoundStillChained);
        }

        [Test]
        public void HoundGoesMissing_DispositionIsMissing()
        {
            var records = new List<GameEventLog.Record>
            {
                Rec("night_begins", "night=1"),
                Rec("hound_state", "BlackHound start Chained trust=0.10 hunger=0.90"),
                Rec("hound_choice", "free_chain outcome=fled trust=0.05"),
                Rec("hound_state", "BlackHound Chained->Angry"),
                Rec("hound_intervention", "went_missing BlackHound"),
                Rec("hound_state", "BlackHound Angry->Missing"),
            };
            var d = MorningReport.Build(records);

            Assert.IsTrue(d.HoundWentMissing);
            Assert.AreEqual("Missing", d.HoundDisposition);
        }

        // ------------------------------------------------------------------
        // Prose: determinism, priority ordering, hound line matches bond
        // ------------------------------------------------------------------

        [Test]
        public void Prose_IsDeterministic_SameLogSameProse()
        {
            var a = MorningReportProse.Compose(MorningReport.Build(DeadlyNight()));
            var b = MorningReportProse.Compose(MorningReport.Build(DeadlyNight()));
            Assert.AreEqual(a, b, "same log window must yield identical prose (no RNG)");
        }

        [Test]
        public void Prose_SentenceCount_WithinFourToEight()
        {
            foreach (var window in new[] { QuietNight(), DeadlyNight(), HoundSavedHeroNight(), FireDiedNight() })
            {
                string prose = MorningReportProse.Compose(MorningReport.Build(window));
                int sentences = CountSentences(prose);
                Assert.GreaterOrEqual(sentences, 4, $"too terse: '{prose}'");
                Assert.LessOrEqual(sentences, MorningReportProse.MaxSentences, $"too long: '{prose}'");
            }
        }

        [Test]
        public void Prose_Priority_DeathOpeningOutranksMissing()
        {
            var d = MorningReport.Build(DeadlyNight()); // has both dead and missing
            string prose = MorningReportProse.Compose(d);
            // Opening line is the deadliest available; the "counting came up short"
            // (missing-only) opening must NOT be chosen when there are dead.
            StringAssert.Contains("did not come for all of us", prose);
            StringAssert.DoesNotContain("the counting came up short", prose);
        }

        [Test]
        public void Prose_QuietNight_UsesCalmFramingAndFiresHeld()
        {
            string prose = MorningReportProse.Compose(MorningReport.Build(QuietNight()));
            StringAssert.Contains("as though the dark had never come", prose);
            StringAssert.Contains("fires held", prose);
        }

        [Test]
        public void Prose_HoundLine_MatchesProtectiveBond()
        {
            string prose = MorningReportProse.Compose(MorningReport.Build(HoundSavedHeroNight()));
            StringAssert.Contains("broke its chain and stood between", prose);
        }

        [Test]
        public void Prose_HoundLine_MatchesFedBondWhenOnlyFed()
        {
            var records = new List<GameEventLog.Record>
            {
                Rec("night_begins", "night=1"),
                Rec("hound_state", "BlackHound start Chained trust=0.10 hunger=0.90"),
                Rec("hound_fed", "trust=0.60 hunger=0.50"),
                Rec("hound_state", "BlackHound Chained->Fed"),
            };
            string prose = MorningReportProse.Compose(MorningReport.Build(records));
            StringAssert.Contains("ate from the Bellkeeper's hand", prose);
        }

        [Test]
        public void Prose_SacredFlameLost_IsSpoken()
        {
            string prose = MorningReportProse.Compose(MorningReport.Build(FireDiedNight()));
            StringAssert.Contains("abbey flame went out", prose);
        }

        // ------------------------------------------------------------------
        // Record format round-trips (P2-08/P2-10 consumers)
        // ------------------------------------------------------------------

        [Test]
        public void FormatRecord_HasStatHeadThenProse()
        {
            var d = MorningReport.Build(DeadlyNight());
            string prose = MorningReportProse.Compose(d);
            string record = MorningReportSystem.FormatRecord(d, prose);

            int sep = record.IndexOf(MorningReportSystem.ProseSeparator);
            Assert.Greater(sep, 0, "the record carries a stat head before the separator");
            string head = record.Substring(0, sep);
            string tail = record.Substring(sep + 1).Trim();

            StringAssert.Contains("dead=2", head);
            StringAssert.Contains("missing=1", head);
            StringAssert.Contains("night=2", head);
            Assert.AreEqual(prose, tail, "the prose after the separator is intact");
        }

        static int CountSentences(string prose)
        {
            int count = 0;
            for (int i = 0; i < prose.Length; i++)
            {
                char c = prose[i];
                if (c == '.' || c == '!' || c == '?')
                {
                    count++;
                }
            }
            return count;
        }
    }
}
