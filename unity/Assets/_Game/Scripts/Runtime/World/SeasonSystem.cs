using Abbey.Core;
using UnityEngine;

namespace Abbey.World
{
    /// <summary>The turning year: Spring (hope) -> Summer (growth) -> Autumn (warning) -> Winter (judgment).</summary>
    public enum Season
    {
        Spring,
        Summer,
        Autumn,
        Winter
    }

    /// <summary>The eight moon phases, New through Waning Crescent, in order.</summary>
    public enum MoonPhase
    {
        New,
        WaxingCrescent,
        FirstQuarter,
        WaxingGibbous,
        Full,
        WaningGibbous,
        LastQuarter,
        WaningCrescent
    }

    /// <summary>
    /// The seasonal calendar. Observes <see cref="GameClock"/>'s day counter (via
    /// <see cref="EventBus.DayChanged"/>) and derives the current <see cref="Season"/>,
    /// day-of-year and in-season day from <see cref="SeasonConfig"/>. It owns exactly
    /// one side effect on the clock: it pushes the season's night-length multiplier
    /// into <see cref="GameClock.NightLengthMultiplier"/>, so nights lengthen toward
    /// Winter. Season transitions raise <see cref="EventBus.SeasonChanged"/> and log.
    ///
    /// Season logic lives here, never inside the clock (AGENTS.md / brief). The
    /// static <see cref="SeasonForDay"/> / <see cref="DayOfYearForDay"/> helpers let
    /// WeatherSystem and tests derive the calendar without an instance, so there is
    /// no event-ordering dependency between the two systems.
    /// [ExecuteAlways] so EditMode tests get the OnEnable/OnDisable lifecycle.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class SeasonSystem : MonoBehaviour
    {
        public static SeasonSystem Instance { get; private set; }

        SeasonConfig _config;
        bool _isDuplicate;
        bool _hasSeason;

        public Season CurrentSeason { get; private set; } = Season.Spring;

        /// <summary>1-based day within the current year (1..daysPerSeason*4).</summary>
        public int DayOfYear { get; private set; } = 1;

        /// <summary>1-based day within the current season (1..daysPerSeason).</summary>
        public int DayInSeason { get; private set; } = 1;

        /// <summary>1-based year counter (year 1 is the first four seasons).</summary>
        public int YearNumber { get; private set; } = 1;

        /// <summary>The night-length multiplier currently applied to the clock's night phase.</summary>
        public float NightLengthMultiplier { get; private set; } = 1f;

        public SeasonConfig Config
        {
            get
            {
                if (_config == null)
                {
                    _config = SeasonConfig.LoadOrDefault();
                }
                return _config;
            }
            set { _config = value; }
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                _isDuplicate = true;
                Debug.LogWarning("[SeasonSystem] Duplicate instance ignored.", this);
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
            // Seed the season for whatever day the clock already sits on.
            Refresh(raiseEvents: false);
        }

        void OnDisable()
        {
            EventBus.DayChanged -= OnDayChanged;
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>Injects a config (tests) and recomputes the season without raising events.</summary>
        public void Configure(SeasonConfig config)
        {
            _config = config;
            _hasSeason = false;
            Refresh(raiseEvents: false);
        }

        void OnDayChanged(int dayNumber)
        {
            Refresh(raiseEvents: true);
        }

        /// <summary>
        /// Recomputes season / day-of-year for the clock's current day, applies the
        /// night-length multiplier, and (optionally) raises SeasonChanged on a turn.
        /// Public so tests and debug tools can force it.
        /// </summary>
        public void Refresh(bool raiseEvents)
        {
            var cfg = Config;
            int dayNumber = GameClock.Instance != null ? GameClock.Instance.DayNumber : 1;
            int daysPerSeason = Mathf.Max(1, cfg.daysPerSeason);
            int daysPerYear = daysPerSeason * 4;

            int zeroBased = Mathf.Max(0, dayNumber - 1);
            Season season = SeasonForDay(dayNumber, daysPerSeason);
            DayOfYear = (zeroBased % daysPerYear) + 1;
            DayInSeason = (zeroBased % daysPerSeason) + 1;
            YearNumber = (zeroBased / daysPerYear) + 1;
            NightLengthMultiplier = cfg.NightMultiplierFor(season);

            // Push the night-length scaling into the clock (its only season coupling).
            if (GameClock.Instance != null)
            {
                GameClock.Instance.NightLengthMultiplier = NightLengthMultiplier;
            }

            bool changed = !_hasSeason || season != CurrentSeason;
            CurrentSeason = season;
            _hasSeason = true;

            if (changed && raiseEvents)
            {
                EventBus.RaiseSeasonChanged(season);
            }
        }

        // ------------------------------------------------------------------
        // Static calendar helpers (no instance / no ordering dependency)
        // ------------------------------------------------------------------

        /// <summary>Season for a 1-based day number given the days-per-season length.</summary>
        public static Season SeasonForDay(int dayNumber, int daysPerSeason)
        {
            int per = Mathf.Max(1, daysPerSeason);
            int zeroBased = Mathf.Max(0, dayNumber - 1);
            return (Season)((zeroBased / per) % 4);
        }

        /// <summary>1-based day-of-year for a 1-based day number.</summary>
        public static int DayOfYearForDay(int dayNumber, int daysPerSeason)
        {
            int per = Mathf.Max(1, daysPerSeason);
            int daysPerYear = per * 4;
            int zeroBased = Mathf.Max(0, dayNumber - 1);
            return (zeroBased % daysPerYear) + 1;
        }
    }
}
