using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Abbey.Core;
using Abbey.Hero;
using Abbey.Light;
using Abbey.Reports;
using Abbey.Villagers;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Abbey.Tests.PlayMode
{
    /// <summary>
    /// P2-07: a short, fully programmatic night (no scene load) with a scripted death
    /// and a scripted rescue. On the Night→Dawn transition the MorningReportSystem must
    /// build its report from exactly that window, append a "morning_report" record and
    /// raise ReportReady with the right headline facts. Deterministic: autoTick off,
    /// manual clock crossings, real villager/hero/light APIs. Mirrors the world-build
    /// pattern of FirstNightTests.
    /// </summary>
    public class MorningReportPlayModeTests
    {
        const float Dt = 0.1f;

        readonly List<GameObject> _spawned = new List<GameObject>();
        readonly List<ScriptableObject> _assets = new List<ScriptableObject>();
        readonly List<VillagerAgent> _villagers = new List<VillagerAgent>();

        GameClock _clock;
        BellkeeperController _hero;
        MorningReportSystem _reports;

        [SetUp]
        public void SetUp()
        {
            EventBus.ResetAll();
            GameEventLog.Clear();
            DarknessEvaluator.Clear();
            DuskRecallSystem.Clear();
            MorningReportSystem.ResetStaticEvents();
            _villagers.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            _spawned.Clear();
            foreach (var asset in _assets)
            {
                if (asset != null) Object.DestroyImmediate(asset);
            }
            _assets.Clear();
            _villagers.Clear();
            DuskRecallSystem.Clear();
            DarknessEvaluator.Clear();
            MorningReportSystem.ResetStaticEvents();
            EventBus.ResetAll();
            GameEventLog.Clear();
        }

        [UnityTest]
        public IEnumerator ScriptedNight_MorningReportRecordAppearsAtDawnWithHeadlineFacts()
        {
            var config = CreateConfig();
            BuildWorld(config);

            MorningReportData captured = default;
            string capturedProse = null;
            int readyCount = 0;
            MorningReportSystem.ReportReady += (d, prose) =>
            {
                captured = d;
                capturedProse = prose;
                readyCount++;
            };

            Assert.AreEqual(DayPhase.Day, _clock.Phase);

            // Day -> Dusk -> Night. The report system arms itself on Night.
            CrossIntoPhase(DayPhase.Dusk);
            CrossIntoPhase(DayPhase.Night);
            Assert.IsFalse(_reports.HasReport, "no report until dawn");
            yield return null;

            // --- Scripted death: one villager is struck twice (healthy -> injured -> dead).
            _villagers[0].OnMonsterAttack();
            _villagers[0].OnMonsterAttack();
            Assert.AreEqual(VillagerState.Dead, _villagers[0].State);

            // --- Scripted rescue: another villager is escorted and released in Safe light.
            Assert.AreEqual(LightZone.Safe, _villagers[1].CurrentZone,
                "the rescued villager is inside the campfire's Safe zone");
            Assert.IsTrue(_villagers[1].BeginRescue(_hero.transform));
            Assert.IsTrue(_villagers[1].ReleaseRescue(), "released in Safe light completes the rescue");
            yield return null;

            // --- Night -> Dawn: the report is built and published.
            CrossIntoPhase(DayPhase.Dawn);

            Assert.AreEqual(1, readyCount, "ReportReady fires exactly once at dawn");
            Assert.IsTrue(_reports.HasReport);

            var record = FindLastRecord(MorningReportSystem.RecordType);
            Assert.IsNotNull(record, "dawn appends a morning_report record to the shared log");

            string data = record.Value.Data;
            Assert.AreEqual(1, ParseCount(data, "dead"), "headline: one death");
            Assert.AreEqual(1, ParseCount(data, "rescued"), "headline: one rescue");

            // The record and the LastReport/ReportReady payload agree.
            Assert.AreEqual(1, captured.Dead);
            Assert.AreEqual(1, captured.Rescued);
            Assert.AreEqual(1, _reports.LastReport.Dead);
            Assert.IsTrue(captured.HeroRescuedSomeone);

            // The prose is the storybook tail after the separator, and speaks the death.
            int sep = data.IndexOf(MorningReportSystem.ProseSeparator);
            Assert.Greater(sep, 0);
            string proseTail = data.Substring(sep + 1).Trim();
            Assert.AreEqual(capturedProse, proseTail, "record prose matches the published prose");
            StringAssert.Contains("did not", proseTail);
            yield return null;
        }

        // ------------------------------------------------------------------
        // World construction
        // ------------------------------------------------------------------

        PrototypeConfig CreateConfig()
        {
            var config = ScriptableObject.CreateInstance<PrototypeConfig>();
            _assets.Add(config);

            config.dayDurationSeconds = 30f;
            config.duskDurationSeconds = 30f;
            config.nightDurationSeconds = 40f;
            config.dawnDurationSeconds = 10f;

            config.edgeBandFraction = 0.3f;
            config.arrivalRadius = 0.3f;
            config.campfireRadius = 10f; // Safe within 7 of origin

            config.bellkeeperMoveSpeed = 6f;
            config.interactRange = 2f;

            config.villagerWalkSpeed = 2f;
            config.villagerFearPerSecondInDark = 0f;
            config.villagerFearPerSecondInEdge = 0f;
            config.villagerInjuredDarkSeconds = 1000f;
            config.villagerMissingDarkSeconds = 2000f;

            config.simulationSeed = 4242;

            return config;
        }

        void BuildWorld(PrototypeConfig config)
        {
            DarknessEvaluator.Config = config;
            DuskRecallSystem.Config = config;

            var clockGO = Track(new GameObject("Clock"));
            _clock = clockGO.AddComponent<GameClock>();
            _clock.autoTick = false;
            _clock.Configure(config);

            var fireGO = Track(new GameObject("Campfire"));
            fireGO.transform.position = Vector3.zero;
            var fire = fireGO.AddComponent<LightSource>();
            fire.autoTick = false;
            fire.radius = config.campfireRadius;
            fire.strength = 1f;
            fire.fuelSeconds = -1f;

            var heroGO = Track(new GameObject("Bellkeeper"));
            heroGO.transform.position = Vector3.zero;
            _hero = heroGO.AddComponent<BellkeeperController>();
            _hero.autoTick = false;
            _hero.useDirectInput = false;
            _hero.Configure(config);

            var reportsGO = Track(new GameObject("MorningReportSystem"));
            _reports = reportsGO.AddComponent<MorningReportSystem>();

            for (int i = 0; i < 3; i++)
            {
                float angle = i / 3f * Mathf.PI * 2f;
                var pos = new Vector3(Mathf.Cos(angle) * 3f, 0f, Mathf.Sin(angle) * 3f);
                var go = Track(new GameObject($"Villager_{i:D2}"));
                go.transform.position = pos;
                var v = go.AddComponent<VillagerAgent>();
                v.autoTick = false;
                v.Config = config;
                v.seed = i;
                _villagers.Add(v);
            }
        }

        GameObject Track(GameObject go)
        {
            _spawned.Add(go);
            return go;
        }

        void CrossIntoPhase(DayPhase expected)
        {
            float remaining = _clock.GetPhaseDuration(_clock.Phase) - _clock.TimeInPhase;
            _clock.Tick(remaining + 0.001f);
            Assert.AreEqual(expected, _clock.Phase,
                $"expected the boundary tick to land in {expected}");
        }

        static GameEventLog.Record? FindLastRecord(string type)
        {
            var records = GameEventLog.Records;
            for (int i = records.Count - 1; i >= 0; i--)
            {
                if (records[i].Type == type) return records[i];
            }
            return null;
        }

        static int ParseCount(string data, string key)
        {
            var match = Regex.Match(data, @"\b" + key + @"=(\d+)\b");
            Assert.IsTrue(match.Success, $"morning_report head contains {key}: '{data}'");
            return int.Parse(match.Groups[1].Value);
        }
    }
}
