using Abbey.Core;
using NUnit.Framework;
using UnityEngine;

namespace Abbey.Tests.EditMode
{
    public class EventBusTests
    {
        [SetUp]
        public void SetUp()
        {
            EventBus.ResetAll();
            GameEventLog.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.ResetAll();
            GameEventLog.Clear();
        }

        [Test]
        public void BellRang_DeliversPositionAndRadius()
        {
            Vector3 receivedPos = Vector3.zero;
            float receivedRadius = 0f;
            EventBus.BellRang += (pos, radius) =>
            {
                receivedPos = pos;
                receivedRadius = radius;
            };

            EventBus.RaiseBellRang(new Vector3(1f, 2f, 3f), 15f);

            Assert.AreEqual(new Vector3(1f, 2f, 3f), receivedPos);
            Assert.AreEqual(15f, receivedRadius);
        }

        [Test]
        public void EveryRaise_AppendsToGameEventLog()
        {
            EventBus.RaisePhaseChanged(DayPhase.Dusk);
            EventBus.RaiseBellRang(Vector3.zero, 10f);
            EventBus.RaiseHoundFed(0.3f);
            EventBus.RaiseVillagerEndangered(null);
            EventBus.RaiseVillagerRescued(null);
            EventBus.RaiseMonsterSpawned(null);

            Assert.AreEqual(6, GameEventLog.Count);
            Assert.AreEqual("PhaseChanged", GameEventLog.Records[0].Type);
            Assert.AreEqual("BellRang", GameEventLog.Records[1].Type);
            Assert.AreEqual("HoundFed", GameEventLog.Records[2].Type);
        }

        [Test]
        public void ResetAll_RemovesSubscribers()
        {
            int calls = 0;
            EventBus.HoundFed += _ => calls++;

            EventBus.ResetAll();
            EventBus.RaiseHoundFed(0.5f);

            Assert.AreEqual(0, calls);
        }

        [Test]
        public void RaisingWithNoSubscribers_DoesNotThrow()
        {
            Assert.DoesNotThrow(() =>
            {
                EventBus.RaisePhaseChanged(DayPhase.Night);
                EventBus.RaiseBellRang(Vector3.one, 5f);
                EventBus.RaiseVillagerEndangered(null);
                EventBus.RaiseVillagerRescued(null);
                EventBus.RaiseHoundFed(1f);
                EventBus.RaiseMonsterSpawned(null);
            });
        }

        [Test]
        public void GameEventLog_IsAppendOnly_InOrder()
        {
            GameEventLog.Append(1f, "A", "first");
            GameEventLog.Append(2f, "B", "second");

            Assert.AreEqual(2, GameEventLog.Count);
            Assert.AreEqual("first", GameEventLog.Records[0].Data);
            Assert.AreEqual("second", GameEventLog.Records[1].Data);
            Assert.AreEqual(1f, GameEventLog.Records[0].Time);

            GameEventLog.Clear();
            Assert.AreEqual(0, GameEventLog.Count);
        }
    }
}
