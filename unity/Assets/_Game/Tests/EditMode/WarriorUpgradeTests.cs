using System.Collections.Generic;
using Abbey.Combat;
using Abbey.Core;
using Abbey.Economy;
using Abbey.Light;
using Abbey.Nightmares;
using Abbey.Villagers;
using Abbey.World;
using NUnit.Framework;
using UnityEngine;

namespace Abbey.Tests.EditMode
{
    /// <summary>
    /// EditMode coverage for the warrior tier + night escalation (P3-06), all pure /
    /// data-driven so tested without play mode:
    /// the upgrade tree applies its stat deltas per config and debits the ledger; the
    /// escalation wave-budget curve is monotonic within a season and steps up in
    /// Autumn/Winter with periodic set-piece spikes; the dark-objective generator is
    /// deterministic for a fixed seed and always places the objective in the Dark.
    /// Worlds are built programmatically; configs are injected.
    /// </summary>
    public class WarriorUpgradeTests
    {
        readonly List<GameObject> _spawned = new List<GameObject>();
        PrototypeConfig _proto;
        CombatConfig _combat;

        [SetUp]
        public void SetUp()
        {
            ClearStatics();

            _proto = ScriptableObject.CreateInstance<PrototypeConfig>();
            _proto.edgeBandFraction = 0.3f;
            _proto.arrivalRadius = 0.3f;
            _proto.simulationSeed = 4242;
            _proto.monsterSpawnMinRadius = 10f;
            _proto.monsterSpawnMaxRadius = 20f;
            _proto.monsterSpawnAttempts = 32;

            _combat = ScriptableObject.CreateInstance<CombatConfig>();
            // Deterministic warrior base + a fresh two-tier tree (no reliance on defaults).
            _combat.warriorBaseMaxHealth = 50f;
            _combat.warriorBaseAttackDamage = 10f;
            _combat.warriorAttackCooldownSeconds = 0.8f;
            _combat.warriorBaseDarkSanityDrainFraction = 0.6f;
            _combat.warriorLodgeCapacity = 3;
            _combat.warriorRecruitTrustMultiplier = 1f;
            _combat.warriorUpgradeTiers = new List<WarriorUpgradeTier>
            {
                new WarriorUpgradeTier
                {
                    id = "blades", displayName = "Blades",
                    cost = new List<ResourceStack> { new ResourceStack(ResourceType.Tools, 2) },
                    damageDelta = 5f, attackCooldownDelta = -0.2f,
                },
                new WarriorUpgradeTier
                {
                    id = "plate", displayName = "Plate",
                    cost = new List<ResourceStack> { new ResourceStack(ResourceType.ScrapIron, 4) },
                    healthDelta = 30f, darkSanityResistDelta = 0.25f,
                },
            };

            // Escalation curve (explicit, monotonic base + season steps + set-piece).
            _combat.escalationBaseWaveBudget = 2f;
            _combat.escalationPerNightGrowth = 1f;
            _combat.escalationMonsterUnitCost = 1f;
            _combat.escalationMaxWaveMonsters = 50;
            _combat.escalationSpringMultiplier = 1f;
            _combat.escalationSummerMultiplier = 1.2f;
            _combat.escalationAutumnMultiplier = 1.6f;
            _combat.escalationWinterMultiplier = 2.2f;
            _combat.escalationSetPieceEveryNNights = 0; // off unless a test opts in
            _combat.escalationSetPieceMultiplier = 1.8f;

            DarknessEvaluator.Config = _proto;
            DuskRecallSystem.Config = _proto;
        }

        [TearDown]
        public void TearDown()
        {
            // Warriors are created inside WarriorStructure.Recruit (not tracked by the
            // test), so destroy any that linger before clearing the registries.
            var warriors = new List<WarriorAgent>(WarriorAgent.Active);
            foreach (var w in warriors)
            {
                if (w != null)
                {
                    Object.DestroyImmediate(w.gameObject);
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
            Object.DestroyImmediate(_proto);
            Object.DestroyImmediate(_combat);
            ClearStatics();
        }

        static void ClearStatics()
        {
            EventBus.ResetAll();
            GameEventLog.Clear();
            DarknessEvaluator.Clear();
            DuskRecallSystem.Clear();
            ResourceLedger.Clear();
            WarriorAgent.ClearRegistry();
            WarriorStructure.ClearRegistry();
            NightEscalationSystem.ResetStaticEvents();
            CombatConfig.ClearCache();
            PrototypeConfig.ClearCache();
        }

        WarriorStructure MakeLodge(Vector3 pos)
        {
            var go = new GameObject("Lodge");
            _spawned.Add(go);
            go.transform.position = pos;
            var s = go.AddComponent<WarriorStructure>();
            s.autoTick = false;
            s.role = WarriorStructureRole.Lodge;
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
            return v;
        }

        // ---- Upgrade tree stats -----------------------------------------

        [Test]
        public void StatsAtTier_Tier0_IsConfigBase()
        {
            var s = WarriorUpgrades.StatsAtTier(_combat, 0);
            Assert.AreEqual(50f, s.MaxHealth, 1e-4f);
            Assert.AreEqual(10f, s.AttackDamage, 1e-4f);
            Assert.AreEqual(0.8f, s.AttackCooldownSeconds, 1e-4f);
            Assert.AreEqual(0.6f, s.DarkSanityDrainFraction, 1e-4f);
        }

        [Test]
        public void StatsAtTier_AppliesTierDeltasCumulatively()
        {
            var t1 = WarriorUpgrades.StatsAtTier(_combat, 1);
            Assert.AreEqual(15f, t1.AttackDamage, 1e-4f, "tier 1 adds +5 damage");
            Assert.AreEqual(0.6f, t1.AttackCooldownSeconds, 1e-4f, "tier 1 is 0.2s faster");
            Assert.AreEqual(50f, t1.MaxHealth, 1e-4f, "tier 1 does not touch health");

            var t2 = WarriorUpgrades.StatsAtTier(_combat, 2);
            Assert.AreEqual(15f, t2.AttackDamage, 1e-4f, "damage carries from tier 1");
            Assert.AreEqual(80f, t2.MaxHealth, 1e-4f, "tier 2 adds +30 health");
            Assert.AreEqual(0.35f, t2.DarkSanityDrainFraction, 1e-4f, "tier 2 hardens vs the dark");
        }

        [Test]
        public void StatsAtTier_ClampsToTreeLength()
        {
            var maxed = WarriorUpgrades.StatsAtTier(_combat, 99);
            var lastTier = WarriorUpgrades.StatsAtTier(_combat, 2);
            Assert.AreEqual(lastTier.MaxHealth, maxed.MaxHealth, 1e-4f);
            Assert.AreEqual(lastTier.AttackDamage, maxed.AttackDamage, 1e-4f);
        }

        [Test]
        public void TryUpgrade_DebitsLedger_AndRaisesRosterStats()
        {
            ResourceLedger.Add(ResourceType.Tools, 5, "test");
            var lodge = MakeLodge(Vector3.zero);
            var warrior = lodge.Recruit(MakeVillager(new Vector3(1f, 0f, 0f), 1));
            Assert.IsNotNull(warrior);
            Assert.AreEqual(10f, warrior.Stats.AttackDamage, 1e-4f, "starts at base");

            bool ok = lodge.TryUpgrade();

            Assert.IsTrue(ok, "the settlement could afford tier 1");
            Assert.AreEqual(1, lodge.AppliedTierCount);
            Assert.AreEqual(3, ResourceLedger.Get(ResourceType.Tools), "tier cost debited (5-2)");
            Assert.AreEqual(15f, warrior.Stats.AttackDamage, 1e-4f,
                "the housed warrior's stats rose with the lodge upgrade");
        }

        [Test]
        public void TryUpgrade_FailsWhenUnaffordable_LedgerUntouched()
        {
            // No tools in the ledger: the first tier is unaffordable.
            var lodge = MakeLodge(Vector3.zero);
            bool ok = lodge.TryUpgrade();
            Assert.IsFalse(ok, "cannot buy a tier with an empty ledger");
            Assert.AreEqual(0, lodge.AppliedTierCount);
            Assert.AreEqual(0, ResourceLedger.Get(ResourceType.Tools), "nothing spent");
        }

        [Test]
        public void Recruit_PromotesVillager_PopulationConserved()
        {
            var lodge = MakeLodge(Vector3.zero);
            var villager = MakeVillager(new Vector3(1f, 0f, 0f), 7);
            DuskRecallSystem.Register(villager);
            Assert.AreEqual(1, DuskRecallSystem.Villagers.Count);

            var warrior = lodge.Recruit(villager);

            Assert.IsNotNull(warrior);
            Assert.AreSame(villager, warrior.SourceVillager);
            Assert.AreEqual(1, lodge.Roster.Count);
            Assert.IsFalse(villager.gameObject.activeSelf,
                "the promoted villager leaves the settler pool (population conservation)");
            Assert.AreEqual(0, DuskRecallSystem.Villagers.Count,
                "and unregisters from the dusk-recall roster");
        }

        [Test]
        public void Recruit_StopsAtEffectiveCapacity()
        {
            _combat.warriorLodgeCapacity = 2;
            _combat.warriorRecruitTrustMultiplier = 1f;
            var lodge = MakeLodge(Vector3.zero);
            Assert.AreEqual(2, lodge.EffectiveCapacity);
            Assert.IsNotNull(lodge.Recruit(MakeVillager(new Vector3(1f, 0f, 0f), 1)));
            Assert.IsNotNull(lodge.Recruit(MakeVillager(new Vector3(2f, 0f, 0f), 2)));
            Assert.IsNull(lodge.Recruit(MakeVillager(new Vector3(3f, 0f, 0f), 3)),
                "a full lodge recruits no more");
        }

        // ---- Escalation curve -------------------------------------------

        [Test]
        public void WaveBudget_MonotonicWithinSeason()
        {
            float prev = -1f;
            for (int night = 1; night <= 6; night++)
            {
                float budget = NightEscalationSystem.WaveBudget(_combat, night, Season.Autumn);
                Assert.GreaterOrEqual(budget, prev,
                    $"night {night} budget must not fall below night {night - 1}");
                prev = budget;
            }
        }

        [Test]
        public void SeasonMultipliers_StepUpTowardWinter()
        {
            float spring = NightEscalationSystem.WaveBudget(_combat, 4, Season.Spring);
            float summer = NightEscalationSystem.WaveBudget(_combat, 4, Season.Summer);
            float autumn = NightEscalationSystem.WaveBudget(_combat, 4, Season.Autumn);
            float winter = NightEscalationSystem.WaveBudget(_combat, 4, Season.Winter);
            Assert.Greater(summer, spring, "summer presses harder than spring");
            Assert.Greater(autumn, summer, "autumn is the warning");
            Assert.Greater(winter, autumn, "winter is judgment");
        }

        [Test]
        public void SetPieceNights_SpikeTheWave()
        {
            _combat.escalationSetPieceEveryNNights = 3;

            Assert.IsFalse(NightEscalationSystem.IsSetPieceNight(_combat, 2));
            Assert.IsTrue(NightEscalationSystem.IsSetPieceNight(_combat, 3));
            Assert.IsTrue(NightEscalationSystem.IsSetPieceNight(_combat, 6));

            float baseBudget = NightEscalationSystem.WaveBudget(_combat, 3, Season.Spring);
            float finalBudget = NightEscalationSystem.FinalWaveBudget(_combat, 3, Season.Spring);
            Assert.AreEqual(baseBudget * 1.8f, finalBudget, 1e-3f,
                "a set-piece night multiplies the wave budget");

            int normal = NightEscalationSystem.WaveMonsterCount(_combat, 2, Season.Spring);
            int setPiece = NightEscalationSystem.WaveMonsterCount(_combat, 3, Season.Spring);
            Assert.Greater(setPiece, normal, "the set-piece stand spawns a larger wave");
        }

        [Test]
        public void WaveMonsterCount_ClampsToConfigCap()
        {
            _combat.escalationMaxWaveMonsters = 4;
            int count = NightEscalationSystem.WaveMonsterCount(_combat, 100, Season.Winter);
            Assert.AreEqual(4, count, "the wave is capped by config");
        }

        // ---- Dark objective generator -----------------------------------

        [Test]
        public void DarkObjective_FixedSeed_ReproducesSequence()
        {
            for (int night = 1; night <= 4; night++)
            {
                bool a = DarkObjectiveGenerator.TryGenerate(
                    1234, night, Vector3.zero, 10f, 20f, 32, out var oa);
                bool b = DarkObjectiveGenerator.TryGenerate(
                    1234, night, Vector3.zero, 10f, 20f, 32, out var ob);
                Assert.IsTrue(a && b, $"night {night} generated");
                Assert.AreEqual(oa.Kind, ob.Kind, $"night {night} kind is reproducible");
                Assert.AreEqual(oa.Location, ob.Location, $"night {night} location is reproducible");
            }
        }

        [Test]
        public void DarkObjective_LocationAlwaysClassifiesDark()
        {
            for (int night = 1; night <= 8; night++)
            {
                Assert.IsTrue(DarkObjectiveGenerator.TryGenerate(
                    777, night, Vector3.zero, 10f, 20f, 32, out var obj), $"night {night} generated");
                Assert.AreEqual(LightZone.Dark, DarknessEvaluator.Classify(obj.Location),
                    $"night {night} objective sits outside all Safe light");
            }
        }

        [Test]
        public void DarkObjective_DiffersAcrossNights()
        {
            DarkObjectiveGenerator.TryGenerate(55, 1, Vector3.zero, 10f, 20f, 32, out var n1);
            DarkObjectiveGenerator.TryGenerate(55, 2, Vector3.zero, 10f, 20f, 32, out var n2);
            Assert.AreNotEqual(n1.Location, n2.Location,
                "successive nights place their objective differently");
        }
    }
}
