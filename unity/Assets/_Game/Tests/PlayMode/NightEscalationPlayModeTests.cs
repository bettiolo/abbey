using System.Collections;
using System.Collections.Generic;
using Abbey.Combat;
using Abbey.Core;
using Abbey.Light;
using Abbey.Nightmares;
using Abbey.Villagers;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Abbey.Tests.PlayMode
{
    /// <summary>
    /// PlayMode coverage for the warrior tier + night escalation (P3-06), worlds built
    /// programmatically with deterministic manual ticks:
    /// a mustered warrior engages a spawned monster out in the Dark band and wins per
    /// its stat config while paying the Dark sanity drain; a warrior that reaches the
    /// nightly dark objective clears it with no penalty; an unsolved objective applies
    /// its configured consequence (a villager lost to the dark); and a set-piece night
    /// spawns the larger wave the escalation curve prescribes.
    /// </summary>
    public class NightEscalationPlayModeTests
    {
        readonly List<GameObject> _spawned = new List<GameObject>();
        readonly List<ScriptableObject> _assets = new List<ScriptableObject>();
        PrototypeConfig _proto;
        CombatConfig _combat;
        GameClock _clock;

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
            _proto.simulationSeed = 9001;
            _proto.monsterMaxHealth = 30f;
            _proto.monsterMoveSpeed = 2f;
            _proto.monsterSpawnMinRadius = 10f;
            _proto.monsterSpawnMaxRadius = 20f;
            _proto.monsterSpawnAttempts = 32;

            _combat = ScriptableObject.CreateInstance<CombatConfig>();
            _assets.Add(_combat);
            _combat.safeFriendlyDamageMultiplier = 1f;
            _combat.edgeFriendlyDamageMultiplier = 1f;
            _combat.darkFriendlyDamageMultiplier = 0.5f;
            _combat.darkFriendlySanityDrainPerSecond = 0.1f;
            _combat.warriorBaseMaxHealth = 80f;
            _combat.warriorBaseAttackDamage = 200f; // one Dark hit (×0.5) still fells a 30hp monster
            _combat.warriorAttackRange = 1.6f;
            _combat.warriorAttackCooldownSeconds = 0.1f;
            _combat.warriorMoveSpeed = 8f;
            _combat.warriorSightRange = 40f;
            _combat.warriorObjectiveSolveRadius = 1.5f;
            _combat.warriorBaseDarkSanityDrainFraction = 0.6f;
            _combat.warriorLodgeCapacity = 3;
            _combat.warriorRecruitTrustMultiplier = 1f;
            _combat.warriorPatrolRadius = 12f;
            _combat.warriorUpgradeTiers = new List<WarriorUpgradeTier>();
            // Escalation curve for the set-piece test.
            _combat.escalationBaseWaveBudget = 2f;
            _combat.escalationPerNightGrowth = 1f;
            _combat.escalationMonsterUnitCost = 1f;
            _combat.escalationMaxWaveMonsters = 50;
            _combat.escalationSpringMultiplier = 1f;
            _combat.escalationSetPieceEveryNNights = 2;
            _combat.escalationSetPieceMultiplier = 1.8f;

            DarknessEvaluator.Config = _proto;
            DuskRecallSystem.Config = _proto;

            var clockGO = new GameObject("Clock");
            _spawned.Add(clockGO);
            _clock = clockGO.AddComponent<GameClock>();
            _clock.autoTick = false;
            _clock.Configure(_proto);
        }

        [TearDown]
        public void TearDown()
        {
            // Warriors are created inside WarriorStructure.Recruit (untracked); destroy
            // any that linger, plus a dangling dark-objective marker.
            var warriors = new List<WarriorAgent>(WarriorAgent.Active);
            foreach (var w in warriors)
            {
                if (w != null)
                {
                    Object.DestroyImmediate(w.gameObject);
                }
            }
            if (NightEscalationSystem.Instance != null
                && NightEscalationSystem.Instance.ActiveMarker != null)
            {
                Object.DestroyImmediate(NightEscalationSystem.Instance.ActiveMarker.gameObject);
            }
            // Monsters spawned inside NightmareDirector are also untracked by the test.
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
            MonsterController.ClearRegistry();
            WarriorAgent.ClearRegistry();
            WarriorStructure.ClearRegistry();
            NightEscalationSystem.ResetStaticEvents();
            CombatConfig.ClearCache();
            PrototypeConfig.ClearCache();
        }

        // ---- Helpers -----------------------------------------------------

        WarriorStructure MakeLodge(Vector3 pos, WarriorStructureRole role = WarriorStructureRole.Lodge)
        {
            var go = new GameObject("Lodge");
            _spawned.Add(go);
            go.transform.position = pos;
            var s = go.AddComponent<WarriorStructure>();
            s.autoTick = false;
            s.role = role;
            s.Configure(_combat, _proto);
            return s;
        }

        VillagerAgent MakeVillager(Vector3 pos, int seed)
        {
            var go = new GameObject($"Villager_{_spawned.Count}");
            _spawned.Add(go);
            go.transform.position = pos;
            var v = go.AddComponent<VillagerAgent>();
            v.autoTick = false;
            v.Config = _proto;
            v.seed = seed;
            DuskRecallSystem.Register(v);
            return v;
        }

        MonsterController SpawnMonster(Vector3 pos)
        {
            var go = new GameObject($"Monster_{_spawned.Count}");
            _spawned.Add(go);
            go.transform.position = pos;
            var m = go.AddComponent<MonsterController>();
            m.autoTick = false;
            m.Configure(_proto);
            m.Combat = _combat;
            return m;
        }

        NightEscalationSystem MakeEscalation()
        {
            var go = new GameObject("NightEscalation");
            _spawned.Add(go);
            var esc = go.AddComponent<NightEscalationSystem>();
            esc.Combat = _combat;
            esc.Config = _proto;
            return esc;
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
        public IEnumerator Warrior_MustersAndEngagesInDark_WinsButDrainsSanity()
        {
            yield return null;

            var lodge = MakeLodge(Vector3.zero);
            var warrior = lodge.Recruit(MakeVillager(new Vector3(1f, 0f, 0f), 1));
            Assert.IsNotNull(warrior);
            warrior.autoTick = false;

            var monster = SpawnMonster(new Vector3(4f, 0f, 0f));
            float sanityBefore = warrior.Sanity;

            AdvanceClockTo(DayPhase.Night);
            Assert.AreEqual(LightZone.Dark, DarknessEvaluator.Classify(warrior.transform.position),
                "the warrior musters out in the Dark band");

            for (int i = 0; i < 40 && monster.IsAlive; i++)
            {
                warrior.Tick(0.2f);
            }

            Assert.IsFalse(monster.IsAlive, "the warrior wins the fight per its stat config");
            Assert.IsTrue(LogContains("warrior_mustered", warrior.name), "the warrior mustered at dusk/night");
            Assert.IsTrue(LogContains("warrior_killed_monster", warrior.name), "the kill is event-logged");
            Assert.Less(warrior.Sanity, sanityBefore,
                "fighting in the Dark drains the warrior's sanity (it is not band-exempt)");
        }

        [UnityTest]
        public IEnumerator DarkObjective_SolvedByWarrior_ClearsWithoutPenalty()
        {
            yield return null;

            var esc = MakeEscalation();
            var lodge = MakeLodge(Vector3.zero);
            var warrior = lodge.Recruit(MakeVillager(new Vector3(1f, 0f, 0f), 1));
            warrior.autoTick = false;

            bool solved = false;
            bool failed = false;
            NightEscalationSystem.DarkObjectiveSolved += _ => solved = true;
            NightEscalationSystem.DarkObjectiveFailed += _ => failed = true;

            var marker = esc.SpawnObjective(
                new DarkObjective(DarkObjectiveKind.DownedVillager, new Vector3(4f, 0f, 3f)));
            Assert.IsTrue(marker.IsRevealed, "with no watchtower the objective is revealed by default");

            AdvanceClockTo(DayPhase.Night);
            for (int i = 0; i < 40 && !marker.IsSolved; i++)
            {
                warrior.Tick(0.2f);
            }

            Assert.IsTrue(marker.IsSolved, "the warrior reached and cleared the dark objective");
            Assert.IsTrue(solved, "DarkObjectiveSolved fired");
            Assert.AreSame(warrior, marker.SolvedBy);

            esc.ResolveNight();
            Assert.IsFalse(failed, "a solved objective applies no consequence at dawn");
            Assert.IsTrue(LogContains("dark_objective", "solved"), "the solve is on the event stream");
        }

        [UnityTest]
        public IEnumerator DarkObjective_Unsolved_AppliesConsequence()
        {
            yield return null;

            var esc = MakeEscalation();
            var objectivePos = new Vector3(6f, 0f, 6f);
            var lost = MakeVillager(objectivePos + new Vector3(0.5f, 0f, 0f), seed: 3);
            Assert.AreEqual(LightZone.Dark, DarknessEvaluator.Classify(lost.transform.position),
                "the imperilled villager is out beyond the light");

            bool failed = false;
            NightEscalationSystem.DarkObjectiveFailed += _ => failed = true;

            esc.SpawnObjective(new DarkObjective(DarkObjectiveKind.DownedVillager, objectivePos));

            // No warrior comes: dawn resolves the objective unanswered.
            esc.ResolveNight();

            Assert.IsTrue(failed, "an unsolved objective fires DarkObjectiveFailed");
            Assert.AreEqual(VillagerState.Missing, lost.State,
                "the downed villager is lost to the dark (the configured consequence)");
            Assert.IsTrue(LogContains("dark_objective", "consequence=villager_lost"),
                "the loss is on the morning-report event stream");
        }

        [UnityTest]
        public IEnumerator SetPieceNight_SpawnsLargerWave_FromCurve()
        {
            yield return null;

            _proto.phase3NightsEnabled = true;

            var esc = MakeEscalation();
            var directorGO = new GameObject("Director");
            _spawned.Add(directorGO);
            directorGO.transform.position = Vector3.zero;
            var director = directorGO.AddComponent<NightmareDirector>();
            director.autoTick = false;
            director.monstersAutoTick = false;
            director.Config = _proto;
            director.Escalation = esc;

            // Night 1 (standard): base budget 2 -> 2 monsters.
            director.BeginNight();
            int night1 = director.SpawnedMonsters.Count;
            Assert.IsFalse(esc.IsSetPieceTonight, "night 1 is a standard night");
            Assert.AreEqual(esc.TonightMonsterCount, night1,
                "night 1 spawns exactly the curve's wave count");
            director.EndNight();

            // Night 2 (set-piece, every 2nd night): a larger wave.
            director.BeginNight();
            int night2 = director.SpawnedMonsters.Count;
            Assert.IsTrue(esc.IsSetPieceTonight, "night 2 is a set-piece stand");
            Assert.Greater(night2, night1, "the set-piece night spawns the larger wave from the curve");
            director.EndNight();
        }
    }
}
