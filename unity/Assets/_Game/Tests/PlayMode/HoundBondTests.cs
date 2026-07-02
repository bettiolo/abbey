using System.Collections;
using System.Collections.Generic;
using Abbey.Beast;
using Abbey.Core;
using Abbey.Hero;
using Abbey.Light;
using Abbey.Nightmares;
using Abbey.Villagers;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Abbey.Tests.PlayMode
{
    /// <summary>
    /// P2-05: the three hound bond branches that make the first encounter matter,
    /// in fully programmatic worlds (no scene load). (a) A fed, bonded but still
    /// chained hound breaks its chain to save the hero endangered in darkness near
    /// a monster. (b) A starved, freed hound hunts on its own: kills the monster,
    /// drags the corpse toward darkness, eats alone and refuses the bell.
    /// (c) A hound freed with no bond turns Angry and flees to Missing.
    /// Deterministic: autoTick off everywhere, manual Tick with fixed dt.
    /// </summary>
    public class HoundBondTests
    {
        const float Dt = 0.1f;

        readonly List<GameObject> _spawned = new List<GameObject>();
        readonly List<ScriptableObject> _assets = new List<ScriptableObject>();

        [SetUp]
        public void SetUp()
        {
            EventBus.ResetAll();
            GameEventLog.Clear();
            DarknessEvaluator.Clear();
            DuskRecallSystem.Clear();
            MonsterController.ClearRegistry();
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
            MonsterController.ClearRegistry();
            DuskRecallSystem.Clear();
            DarknessEvaluator.Clear();
            EventBus.ResetAll();
            GameEventLog.Clear();
        }

        // ------------------------------------------------------------------
        // World construction
        // ------------------------------------------------------------------

        PrototypeConfig CreateConfig()
        {
            var config = ScriptableObject.CreateInstance<PrototypeConfig>();
            _assets.Add(config);

            config.edgeBandFraction = 0.3f;
            config.arrivalRadius = 0.3f;
            config.campfireRadius = 10f; // Safe within 7

            config.bellkeeperMoveSpeed = 6f;
            config.interactRange = 2f;
            config.bellRadius = 15f;
            config.bellCooldownSeconds = 1f;
            config.feedCooldownSeconds = 0.2f;
            config.startingCarriedFood = 3;

            config.houndStartTrust = 0.1f;
            config.houndStartHunger = 0.9f;
            config.houndStartPain = 0.6f;
            config.houndStartFear = 0.5f;
            config.hungerStarvingThreshold = 0.8f;
            config.feedTrustGain = 0.5f;      // one meal crosses the Fed threshold
            config.feedHungerRelief = 0.4f;
            config.trustFedThreshold = 0.5f;
            config.trustFollowThreshold = 0.95f;
            config.houndHungerPerSecond = 0f;
            config.houndMoveSpeed = 6f;
            config.houndEngageRange = 12f;
            config.houndAttackRange = 1.5f;
            config.houndAttackDamage = 60f;
            config.houndAttackCooldownSeconds = 0.2f;

            config.freeChainTrustGain = 0.05f;
            config.freeChainFollowThreshold = 0.35f;
            config.houndFleeDistance = 8f;
            config.chainBreakTrustThreshold = 0.5f;
            config.houndProtectMonsterRange = 10f;
            config.houndDragDistance = 6f;
            config.houndEatHungerRelief = 0.35f;
            config.guardTrustThreshold = 0.75f;

            config.monsterMaxHealth = 50f;    // one hound bite kills
            config.monsterMoveSpeed = 1f;
            config.monsterFleeSpeed = 1f;     // slower than the hound: interception wins
            config.monsterLightTolerance = 0.15f;
            config.monsterFleeDistance = 15f;
            config.monsterSightRange = 60f;

            return config;
        }

        GameObject Track(GameObject go)
        {
            _spawned.Add(go);
            return go;
        }

        LightSource CreateCampfire(Vector3 position, PrototypeConfig config)
        {
            DarknessEvaluator.Config = config;
            var go = Track(new GameObject("Campfire"));
            go.transform.position = position;
            var fire = go.AddComponent<LightSource>();
            fire.autoTick = false;
            fire.radius = config.campfireRadius;
            fire.strength = 1f;
            fire.fuelSeconds = -1f;
            return fire;
        }

        BellkeeperController CreateHero(Vector3 position, PrototypeConfig config)
        {
            var go = Track(new GameObject("Bellkeeper"));
            go.transform.position = position;
            var hero = go.AddComponent<BellkeeperController>();
            hero.autoTick = false;
            hero.useDirectInput = false;
            hero.Configure(config);
            return hero;
        }

        HoundController CreateHound(Vector3 position, PrototypeConfig config)
        {
            var go = Track(new GameObject("BlackHound"));
            go.transform.position = position;
            var hound = go.AddComponent<HoundController>();
            hound.autoTick = false;
            hound.Configure(config);
            return hound;
        }

        MonsterController CreateMonster(Vector3 position, PrototypeConfig config)
        {
            var go = Track(new GameObject("PaleHound"));
            go.transform.position = position;
            var monster = go.AddComponent<MonsterController>();
            monster.autoTick = false;
            monster.Configure(config);
            return monster;
        }

        static bool LogContains(string type, string dataFragment = null)
        {
            var records = GameEventLog.Records;
            for (int i = 0; i < records.Count; i++)
            {
                if (records[i].Type == type
                    && (dataFragment == null || records[i].Data.Contains(dataFragment)))
                {
                    return true;
                }
            }
            return false;
        }

        // ------------------------------------------------------------------
        // (a) Fed + bonded: the chain breaks to save the hero
        // ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator FedBondedHound_BreaksChain_ToSaveEndangeredHero()
        {
            var config = CreateConfig();
            CreateCampfire(Vector3.zero, config);
            var hero = CreateHero(new Vector3(2f, 0f, 0f), config);
            var hound = CreateHound(new Vector3(3f, 0f, 0f), config);

            // Day choice: feed the chained hound. Bond: trust 0.6, no longer starving.
            Assert.IsTrue(hero.FeedHound(hound));
            Assert.AreEqual(HoundState.Fed, hound.State);
            Assert.IsTrue(hound.IsChained, "feeding alone does not remove the chain");
            Assert.IsFalse(hound.IsStarving);
            yield return null;

            // The hero strays deep into darkness (campfire Safe ends at 7).
            hero.SetMoveTarget(new Vector3(20f, 0f, 0f));
            for (int i = 0; i < 100 && hero.HasMoveTarget; i++)
            {
                hero.Tick(Dt);
            }
            Assert.AreEqual(LightZone.Dark,
                DarknessEvaluator.Classify(hero.transform.position));

            // A monster closes in on the hero: the Protective trigger.
            var monster = CreateMonster(new Vector3(24f, 0f, 0f), config);
            bool sawProtective = false;
            for (int i = 0; i < 200 && monster.IsAlive; i++)
            {
                hound.Tick(Dt);
                monster.Tick(Dt);
                sawProtective |= hound.State == HoundState.Protective;
                if (i % 50 == 49)
                {
                    yield return null;
                }
            }

            Assert.IsTrue(sawProtective, "the hound went Protective for the hero");
            Assert.IsFalse(hound.IsChained);
            Assert.IsTrue(LogContains("hound_intervention", "broke_chain reason=save_hero"),
                "the chain break is a logged intervention");
            Assert.IsTrue(LogContains("hound_intervention", "protect_hero"));
            Assert.IsFalse(monster.IsAlive, "the hound killed the threat");
            Assert.IsTrue(hero.IsAlive);

            // Danger passed: the override resolves back to the bond ladder.
            hound.Tick(Dt);
            Assert.AreEqual(HoundState.Fed, hound.State,
                "trust 0.6 sits between the Fed and Following thresholds");
            yield return null;
        }

        // ------------------------------------------------------------------
        // (b) Starved + freed: hunts alone, drags the kill, refuses the bell
        // ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator StarvedFreedHound_Hunts_DragsCorpseTowardDark_AndRefusesBell()
        {
            var config = CreateConfig();
            var fire = CreateCampfire(Vector3.zero, config);
            var hero = CreateHero(new Vector3(6f, 0f, 0f), config);
            var hound = CreateHound(new Vector3(7f, 0f, 0f), config);
            hound.Trust = 0.4f; // enough to stay when freed (0.45 >= 0.35), not bonded

            Assert.IsTrue(hero.FreeHound(hound));
            Assert.IsFalse(hound.IsChained);
            Assert.AreEqual(HoundState.Wary, hound.State);
            Assert.IsTrue(hound.IsStarving, "never fed: start hunger 0.9");
            Assert.IsTrue(LogContains("hound_choice", "free_chain"));
            yield return null;

            // Night: a monster prowls the dark within the hound's reach.
            var monster = CreateMonster(new Vector3(16f, 0f, 0f), config);
            hound.Tick(Dt);
            Assert.AreEqual(HoundState.Hunting, hound.State);
            Assert.IsTrue(LogContains("hound_intervention", "hunting_starved"));

            // The bell means nothing to a starved hunter.
            Assert.IsTrue(hero.RingBell());
            Assert.IsFalse(hound.HasBellTarget);
            Assert.IsTrue(LogContains("hound_ignored_bell"));
            Assert.IsFalse(LogContains("hound_answered_bell"));

            Vector3 killSite = Vector3.zero;
            bool killSeen = false;
            for (int i = 0; i < 400 && !LogContains("hound_dragged_corpse"); i++)
            {
                hound.Tick(Dt);
                if (monster.IsAlive)
                {
                    monster.Tick(Dt);
                }
                else if (!killSeen)
                {
                    killSeen = true;
                    killSite = monster.transform.position;
                }
                if (i % 50 == 49)
                {
                    yield return null;
                }
            }

            Assert.IsFalse(monster.IsAlive, "the starved hound made its own kill");
            Assert.IsTrue(LogContains("hound_killed_monster"));
            Assert.IsTrue(LogContains("hound_dragged_corpse"));
            float fireToKill = PlanarMotion.Distance(fire.transform.position, killSite);
            float fireToCorpse = PlanarMotion.Distance(
                fire.transform.position, monster.transform.position);
            Assert.Greater(fireToCorpse, fireToKill + config.houndDragDistance * 0.5f,
                "the corpse was dragged away from the light");
            Assert.IsTrue(LogContains("hound_intervention", "ate_kill"));
            Assert.IsFalse(hound.IsStarving, "it ate the kill alone");
            Assert.AreEqual(HoundState.Wary, hound.State,
                "sated but unbonded: back to Wary, not to the hero's side");
            yield return null;
        }

        // ------------------------------------------------------------------
        // (c) Freed with no bond: Angry flight to Missing
        // ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator FreedWithNoTrust_TurnsAngry_AndFleesToMissing()
        {
            var config = CreateConfig();
            CreateCampfire(Vector3.zero, config);
            var hero = CreateHero(new Vector3(6f, 0f, 0f), config);
            var hound = CreateHound(new Vector3(7f, 0f, 0f), config);
            // Start trust 0.1 + 0.05 gain = 0.15: below the keep threshold.

            Vector3 releasePoint = hound.transform.position;
            Assert.IsTrue(hero.FreeHound(hound));
            Assert.AreEqual(HoundState.Angry, hound.State);
            Assert.IsFalse(hound.IsChained);
            Assert.IsTrue(LogContains("hound_choice", "outcome=fled"));
            yield return null;

            for (int i = 0; i < 100 && !hound.IsMissing; i++)
            {
                hound.Tick(Dt);
                if (i % 50 == 49)
                {
                    yield return null;
                }
            }

            Assert.AreEqual(HoundState.Missing, hound.State);
            Assert.GreaterOrEqual(
                PlanarMotion.Distance(hound.transform.position, releasePoint),
                config.houndFleeDistance, "it left the map, not just the tower");
            Assert.IsTrue(LogContains("hound_intervention", "went_missing"));

            // Gone means gone: the bell raises nothing, the log stays silent.
            Assert.IsTrue(hero.RingBell());
            Assert.IsFalse(hound.HasBellTarget);
            Assert.IsFalse(LogContains("hound_answered_bell"));
            hound.Tick(Dt);
            Assert.AreEqual(HoundState.Missing, hound.State, "Missing is terminal for the night");
            yield return null;
        }
    }
}
