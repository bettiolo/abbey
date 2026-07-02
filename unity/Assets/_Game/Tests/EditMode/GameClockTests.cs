using System.Collections.Generic;
using Abbey.Core;
using NUnit.Framework;
using UnityEngine;

namespace Abbey.Tests.EditMode
{
    public class GameClockTests
    {
        GameObject _clockGO;
        GameClock _clock;
        PrototypeConfig _config;

        [SetUp]
        public void SetUp()
        {
            EventBus.ResetAll();
            GameEventLog.Clear();

            _config = ScriptableObject.CreateInstance<PrototypeConfig>();
            _config.dayDurationSeconds = 10f;
            _config.duskDurationSeconds = 5f;
            _config.nightDurationSeconds = 10f;
            _config.dawnDurationSeconds = 5f;

            _clockGO = new GameObject("TestGameClock");
            _clock = _clockGO.AddComponent<GameClock>();
            _clock.autoTick = false;
            _clock.Configure(_config);
        }

        [TearDown]
        public void TearDown()
        {
            if (_clockGO != null)
            {
                Object.DestroyImmediate(_clockGO);
            }
            Object.DestroyImmediate(_config);
            EventBus.ResetAll();
            GameEventLog.Clear();
        }

        [Test]
        public void StartsAtDayOnePhaseDay()
        {
            Assert.AreEqual(DayPhase.Day, _clock.Phase);
            Assert.AreEqual(1, _clock.DayNumber);
            Assert.AreEqual(0f, _clock.PhaseProgress);
        }

        [Test]
        public void PhaseProgress_IsNormalized()
        {
            _clock.Tick(5f); // halfway through a 10s day
            Assert.AreEqual(DayPhase.Day, _clock.Phase);
            Assert.AreEqual(0.5f, _clock.PhaseProgress, 1e-4f);
        }

        [Test]
        public void TicksThroughFullCycleInOrder()
        {
            var seen = new List<DayPhase>();
            EventBus.PhaseChanged += phase => seen.Add(phase);

            _clock.Tick(10f); // -> Dusk
            _clock.Tick(5f);  // -> Night
            _clock.Tick(10f); // -> Dawn
            _clock.Tick(5f);  // -> Day (day 2)

            CollectionAssert.AreEqual(
                new[] { DayPhase.Dusk, DayPhase.Night, DayPhase.Dawn, DayPhase.Day },
                seen);
            Assert.AreEqual(DayPhase.Day, _clock.Phase);
            Assert.AreEqual(2, _clock.DayNumber);
            Assert.AreEqual(30f, _clock.TotalTime, 1e-4f);
        }

        [Test]
        public void LargeTick_CrossesMultipleBoundaries_RaisingEachEvent()
        {
            var seen = new List<DayPhase>();
            EventBus.PhaseChanged += phase => seen.Add(phase);

            _clock.Tick(26f); // 10 day + 5 dusk + 10 night = 25, leaves 1s into Dawn

            CollectionAssert.AreEqual(
                new[] { DayPhase.Dusk, DayPhase.Night, DayPhase.Dawn },
                seen);
            Assert.AreEqual(DayPhase.Dawn, _clock.Phase);
            Assert.AreEqual(1f, _clock.TimeInPhase, 1e-4f);
        }

        [Test]
        public void PhaseChanges_AreRecordedInGameEventLog()
        {
            _clock.Tick(15f); // Day -> Dusk -> Night

            Assert.AreEqual(2, GameEventLog.Count);
            Assert.AreEqual("PhaseChanged", GameEventLog.Records[0].Type);
            Assert.AreEqual("Dusk", GameEventLog.Records[0].Data);
            Assert.AreEqual("Night", GameEventLog.Records[1].Data);
        }

        [Test]
        public void ZeroOrNegativeTick_DoesNothing()
        {
            _clock.Tick(0f);
            _clock.Tick(-5f);
            Assert.AreEqual(0f, _clock.TotalTime);
            Assert.AreEqual(DayPhase.Day, _clock.Phase);
        }

        [Test]
        public void SingletonInstance_IsSet_AndClearedOnDestroy()
        {
            Assert.AreSame(_clock, GameClock.Instance);

            Object.DestroyImmediate(_clockGO);
            _clockGO = null;

            Assert.IsNull(GameClock.Instance);
        }
    }
}
