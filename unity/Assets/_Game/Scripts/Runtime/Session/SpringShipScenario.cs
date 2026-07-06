using System;
using System.Collections.Generic;
using Abbey.Core;
using Abbey.Economy;
using Abbey.Island;
using Abbey.Villagers;
using Abbey.World;
using UnityEngine;

namespace Abbey.Session
{
    /// <summary>A read of the three-part launch manifest at one instant. Pure data.</summary>
    public readonly struct ManifestStatus
    {
        public readonly bool SettlersReady;
        public readonly bool ProvisionsReady;
        public readonly bool HullReady;
        public readonly int WillingSailors;
        public readonly int SettlersRequired;

        public ManifestStatus(bool settlers, bool provisions, bool hull,
            int willingSailors, int settlersRequired)
        {
            SettlersReady = settlers;
            ProvisionsReady = provisions;
            HullReady = hull;
            WillingSailors = willingSailors;
            SettlersRequired = settlersRequired;
        }

        public bool Complete => SettlersReady && ProvisionsReady && HullReady;
    }

    /// <summary>The who-sails / who-stays roster resolved at launch. Pure data.</summary>
    public readonly struct CrewRoster
    {
        public readonly int Sailed;
        public readonly int Stayed;
        public readonly int Volunteers;
        public readonly int Departures;

        public CrewRoster(int sailed, int stayed, int volunteers, int departures)
        {
            Sailed = sailed;
            Stayed = stayed;
            Volunteers = volunteers;
            Departures = departures;
        }
    }

    /// <summary>
    /// The Phase 3 campaign close (ROADMAP Phase 3 item 20): the spring-ship season win.
    /// Survive the year, complete a three-part manifest — settlers (willing sailors),
    /// provisions (ledger thresholds, grain counting toward food) and hull/rigging (the
    /// staged ship reconstruction, sailcloth included) — and at the following spring's
    /// launch window the ship sails. The who-sails/who-stays roster is resolved from the
    /// P3-13 departure intents + trust volunteers, the outcome is recorded as a
    /// <see cref="CampaignOutcome"/> (serialized for Phase 4), and the win is latched on
    /// <see cref="GameSession"/> so the existing end screen shows it.
    ///
    /// A missing manifest at the window is NOT a loss: the year rolls on (survival
    /// continues) and the run records "the ship did not sail" — the launch is retried the
    /// next spring. Decoupled and deterministic: all balance is in
    /// <see cref="GameSessionConfig"/>; the manifest is a pure read of the live ledger +
    /// arrival rollups + the ship construction site. Singleton + [ExecuteAlways] like the
    /// other Phase 3 systems. Tests call <see cref="EvaluateManifest"/> / <see cref="Reevaluate"/>
    /// directly; in play it evaluates on <see cref="EventBus.DayChanged"/> /
    /// <see cref="EventBus.SeasonChanged"/>.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class SpringShipScenario : MonoBehaviour
    {
        /// <summary>The log Type this system appends.</summary>
        public const string RecordType = "spring_ship";

        public static SpringShipScenario Instance { get; private set; }

        /// <summary>Raised once when the ship sails, carrying the recorded outcome.</summary>
        public static event Action<CampaignOutcome> ShipSailed;

        /// <summary>Test isolation — clear the static subscriber list in [SetUp]/[TearDown].</summary>
        public static void ResetStaticEvents()
        {
            ShipSailed = null;
        }

        [Tooltip("The staged ship reconstruction site; its completion is the hull/rigging part "
                 + "of the manifest. Auto-found by building id when unset.")]
        public Abbey.Buildings.ConstructionSite shipSite;

        [Tooltip("The outcome authority the win is latched on. Auto-found when unset.")]
        public GameSession session;

        [Tooltip("Write the CampaignOutcome JSON to persistentDataPath when the ship sails "
                 + "(tests turn this off / point it elsewhere).")]
        public bool persistOnLaunch = true;

        GameSessionConfig _config;
        bool _isDuplicate;
        bool _resolved;                 // the ship has sailed (terminal)
        int _neverSailedLoggedForYear;  // guards the "did not sail" record per spring
        bool _hullCompleteOverride;     // test hook when no ConstructionSite is wired

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

        public GameSession Session
        {
            get
            {
                if (session == null)
                {
                    session = FindFirstObjectByType<GameSession>();
                }
                return session;
            }
            set { session = value; }
        }

        /// <summary>True once the ship has sailed (the campaign win is latched).</summary>
        public bool HasSailed => _resolved;

        /// <summary>The launch window: spring of the configured launch year (or later).</summary>
        public bool LaunchWindowOpen
        {
            get
            {
                var season = SeasonSystem.Instance;
                if (season == null)
                {
                    return false;
                }
                return season.CurrentSeason == Season.Spring
                       && season.YearNumber >= Config.springLaunchYear;
            }
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                _isDuplicate = true;
                Debug.LogWarning("[SpringShipScenario] Duplicate instance ignored.", this);
                if (Application.isPlaying)
                {
                    Destroy(gameObject);
                }
                return;
            }
            _isDuplicate = false;
            Instance = this;
        }

        void OnEnable()
        {
            if (_isDuplicate)
            {
                return;
            }
            EventBus.DayChanged -= OnDayChanged;
            EventBus.DayChanged += OnDayChanged;
            EventBus.SeasonChanged -= OnSeasonChanged;
            EventBus.SeasonChanged += OnSeasonChanged;
        }

        void OnDisable()
        {
            EventBus.DayChanged -= OnDayChanged;
            EventBus.SeasonChanged -= OnSeasonChanged;
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>Injects a config (tests).</summary>
        public void Configure(GameSessionConfig config)
        {
            _config = config;
        }

        /// <summary>Test hook: marks the hull/rigging part complete without a ConstructionSite.</summary>
        public void SetHullComplete(bool complete)
        {
            _hullCompleteOverride = complete;
        }

        void OnDayChanged(int day)
        {
            Reevaluate();
        }

        void OnSeasonChanged(Season season)
        {
            Reevaluate();
        }

        // ------------------------------------------------------------------
        // Manifest
        // ------------------------------------------------------------------

        /// <summary>Willing sailors: trust volunteers + spring departure intents (P3-13).</summary>
        public int WillingSailors()
        {
            var arrivals = ArrivalSystem.Instance;
            if (arrivals == null)
            {
                return 0;
            }
            return arrivals.VolunteeredCount + arrivals.DepartureIntents.Count;
        }

        /// <summary>True once the hull/rigging (ship reconstruction) is complete.</summary>
        public bool HullReady
        {
            get
            {
                if (_hullCompleteOverride)
                {
                    return true;
                }
                var site = ResolveShipSite();
                return site != null && site.IsComplete;
            }
        }

        Abbey.Buildings.ConstructionSite ResolveShipSite()
        {
            if (shipSite != null)
            {
                return shipSite;
            }
            // Fall back to any active/finished site matching the configured ship id.
            var active = Abbey.Buildings.ConstructionSite.Active;
            for (int i = 0; i < active.Count; i++)
            {
                var s = active[i];
                if (s != null && s.Type != null && s.Type.id == Config.shipBuildingId)
                {
                    shipSite = s;
                    return s;
                }
            }
            return null;
        }

        /// <summary>
        /// Evaluates the three manifest parts independently against the config thresholds.
        /// Provisions fold grain into food at <see cref="EconomyConfig.GrainToFood"/> before
        /// the check. Pure read — mutates nothing.
        /// </summary>
        public ManifestStatus EvaluateManifest()
        {
            var cfg = Config;
            int willing = WillingSailors();
            bool settlers = willing >= cfg.manifestSettlers;
            bool provisions = ProvisionsReady(cfg);
            bool hull = HullReady;
            return new ManifestStatus(settlers, provisions, hull, willing, cfg.manifestSettlers);
        }

        bool ProvisionsReady(GameSessionConfig cfg)
        {
            if (cfg.manifestProvisions == null || cfg.manifestProvisions.Count == 0)
            {
                return true;
            }
            var econ = ResourceLedger.Config;
            for (int i = 0; i < cfg.manifestProvisions.Count; i++)
            {
                var stack = cfg.manifestProvisions[i];
                if (stack.amount <= 0)
                {
                    continue;
                }
                int have = ResourceLedger.Get(stack.type);
                if (stack.type == ResourceType.Food && econ != null)
                {
                    // Grain mills/cooks into food at the config ratio before the check.
                    have += econ.GrainToFood(ResourceLedger.Get(ResourceType.Grain));
                }
                if (have < stack.amount)
                {
                    return false;
                }
            }
            return true;
        }

        // ------------------------------------------------------------------
        // Resolution
        // ------------------------------------------------------------------

        /// <summary>
        /// The core tick: at the launch window, complete manifest ⇒ the ship sails; a
        /// window that opens and later closes with an incomplete manifest records "the ship
        /// did not sail" once and lets the year roll on. Idempotent; the win latches once.
        /// </summary>
        public void Reevaluate()
        {
            if (_isDuplicate || _resolved || !Config.phase3CampaignEnabled)
            {
                return;
            }
            if (!LaunchWindowOpen)
            {
                return;
            }
            var manifest = EvaluateManifest();
            if (manifest.Complete)
            {
                ResolveShipSailed(manifest);
            }
            else
            {
                RecordShipNeverSailed(manifest);
            }
        }

        void ResolveShipSailed(ManifestStatus manifest)
        {
            _resolved = true;
            var roster = ResolveRoster(manifest);

            GameEventLog.Append(RecordType,
                $"launched result={CampaignResult.ShipSailed} sailed={roster.Sailed} " +
                $"stayed={roster.Stayed} volunteers={roster.Volunteers} " +
                $"departures={roster.Departures}");

            var outcome = CampaignOutcome.Capture(CampaignResult.ShipSailed);
            FillManifest(outcome, manifest, roster);
            if (persistOnLaunch)
            {
                outcome.Save();
            }

            ShipSailed?.Invoke(outcome);

            var s = Session;
            if (s != null)
            {
                s.ReportShipSailed(outcome);
            }
        }

        void RecordShipNeverSailed(ManifestStatus manifest)
        {
            int year = SeasonSystem.Instance != null ? SeasonSystem.Instance.YearNumber : 0;
            if (_neverSailedLoggedForYear == year)
            {
                return;
            }
            _neverSailedLoggedForYear = year;
            GameEventLog.Append(RecordType,
                $"ship_did_not_sail year={year} settlers={manifest.SettlersReady} " +
                $"provisions={manifest.ProvisionsReady} hull={manifest.HullReady} " +
                "(the year rolls on)");
        }

        CrewRoster ResolveRoster(ManifestStatus manifest)
        {
            var arrivals = ArrivalSystem.Instance;
            int volunteers = arrivals != null ? arrivals.VolunteeredCount : 0;
            int departures = arrivals != null ? arrivals.DepartureIntents.Count : 0;

            int aliveVillagers = CountAliveVillagers();
            int sailed = manifest.WillingSailors;
            int stayed = Mathf.Max(0, aliveVillagers - sailed);
            return new CrewRoster(sailed, stayed, volunteers, departures);
        }

        static int CountAliveVillagers()
        {
            int alive = 0;
            var villagers = DuskRecallSystem.Villagers;
            for (int i = 0; i < villagers.Count; i++)
            {
                var v = villagers[i];
                if (v != null && v.State != VillagerState.Dead && v.State != VillagerState.Missing)
                {
                    alive++;
                }
            }
            return alive;
        }

        static void FillManifest(CampaignOutcome outcome, ManifestStatus manifest, CrewRoster roster)
        {
            outcome.settlersReady = manifest.SettlersReady;
            outcome.provisionsReady = manifest.ProvisionsReady;
            outcome.hullReady = manifest.HullReady;
            outcome.manifestComplete = manifest.Complete;
            outcome.willingSailors = manifest.WillingSailors;
            outcome.settlersRequired = manifest.SettlersRequired;
            outcome.sailedCount = roster.Sailed;
            outcome.stayedBehindCount = roster.Stayed;
            outcome.volunteeredCount = roster.Volunteers;
            outcome.leftCount = roster.Departures;
        }

        /// <summary>Resets the scenario to before the run (test isolation).</summary>
        public void Clear()
        {
            _resolved = false;
            _neverSailedLoggedForYear = 0;
            _hullCompleteOverride = false;
        }
    }
}
