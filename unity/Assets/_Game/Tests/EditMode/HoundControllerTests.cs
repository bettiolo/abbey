using System.Collections.Generic;
using Abbey.Beast;
using Abbey.Core;
using Abbey.Light;
using Abbey.Nightmares;
using NUnit.Framework;
using UnityEngine;

namespace Abbey.Tests.EditMode
{
    /// <summary>
    /// Black Hound bond math and state transitions: feeding lowers hunger and
    /// raises trust, thresholds gate Chained -> Wary -> Fed -> Following, a fed
    /// hound answers the bell, and a starving or low-trust hound ignores it.
    /// </summary>
    public class HoundControllerTests
    {
        readonly List<GameObject> _spawned = new List<GameObject>();
        PrototypeConfig _config;

        [SetUp]
        public void SetUp()
        {
            EventBus.ResetAll();
            GameEventLog.Clear();
            DarknessEvaluator.Clear();
            MonsterController.ClearRegistry();

            _config = ScriptableObject.CreateInstance<PrototypeConfig>();
            _config.houndStartTrust = 0.2f;
            _config.houndStartHunger = 0.9f;
            _config.houndStartPain = 0.6f;
            _config.houndStartFear = 0.5f;
            _config.houndStartAttachment = 0f;
            _config.feedTrustGain = 0.2f;
            _config.feedHungerRelief = 0.3f;
            _config.feedFearRelief = 0.1f;
            _config.feedAttachmentGain = 0.05f;
            _config.trustFedThreshold = 0.5f;
            _config.trustFollowThreshold = 0.9f;
            _config.hungerStarvingThreshold = 0.8f;
            _config.houndHungerPerSecond = 0.01f;
            _config.houndMoveSpeed = 5f;
            _config.houndEngageRange = 12f;
            _config.houndAttackRange = 1.5f;
            _config.houndAttackDamage = 60f;
            _config.houndAttackCooldownSeconds = 0.5f;
            _config.arrivalRadius = 0.3f;
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
            Object.DestroyImmediate(_config);
            DarknessEvaluator.Clear();
            MonsterController.ClearRegistry();
            EventBus.ResetAll();
            GameEventLog.Clear();
        }

        HoundController SpawnHound(Vector3 position)
        {
            var go = new GameObject("TestHound");
            _spawned.Add(go);
            go.transform.position = position;
            var hound = go.AddComponent<HoundController>();
            hound.autoTick = false;
            hound.Configure(_config);
            return hound;
        }

        static int CountLog(string type)
        {
            int count = 0;
            var records = GameEventLog.Records;
            for (int i = 0; i < records.Count; i++)
            {
                if (records[i].Type == type)
                {
                    count++;
                }
            }
            return count;
        }

        [Test]
        public void StartValues_ComeFromConfig_StateChained()
        {
            var hound = SpawnHound(Vector3.zero);

            Assert.AreEqual(HoundState.Chained, hound.State);
            Assert.AreEqual(_config.houndStartTrust, hound.Trust, 1e-5f);
            Assert.AreEqual(_config.houndStartHunger, hound.Hunger, 1e-5f);
            Assert.AreEqual(_config.houndStartPain, hound.Pain, 1e-5f);
            Assert.AreEqual(_config.houndStartFear, hound.Fear, 1e-5f);
            Assert.AreEqual(_config.houndStartAttachment, hound.Attachment, 1e-5f);
            Assert.IsTrue(hound.IsStarving, "the hound starts starving (0.9 >= 0.8)");
        }

        [Test]
        public void Feed_LowersHunger_RaisesTrust_AndRaisesHoundFedEvent()
        {
            var hound = SpawnHound(Vector3.zero);
            float eventTrust = -1f;
            EventBus.HoundFed += trust => eventTrust = trust;

            hound.Feed();

            Assert.AreEqual(0.4f, hound.Trust, 1e-5f);       // 0.2 + 0.2
            Assert.AreEqual(0.6f, hound.Hunger, 1e-5f);      // 0.9 - 0.3
            Assert.AreEqual(0.4f, hound.Fear, 1e-5f);        // 0.5 - 0.1
            Assert.AreEqual(0.05f, hound.Attachment, 1e-5f); // 0 + 0.05
            Assert.AreEqual(hound.Trust, eventTrust, 1e-5f, "HoundFed carries the new trust");
            Assert.AreEqual(HoundState.Wary, hound.State, "first feed unchains wariness, not devotion");
            Assert.IsFalse(hound.IsStarving);
            Assert.AreEqual(1, CountLog("hound_fed"));
        }

        [Test]
        public void Feed_CrossesThresholds_WaryToFedToFollowing()
        {
            var hound = SpawnHound(Vector3.zero);

            hound.Feed(); // trust 0.4
            Assert.AreEqual(HoundState.Wary, hound.State);

            hound.Feed(); // trust 0.6 >= fed threshold 0.5
            Assert.AreEqual(HoundState.Fed, hound.State);

            hound.Feed(); // trust 0.8
            Assert.AreEqual(HoundState.Fed, hound.State);

            hound.Feed(); // trust 1.0 >= follow threshold 0.9
            Assert.AreEqual(HoundState.Following, hound.State);
            Assert.AreEqual(1f, hound.Trust, 1e-5f, "trust clamps at 1");
            Assert.AreEqual(0f, hound.Hunger, 1e-5f, "hunger clamps at 0");
        }

        [Test]
        public void Values_ClampToUnitRange()
        {
            var hound = SpawnHound(Vector3.zero);
            hound.Trust = 5f;
            hound.Hunger = -2f;
            hound.Pain = 1.5f;
            Assert.AreEqual(1f, hound.Trust);
            Assert.AreEqual(0f, hound.Hunger);
            Assert.AreEqual(1f, hound.Pain);
        }

        [Test]
        public void Hunger_AccumulatesWithTime()
        {
            var hound = SpawnHound(Vector3.zero);
            hound.Hunger = 0.2f;

            hound.Tick(10f); // 0.01/s

            Assert.AreEqual(0.3f, hound.Hunger, 1e-4f);
        }

        [Test]
        public void FedHound_AnswersBell_AndWalksToIt()
        {
            var hound = SpawnHound(Vector3.zero);
            hound.Feed();
            hound.Feed(); // Fed, hunger 0.3, not starving
            Assert.AreEqual(HoundState.Fed, hound.State);

            EventBus.RaiseBellRang(new Vector3(5f, 0f, 0f), 10f);

            Assert.IsTrue(hound.HasBellTarget);
            Assert.AreEqual(HoundState.Following, hound.State, "answering the bell means following");
            Assert.AreEqual(1, CountLog("hound_answered_bell"));

            for (int i = 0; i < 50 && hound.HasBellTarget; i++)
            {
                hound.Tick(0.1f);
            }
            Assert.IsFalse(hound.HasBellTarget, "the hound must reach the bell position");
            Assert.LessOrEqual(
                PlanarMotion.Distance(hound.transform.position, new Vector3(5f, 0f, 0f)),
                _config.arrivalRadius + 1e-3f);
            Assert.AreEqual(1, CountLog("hound_reached_bell"));
        }

        [Test]
        public void StarvingHound_IgnoresBell_EvenWhenFedState()
        {
            var hound = SpawnHound(Vector3.zero);
            hound.Feed();
            hound.Feed(); // state Fed
            hound.Hunger = 0.95f; // but starving again

            EventBus.RaiseBellRang(new Vector3(5f, 0f, 0f), 10f);

            Assert.IsFalse(hound.HasBellTarget);
            Assert.AreEqual(1, CountLog("hound_ignored_bell"));
            Assert.AreEqual(0, CountLog("hound_answered_bell"));
        }

        [Test]
        public void ChainedLowTrustHound_IgnoresBell_AndDoesNotMove()
        {
            var hound = SpawnHound(new Vector3(3f, 0f, 0f));

            EventBus.RaiseBellRang(Vector3.zero, 10f);
            for (int i = 0; i < 50; i++)
            {
                hound.Tick(0.1f);
            }

            Assert.IsFalse(hound.HasBellTarget);
            Assert.AreEqual(HoundState.Chained, hound.State);
            Assert.AreEqual(0f,
                PlanarMotion.Distance(hound.transform.position, new Vector3(3f, 0f, 0f)),
                1e-5f, "a chained hound does not leave the tower");
            Assert.AreEqual(1, CountLog("hound_ignored_bell"));
        }

        [Test]
        public void Feed_NeverDemotes_AFollowingHound()
        {
            var hound = SpawnHound(Vector3.zero);
            hound.Feed();
            hound.Feed(); // Fed at trust 0.6
            EventBus.RaiseBellRang(Vector3.one, 10f); // promoted to Following by the bell

            hound.Feed(); // trust 0.8, still below follow threshold 0.9

            Assert.AreEqual(HoundState.Following, hound.State,
                "feeding must never lower the bond state");
        }
    }
}
