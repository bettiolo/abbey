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
    /// raises trust, thresholds gate Chained -> Wary -> Fed -> Following ->
    /// Trusting, a fed hound answers the bell, and a starving or low-trust hound
    /// ignores it. P2-05 adds the full state set (Guarding, Hunting, Protective,
    /// Angry, Missing, Wounded, Trusting) and the first-encounter choices
    /// (free-from-chain, approach-slowly, leave-chained, calm-with-bell).
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

        // ------------------------------------------------------------------
        // First-encounter choices (P2-05) — every choice branches the night
        // ------------------------------------------------------------------

        MonsterController SpawnMonster(Vector3 position)
        {
            var go = new GameObject("TestMonster");
            _spawned.Add(go);
            go.transform.position = position;
            var monster = go.AddComponent<MonsterController>();
            monster.autoTick = false;
            monster.Configure(_config);
            return monster;
        }

        [Test]
        public void FreeFromChain_NoBond_TurnsAngry_AndFleesToMissing()
        {
            _config.houndFleeDistance = 5f;
            var hound = SpawnHound(Vector3.zero);

            Assert.IsTrue(hound.FreeFromChain(new Vector3(-1f, 0f, 0f)));

            Assert.IsFalse(hound.IsChained, "the chain is off either way");
            Assert.AreEqual(HoundState.Angry, hound.State,
                "0.2 + 0.05 gain = 0.25 trust, below the 0.35 keep threshold");
            Assert.AreEqual(1, CountLog("hound_choice"));

            for (int i = 0; i < 50 && hound.State == HoundState.Angry; i++)
            {
                hound.Tick(0.1f);
            }

            Assert.AreEqual(HoundState.Missing, hound.State);
            Assert.IsTrue(hound.IsMissing);
            Assert.GreaterOrEqual(
                PlanarMotion.Distance(hound.transform.position, Vector3.zero),
                _config.houndFleeDistance, "it ran at least the flee distance");

            // A missing hound answers nothing.
            EventBus.RaiseBellRang(Vector3.zero, 100f);
            Assert.IsFalse(hound.HasBellTarget);
            Assert.AreEqual(0, CountLog("hound_answered_bell"));
        }

        [Test]
        public void FreeFromChain_DecentTrust_StaysWary()
        {
            var hound = SpawnHound(Vector3.zero);
            hound.Trust = 0.4f; // + 0.05 gain = 0.45: above keep, below Fed

            Assert.IsTrue(hound.FreeFromChain(Vector3.zero));

            Assert.IsFalse(hound.IsChained);
            Assert.AreEqual(HoundState.Wary, hound.State);
            Assert.AreEqual(0.45f, hound.Trust, 1e-5f);
            Assert.IsFalse(hound.FreeFromChain(Vector3.zero), "cannot free twice");
        }

        [Test]
        public void FreeFromChain_HighTrust_Follows()
        {
            var hound = SpawnHound(Vector3.zero);
            hound.Trust = 0.9f; // + 0.05 = 0.95 >= follow threshold 0.9

            Assert.IsTrue(hound.FreeFromChain(Vector3.zero));

            Assert.AreEqual(HoundState.Following, hound.State);
        }

        [Test]
        public void LeaveChained_LogsTheChoice_AndBreedsResentment()
        {
            var hound = SpawnHound(Vector3.zero);

            Assert.IsTrue(hound.LeaveChained());

            Assert.AreEqual(0.15f, hound.Trust, 1e-5f); // 0.2 - 0.05 resentment
            Assert.AreEqual(HoundState.Chained, hound.State);
            Assert.IsTrue(hound.IsChained);
            Assert.AreEqual(1, CountLog("hound_choice"));
        }

        [Test]
        public void ApproachSlowly_CalmHound_GainsAttachmentAndTrust()
        {
            var hound = SpawnHound(Vector3.zero);
            hound.Fear = 0.3f;
            hound.Pain = 0.3f; // 0.6 sum, under the 1.0 bite threshold

            var result = hound.ApproachSlowly();

            Assert.AreEqual(HoundApproachResult.Calmed, result);
            Assert.AreEqual(0.25f, hound.Trust, 1e-5f);      // +0.05
            Assert.AreEqual(0.08f, hound.Attachment, 1e-5f); // +0.08
            Assert.AreEqual(0.25f, hound.Fear, 1e-5f);       // -0.05
            Assert.AreEqual(1, CountLog("hound_choice"));
        }

        [Test]
        public void ApproachSlowly_FearfulWoundedHound_Bites_Deterministically()
        {
            var hound = SpawnHound(Vector3.zero);
            // Start values: fear 0.5 + pain 0.6 = 1.1 >= bite threshold 1.0.

            var result = hound.ApproachSlowly();

            Assert.AreEqual(HoundApproachResult.Bitten, result);
            Assert.AreEqual(0.1f, hound.Trust, 1e-5f); // -0.1
            Assert.AreEqual(0.6f, hound.Fear, 1e-5f);  // +0.1
            Assert.AreEqual(HoundState.Chained, hound.State,
                "a bite is not yet anger: pain 0.6 is under the angry threshold");
            Assert.AreEqual(1, CountLog("hound_choice"));
        }

        [Test]
        public void BellRungNearby_CalmsTheChainedHound()
        {
            var hound = SpawnHound(new Vector3(2f, 0f, 0f));

            EventBus.RaiseBellRang(Vector3.zero, 10f); // within the pulse

            Assert.AreEqual(0.35f, hound.Fear, 1e-5f);  // 0.5 - 0.15
            Assert.AreEqual(0.25f, hound.Trust, 1e-5f); // 0.2 + 0.05
            Assert.AreEqual(1, CountLog("hound_calmed_by_bell"));
            Assert.AreEqual(1, CountLog("hound_ignored_bell"), "it still does not come");
            Assert.AreEqual(HoundState.Chained, hound.State);
        }

        [Test]
        public void BellRungFarAway_DoesNotCalm()
        {
            var hound = SpawnHound(new Vector3(50f, 0f, 0f));

            EventBus.RaiseBellRang(Vector3.zero, 10f); // out of the pulse

            Assert.AreEqual(0.5f, hound.Fear, 1e-5f, "fear unchanged");
            Assert.AreEqual(0, CountLog("hound_calmed_by_bell"));
            Assert.AreEqual(1, CountLog("hound_ignored_bell"));
        }

        // ------------------------------------------------------------------
        // Value-driven overrides: Wounded, Angry, Hunting, Guarding, Trusting
        // ------------------------------------------------------------------

        [Test]
        public void TakeHit_HighPain_ReadsWounded_ThenRecovers()
        {
            _config.houndPainRecoveryPerSecond = 0.05f;
            var hound = SpawnHound(Vector3.zero);
            hound.Fear = 0.2f; // fear stays under the angry threshold

            hound.TakeHit(0.3f); // pain 0.9 >= wounded threshold 0.75

            Assert.AreEqual(HoundState.Wounded, hound.State);
            Assert.AreEqual(0.3f, hound.Fear, 1e-5f, "a hit frightens: +0.1");

            for (int i = 0; i < 60 && hound.State == HoundState.Wounded; i++)
            {
                hound.Tick(0.1f); // pain recovers 0.05/s
            }

            Assert.AreEqual(HoundState.Chained, hound.State,
                "healed under the threshold, the unbonded hound is simply chained again");
            Assert.Less(hound.Pain, _config.houndWoundedPainThreshold);
        }

        [Test]
        public void TakeHit_PainAndFearTogether_ReadAngry()
        {
            var hound = SpawnHound(Vector3.zero);

            hound.TakeHit(0.2f); // pain 0.8 >= 0.7, fear 0.6 >= 0.6

            Assert.AreEqual(HoundState.Angry, hound.State);

            hound.Feed(); // fear relief 0.1 drops fear to 0.5: anger breaks...

            Assert.AreEqual(HoundState.Wounded, hound.State,
                "...but pain 0.8 still reads as Wounded");
        }

        [Test]
        public void StarvedFreedHound_Hunts_RefusesBell_AndDragsItsKill()
        {
            var hound = SpawnHound(Vector3.zero);
            hound.Trust = 0.4f;
            var monster = SpawnMonster(new Vector3(5f, 0f, 0f));

            Assert.IsTrue(hound.FreeFromChain(new Vector3(-1f, 0f, 0f))); // Wary, starving
            Assert.AreEqual(HoundState.Wary, hound.State);
            Assert.IsTrue(hound.IsStarving);

            hound.Tick(0.1f);
            Assert.AreEqual(HoundState.Hunting, hound.State,
                "a starved, freed hound hunts for itself");

            EventBus.RaiseBellRang(Vector3.zero, 100f);
            Assert.IsFalse(hound.HasBellTarget, "a hunting hound refuses the bell");
            Assert.AreEqual(1, CountLog("hound_ignored_bell"));

            for (int i = 0; i < 200 && CountLog("hound_dragged_corpse") == 0; i++)
            {
                hound.Tick(0.1f);
            }

            Assert.IsFalse(monster.IsAlive, "the hunt ends in a kill");
            Assert.AreEqual(1, CountLog("hound_killed_monster"));
            Assert.AreEqual(1, CountLog("hound_dragged_corpse"));
            Assert.Greater(monster.transform.position.z, 6f,
                "the corpse was dragged away from the kill site toward darkness");
            Assert.IsFalse(hound.IsStarving, "it ate the kill alone");
            Assert.AreEqual(HoundState.Wary, hound.State,
                "sated, the low-bond hound settles back to Wary");
        }

        [Test]
        public void ChainedStarvingHound_DoesNotHunt()
        {
            var hound = SpawnHound(Vector3.zero);
            SpawnMonster(new Vector3(5f, 0f, 0f));

            for (int i = 0; i < 20; i++)
            {
                hound.Tick(0.1f);
            }

            Assert.AreEqual(HoundState.Chained, hound.State,
                "the chain holds: the unfed first-night hound never stirs");
            Assert.AreEqual(0, CountLog("hound_engaged_monster"));
        }

        [Test]
        public void TrustedFreedHound_GuardsInsideSafeLight_AndStillAnswersTheBell()
        {
            DarknessEvaluator.Config = _config;
            var fireGO = new GameObject("Campfire");
            _spawned.Add(fireGO);
            fireGO.transform.position = Vector3.zero;
            var fire = fireGO.AddComponent<LightSource>();
            fire.autoTick = false;
            fire.radius = 10f;
            fire.strength = 1f;
            fire.fuelSeconds = -1f;

            var hound = SpawnHound(new Vector3(1f, 0f, 0f)); // Safe (10 * 0.7 = 7)
            hound.Trust = 0.9f;
            hound.Hunger = 0.2f;
            Assert.IsTrue(hound.FreeFromChain(Vector3.zero)); // trust 0.95: Following

            hound.Tick(0.1f);
            Assert.AreEqual(HoundState.Guarding, hound.State,
                "a free, trusted, sated hound settles by the fire");

            EventBus.RaiseBellRang(new Vector3(5f, 0f, 0f), 15f);
            Assert.IsTrue(hound.HasBellTarget, "the guard still answers the bell");
            Assert.AreEqual(HoundState.Following, hound.State);
        }

        [Test]
        public void Trusting_NeedsTrustAndAttachment()
        {
            var hound = SpawnHound(Vector3.zero);
            hound.Trust = 0.85f;
            hound.Attachment = 0.5f;

            hound.Feed(); // trust 1.0 >= 0.9, attachment 0.55 >= 0.4

            Assert.AreEqual(HoundState.Trusting, hound.State);

            EventBus.RaiseBellRang(new Vector3(5f, 0f, 0f), 15f);
            Assert.IsTrue(hound.HasBellTarget);
            Assert.AreEqual(HoundState.Trusting, hound.State,
                "answering the bell never demotes a Trusting hound");
        }
    }
}
