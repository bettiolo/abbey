using System.Collections;
using System.Collections.Generic;
using Abbey.Core;
using Abbey.Hero;
using Abbey.Light;
using Abbey.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Abbey.Tests.PlayMode
{
    /// <summary>
    /// PlayMode coverage for the seasonal calendar + weather (P3-01). Worlds are
    /// built programmatically. Under a real auto-ticking clock with a fast time
    /// scale it asserts the year turns (SeasonChanged fires), the night phase
    /// duration physically grows toward Winter, and a forced tempest shrinks the
    /// bell recall range compared with clear weather.
    /// </summary>
    public class SeasonWeatherPlayModeTests
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

        PrototypeConfig CreateProtoConfig()
        {
            var config = ScriptableObject.CreateInstance<PrototypeConfig>();
            _assets.Add(config);
            return config;
        }

        SeasonConfig CreateSeasonConfig()
        {
            var cfg = ScriptableObject.CreateInstance<SeasonConfig>();
            _assets.Add(cfg);
            return cfg;
        }

        [UnityTest]
        public IEnumerator SeasonAdvances_AndNightGrows_UnderAutoTick()
        {
            var proto = CreateProtoConfig();
            proto.dayDurationSeconds = 0.02f;
            proto.duskDurationSeconds = 0.02f;
            proto.nightDurationSeconds = 0.02f;
            proto.dawnDurationSeconds = 0.02f;

            var clockGO = new GameObject("Clock");
            _spawned.Add(clockGO);
            var clock = clockGO.AddComponent<GameClock>();
            clock.autoTick = true;
            clock.Configure(proto);

            var seasonCfg = CreateSeasonConfig();
            seasonCfg.daysPerSeason = 1; // day1 Spring, day2 Summer, ... day4 Winter
            var worldGO = new GameObject("WorldSystems");
            _spawned.Add(worldGO);
            var season = worldGO.AddComponent<SeasonSystem>();
            season.Configure(seasonCfg);

            float springNight = clock.GetPhaseDuration(DayPhase.Night);

            var seenSeasons = new List<Season>();
            EventBus.SeasonChanged += s => seenSeasons.Add(s);

            // Run until Winter (day 4) or a generous frame deadline.
            int guard = 100000;
            while (clock.DayNumber < 4 && guard-- > 0)
            {
                yield return null;
            }

            Assert.AreEqual(Season.Winter, season.CurrentSeason,
                "the year must turn to Winter under the auto-ticking clock");
            CollectionAssert.Contains(seenSeasons, Season.Winter,
                "SeasonChanged must fire as the season turns");

            float winterNight = clock.GetPhaseDuration(DayPhase.Night);
            Assert.Greater(winterNight, springNight,
                "the night phase duration must grow toward Winter");
        }

        [UnityTest]
        public IEnumerator Tempest_ShrinksBellRecallRange_VersusClear()
        {
            var proto = CreateProtoConfig();
            proto.bellRadius = 15f;
            proto.bellCooldownSeconds = 0f; // ring repeatedly for the comparison

            var clockGO = new GameObject("Clock");
            _spawned.Add(clockGO);
            var clock = clockGO.AddComponent<GameClock>();
            clock.autoTick = false;
            clock.Configure(proto);

            var heroGO = new GameObject("Bellkeeper");
            _spawned.Add(heroGO);
            var keeper = heroGO.AddComponent<BellkeeperController>();
            keeper.autoTick = false;
            keeper.Configure(proto);

            var clearCfg = CreateSeasonConfig();
            SetWeather(clearCfg, tempestOnly: false);
            NeutraliseMoon(clearCfg);
            clearCfg.whiteNightEnabled = false;

            var tempestCfg = CreateSeasonConfig();
            SetWeather(tempestCfg, tempestOnly: true);
            NeutraliseMoon(tempestCfg);
            tempestCfg.whiteNightEnabled = false;

            var worldGO = new GameObject("WorldSystems");
            _spawned.Add(worldGO);
            var weather = worldGO.AddComponent<WeatherSystem>();
            weather.bellkeeper = keeper;
            weather.Configure(clearCfg);

            yield return null; // let a frame pass so play-mode wiring settles

            float clearRadius = -1f;
            float tempestRadius = -1f;
            EventBus.BellRang += (pos, radius) =>
            {
                if (keeper.BellReliabilityMultiplier < 1f)
                {
                    tempestRadius = radius;
                }
                else
                {
                    clearRadius = radius;
                }
            };

            // Clear weather ring.
            Assert.AreEqual(1f, keeper.BellReliabilityMultiplier, 1e-4f);
            Assert.IsTrue(keeper.RingBell());

            // Switch to a tempest and ring again.
            weather.Configure(tempestCfg);
            Assert.Less(keeper.BellReliabilityMultiplier, 1f);
            Assert.IsTrue(keeper.RingBell());

            Assert.Greater(clearRadius, 0f);
            Assert.Greater(tempestRadius, 0f);
            Assert.Less(tempestRadius, clearRadius,
                "the tempest recall pulse must reach less far than the clear one");
            Assert.AreEqual(proto.bellRadius, clearRadius, 1e-3f);
        }

        static void SetWeather(SeasonConfig cfg, bool tempestOnly)
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
