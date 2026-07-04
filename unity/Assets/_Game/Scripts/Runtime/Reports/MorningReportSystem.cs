using System;
using System.Collections.Generic;
using Abbey.Core;
using UnityEngine;

namespace Abbey.Reports
{
    /// <summary>
    /// Turns the night's slice of the shared <see cref="GameEventLog"/> into the dawn
    /// consequence report (VERTICAL_SLICE_SPEC §3, minutes 18–20). It subscribes to
    /// <see cref="EventBus.PhaseChanged"/> only: on entering Night it snapshots the log
    /// index; on the Night→Dawn transition it builds a <see cref="MorningReportData"/>
    /// and its storybook prose from exactly that window, appends a "morning_report"
    /// record for downstream consumers (P2-08/P2-10), exposes <see cref="LastReport"/>
    /// / <see cref="LastProse"/>, and raises the static <see cref="ReportReady"/> event.
    ///
    /// Consumes public APIs only. It never writes to the schema or touches other
    /// systems — one log, many consumers.
    /// </summary>
    [DisallowMultipleComponent]
    public class MorningReportSystem : MonoBehaviour
    {
        /// <summary>The log Type this system appends at dawn.</summary>
        public const string RecordType = "morning_report";

        /// <summary>Separates the machine-readable stat head from the prose tail.</summary>
        public const char ProseSeparator = '|';

        /// <summary>Raised once per dawn, after the record is appended. Static for UI/audio.</summary>
        public static event Action<MorningReportData, string> ReportReady;

        /// <summary>Test isolation — clear the static subscriber list in [SetUp]/[TearDown].</summary>
        public static void ResetStaticEvents()
        {
            ReportReady = null;
        }

        public MorningReportData LastReport { get; private set; }
        public string LastProse { get; private set; } = string.Empty;
        public bool HasReport { get; private set; }

        int _nightStartIndex = -1;
        bool _armed;

        void OnEnable()
        {
            EventBus.PhaseChanged += OnPhaseChanged;
        }

        void OnDisable()
        {
            EventBus.PhaseChanged -= OnPhaseChanged;
        }

        void OnPhaseChanged(DayPhase phase)
        {
            if (phase == DayPhase.Night)
            {
                // Everything appended from here to Dawn is this night's window.
                _nightStartIndex = GameEventLog.Count;
                _armed = true;
            }
            else if (phase == DayPhase.Dawn && _armed)
            {
                BuildAndPublish(_nightStartIndex, GameEventLog.Count);
                _armed = false;
            }
        }

        /// <summary>
        /// Builds the report from records in [startIndex, endIndex) of the shared log,
        /// appends the "morning_report" record and raises <see cref="ReportReady"/>.
        /// Public so tests/tools can force it without a full phase cycle.
        /// </summary>
        public MorningReportData BuildAndPublish(int startIndex, int endIndex)
        {
            var window = Slice(startIndex, endIndex);
            var data = MorningReport.Build(window);
            string prose = MorningReportProse.Compose(data);

            LastReport = data;
            LastProse = prose;
            HasReport = true;

            GameEventLog.Append(RecordType, FormatRecord(data, prose));
            ReportReady?.Invoke(data, prose);
            return data;
        }

        static List<GameEventLog.Record> Slice(int startIndex, int endIndex)
        {
            var records = GameEventLog.Records;
            int from = Mathf.Clamp(startIndex, 0, records.Count);
            int to = Mathf.Clamp(endIndex, from, records.Count);
            var window = new List<GameEventLog.Record>(to - from);
            for (int i = from; i < to; i++)
            {
                window.Add(records[i]);
            }
            return window;
        }

        /// <summary>
        /// The canonical "morning_report" Data payload: a compact key=value stat head,
        /// then <see cref="ProseSeparator"/>, then the storybook prose (which never
        /// contains the separator). Downstream systems split on the first
        /// <see cref="ProseSeparator"/> to read either half.
        /// </summary>
        public static string FormatRecord(MorningReportData d, string prose)
        {
            return
                $"night={d.NightNumber} survivors={d.Survivors} dead={d.Dead} missing={d.Missing} " +
                $"injured={d.Injured} rescued={d.Rescued} firesLost={d.FiresLost} firesRelit={d.FiresRelit} " +
                $"sacredLost={d.SacredFlameLost} foodEaten={d.FoodConsumed} woodBurned={d.WoodConsumed} " +
                $"oilBurned={d.OilConsumed} monsters={d.MonstersFaced} monstersKilled={d.MonstersKilled} " +
                $"whispers={d.Whispers} shadow={d.ShadowSeen} panic={d.PanicEvents} bell={d.BellRangCount} " +
                $"houndDisposition={NonEmpty(d.HoundDisposition)} houndTrust={Dir(d.HoundTrustDirection)} " +
                $"houndFear={Dir(d.HoundFearDirection)} houndMissing={d.HoundWentMissing} " +
                $"houndProtected={d.HoundProtectedHero} houndAnsweredBell={d.HoundAnsweredBell} " +
                $"heroRescued={d.HeroRescuedSomeone} heroCarriedFire={d.HeroCarriedFireIntoDark} " +
                $"heroDied={d.HeroDied} heroBitten={d.HeroBitten} " +
                $"{ProseSeparator} {prose}";
        }

        static string NonEmpty(string s) => string.IsNullOrEmpty(s) ? "none" : s;

        static string Dir(int d) => d > 0 ? "rose" : d < 0 ? "fell" : "steady";
    }
}
