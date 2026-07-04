using System;
using Abbey.Core;
using Abbey.Hero;
using Abbey.Light;
using Abbey.Reports;
using Abbey.Villagers;
using UnityEngine;

namespace Abbey.Session
{
    /// <summary>The run's verdict.</summary>
    public enum GameOutcome
    {
        Undecided,
        Win,
        Loss,

        /// <summary>
        /// Bittersweet Survival (USER DESIGN DECISION, P2-10): the settlement
        /// cleared the First White Night with the Bellkeeper alive and the abbey
        /// fire lit, but with only 1..(villagerWinThreshold-1) villagers left. A
        /// distinct TERMINAL outcome — the settlement endures at heavy cost. It is
        /// neither the clean Win (&gt;= threshold) nor any of the three hard Losses.
        /// Appended last so the serialized Win/Loss indices stay stable.
        /// </summary>
        SurvivedBittersweet
    }

    /// <summary>Why the settlement fell (VERTICAL_SLICE_SPEC §11 loss list).</summary>
    public enum LossReason
    {
        None,
        BellkeeperDead,
        AbbeyFireOut,
        VillagersLost
    }

    /// <summary>
    /// The soft-failure spectrum snapshot handed to the morning report and the end
    /// screen (VERTICAL_SLICE_SPEC §11): the hard verdict plus the shades between —
    /// hound trusts/fears/vanishes, villagers hopeful/terrified, the abbey damaged,
    /// supplies low, injuries, one missing villager. Numeric fields are the live
    /// authority (registry + components); the embedded <see cref="Report"/> supplies
    /// the narrative colour (reuses <see cref="MorningReportData"/>, never duplicates it).
    /// </summary>
    public struct SessionSummary
    {
        public GameOutcome Outcome;
        public LossReason Reason;
        public int Day;
        public int NightNumber;
        public int WhiteNightIndex;
        public bool WhiteNightCleared;

        // --- Hard-condition live state -----------------------------------
        public int VillagersAlive;   // not Dead, not Missing (Injured still counts alive)
        public int VillagersKnown;   // registered villagers
        public int VillagersDead;
        public int VillagersMissing;
        public int VillagersInjured;
        public bool BellkeeperAlive;
        public float BellkeeperHealth;
        public bool AbbeyFireLit;

        // --- Soft-failure spectrum (VERTICAL_SLICE_SPEC §11) --------------
        /// <summary>The hound's last known state word ("Following"/"Missing"/…).</summary>
        public string HoundDisposition;
        public bool HoundVanished;         // hound went Missing
        public int HoundTrustDirection;    // -1 fell, 0 steady, +1 rose
        public int HoundFearDirection;
        public bool VillagersTerrified;    // panic struck or someone died
        public bool AbbeyDamaged;          // a fire (sacred or otherwise) was lost
        public bool SuppliesLow;           // ate more than gathered
        public bool AnyInjured;            // at least one villager wounded at dawn
        public bool MissingVillager;       // the "one villager too far" shade

        /// <summary>The night's storybook facts, reused so the report and end screen agree.</summary>
        public MorningReportData Report;
    }

    /// <summary>
    /// The outcome authority (P2-08). Deterministically evaluates the run against the
    /// VERTICAL_SLICE_SPEC §11 win/loss rules and latches the first decision.
    ///
    /// Win  = the dawn after the First White Night survives with at least
    ///        <see cref="GameSessionConfig.villagerWinThreshold"/> villagers alive,
    ///        the Bellkeeper alive, and the abbey fire lit.
    /// SurvivedBittersweet = the same survival but with only 1..(threshold-1)
    ///        villagers left — a distinct terminal outcome (P2-10 design decision):
    ///        the settlement endures, at heavy cost.
    /// Loss = the Bellkeeper dies, the abbey fire goes out (after it was ever lit),
    ///        or every villager is Dead/Missing (fled == Missing in this slice).
    ///
    /// Reads public APIs only — it never mutates another system:
    ///  • "abbey fire lit" = an explicitly-assigned <see cref="abbeyFlame"/> LightSource
    ///    being <c>isLit</c>, or (when none is assigned) ANY registered sacred, lit
    ///    <see cref="LightSource"/> in <see cref="DarknessEvaluator.Sources"/>. The loss
    ///    only fires once the flame was ever seen lit, so a world with no sacred flame
    ///    yet built does not insta-lose.
    ///  • "villagers alive" = <see cref="DuskRecallSystem.Villagers"/> entries whose
    ///    <see cref="VillagerState"/> is neither Dead nor Missing.
    ///  • "Bellkeeper alive" = <see cref="BellkeeperController.IsAlive"/> (Health &gt; 0).
    ///
    /// Every decision is written to the shared log as a "session" record (outcome,
    /// reason, day) and raised on the static <see cref="OutcomeDecided"/> event once.
    /// [ExecuteAlways] so EditMode tests get the OnEnable phase subscription.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class GameSession : MonoBehaviour
    {
        /// <summary>The log Type this system appends.</summary>
        public const string RecordType = "session";

        /// <summary>Raised exactly once, when the outcome first leaves Undecided.</summary>
        public static event Action<SessionSummary> OutcomeDecided;

        /// <summary>Test isolation — clear the static subscriber list in [SetUp]/[TearDown].</summary>
        public static void ResetStaticEvents()
        {
            OutcomeDecided = null;
        }

        [Tooltip("The Bellkeeper whose life gates win/loss. Required for a decisive verdict.")]
        public BellkeeperController bellkeeper;

        [Tooltip("The sacred abbey flame. If unset, any registered sacred+lit LightSource counts as the abbey fire.")]
        public LightSource abbeyFlame;

        [Tooltip("Re-evaluate every frame in play so a mid-night death/extinguish is caught promptly. Tests set false and call Evaluate().")]
        public bool autoEvaluate = true;

        GameSessionConfig _config;
        bool _started;
        bool _armed;
        int _nightsBegun;
        bool _abbeyFlameEverLit;
        bool _villagersEverPresent;

        public GameOutcome Outcome { get; private set; } = GameOutcome.Undecided;

        public LossReason Reason { get; private set; } = LossReason.None;

        public SessionSummary LastSummary { get; private set; }

        public bool IsDecided => Outcome != GameOutcome.Undecided;

        /// <summary>
        /// True once the dawn after the White Night has been reached. Set by the phase
        /// tracker; also settable so EditMode truth-table tests can pose the state
        /// directly without driving a whole night.
        /// </summary>
        public bool WhiteNightCleared { get; set; }

        public GameSessionConfig Config
        {
            get
            {
                if (_config == null)
                {
                    _config = GameSessionConfig.LoadOrDefault();
                }
                return _config;
            }
            set { _config = value; }
        }

        // ------------------------------------------------------------------
        // Live queries (public APIs only)
        // ------------------------------------------------------------------

        /// <summary>
        /// The abbey fire signal: the assigned flame being lit, or any registered
        /// sacred, lit source when no flame is assigned.
        /// </summary>
        public bool AbbeyFireLit
        {
            get
            {
                if (abbeyFlame != null)
                {
                    return abbeyFlame.isLit;
                }
                var sources = DarknessEvaluator.Sources;
                for (int i = 0; i < sources.Count; i++)
                {
                    var s = sources[i];
                    if (s != null && s.sacred && s.isLit)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        public bool BellkeeperAlive => bellkeeper != null && bellkeeper.IsAlive;

        void OnEnable()
        {
            EventBus.PhaseChanged += OnPhaseChanged;
        }

        void OnDisable()
        {
            EventBus.PhaseChanged -= OnPhaseChanged;
        }

        void Start()
        {
            if (Application.isPlaying)
            {
                Begin();
            }
        }

        void Update()
        {
            if (!Application.isPlaying || !autoEvaluate || !_armed)
            {
                return;
            }
            Evaluate();
        }

        /// <summary>
        /// Arms auto-evaluation and logs the run's start. Idempotent. Call once the
        /// world is built (the scene bootstrap / scenario does this in play).
        /// </summary>
        public void Begin()
        {
            if (_started)
            {
                return;
            }
            _started = true;
            _armed = true;
            GameEventLog.Append(RecordType,
                $"session_start whiteNight={Config.whiteNightIndex} " +
                $"villagerWinThreshold={Config.villagerWinThreshold}");
        }

        void OnPhaseChanged(DayPhase phase)
        {
            _armed = true; // the sim is running: from here decisions may be made
            if (phase == DayPhase.Night)
            {
                _nightsBegun++;
            }
            else if (phase == DayPhase.Dawn)
            {
                if (_nightsBegun >= Config.whiteNightIndex)
                {
                    WhiteNightCleared = true;
                }
            }
            Evaluate();
        }

        /// <summary>
        /// Evaluates the current world against the win/loss rules and latches the
        /// first non-Undecided verdict. Pure w.r.t. other systems (reads only). Safe
        /// to call any number of times; the verdict is decided at most once.
        /// </summary>
        public GameOutcome Evaluate()
        {
            if (IsDecided)
            {
                return Outcome;
            }

            bool abbeyLit = AbbeyFireLit;
            if (abbeyLit)
            {
                _abbeyFlameEverLit = true;
            }

            CountVillagers(out int known, out int alive, out _, out _, out _);
            if (known > 0)
            {
                _villagersEverPresent = true;
            }

            // ---- Loss (fixed priority) -----------------------------------
            LossReason reason = LossReason.None;
            if (bellkeeper != null && !bellkeeper.IsAlive)
            {
                reason = LossReason.BellkeeperDead;
            }
            else if (_abbeyFlameEverLit && !abbeyLit)
            {
                reason = LossReason.AbbeyFireOut;
            }
            else if (_villagersEverPresent && alive == 0)
            {
                reason = LossReason.VillagersLost;
            }

            if (reason != LossReason.None)
            {
                Decide(GameOutcome.Loss, reason);
                return Outcome;
            }

            // ---- Win / Bittersweet Survival (both terminal survivals) -----
            // Same gate as the clean Win — the dawn after the White Night is
            // survived with the Bellkeeper alive and the abbey fire lit — split by
            // how many villagers remain. A settlement with zero villagers has
            // already lost above (VillagersLost), so alive >= 1 here.
            if (WhiteNightCleared
                && alive >= 1
                && bellkeeper != null && bellkeeper.IsAlive
                && abbeyLit)
            {
                GameOutcome outcome = alive >= Config.villagerWinThreshold
                    ? GameOutcome.Win
                    : GameOutcome.SurvivedBittersweet;
                Decide(outcome, LossReason.None);
                return Outcome;
            }

            return GameOutcome.Undecided;
        }

        void Decide(GameOutcome outcome, LossReason reason)
        {
            Outcome = outcome;
            Reason = reason;
            var summary = BuildSummary(outcome, reason);
            LastSummary = summary;

            GameEventLog.Append(RecordType,
                $"outcome={outcome} reason={reason} day={summary.Day} " +
                $"villagersAlive={summary.VillagersAlive} bellkeeperAlive={summary.BellkeeperAlive} " +
                $"abbeyLit={summary.AbbeyFireLit} whiteNight={Config.whiteNightIndex} " +
                $"whiteNightCleared={summary.WhiteNightCleared}");

            OutcomeDecided?.Invoke(summary);
        }

        SessionSummary BuildSummary(GameOutcome outcome, LossReason reason)
        {
            CountVillagers(out int known, out int alive, out int dead, out int missing, out int injured);
            var report = MorningReport.Build(GameEventLog.Records);
            int day = GameClock.Instance != null ? GameClock.Instance.DayNumber : 0;

            return new SessionSummary
            {
                Outcome = outcome,
                Reason = reason,
                Day = day,
                NightNumber = report.NightNumber,
                WhiteNightIndex = Config.whiteNightIndex,
                WhiteNightCleared = WhiteNightCleared,

                VillagersAlive = alive,
                VillagersKnown = known,
                VillagersDead = dead,
                VillagersMissing = missing,
                VillagersInjured = injured,
                BellkeeperAlive = bellkeeper != null && bellkeeper.IsAlive,
                BellkeeperHealth = bellkeeper != null ? bellkeeper.Health : 0f,
                AbbeyFireLit = AbbeyFireLit,

                HoundDisposition = string.IsNullOrEmpty(report.HoundDisposition)
                    ? "unknown" : report.HoundDisposition,
                HoundVanished = report.HoundWentMissing,
                HoundTrustDirection = report.HoundTrustDirection,
                HoundFearDirection = report.HoundFearDirection,
                VillagersTerrified = report.PanicEvents > 0 || report.Dead > 0,
                AbbeyDamaged = report.SacredFlameLost || report.FiresLost > 0,
                SuppliesLow = report.FoodConsumed > report.FoodGained,
                AnyInjured = injured > 0 || report.Injured > 0,
                MissingVillager = missing > 0 || report.Missing > 0,

                Report = report,
            };
        }

        static void CountVillagers(out int known, out int alive, out int dead,
            out int missing, out int injured)
        {
            known = alive = dead = missing = injured = 0;
            var villagers = DuskRecallSystem.Villagers;
            for (int i = 0; i < villagers.Count; i++)
            {
                var v = villagers[i];
                if (v == null)
                {
                    continue;
                }
                known++;
                switch (v.State)
                {
                    case VillagerState.Dead:
                        dead++;
                        break;
                    case VillagerState.Missing:
                        missing++;
                        break;
                    case VillagerState.Injured:
                        injured++;
                        alive++;
                        break;
                    default:
                        alive++;
                        break;
                }
            }
        }

        /// <summary>Resets instance state to a fresh, undecided run (test isolation).</summary>
        public void Clear()
        {
            Outcome = GameOutcome.Undecided;
            Reason = LossReason.None;
            WhiteNightCleared = false;
            LastSummary = default;
            _started = false;
            _armed = false;
            _nightsBegun = 0;
            _abbeyFlameEverLit = false;
            _villagersEverPresent = false;
        }
    }
}
