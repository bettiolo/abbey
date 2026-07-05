using System.Collections;
using System.Collections.Generic;
using Abbey.Beast;
using Abbey.Combat;
using Abbey.Core;
using Abbey.Light;
using Abbey.Nightmares;
using Abbey.Sanity;
using Abbey.Villagers;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Abbey.Tests.PlayMode
{
    /// <summary>
    /// PlayMode coverage for hound evolution (P3-07) in fully programmatic worlds.
    /// Across simulated days, feeding + co-fighting hardens the hound into the Guardian
    /// path and it then defends at night; a neglected, starved hound hunting alone
    /// hardens into the Starved path and refuses the bell (reusing the P2-05 starved
    /// assertions to prove continuity); and on every path the beast stays exempt from the
    /// light-band combat penalties and absent from the sanity records. Deterministic:
    /// autoTick off, manual Tick with fixed dt; the dawn evaluation runs off the clock's
    /// phase event.
    /// </summary>
    public class HoundEvolutionPlayModeTests
    {
        const float Dt = 0.1f;

        readonly List<GameObject> _spawned = new List<GameObject>();
        readonly List<ScriptableObject> _assets = new List<ScriptableObject>();
        GameClock _clock;

        [SetUp]
        public void SetUp()
        {
            ClearStatics();
        }

        [TearDown]
        public void TearDown()
        {
            if (HoundEvolutionSystem.Instance != null)
            {
                Object.DestroyImmediate(HoundEvolutionSystem.Instance.gameObject);
            }
            if (SanitySystem.Instance != null)
            {
                Object.DestroyImmediate(SanitySystem.Instance.gameObject);
            }
            var monsters = new List<MonsterController>(MonsterController.Active);
            foreach (var m in monsters)
            {
                if (m != null)
                {
                    Object.DestroyImmediate(m.gameObject);
                }
            }
            foreach (var go in _spawned)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }
            _spawned.Clear();
            foreach (var a in _assets)
            {
                if (a != null)
                {
                    Object.DestroyImmediate(a);
                }
            }
            _assets.Clear();
            ClearStatics();
        }

        static void ClearStatics()
        {
            EventBus.ResetAll();
            GameEventLog.Clear();
            DarknessEvaluator.Clear();
            DuskRecallSystem.Clear();
            MonsterController.ClearRegistry();
            HoundEvolutionConfig.ClearCache();
            CombatConfig.ClearCache();
            PrototypeConfig.ClearCache();
        }

        // ---- World construction ------------------------------------------

        PrototypeConfig CreateConfig()
        {
            var config = ScriptableObject.CreateInstance<PrototypeConfig>();
            _assets.Add(config);
            config.edgeBandFraction = 0.3f;
            config.arrivalRadius = 0.3f;
            config.campfireRadius = 10f;
            config.dayDurationSeconds = 1f;
            config.duskDurationSeconds = 1f;
            config.nightDurationSeconds = 1f;
            config.dawnDurationSeconds = 1f;
            config.bellRadius = 40f;
            config.bellCooldownSeconds = 0.01f;

            config.houndStartTrust = 0.1f;
            config.houndStartHunger = 0.9f;
            config.houndStartPain = 0.2f;
            config.houndStartFear = 0.2f;
            config.hungerStarvingThreshold = 0.8f;
            config.feedTrustGain = 0.2f;
            config.feedHungerRelief = 0.3f;
            config.trustFedThreshold = 0.5f;
            config.trustFollowThreshold = 0.75f;
            config.guardTrustThreshold = 0.75f;
            config.houndHungerPerSecond = 0f;
            config.houndMoveSpeed = 6f;
            config.houndEngageRange = 12f;
            config.houndAttackRange = 1.5f;
            config.houndAttackDamage = 50f;
            config.houndAttackCooldownSeconds = 0.1f;
            config.freeChainTrustGain = 0.02f;
            config.freeChainFollowThreshold = 0.35f;
            config.houndFleeDistance = 8f;
            config.chainBreakTrustThreshold = 0.5f;
            config.houndProtectMonsterRange = 10f;
            config.houndDragDistance = 4f;
            config.houndEatHungerRelief = 0.05f; // stays starving: keeps hunting

            config.monsterMaxHealth = 30f;
            config.monsterMoveSpeed = 1f;
            config.monsterFleeSpeed = 1f;
            config.monsterFleeDistance = 12f;
            config.monsterSightRange = 60f;
            return config;
        }

        HoundEvolutionConfig CreateEvoConfig()
        {
            var cfg = ScriptableObject.CreateInstance<HoundEvolutionConfig>();
            _assets.Add(cfg);
            return cfg;
        }

        GameClock CreateClock(PrototypeConfig config)
        {
            var go = new GameObject("Clock");
            _spawned.Add(go);
            _clock = go.AddComponent<GameClock>();
            _clock.autoTick = false;
            _clock.Configure(config);
            return _clock;
        }

        LightSource CreateCampfire(Vector3 pos, PrototypeConfig config)
        {
            DarknessEvaluator.Config = config;
            var go = new GameObject("Campfire");
            _spawned.Add(go);
            go.transform.position = pos;
            var fire = go.AddComponent<LightSource>();
            fire.autoTick = false;
            fire.radius = config.campfireRadius;
            fire.strength = 1f;
            fire.fuelSeconds = -1f;
            return fire;
        }

        HoundController CreateHound(Vector3 pos, PrototypeConfig config)
        {
            var go = new GameObject("BlackHound");
            _spawned.Add(go);
            go.transform.position = pos;
            var hound = go.AddComponent<HoundController>();
            hound.autoTick = false;
            hound.Configure(config);
            return hound;
        }

        MonsterController CreateMonster(Vector3 pos, PrototypeConfig config)
        {
            var go = new GameObject("Monster");
            _spawned.Add(go);
            go.transform.position = pos;
            var m = go.AddComponent<MonsterController>();
            m.autoTick = false;
            m.Configure(config);
            return m;
        }

        HoundEvolutionSystem CreateEvolution(HoundEvolutionConfig cfg, HoundController hound)
        {
            var go = new GameObject("HoundEvolution");
            _spawned.Add(go);
            var sys = go.AddComponent<HoundEvolutionSystem>();
            sys.Configure(cfg, hound);
            return sys;
        }

        void AdvanceClockTo(DayPhase target)
        {
            int guard = 10000;
            while (_clock.Phase != target && guard-- > 0)
            {
                _clock.Tick(0.25f);
            }
            Assert.Greater(guard, 0, $"clock never reached {target}");
        }

        static bool LogContains(string type, string fragment = null)
        {
            var records = GameEventLog.Records;
            for (int i = 0; i < records.Count; i++)
            {
                if (records[i].Type == type
                    && (fragment == null || records[i].Data.Contains(fragment)))
                {
                    return true;
                }
            }
            return false;
        }

        // ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator FeedingAndCoFighting_ProduceGuardian_ThenHoundDefends()
        {
            var config = CreateConfig();
            var evo = CreateEvoConfig();
            CreateClock(config);
            CreateCampfire(Vector3.zero, config);

            // A well-treated hound out at the dark edge: fed, freed, bonded.
            var hound = CreateHound(new Vector3(12f, 0f, 0f), config);
            hound.Trust = 0.95f;
            for (int i = 0; i < 4; i++)
            {
                hound.Feed();
            }
            Assert.IsTrue(hound.FreeFromChain(Vector3.zero));
            hound.Trust = 0.95f;
            hound.Attachment = 0.8f;
            hound.Fear = 0.05f;
            hound.Pain = 0.05f;
            hound.Hunger = 0.2f;

            var evolution = CreateEvolution(evo, hound);

            // It fights alongside the settlement: kills a monster at the edge.
            var monster = CreateMonster(new Vector3(14f, 0f, 0f), config);
            for (int i = 0; i < 60 && monster.IsAlive; i++)
            {
                hound.Tick(Dt);
                monster.Tick(Dt);
            }
            Assert.IsFalse(monster.IsAlive, "the hound co-fought and killed the threat");
            Assert.GreaterOrEqual(hound.AlliedFights, 1, "the allied kill was recorded");

            // Dawn: the year's treatment hardens it into the Guardian path.
            AdvanceClockTo(DayPhase.Dawn);
            Assert.AreEqual(HoundPath.Guardian, evolution.CurrentPath);
            Assert.AreEqual(HoundPath.Guardian, hound.Path);
            Assert.IsTrue(LogContains("hound_evolved", "-> Guardian"));
            Assert.Greater(evolution.BeastStatus, 0f, "a Guardian is beloved");
            Assert.Greater(hound.BellResponseMultiplier, 0f, "a Guardian still answers the bell");

            // Night: the Guardian defends — it engages and kills a fresh raider.
            AdvanceClockTo(DayPhase.Night);
            var raider = CreateMonster(hound.transform.position + new Vector3(3f, 0f, 0f), config);
            for (int i = 0; i < 60 && raider.IsAlive; i++)
            {
                hound.Tick(Dt);
                raider.Tick(Dt);
            }
            Assert.IsFalse(raider.IsAlive, "the Guardian defends at night");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Neglect_ProducesStarved_WhichRefusesTheBell()
        {
            var config = CreateConfig();
            var evo = CreateEvoConfig();
            CreateClock(config);
            CreateCampfire(Vector3.zero, config);

            // Never fed: starving, freed with just enough trust to stay (not flee).
            var hound = CreateHound(new Vector3(7f, 0f, 0f), config);
            hound.Trust = 0.4f;
            Assert.IsTrue(hound.FreeFromChain(Vector3.zero));
            Assert.IsTrue(hound.IsStarving, "never fed: it starves");

            var evolution = CreateEvolution(evo, hound);

            // It hunts alone across the night: two solo kills, eaten in the dark.
            for (int k = 0; k < 2; k++)
            {
                CreateMonster(new Vector3(14f + k, 0f, 2f), config);
                for (int i = 0; i < 300 && hound.SoloHunts <= k; i++)
                {
                    hound.Tick(Dt);
                    var monsters = MonsterController.Active;
                    for (int m = 0; m < monsters.Count; m++)
                    {
                        if (monsters[m] != null && monsters[m].IsAlive)
                        {
                            monsters[m].Tick(Dt);
                        }
                    }
                    if (i % 50 == 49)
                    {
                        yield return null;
                    }
                }
            }
            Assert.GreaterOrEqual(hound.SoloHunts, 1, "the starved hound hunted for itself");
            Assert.IsTrue(LogContains("hound_intervention", "ate_kill"));

            // Dawn: neglect hardens it into the permanent Starved path.
            AdvanceClockTo(DayPhase.Dawn);
            Assert.AreEqual(HoundPath.Starved, evolution.CurrentPath);
            Assert.IsTrue(hound.HuntsAlone, "the Starved path hunts alone");
            Assert.AreEqual(0f, hound.BellResponseMultiplier, 1e-4f, "and refuses the bell");

            // The bell now means nothing to it — on any state, because of the path.
            EventBus.RaiseBellRang(hound.transform.position, config.bellRadius);
            Assert.IsFalse(hound.HasBellTarget, "the Starved hound will not answer the bell");
            Assert.IsTrue(LogContains("hound_ignored_bell", "path=Starved"),
                "the refusal is the permanent path, logged as such (P2-05 continuity)");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Beast_ExemptFromBands_AndUntracked_OnEveryPath()
        {
            var config = CreateConfig();
            var evo = CreateEvoConfig();
            var combat = ScriptableObject.CreateInstance<CombatConfig>();
            _assets.Add(combat);
            CreateCampfire(Vector3.zero, config);

            // A tracked villager + the beast, side by side.
            var sanityGO = new GameObject("SanitySystem");
            _spawned.Add(sanityGO);
            var sanity = sanityGO.AddComponent<SanitySystem>();
            sanity.autoTick = false;
            var sanityConfig = ScriptableObject.CreateInstance<SanityConfig>();
            _assets.Add(sanityConfig);
            sanity.Configure(sanityConfig);

            var villagerGO = new GameObject("Villager");
            _spawned.Add(villagerGO);
            var villager = villagerGO.AddComponent<VillagerAgent>();
            villager.autoTick = false;
            villager.Config = config;
            DuskRecallSystem.Register(villager);

            var hound = CreateHound(new Vector3(12f, 0f, 0f), config);

            foreach (HoundPath path in System.Enum.GetValues(typeof(HoundPath)))
            {
                hound.ApplyEvolution(path, evo.ProfileFor(path).behaviour);
                Assert.IsTrue(hound.IsBeast, $"{path}: always the beast");

                var dark = LightBandCombatResolver.Resolve(
                    CombatSide.Friendly, hound.IsBeast, LightZone.Dark, combat);
                Assert.IsTrue(dark.BeastExempt, $"{path}: exempt from Dark-band penalties");
                Assert.AreEqual(1f, dark.DamageMultiplier, 1e-4f);
                Assert.AreEqual(0f, dark.SanityDrainPerSecond, 1e-4f);

                sanity.RefreshRecords();
                Assert.AreEqual(1, sanity.Records.Count,
                    $"{path}: only the villager is tracked, never the beast");
            }
            yield return null;
        }
    }
}
