using System.Collections.Generic;
using Abbey.Buildings;
using Abbey.Combat;
using Abbey.Core;
using Abbey.Economy;
using Abbey.Light;
using Abbey.Villagers;
using NUnit.Framework;
using UnityEngine;

namespace Abbey.Tests.EditMode
{
    /// <summary>
    /// The light-band combat resolver (P3-05) is pure, so it is tested exhaustively:
    /// Safe debuffs monsters, Edge is even, Dark debuffs friendlies and drains their
    /// sanity, and the beast is exempt in every band. Every multiplier is read from
    /// the injected <see cref="CombatConfig"/> (varied per test) — never a constant.
    /// The destructible-home mechanics (flare ⇒ Safe door, raze ⇒ occupants dead +
    /// light node gone) are covered here too since <see cref="Building"/> works in
    /// EditMode. Worlds are built programmatically; configs are injected.
    /// </summary>
    public class LightBandCombatTests
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

            _combat = ScriptableObject.CreateInstance<CombatConfig>();
            _combat.safeMonsterDamageMultiplier = 0.35f;
            _combat.edgeMonsterDamageMultiplier = 1f;
            _combat.darkMonsterDamageMultiplier = 1f;
            _combat.safeFriendlyDamageMultiplier = 1f;
            _combat.edgeFriendlyDamageMultiplier = 1f;
            _combat.darkFriendlyDamageMultiplier = 0.5f;
            _combat.darkFriendlySanityDrainPerSecond = 0.05f;
            _combat.baseHomeHitPoints = 30f;
            _combat.flareLightRadius = 5f;
            _combat.flareLightStrength = 1f;

            DarknessEvaluator.Config = _proto;
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
            Building.ClearRegistry();
            CombatConfig.ClearCache();
            PrototypeConfig.ClearCache();
        }

        // ---- Resolver: band multipliers ---------------------------------

        [Test]
        public void Monster_Debuffed_InSafe_Even_InEdge_And_Dark()
        {
            var safe = LightBandCombatResolver.Resolve(CombatSide.Monster, false, LightZone.Safe, _combat);
            var edge = LightBandCombatResolver.Resolve(CombatSide.Monster, false, LightZone.Edge, _combat);
            var dark = LightBandCombatResolver.Resolve(CombatSide.Monster, false, LightZone.Dark, _combat);

            Assert.AreEqual(_combat.safeMonsterDamageMultiplier, safe.DamageMultiplier, 1e-4f,
                "a monster's damage is debuffed in Safe light");
            Assert.Less(safe.DamageMultiplier, edge.DamageMultiplier, "Safe debuffs the monster below Edge");
            Assert.AreEqual(1f, edge.DamageMultiplier, 1e-4f, "Edge is even for monsters");
            Assert.AreEqual(1f, dark.DamageMultiplier, 1e-4f, "Dark is the monster's element (even)");
            Assert.AreEqual(0f, safe.SanityDrainPerSecond, 1e-4f, "monsters have no sanity to drain");
            Assert.AreEqual(0f, dark.SanityDrainPerSecond, 1e-4f);
        }

        [Test]
        public void Friendly_Even_InSafe_And_Edge_Debuffed_And_Drained_InDark()
        {
            var safe = LightBandCombatResolver.Resolve(CombatSide.Friendly, false, LightZone.Safe, _combat);
            var edge = LightBandCombatResolver.Resolve(CombatSide.Friendly, false, LightZone.Edge, _combat);
            var dark = LightBandCombatResolver.Resolve(CombatSide.Friendly, false, LightZone.Dark, _combat);

            Assert.AreEqual(1f, safe.DamageMultiplier, 1e-4f, "friendlies are even in Safe");
            Assert.AreEqual(1f, edge.DamageMultiplier, 1e-4f, "friendlies are even in Edge");
            Assert.AreEqual(_combat.darkFriendlyDamageMultiplier, dark.DamageMultiplier, 1e-4f,
                "friendlies are debuffed in the Dark");
            Assert.Less(dark.DamageMultiplier, edge.DamageMultiplier, "Dark debuffs the friendly below Edge");

            Assert.AreEqual(0f, safe.SanityDrainPerSecond, 1e-4f, "no drain in Safe");
            Assert.AreEqual(0f, edge.SanityDrainPerSecond, 1e-4f, "no drain in Edge");
            Assert.Greater(dark.SanityDrainPerSecond, 0f, "fighting in the Dark drains a friendly's sanity");
            Assert.AreEqual(_combat.darkFriendlySanityDrainPerSecond, dark.SanityDrainPerSecond, 1e-4f);
        }

        [Test]
        public void Beast_IsExempt_InEveryBand_ForBothSides()
        {
            foreach (var side in new[] { CombatSide.Monster, CombatSide.Friendly })
            {
                foreach (var band in new[] { LightZone.Safe, LightZone.Edge, LightZone.Dark })
                {
                    var t = LightBandCombatResolver.Resolve(side, true, band, _combat);
                    Assert.IsTrue(t.BeastExempt, $"beast is exempt ({side}/{band})");
                    Assert.AreEqual(1f, t.DamageMultiplier, 1e-4f,
                        $"the beast takes no band damage penalty ({side}/{band})");
                    Assert.AreEqual(0f, t.SanityDrainPerSecond, 1e-4f,
                        $"the beast takes no sanity drain ({side}/{band})");
                }
            }
        }

        [Test]
        public void Multipliers_ComeFromConfig_NotConstants()
        {
            var other = ScriptableObject.CreateInstance<CombatConfig>();
            other.safeMonsterDamageMultiplier = 0.1f;
            other.darkFriendlyDamageMultiplier = 0.2f;
            other.darkFriendlySanityDrainPerSecond = 0.3f;

            var m = LightBandCombatResolver.Resolve(CombatSide.Monster, false, LightZone.Safe, other);
            var f = LightBandCombatResolver.Resolve(CombatSide.Friendly, false, LightZone.Dark, other);
            Assert.AreEqual(0.1f, m.DamageMultiplier, 1e-4f, "the resolver reads the injected config");
            Assert.AreEqual(0.2f, f.DamageMultiplier, 1e-4f);
            Assert.AreEqual(0.3f, f.SanityDrainPerSecond, 1e-4f);

            // The same call against the SetUp config differs — proof it is data, not a constant.
            var mBase = LightBandCombatResolver.Resolve(CombatSide.Monster, false, LightZone.Safe, _combat);
            Assert.AreNotEqual(m.DamageMultiplier, mBase.DamageMultiplier,
                "two configs give two answers");

            Object.DestroyImmediate(other);
        }

        [Test]
        public void ResolveAt_ClassifiesThroughDarknessEvaluator()
        {
            SpawnLight(Vector3.zero, 10f); // Safe near the center, Dark far away

            var safe = LightBandCombatResolver.ResolveAt(CombatSide.Monster, false, Vector3.zero, _combat);
            var dark = LightBandCombatResolver.ResolveAt(
                CombatSide.Monster, false, new Vector3(100f, 0f, 100f), _combat);
            Assert.AreEqual(LightZone.Safe, safe.Band, "at the light: Safe");
            Assert.AreEqual(_combat.safeMonsterDamageMultiplier, safe.DamageMultiplier, 1e-4f);
            Assert.AreEqual(LightZone.Dark, dark.Band, "far away: Dark");
            Assert.AreEqual(1f, dark.DamageMultiplier, 1e-4f);
        }

        [Test]
        public void HomeHitPoints_ScaleByBuildingTypeMultiplier()
        {
            var plain = new BuildingType { id = "h1", destructibleHome = true, homeHitPointMultiplier = 1f };
            var sturdy = new BuildingType { id = "h2", destructibleHome = true, homeHitPointMultiplier = 2f };
            Assert.AreEqual(_combat.baseHomeHitPoints, _combat.HomeHitPointsFor(plain), 1e-4f);
            Assert.AreEqual(_combat.baseHomeHitPoints * 2f, _combat.HomeHitPointsFor(sturdy), 1e-4f,
                "a sturdier home takes more to raze (per-type multiplier)");
        }

        // ---- Destructible home: flare + raze -----------------------------

        [Test]
        public void Flare_MakesDoorstep_Safe_ForMonsters()
        {
            var home = SpawnHome(Vector3.zero);
            Assert.AreEqual(LightZone.Dark, DarknessEvaluator.Classify(home.transform.position),
                "an unlit home doorstep is Dark");

            home.FlareOn(_combat.flareLightRadius, _combat.flareLightStrength);
            Assert.IsTrue(home.IsFlaring, "the interior light flares on wakeup");
            Assert.AreEqual(LightZone.Safe, DarknessEvaluator.Classify(home.transform.position),
                "the flared interior light makes the doorstep Safe (monsters are then debuffed)");
        }

        [Test]
        public void Raze_KillsOccupants_RemovesLightNode_AndFiresEvents()
        {
            var home = SpawnHome(Vector3.zero);
            home.InitializeDefense(_combat.baseHomeHitPoints);
            var a = SpawnVillager(new Vector3(0.2f, 0f, 0f));
            var b = SpawnVillager(new Vector3(-0.2f, 0f, 0f));
            home.AddOccupant(a);
            home.AddOccupant(b);
            home.FlareOn(_combat.flareLightRadius, _combat.flareLightStrength);

            bool razed = false;
            bool killed = false;
            EventBus.HomeRazed += _ => razed = true;
            EventBus.SettlersKilledInHome += _ => killed = true;

            // Batter the home to zero hit points.
            home.TakeStructuralDamage(_combat.baseHomeHitPoints * 0.5f);
            Assert.IsFalse(home.IsRazed, "half damage does not raze it");
            home.TakeStructuralDamage(_combat.baseHomeHitPoints);

            Assert.IsTrue(home.IsRazed, "zero hit points razes the home");
            Assert.AreEqual(VillagerState.Dead, a.State, "occupants are killed");
            Assert.AreEqual(VillagerState.Dead, b.State);
            Assert.IsTrue(razed, "HomeRazed fires");
            Assert.IsTrue(killed, "SettlersKilledInHome fires");
            Assert.IsFalse(home.IsFlaring, "the interior light node is gone");
            Assert.AreEqual(LightZone.Dark, DarknessEvaluator.Classify(home.transform.position),
                "the razed home's doorstep reclassifies Dark (light node lost)");
            Assert.IsTrue(LogContains("HomeRazed", home.name));
        }

        [Test]
        public void TakeStructuralDamage_IgnoredOnNonHome()
        {
            var go = new GameObject("NotAHome");
            _spawned.Add(go);
            var b = go.AddComponent<Building>();
            b.Initialize(new BuildingType { id = "store", destructibleHome = false });
            b.TakeStructuralDamage(1000f);
            Assert.IsFalse(b.IsRazed, "a non-home building is not destructible under night assault");
        }

        // ---- Helpers -----------------------------------------------------

        LightSource SpawnLight(Vector3 position, float radius)
        {
            var go = new GameObject($"Light_{_spawned.Count}");
            _spawned.Add(go);
            go.transform.position = position;
            var s = go.AddComponent<LightSource>();
            s.radius = radius;
            s.strength = 1f;
            s.isLit = true;
            s.fuelSeconds = -1f;
            s.autoTick = false;
            DarknessEvaluator.Register(s);
            return s;
        }

        Building SpawnHome(Vector3 position)
        {
            var go = new GameObject($"Home_{_spawned.Count}");
            _spawned.Add(go);
            go.transform.position = position;
            var b = go.AddComponent<Building>();
            b.Initialize(new BuildingType
            {
                id = "shelter_test",
                destructibleHome = true,
                homeHitPointMultiplier = 1f,
                footprint = new Vector2(3f, 3f),
            });
            return b;
        }

        VillagerAgent SpawnVillager(Vector3 position)
        {
            var go = new GameObject($"Villager_{_spawned.Count}");
            _spawned.Add(go);
            go.transform.position = position;
            var v = go.AddComponent<VillagerAgent>();
            v.autoTick = false;
            v.Config = _proto;
            DuskRecallSystem.Register(v);
            return v;
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
    }
}
