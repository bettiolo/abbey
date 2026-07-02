using System.Collections;
using System.Collections.Generic;
using Abbey.Core;
using Abbey.Light;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Abbey.Tests.PlayMode
{
    public class CoreSimPlayModeTests
    {
        readonly List<GameObject> _spawned = new List<GameObject>();
        readonly List<ScriptableObject> _assets = new List<ScriptableObject>();

        [SetUp]
        public void SetUp()
        {
            EventBus.ResetAll();
            GameEventLog.Clear();
            DarknessEvaluator.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned)
            {
                if (go != null)
                {
                    // Immediate so singletons (GameClock.Instance) are gone before
                    // the next test's SetUp runs.
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
            EventBus.ResetAll();
            GameEventLog.Clear();
        }

        PrototypeConfig CreateConfig()
        {
            var config = ScriptableObject.CreateInstance<PrototypeConfig>();
            _assets.Add(config);
            return config;
        }

        [UnityTest]
        public IEnumerator GameClock_AutoTick_AdvancesWithFrameTime()
        {
            var go = new GameObject("Clock");
            _spawned.Add(go);
            var clock = go.AddComponent<GameClock>();
            clock.autoTick = true;

            float before = clock.TotalTime;
            yield return null;
            yield return null;
            yield return null;

            Assert.Greater(clock.TotalTime, before,
                "autoTick GameClock must advance from Update");
        }

        [UnityTest]
        public IEnumerator GameClock_AutoTick_ReachesDuskWithShortDay()
        {
            var config = CreateConfig();
            config.dayDurationSeconds = 0.05f;
            config.duskDurationSeconds = 100f;
            config.nightDurationSeconds = 100f;
            config.dawnDurationSeconds = 100f;

            var go = new GameObject("Clock");
            _spawned.Add(go);
            var clock = go.AddComponent<GameClock>();
            clock.autoTick = true;
            clock.Configure(config);

            DayPhase? observed = null;
            EventBus.PhaseChanged += phase => observed = phase;

            float deadline = Time.time + 5f;
            while (clock.Phase == DayPhase.Day && Time.time < deadline)
            {
                yield return null;
            }

            Assert.AreEqual(DayPhase.Dusk, clock.Phase);
            Assert.AreEqual(DayPhase.Dusk, observed);
            Assert.IsTrue(GameEventLog.Count > 0, "Phase change must be logged");
        }

        [UnityTest]
        public IEnumerator LightSource_AutoTick_BurnsFuelInPlayMode()
        {
            var go = new GameObject("Campfire");
            _spawned.Add(go);
            var source = go.AddComponent<LightSource>();
            source.radius = 5f;
            source.strength = 1f;
            source.fuelSeconds = 1000f;
            source.fuelConsumptionPerSecond = 1f;
            source.autoTick = true;

            float before = source.fuelSeconds;
            yield return null;
            yield return null;
            yield return null;

            Assert.Less(source.fuelSeconds, before,
                "autoTick LightSource must burn fuel from Update");
            Assert.IsTrue(source.isLit);
        }

        [UnityTest]
        public IEnumerator LightSource_RegistersWithEvaluator_InPlayMode()
        {
            var go = new GameObject("Campfire");
            _spawned.Add(go);
            go.transform.position = Vector3.zero;
            var source = go.AddComponent<LightSource>();
            source.radius = 10f;
            source.strength = 1f;
            source.autoTick = false;

            yield return null;

            Assert.AreEqual(LightZone.Safe, DarknessEvaluator.Classify(Vector3.zero));

            Object.Destroy(go);
            yield return null;

            Assert.AreEqual(LightZone.Dark, DarknessEvaluator.Classify(Vector3.zero));
        }
    }
}
