using System;
using System.Collections.Generic;
using Abbey.Beast;
using Abbey.Buildings;
using Abbey.Core;
using Abbey.Island;
using Abbey.Map2;
using Abbey.Morale;
using Abbey.Reports;
using Abbey.World;
using UnityEngine;

namespace Abbey.Session
{
    /// <summary>The terminal (or in-progress) verdict of the Phase 3 campaign (P3-14).</summary>
    public enum CampaignResult
    {
        InProgress,
        ShipSailed,       // manifest complete at the spring window: the campaign win
        ShipNeverSailed,  // window reached with an incomplete manifest — the year rolled on
        WinterCollapse    // the settlement was wiped before the spring tide (a loss)
    }

    /// <summary>
    /// The serialized close of a Phase 3 run (P3-14) and the Phase 4 carryover payload
    /// (ROADMAP Phase 4 reads it for the Bellkeeper trait grant). Plain, JsonUtility-
    /// friendly data (public fields, arrays, no properties): the campaign result, the
    /// chapter reached, the three-part manifest state, the who-sails/who-stays roster and
    /// the key choices of the run (hound path, abbey form, trust, laws, deaths, POIs).
    ///
    /// Built deterministically by <see cref="Capture"/> from the live system states +
    /// the append-only <see cref="GameEventLog"/>, and (for the win) it carries the
    /// storybook <see cref="EndSummary"/> chronicle so the end screen and the save agree.
    /// <see cref="Save"/> writes it under <see cref="Application.persistentDataPath"/> as
    /// <c>campaign_outcome.json</c>; <see cref="Load"/> reads it back.
    /// </summary>
    [Serializable]
    public class CampaignOutcome
    {
        /// <summary>File name under <see cref="Application.persistentDataPath"/>.</summary>
        public const string FileName = "campaign_outcome.json";

        [Tooltip("Serialized schema version so Phase 4 can migrate.")]
        public int schemaVersion = 2;

        // ---- Result --------------------------------------------------------
        public string result = CampaignResult.InProgress.ToString();
        public int resultCode = (int)CampaignResult.InProgress;

        // ---- Where the year ended -----------------------------------------
        public int year;
        public int dayOfYear;
        public string season = Season.Spring.ToString();
        public int chapterIndex;
        public string chapterReached = string.Empty;

        // ---- Manifest (three independent parts) ---------------------------
        public bool settlersReady;
        public bool provisionsReady;
        public bool hullReady;
        public bool manifestComplete;
        public int willingSailors;
        public int settlersRequired;

        // ---- Who-sails / who-stays roster ---------------------------------
        public int sailedCount;
        public int stayedBehindCount;
        public int volunteeredCount;
        public int leftCount;

        // ---- Key choices of the run ---------------------------------------
        public string houndPath = HoundPath.Unevolved.ToString();
        public float beastStatus;
        public string abbeyForm = AbbeyForm.Balanced.ToString();
        public string trustTier = TrustTier.Neutral.ToString();
        public string[] lawsEnacted = Array.Empty<string>();
        public int villagerDeaths;
        public int poisDiscovered;

        // ---- Phase 4 carryover -------------------------------------------
        public string bellkeeperTrait = BellkeeperTrait.None.ToString();

        // ---- Chronicle -----------------------------------------------------
        [TextArea] public string chronicle = string.Empty;

        /// <summary>The strongly-typed result (mirror of <see cref="resultCode"/>).</summary>
        public CampaignResult Result => (CampaignResult)resultCode;

        /// <summary>
        /// Captures the run's close from the live systems + the event log. Pure read —
        /// mutates nothing. Any missing system falls back to a neutral default so this is
        /// safe to call from tests with a partial world.
        /// </summary>
        public static CampaignOutcome Capture(CampaignResult result)
        {
            var outcome = new CampaignOutcome
            {
                result = result.ToString(),
                resultCode = (int)result,
            };

            var season = SeasonSystem.Instance;
            if (season != null)
            {
                outcome.year = season.YearNumber;
                outcome.dayOfYear = season.DayOfYear;
                outcome.season = season.CurrentSeason.ToString();
            }

            var chapters = ChapterSystem.Instance;
            if (chapters != null)
            {
                outcome.chapterIndex = chapters.CurrentChapterIndex;
                outcome.chapterReached = chapters.CurrentChapterName;
            }

            var arrivals = ArrivalSystem.Instance;
            if (arrivals != null)
            {
                outcome.volunteeredCount = arrivals.VolunteeredCount;
                outcome.leftCount = arrivals.LeftCount;
                outcome.stayedBehindCount = arrivals.StayedCount;
            }

            var hound = HoundEvolutionSystem.Instance;
            if (hound != null)
            {
                var report = hound.Report();
                outcome.houndPath = report.Path.ToString();
                outcome.beastStatus = report.BeastStatus;
            }

            outcome.abbeyForm = AbbeyState.CurrentForm.ToString();

            var pressures = PressureSystem.Instance;
            if (pressures != null)
            {
                outcome.trustTier = pressures.TrustTier.ToString();
            }

            var pois = Abbey.Island.ExplorationSystem.Instance;
            if (pois != null)
            {
                int discovered = 0;
                var list = pois.Pois;
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i] != null && list[i].discovered)
                    {
                        discovered++;
                    }
                }
                outcome.poisDiscovered = discovered;
            }

            var summary = EndSummary.Build(GameEventLog.Records);
            outcome.lawsEnacted = summary.LawsEnacted.ToArray();
            outcome.villagerDeaths = summary.Deaths;
            outcome.chronicle = EndSummary.Compose(summary);
            outcome.bellkeeperTrait = CampaignCarryoverSystem.DeriveTrait(outcome).ToString();
            return outcome;
        }

        /// <summary>Serializes to pretty JSON (documented schema for Phase 4).</summary>
        public string ToJson()
        {
            return JsonUtility.ToJson(this, true);
        }

        /// <summary>Parses a CampaignOutcome from JSON (round-trips <see cref="ToJson"/>).</summary>
        public static CampaignOutcome FromJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            var outcome = JsonUtility.FromJson<CampaignOutcome>(json);
            if (outcome == null) return null;
            if (outcome.schemaVersion < 2 || string.IsNullOrEmpty(outcome.bellkeeperTrait))
            {
                outcome.bellkeeperTrait = CampaignCarryoverSystem.DeriveTrait(outcome).ToString();
                outcome.schemaVersion = 2;
            }
            return outcome;
        }

        /// <summary>Default carryover path under the persistent data directory.</summary>
        public static string DefaultPath =>
            System.IO.Path.Combine(Application.persistentDataPath, FileName);

        /// <summary>Writes the outcome as JSON. Returns the path written (or null on error).</summary>
        public string Save(string path = null)
        {
            path ??= DefaultPath;
            try
            {
                System.IO.File.WriteAllText(path, ToJson());
                GameEventLog.Append("campaign", $"outcome_saved result={result} path={path}");
                return path;
            }
            catch (Exception e)
            {
                GameEventLog.Append("campaign", $"outcome_save_failed {e.GetType().Name}");
                return null;
            }
        }

        /// <summary>Reads a saved outcome, or null when none exists / unreadable.</summary>
        public static CampaignOutcome Load(string path = null)
        {
            path ??= DefaultPath;
            try
            {
                if (!System.IO.File.Exists(path))
                {
                    return null;
                }
                return FromJson(System.IO.File.ReadAllText(path));
            }
            catch
            {
                return null;
            }
        }
    }
}
