using UnityEngine;

namespace Abbey.World
{
    /// <summary>
    /// Single ScriptableObject holding ALL Phase 3 seasonal calendar + weather
    /// tunables (AGENTS.md rule: no balance values inside MonoBehaviours). Systems
    /// fetch it via <see cref="LoadOrDefault"/> so tests and CI never need an asset
    /// file to exist. An optional asset at Resources/SeasonConfig overrides the
    /// coded defaults. Mirrors <see cref="Abbey.Core.PrototypeConfig"/> and
    /// <see cref="Abbey.Economy.EconomyConfig"/>.
    ///
    /// Weather and moon multipliers are all "effectiveness" scalars: 1 = neutral,
    /// below 1 = light reaches less far / the bell recalls a shorter distance,
    /// above 1 = the reverse (a full moon lends the lanterns extra reach).
    /// </summary>
    [CreateAssetMenu(fileName = "SeasonConfig", menuName = "Abbey/Season Config")]
    public class SeasonConfig : ScriptableObject
    {
        public const string ResourcePath = "SeasonConfig";

        /// <summary>Per-weather light/bell effectiveness multipliers.</summary>
        [System.Serializable]
        public struct WeatherModifier
        {
            [Tooltip("Global light-effectiveness multiplier (scales every lit radius).")]
            [Min(0f)] public float lightEffectiveness;
            [Tooltip("Bell recall range multiplier (the pulse reaches less far in foul weather).")]
            [Min(0f)] public float bellReliability;
        }

        /// <summary>Weighted weather draw for one season (weights need not sum to 1).</summary>
        [System.Serializable]
        public struct WeatherWeights
        {
            [Min(0f)] public float clear;
            [Min(0f)] public float fog;
            [Min(0f)] public float rain;
            [Min(0f)] public float tempest;
        }

        [Header("Calendar")]
        [Tooltip("Days spent in each season before it turns. A year is four of these.")]
        [Min(1)] public int daysPerSeason = 4;

        [Header("Night-length multiplier per season (scales GameClock's night phase)")]
        [Tooltip("Spring (hope): the baseline short night.")]
        [Min(0.01f)] public float springNightMultiplier = 1f;
        [Tooltip("Summer (growth): the shortest nights of the year.")]
        [Min(0.01f)] public float summerNightMultiplier = 0.75f;
        [Tooltip("Autumn (warning): nights lengthen past spring.")]
        [Min(0.01f)] public float autumnNightMultiplier = 1.25f;
        [Tooltip("Winter (judgment): the longest, most dangerous nights.")]
        [Min(0.01f)] public float winterNightMultiplier = 1.6f;

        [Header("Weather draw weights per season")]
        public WeatherWeights springWeather =
            new WeatherWeights { clear = 6f, fog = 2f, rain = 2f, tempest = 0.5f };
        public WeatherWeights summerWeather =
            new WeatherWeights { clear = 8f, fog = 1f, rain = 1f, tempest = 0.25f };
        public WeatherWeights autumnWeather =
            new WeatherWeights { clear = 4f, fog = 3f, rain = 3f, tempest = 1.5f };
        public WeatherWeights winterWeather =
            new WeatherWeights { clear = 2f, fog = 3f, rain = 2f, tempest = 4f };

        [Header("Per-weather effectiveness modifiers")]
        public WeatherModifier clearModifier =
            new WeatherModifier { lightEffectiveness = 1f, bellReliability = 1f };
        public WeatherModifier fogModifier =
            new WeatherModifier { lightEffectiveness = 0.7f, bellReliability = 0.9f };
        public WeatherModifier rainModifier =
            new WeatherModifier { lightEffectiveness = 0.85f, bellReliability = 0.8f };
        public WeatherModifier tempestModifier =
            new WeatherModifier { lightEffectiveness = 0.5f, bellReliability = 0.5f };

        [Header("Moon phase (8 phases; index by MoonPhase enum)")]
        [Tooltip("Days for one full New -> Full -> New cycle.")]
        [Min(1)] public int moonCycleDays = 8;
        [Tooltip("Light-effectiveness multiplier per moon phase: a full moon lends reach, a new moon steals it. Length 8, indexed by MoonPhase.")]
        public float[] moonLightModifiers =
        {
            0.85f, // New
            0.90f, // WaxingCrescent
            0.97f, // FirstQuarter
            1.05f, // WaxingGibbous
            1.15f, // Full
            1.05f, // WaningGibbous
            0.97f, // LastQuarter
            0.90f, // WaningCrescent
        };

        [Header("White Night omen")]
        [Tooltip("Enable the deterministic White Night omen roll at all.")]
        public bool whiteNightEnabled = true;
        [Tooltip("Per-night probability of a White Night in an allowed season (0..1).")]
        [Range(0f, 1f)] public float whiteNightProbability = 0.2f;
        public bool whiteNightInSpring;
        public bool whiteNightInSummer;
        [Tooltip("Autumn is the season of warning: the omen begins here.")]
        public bool whiteNightInAutumn = true;
        [Tooltip("Winter is judgment: the omen is common.")]
        public bool whiteNightInWinter = true;
        [Tooltip("Light-effectiveness multiplier while the White Night burns (a false, treacherous brightness).")]
        [Min(0f)] public float whiteNightLightEffectiveness = 0.4f;
        [Tooltip("Bell reliability while the White Night burns (the bell rings hollow).")]
        [Min(0f)] public float whiteNightBellReliability = 0.6f;

        [Header("Determinism")]
        [Tooltip("Seed for every deterministic weather/omen draw. Same seed => identical year.")]
        public int weatherSeed = 20250705;

        static SeasonConfig _cached;

        /// <summary>
        /// Returns the config asset from Resources/SeasonConfig if one exists,
        /// otherwise an in-memory instance with the coded defaults. Never returns null.
        /// </summary>
        public static SeasonConfig LoadOrDefault()
        {
            if (_cached != null)
            {
                return _cached;
            }

            _cached = Resources.Load<SeasonConfig>(ResourcePath);
            if (_cached == null)
            {
                _cached = CreateInstance<SeasonConfig>();
                _cached.name = "SeasonConfig (defaults)";
                _cached.hideFlags = HideFlags.HideAndDontSave;
            }
            return _cached;
        }

        /// <summary>Drops the cached instance (test isolation).</summary>
        public static void ClearCache()
        {
            _cached = null;
        }

        /// <summary>Night-length multiplier for a season, read from the per-season fields.</summary>
        public float NightMultiplierFor(Season season)
        {
            switch (season)
            {
                case Season.Spring: return springNightMultiplier;
                case Season.Summer: return summerNightMultiplier;
                case Season.Autumn: return autumnNightMultiplier;
                case Season.Winter: return winterNightMultiplier;
                default: return 1f;
            }
        }

        /// <summary>Weather draw weights for a season.</summary>
        public WeatherWeights WeightsFor(Season season)
        {
            switch (season)
            {
                case Season.Spring: return springWeather;
                case Season.Summer: return summerWeather;
                case Season.Autumn: return autumnWeather;
                case Season.Winter: return winterWeather;
                default: return springWeather;
            }
        }

        /// <summary>Light/bell modifier for a weather state.</summary>
        public WeatherModifier ModifierFor(Weather weather)
        {
            switch (weather)
            {
                case Weather.Clear: return clearModifier;
                case Weather.Fog: return fogModifier;
                case Weather.Rain: return rainModifier;
                case Weather.Tempest: return tempestModifier;
                default: return clearModifier;
            }
        }

        /// <summary>Light-effectiveness multiplier for a moon phase (safe-indexed).</summary>
        public float MoonLightMultiplier(MoonPhase phase)
        {
            if (moonLightModifiers == null || moonLightModifiers.Length == 0)
            {
                return 1f;
            }
            int i = Mathf.Clamp((int)phase, 0, moonLightModifiers.Length - 1);
            return moonLightModifiers[i];
        }

        /// <summary>Whether a White Night omen may appear in the given season.</summary>
        public bool WhiteNightAllowedIn(Season season)
        {
            if (!whiteNightEnabled)
            {
                return false;
            }
            switch (season)
            {
                case Season.Spring: return whiteNightInSpring;
                case Season.Summer: return whiteNightInSummer;
                case Season.Autumn: return whiteNightInAutumn;
                case Season.Winter: return whiteNightInWinter;
                default: return false;
            }
        }
    }
}
