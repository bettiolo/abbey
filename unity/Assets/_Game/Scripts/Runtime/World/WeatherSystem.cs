using Abbey.Core;
using Abbey.Hero;
using Abbey.Light;
using UnityEngine;

namespace Abbey.World
{
    /// <summary>The sky states that bend the light: a clear night, fog, rain, or a full tempest.</summary>
    public enum Weather
    {
        Clear,
        Fog,
        Rain,
        Tempest
    }

    /// <summary>
    /// The deterministic weather + omen layer. For each calendar day it draws a
    /// <see cref="Weather"/> state (weighted by the season, <see cref="SeasonConfig"/>),
    /// reads the <see cref="MoonPhase"/> from the day's place in the lunar cycle, and
    /// rolls the White Night omen. From those it computes two effectiveness scalars:
    ///
    ///   light = weather.light * moon.light * (whiteNight ? whiteNight.light : 1)
    ///   bell  = weather.bell             * (whiteNight ? whiteNight.bell  : 1)
    ///
    /// and pushes them each phase into the global
    /// <see cref="DarknessEvaluator.LightEffectivenessMultiplier"/> and the
    /// controlled hero's <see cref="BellkeeperController.BellReliabilityMultiplier"/>.
    /// Every draw is a pure function of (config.weatherSeed, dayNumber), so the same
    /// seed yields an identical year and a different seed a different one. Weather
    /// and omen transitions raise <see cref="EventBus.WeatherChanged"/> /
    /// <see cref="EventBus.OmenAppeared"/> and log.
    /// [ExecuteAlways] so EditMode tests get the OnEnable/OnDisable lifecycle.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class WeatherSystem : MonoBehaviour
    {
        public static WeatherSystem Instance { get; private set; }

        [Tooltip("The hero whose bell reliability weather scales. Auto-found when unset.")]
        public BellkeeperController bellkeeper;

        SeasonConfig _config;
        bool _isDuplicate;
        bool _hasState;

        public Weather CurrentWeather { get; private set; } = Weather.Clear;

        public MoonPhase CurrentMoonPhase { get; private set; } = MoonPhase.New;

        public bool IsWhiteNight { get; private set; }

        /// <summary>Global light-effectiveness multiplier this system last published.</summary>
        public float LightEffectivenessMultiplier { get; private set; } = 1f;

        /// <summary>Bell-reliability (recall range) multiplier this system last published.</summary>
        public float BellReliabilityMultiplier { get; private set; } = 1f;

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
                Debug.LogWarning("[WeatherSystem] Duplicate instance ignored.", this);
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
            EventBus.PhaseChanged -= OnPhaseChanged;
            EventBus.PhaseChanged += OnPhaseChanged;
            Refresh(raiseEvents: false);
        }

        void OnDisable()
        {
            EventBus.DayChanged -= OnDayChanged;
            EventBus.PhaseChanged -= OnPhaseChanged;
            // Removing the weather layer restores neutral light for everyone else.
            DarknessEvaluator.LightEffectivenessMultiplier = 1f;
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>Injects a config (tests) and recomputes weather without raising events.</summary>
        public void Configure(SeasonConfig config)
        {
            _config = config;
            _hasState = false;
            Refresh(raiseEvents: false);
        }

        void OnDayChanged(int dayNumber)
        {
            Refresh(raiseEvents: true);
        }

        void OnPhaseChanged(DayPhase phase)
        {
            // Weather is per-day, but push the multipliers every phase (brief), so a
            // hero that appears mid-day still gets the current sky.
            Refresh(raiseEvents: true);
        }

        BellkeeperController ResolveBellkeeper()
        {
            if (bellkeeper == null)
            {
                bellkeeper = FindFirstObjectByType<BellkeeperController>();
            }
            return bellkeeper;
        }

        /// <summary>
        /// Recomputes weather/moon/omen for the clock's current day, publishes the
        /// two multipliers, and raises WeatherChanged / OmenAppeared on transitions.
        /// Public so tests and debug tools can force it.
        /// </summary>
        public void Refresh(bool raiseEvents)
        {
            var cfg = Config;
            int dayNumber = GameClock.Instance != null ? GameClock.Instance.DayNumber : 1;

            Weather weather = WeatherForDay(dayNumber, cfg);
            MoonPhase moon = MoonPhaseForDay(dayNumber, cfg.moonCycleDays);
            bool whiteNight = IsWhiteNightForDay(dayNumber, cfg);

            var weatherMod = cfg.ModifierFor(weather);
            float light = weatherMod.lightEffectiveness * cfg.MoonLightMultiplier(moon);
            float bell = weatherMod.bellReliability;
            if (whiteNight)
            {
                light *= cfg.whiteNightLightEffectiveness;
                bell *= cfg.whiteNightBellReliability;
            }
            light = Mathf.Max(0f, light);
            bell = Mathf.Max(0f, bell);

            bool weatherChanged = !_hasState || weather != CurrentWeather;
            bool omenBegan = whiteNight && (!_hasState || !IsWhiteNight);

            CurrentWeather = weather;
            CurrentMoonPhase = moon;
            IsWhiteNight = whiteNight;
            LightEffectivenessMultiplier = light;
            BellReliabilityMultiplier = bell;
            _hasState = true;

            // Publish to the two consumers.
            DarknessEvaluator.LightEffectivenessMultiplier = light;
            var keeper = ResolveBellkeeper();
            if (keeper != null)
            {
                keeper.BellReliabilityMultiplier = bell;
            }

            if (raiseEvents && weatherChanged)
            {
                EventBus.RaiseWeatherChanged(weather);
            }
            if (raiseEvents && omenBegan)
            {
                EventBus.RaiseOmenAppeared($"WhiteNight day={dayNumber}");
            }
        }

        /// <summary>The next day (from the current clock day forward) that carries a
        /// White Night, scanning up to one year ahead, or null if none is found.</summary>
        public int? NextWhiteNightDay()
        {
            var cfg = Config;
            int dayNumber = GameClock.Instance != null ? GameClock.Instance.DayNumber : 1;
            int scan = Mathf.Max(1, cfg.daysPerSeason) * 4;
            for (int i = 1; i <= scan; i++)
            {
                if (IsWhiteNightForDay(dayNumber + i, cfg))
                {
                    return dayNumber + i;
                }
            }
            return null;
        }

        // ------------------------------------------------------------------
        // Deterministic static draws (pure functions of seed + day)
        // ------------------------------------------------------------------

        /// <summary>Moon phase for a 1-based day number over an 8-phase cycle.</summary>
        public static MoonPhase MoonPhaseForDay(int dayNumber, int moonCycleDays)
        {
            int cycle = Mathf.Max(1, moonCycleDays);
            int zeroBased = Mathf.Max(0, dayNumber - 1);
            int within = zeroBased % cycle;
            int index = Mathf.Clamp(within * 8 / cycle, 0, 7);
            return (MoonPhase)index;
        }

        /// <summary>Deterministic weather draw for a 1-based day, weighted by the day's season.</summary>
        public static Weather WeatherForDay(int dayNumber, SeasonConfig cfg)
        {
            Season season = SeasonSystem.SeasonForDay(dayNumber, cfg.daysPerSeason);
            var w = cfg.WeightsFor(season);
            float total = w.clear + w.fog + w.rain + w.tempest;
            if (total <= 0f)
            {
                return Weather.Clear;
            }

            var rng = new System.Random(DaySeed(cfg.weatherSeed, dayNumber, 0x5EA5));
            float roll = (float)rng.NextDouble() * total;

            if (roll < w.clear)
            {
                return Weather.Clear;
            }
            roll -= w.clear;
            if (roll < w.fog)
            {
                return Weather.Fog;
            }
            roll -= w.fog;
            if (roll < w.rain)
            {
                return Weather.Rain;
            }
            return Weather.Tempest;
        }

        /// <summary>Deterministic White Night roll for a 1-based day.</summary>
        public static bool IsWhiteNightForDay(int dayNumber, SeasonConfig cfg)
        {
            Season season = SeasonSystem.SeasonForDay(dayNumber, cfg.daysPerSeason);
            if (!cfg.WhiteNightAllowedIn(season))
            {
                return false;
            }
            var rng = new System.Random(DaySeed(cfg.weatherSeed, dayNumber, 0x0EE7));
            return rng.NextDouble() < cfg.whiteNightProbability;
        }

        /// <summary>Stable per-day seed mixing the config seed, the day, and a channel salt.</summary>
        static int DaySeed(int baseSeed, int dayNumber, int salt)
        {
            unchecked
            {
                int h = baseSeed;
                h = h * 486187739 + dayNumber;
                h = h * 486187739 + salt;
                return h;
            }
        }
    }
}
