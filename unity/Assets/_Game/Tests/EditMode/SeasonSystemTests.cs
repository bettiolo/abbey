using System.Collections.Generic;
using Abbey.Core;
using Abbey.Hero;
using Abbey.Light;
using Abbey.World;
using NUnit.Framework;
using UnityEngine;

namespace Abbey.Tests.EditMode
{
    /// <summary>
    /// EditMode coverage for the seasonal calendar + weather (P3-01). Every world is
    /// built programmatically (no scene file). Covers: season advance after the
    /// configured days, night-length scaling toward Winter, the weather/moon light
    /// multiplier changing DarknessEvaluator classification, bell reliability under a
    /// tempest, and same-seed / different-seed weather-schedule reproducibility.
    /// </summary>
    public class SeasonSystemTests
    {
        readonly List<GameObject> _spawned = new List<GameObject>();
        readonly List<ScriptableObject> _assets = new List<ScriptableObject>();

        [SetUp]
        public void SetUp()
        {
            EventBus.ResetAll();
            GameEventLog.Clear();
            DarknessEvaluator.Clear();
            SeasonConfig.ClearCache();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }
            _spawned.Clear();
            foreach (var asset in _assets)
            {
                if (asset != null)
                {
                    Object.DestroyImmediate(asset);
                }
            }
            _assets.Clear();
            DarknessEvaluator.Clear();
            SeasonConfig.ClearCache();
            EventBus.ResetAll();
            GameEventLog.Clear();
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        PrototypeConfig CreateProtoConfig()
        {
            var config = ScriptableObject.CreateInstance<PrototypeConfig>();
            config.dayDurationSeconds = 1f;
            config.duskDurationSeconds = 1f;
            config.nightDurationSeconds = 1f;
            config.dawnDurationSeconds = 1f;
            config.bellRadius = 15f;
            config.bellCooldownSeconds = 0f;
            _assets.Add(config);
            return config;
        }

        SeasonConfig CreateSeasonConfig()
        {
            var cfg = ScriptableObject.CreateInstance<SeasonConfig>();
            _assets.Add(cfg);
            return cfg;
        }

        GameClock CreateClock(PrototypeConfig config)
        {
            var go = new GameObject("TestGameClock");
            _spawned.Add(go);
            var clock = go.AddComponent<GameClock>();
            clock.autoTick = false;
            clock.Configure(config);
            return clock;
        }

        SeasonSystem CreateSeason(SeasonConfig cfg)
        {
            var go = new GameObject("TestSeasonSystem");
            _spawned.Add(go);
            var season = go.AddComponent<SeasonSystem>();
            season.Configure(cfg);
            return season;
        }

        WeatherSystem CreateWeather(SeasonConfig cfg, BellkeeperController keeper = null)
        {
            var go = new GameObject("TestWeatherSystem");
            _spawned.Add(go);
            var weather = go.AddComponent<WeatherSystem>();
            weather.bellkeeper = keeper;
            weather.Configure(cfg);
            return weather;
        }

        BellkeeperController CreateBellkeeper(PrototypeConfig config)
        {
            var go = new GameObject("TestBellkeeper");
            _spawned.Add(go);
            var keeper = go.AddComponent<BellkeeperController>();
            keeper.autoTick = false;
            keeper.Configure(config);
            return keeper;
        }

        static void AdvanceToDay(GameClock clock, int targetDay)
        {
            int safety = 10000;
            while (clock.DayNumber < targetDay && safety-- > 0)
            {
                clock.Tick(0.5f);
            }
        }

        // ------------------------------------------------------------------
        // Calendar
        // ------------------------------------------------------------------

        [Test]
        public void SeasonForDay_MapsDaysToSeasons_AndWraps()
        {
            Assert.AreEqual(Season.Spring, SeasonSystem.SeasonForDay(1, 2));
            Assert.AreEqual(Season.Spring, SeasonSystem.SeasonForDay(2, 2));
            Assert.AreEqual(Season.Summer, SeasonSystem.SeasonForDay(3, 2));
            Assert.AreEqual(Season.Autumn, SeasonSystem.SeasonForDay(5, 2));
            Assert.AreEqual(Season.Winter, SeasonSystem.SeasonForDay(7, 2));
            Assert.AreEqual(Season.Spring, SeasonSystem.SeasonForDay(9, 2)); // year wraps
            Assert.AreEqual(3, SeasonSystem.DayOfYearForDay(3, 2)); // 2/season => year of 8
            Assert.AreEqual(1, SeasonSystem.DayOfYearForDay(9, 2));
        }

        [Test]
        public void Season_Advances_AfterConfiguredDays_AndRaisesEvent()
        {
            var proto = CreateProtoConfig();
            var clock = CreateClock(proto);
            var seasonCfg = CreateSeasonConfig();
            seasonCfg.daysPerSeason = 1; // one day per season for a fast test
            var season = CreateSeason(seasonCfg);

            Assert.AreEqual(Season.Spring, season.CurrentSeason);

            var seen = new List<Season>();
            EventBus.SeasonChanged += s => seen.Add(s);

            AdvanceToDay(clock, 2);
            Assert.AreEqual(Season.Summer, season.CurrentSeason);
            Assert.AreEqual(2, season.DayOfYear);

            AdvanceToDay(clock, 4);
            Assert.AreEqual(Season.Winter, season.CurrentSeason);

            CollectionAssert.Contains(seen, Season.Summer);
            CollectionAssert.Contains(seen, Season.Autumn);
            CollectionAssert.Contains(seen, Season.Winter);
        }

        // ------------------------------------------------------------------
        // Night-length scaling
        // ------------------------------------------------------------------

        [Test]
        public void NightMultipliers_LengthenTowardWinter()
        {
            var cfg = CreateSeasonConfig();
            Assert.Greater(cfg.NightMultiplierFor(Season.Winter),
                cfg.NightMultiplierFor(Season.Autumn), "winter nights longest");
            Assert.Greater(cfg.NightMultiplierFor(Season.Autumn),
                cfg.NightMultiplierFor(Season.Spring), "autumn longer than spring");
            Assert.Less(cfg.NightMultiplierFor(Season.Summer),
                cfg.NightMultiplierFor(Season.Spring), "summer nights shortest");
        }

        [Test]
        public void GameClock_NightDuration_ScalesWithMultiplier()
        {
            var proto = CreateProtoConfig();
            var clock = CreateClock(proto);
            float baseNight = clock.GetPhaseDuration(DayPhase.Night);

            clock.NightLengthMultiplier = 2f;
            Assert.AreEqual(baseNight * 2f, clock.GetPhaseDuration(DayPhase.Night), 1e-4f);

            // Day/Dusk/Dawn are never scaled.
            Assert.AreEqual(proto.dayDurationSeconds, clock.GetPhaseDuration(DayPhase.Day), 1e-4f);
        }

        [Test]
        public void SeasonSystem_AdvancingToWinter_GrowsClockNightDuration()
        {
            var proto = CreateProtoConfig();
            var clock = CreateClock(proto);
            var seasonCfg = CreateSeasonConfig();
            seasonCfg.daysPerSeason = 1;
            CreateSeason(seasonCfg);

            float springNight = clock.GetPhaseDuration(DayPhase.Night);

            AdvanceToDay(clock, 4); // Winter
            float winterNight = clock.GetPhaseDuration(DayPhase.Night);

            Assert.Greater(winterNight, springNight,
                "the night phase must physically lengthen in Winter");
            Assert.AreEqual(proto.nightDurationSeconds * seasonCfg.winterNightMultiplier,
                winterNight, 1e-4f);
        }

        // ------------------------------------------------------------------
        // Weather / moon light multiplier
        // ------------------------------------------------------------------

        [Test]
        public void LightMultiplier_ShrinksSafeZoneToEdge()
        {
            // A pure DarknessEvaluator check: the global multiplier scales reach.
            var proto = ScriptableObject.CreateInstance<PrototypeConfig>();
            proto.edgeBandFraction = 0.3f;
            _assets.Add(proto);
            DarknessEvaluator.Config = proto;

            var lightGO = new GameObject("Campfire");
            _spawned.Add(lightGO);
            lightGO.transform.position = Vector3.zero;
            var light = lightGO.AddComponent<LightSource>();
            light.radius = 10f;
            light.strength = 1f;
            light.autoTick = false;

            var probe = new Vector3(4.5f, 0f, 0f); // inside safe (7) at full strength

            DarknessEvaluator.LightEffectivenessMultiplier = 1f;
            Assert.AreEqual(LightZone.Safe, DarknessEvaluator.Classify(probe));

            DarknessEvaluator.LightEffectivenessMultiplier = 0.5f; // tempest-like
            // effective 5, inner safe 3.5 => 4.5 lands in the Edge band
            Assert.AreEqual(LightZone.Edge, DarknessEvaluator.Classify(probe));
        }

        [Test]
        public void WeatherSystem_ForcedTempest_LowersLightEffectiveness()
        {
            var proto = CreateProtoConfig();
            CreateClock(proto);

            var cfg = CreateSeasonConfig();
            cfg.daysPerSeason = 4;
            ForceWeather(cfg, tempestOnly: true);
            NeutraliseMoon(cfg);
            cfg.whiteNightEnabled = false;

            var weather = CreateWeather(cfg);

            Assert.AreEqual(Weather.Tempest, weather.CurrentWeather);
            Assert.AreEqual(cfg.tempestModifier.lightEffectiveness,
                weather.LightEffectivenessMultiplier, 1e-4f);
            Assert.AreEqual(weather.LightEffectivenessMultiplier,
                DarknessEvaluator.LightEffectivenessMultiplier, 1e-4f,
                "weather must publish the multiplier to DarknessEvaluator");
            Assert.Less(weather.LightEffectivenessMultiplier, 1f);
        }

        [Test]
        public void WhiteNight_CompoundsLightAndBellPenalty()
        {
            var proto = CreateProtoConfig();
            CreateClock(proto);

            var cfg = CreateSeasonConfig();
            cfg.daysPerSeason = 4;
            ForceWeather(cfg, tempestOnly: false); // clear only
            NeutraliseMoon(cfg);
            // Force a White Night on day 1 with certainty in every season.
            cfg.whiteNightEnabled = true;
            cfg.whiteNightProbability = 1f;
            cfg.whiteNightInSpring = true;

            var weather = CreateWeather(cfg);

            Assert.IsTrue(weather.IsWhiteNight);
            Assert.AreEqual(cfg.whiteNightLightEffectiveness,
                weather.LightEffectivenessMultiplier, 1e-4f);
            Assert.AreEqual(cfg.whiteNightBellReliability,
                weather.BellReliabilityMultiplier, 1e-4f);
        }

        // ------------------------------------------------------------------
        // Bell reliability
        // ------------------------------------------------------------------

        [Test]
        public void Tempest_ShrinksBellRecallRadius_VersusClear()
        {
            var proto = CreateProtoConfig();
            CreateClock(proto);
            var keeper = CreateBellkeeper(proto);

            float clearRadius = 0f;
            float tempestRadius = 0f;
            EventBus.BellRang += (pos, radius) =>
            {
                if (tempestRadius <= 0f && keeper.BellReliabilityMultiplier < 1f)
                {
                    tempestRadius = radius;
                }
                else
                {
                    clearRadius = radius;
                }
            };

            // Clear weather: full-strength pulse.
            var clearCfg = CreateSeasonConfig();
            ForceWeather(clearCfg, tempestOnly: false);
            NeutraliseMoon(clearCfg);
            clearCfg.whiteNightEnabled = false;
            var weather = CreateWeather(clearCfg, keeper);
            Assert.AreEqual(1f, keeper.BellReliabilityMultiplier, 1e-4f);

            // Tempest weather: the pulse must reach less far.
            var tempestCfg = CreateSeasonConfig();
            ForceWeather(tempestCfg, tempestOnly: true);
            NeutraliseMoon(tempestCfg);
            tempestCfg.whiteNightEnabled = false;
            weather.Configure(tempestCfg);
            Assert.Less(keeper.BellReliabilityMultiplier, 1f);

            Assert.IsTrue(keeper.RingBell());
            weather.Configure(clearCfg); // back to clear
            Assert.IsTrue(keeper.RingBell());

            Assert.Greater(tempestRadius, 0f);
            Assert.Greater(clearRadius, 0f);
            Assert.Less(tempestRadius, clearRadius,
                "a tempest must shrink the bell recall radius");
            Assert.AreEqual(proto.bellRadius, clearRadius, 1e-3f);
        }

        // ------------------------------------------------------------------
        // Deterministic schedule reproducibility
        // ------------------------------------------------------------------

        [Test]
        public void SameSeed_ProducesIdenticalYearSchedule()
        {
            var a = CreateSeasonConfig();
            var b = CreateSeasonConfig();
            a.weatherSeed = b.weatherSeed = 777;

            int days = a.daysPerSeason * 4;
            for (int day = 1; day <= days; day++)
            {
                Assert.AreEqual(WeatherSystem.WeatherForDay(day, a),
                    WeatherSystem.WeatherForDay(day, b), $"weather differs on day {day}");
                Assert.AreEqual(WeatherSystem.IsWhiteNightForDay(day, a),
                    WeatherSystem.IsWhiteNightForDay(day, b), $"omen differs on day {day}");
                Assert.AreEqual(WeatherSystem.MoonPhaseForDay(day, a.moonCycleDays),
                    WeatherSystem.MoonPhaseForDay(day, b.moonCycleDays));
            }
        }

        [Test]
        public void DifferentSeed_ProducesDifferentSchedule()
        {
            var a = CreateSeasonConfig();
            var b = CreateSeasonConfig();
            a.weatherSeed = 111;
            b.weatherSeed = 999;

            int days = a.daysPerSeason * 4;
            bool anyDifference = false;
            for (int day = 1; day <= days && !anyDifference; day++)
            {
                if (WeatherSystem.WeatherForDay(day, a) != WeatherSystem.WeatherForDay(day, b))
                {
                    anyDifference = true;
                }
            }
            Assert.IsTrue(anyDifference,
                "two different seeds must not produce an identical year of weather");
        }

        [Test]
        public void MoonPhase_CyclesThroughAllEightPhases()
        {
            var seen = new HashSet<MoonPhase>();
            for (int day = 1; day <= 8; day++)
            {
                seen.Add(WeatherSystem.MoonPhaseForDay(day, 8));
            }
            Assert.AreEqual(8, seen.Count, "an 8-day cycle must visit every moon phase");
        }

        // ------------------------------------------------------------------
        // Local config shaping helpers
        // ------------------------------------------------------------------

        static void ForceWeather(SeasonConfig cfg, bool tempestOnly)
        {
            var w = tempestOnly
                ? new SeasonConfig.WeatherWeights { clear = 0f, fog = 0f, rain = 0f, tempest = 1f }
                : new SeasonConfig.WeatherWeights { clear = 1f, fog = 0f, rain = 0f, tempest = 0f };
            cfg.springWeather = w;
            cfg.summerWeather = w;
            cfg.autumnWeather = w;
            cfg.winterWeather = w;
        }

        static void NeutraliseMoon(SeasonConfig cfg)
        {
            cfg.moonLightModifiers = new[] { 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f };
        }
    }
}
