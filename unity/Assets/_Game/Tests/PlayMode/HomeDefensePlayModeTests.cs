using System.Collections;
using System.Collections.Generic;
using Abbey.Buildings;
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
    /// PlayMode coverage for the two-tier settler home defense (P3-05), worlds built
    /// programmatically with deterministic manual ticks:
    /// danger far away ⇒ the house sleeps all night at no sanity cost; a monster at
    /// the door ⇒ the house wakes, its interior light flares (the doorstep
    /// reclassifies Safe), window volleys damage the monster and the woken occupants
    /// lose sanity per volley; a Safe-flared door debuffs the assaulting monster's
    /// structural damage through the resolver; and an overwhelmed home is razed —
    /// occupants dead, light node gone (Dark again), <c>HomeRazed</c> on the event
    /// stream.
    /// </summary>
    public class HomeDefensePlayModeTests
    {
        readonly List<GameObject> _spawned = new List<GameObject>();
        readonly List<ScriptableObject> _assets = new List<ScriptableObject>();
        PrototypeConfig _proto;
        CombatConfig _combat;
        SanityConfig _sanity;
        GameClock _clock;
        SanitySystem _sanitySystem;
        HomeDefenseSystem _defense;

        [SetUp]
        public void SetUp()
        {
            ClearStatics();

            _proto = ScriptableObject.CreateInstance<PrototypeConfig>();
            _assets.Add(_proto);
            _proto.dayDurationSeconds = 1f;
            _proto.duskDurationSeconds = 1f;
            _proto.nightDurationSeconds = 1f;
            _proto.dawnDurationSeconds = 1f;
            _proto.edgeBandFraction = 0.3f;
            _proto.arrivalRadius = 0.3f;
            _proto.villagerFearPerSecondInDark = 0f;
            _proto.villagerFearPerSecondInEdge = 0f;
            _proto.villagerInjuredDarkSeconds = 100000f;
            _proto.villagerMissingDarkSeconds = 200000f;
            _proto.monsterMaxHealth = 100f;
            _proto.monsterMoveSpeed = 5f;
            _proto.monsterAttackRange = 1.2f;
            _proto.monsterAttackCooldownSeconds = 0.4f;
            _proto.monsterSightRange = 60f;

            _combat = ScriptableObject.CreateInstance<CombatConfig>();
            _assets.Add(_combat);
            _combat.safeMonsterDamageMultiplier = 0.35f;
            _combat.edgeMonsterDamageMultiplier = 1f;
            _combat.darkMonsterDamageMultiplier = 1f;
            _combat.safeFriendlyDamageMultiplier = 1f;
            _combat.edgeFriendlyDamageMultiplier = 1f;
            _combat.darkFriendlyDamageMultiplier = 0.5f;
            _combat.darkFriendlySanityDrainPerSecond = 0.05f;
            _combat.wakeRadius = 4f;
            _combat.flareLightRadius = 5f;
            _combat.flareLightStrength = 1f;
            _combat.windowShotDamage = 8f;
            _combat.windowShotIntervalSeconds = 0.5f;
            _combat.sanityCostPerVolley = 0.05f;
            _combat.defenseEngageRange = 8f;
            _combat.baseHomeHitPoints = 1000f;
            _combat.monsterHomeAttackDamage = 6f;

            _sanity = ScriptableObject.CreateInstance<SanityConfig>();
            _assets.Add(_sanity);

            DarknessEvaluator.Config = _proto;
            DuskRecallSystem.Config = _proto;

            var clockGO = new GameObject("Clock");
            _spawned.Add(clockGO);
            _clock = clockGO.AddComponent<GameClock>();
            _clock.autoTick = false;
            _clock.Configure(_proto);

            var sanityGO = new GameObject("SanitySystem");
            _spawned.Add(sanityGO);
            _sanitySystem = sanityGO.AddComponent<SanitySystem>();
            _sanitySystem.autoTick = false;
            _sanitySystem.Config = _sanity;

            var defenseGO = new GameObject("HomeDefenseSystem");
            _spawned.Add(defenseGO);
            _defense = defenseGO.AddComponent<HomeDefenseSystem>();
            _defense.autoTick = false;
            _defense.Config = _combat;
            _defense.Sanity = _sanitySystem;
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
            ClearStatics();
        }

        static void ClearStatics()
        {
            EventBus.ResetAll();
            GameEventLog.Clear();
            DarknessEvaluator.Clear();
            DuskRecallSystem.Clear();
            Building.ClearRegistry();
            MonsterController.ClearRegistry();
            CombatConfig.ClearCache();
            SanityConfig.ClearCache();
            PrototypeConfig.ClearCache();
        }

        // ---- Helpers -----------------------------------------------------

        Building SpawnHome(Vector3 position, float hitPointMultiplier = 1f)
        {
            var go = new GameObject($"Home_{_spawned.Count}");
            _spawned.Add(go);
            go.transform.position = position;
            var b = go.AddComponent<Building>();
            b.Initialize(new BuildingType
            {
                id = "shelter_test",
                destructibleHome = true,
                homeHitPointMultiplier = hitPointMultiplier,
                footprint = new Vector2(3f, 3f),
            });
            return b;
        }

        VillagerAgent SpawnOccupant(Building home, Vector3 position, int seed)
        {
            var go = new GameObject($"Occupant_{_spawned.Count}");
            _spawned.Add(go);
            go.transform.position = position;
            var v = go.AddComponent<VillagerAgent>();
            v.autoTick = false;
            v.Config = _proto;
            v.seed = seed;
            v.Bravery = 0.5f;
            DuskRecallSystem.Register(v);
            _sanitySystem.AssignHome(v, home);
            return v;
        }

        MonsterController SpawnMonster(Vector3 position)
        {
            var go = new GameObject($"Monster_{_spawned.Count}");
            _spawned.Add(go);
            go.transform.position = position;
            var m = go.AddComponent<MonsterController>();
            m.autoTick = false;
            m.Configure(_proto);
            m.Combat = _combat;
            return m;
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

        static bool LogContains(string type, string dataFragment)
        {
            var records = GameEventLog.Records;
            for (int i = 0; i < records.Count; i++)
            {
                if (records[i].Type == type && records[i].Data.Contains(dataFragment))
                {
                    return true;
                }
            }
            return false;
        }

        // ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator DangerFarAway_HouseSleepsAllNight_NoSanityCost()
        {
            yield return null;

            var home = SpawnHome(Vector3.zero);
            var a = SpawnOccupant(home, Vector3.zero, seed: 1);
            var b = SpawnOccupant(home, new Vector3(0.3f, 0f, 0f), seed: 2);
            SpawnMonster(new Vector3(50f, 0f, 50f)); // far beyond wakeRadius; never ticked

            float sanityA = _sanitySystem.RecordFor(a).Sanity;
            float sanityB = _sanitySystem.RecordFor(b).Sanity;

            AdvanceClockTo(DayPhase.Night);
            for (int i = 0; i < 40; i++)
            {
                _defense.Tick(0.1f);
            }

            Assert.AreEqual(HomeDefenseState.Sleeping, _defense.StateOf(home),
                "with danger far away the house sleeps all night");
            Assert.IsFalse(home.IsFlaring, "no interior flare while asleep");
            Assert.IsFalse(LogContains("window_shot", home.name), "no volleys fired");
            Assert.AreEqual(sanityA, _sanitySystem.RecordFor(a).Sanity, 1e-4f,
                "a sleeping occupant spends no sanity");
            Assert.AreEqual(sanityB, _sanitySystem.RecordFor(b).Sanity, 1e-4f);
        }

        [UnityTest]
        public IEnumerator MonsterAtDoor_WakesHouse_Flares_FiresVolleys_CostsSanity()
        {
            yield return null;

            var home = SpawnHome(Vector3.zero);
            var a = SpawnOccupant(home, Vector3.zero, seed: 1);
            var b = SpawnOccupant(home, new Vector3(0.3f, 0f, 0f), seed: 2);
            var monster = SpawnMonster(new Vector3(2f, 0f, 0f)); // inside wakeRadius

            bool woke = false;
            EventBus.HomeWokeForDefense += _ => woke = true;

            float sanityBefore = _sanitySystem.RecordFor(a).Sanity;
            float monsterHpBefore = monster.Health;

            AdvanceClockTo(DayPhase.Night);
            _defense.Tick(0.5f); // wake + first volley(s)

            Assert.AreEqual(HomeDefenseState.Awake, _defense.StateOf(home), "the house woke");
            Assert.IsTrue(woke, "HomeWokeForDefense fired");
            Assert.IsTrue(home.IsFlaring, "the interior light flared");
            Assert.AreEqual(LightZone.Safe, DarknessEvaluator.Classify(home.transform.position),
                "the flared doorstep reclassifies Safe");
            Assert.Less(monster.Health, monsterHpBefore, "window volleys damage the monster");
            Assert.Less(_sanitySystem.RecordFor(a).Sanity, sanityBefore,
                "a woken occupant loses sanity per volley");
            Assert.Less(_sanitySystem.RecordFor(b).Sanity, sanityBefore,
                "every woken occupant pays the volley cost");
            Assert.IsTrue(LogContains("window_shot", home.name), "the volley is event-logged");
        }

        [UnityTest]
        public IEnumerator SafeFlare_DebuffsMonsterHomeDamage_ThroughResolver()
        {
            yield return null;

            var home = SpawnHome(Vector3.zero);
            SpawnOccupant(home, Vector3.zero, seed: 1);
            _combat.windowShotDamage = 0f; // isolate the structural-damage assertion
            var monster = SpawnMonster(new Vector3(1f, 0f, 0f)); // within attack range, inside the flare

            AdvanceClockTo(DayPhase.Night);
            _defense.Tick(0.1f); // wake + flare (door is Safe)
            Assert.IsTrue(home.IsFlaring);
            Assert.AreEqual(LightZone.Safe, DarknessEvaluator.Classify(monster.transform.position),
                "the monster stands in the flared Safe light");

            float hpBefore = home.HitPoints;
            monster.Tick(0.1f); // one door strike (cooldown gates the rest)

            float expected = _combat.monsterHomeAttackDamage * _combat.safeMonsterDamageMultiplier;
            Assert.AreEqual(hpBefore - expected, home.HitPoints, 1e-3f,
                "the monster's home damage is debuffed by the Safe band (resolver-scaled from config)");
            Assert.IsTrue(LogContains("monster_attacked_home", home.name));
        }

        [UnityTest]
        public IEnumerator OverwhelmedHome_IsRazed_OccupantsDead_LightNodeGone()
        {
            yield return null;

            // A fragile home and a hard-hitting monster: even Safe-debuffed strikes raze it.
            _combat.baseHomeHitPoints = 1f;
            _combat.monsterHomeAttackDamage = 10f;
            _combat.windowShotDamage = 1f; // window fire cannot save it in time

            var home = SpawnHome(Vector3.zero);
            var a = SpawnOccupant(home, Vector3.zero, seed: 1);
            var b = SpawnOccupant(home, new Vector3(0.3f, 0f, 0f), seed: 2);
            var monster = SpawnMonster(new Vector3(1f, 0f, 0f)); // already at the door

            bool razed = false;
            bool killed = false;
            GameObject razedHome = null;
            EventBus.HomeRazed += go => { razed = true; razedHome = go; };
            EventBus.SettlersKilledInHome += _ => killed = true;

            AdvanceClockTo(DayPhase.Night);
            for (int i = 0; i < 20 && !home.IsRazed; i++)
            {
                _defense.Tick(0.5f); // wake / flare / fire + sync occupants
                monster.Tick(0.5f);  // charge + batter the door
            }

            Assert.IsTrue(home.IsRazed, "the outer line broke: the home is razed");
            Assert.IsTrue(razed, "HomeRazed fired");
            Assert.IsTrue(killed, "SettlersKilledInHome fired");
            Assert.AreSame(home.gameObject, razedHome, "the razed-home event carried the home");
            Assert.AreEqual(VillagerState.Dead, a.State, "the settlers inside are killed");
            Assert.AreEqual(VillagerState.Dead, b.State);
            Assert.IsFalse(home.IsFlaring, "the light node is gone");
            Assert.AreEqual(LightZone.Dark, DarknessEvaluator.Classify(home.transform.position),
                "the razed home's doorstep reclassifies Dark (colonists and light node lost)");

            _defense.Tick(0.5f);
            Assert.AreEqual(HomeDefenseState.Razed, _defense.StateOf(home),
                "the home reads Razed on the debug/state surface");
            Assert.IsTrue(LogContains("HomeRazed", home.name),
                "the loss is on the morning-report event stream");
        }
    }
}
