using System.Collections.Generic;
using Abbey.Buildings;
using Abbey.Core;
using Abbey.Economy;
using Abbey.Light;
using Abbey.Nightmares;
using Abbey.Villagers;
using NUnit.Framework;
using UnityEngine;

namespace Abbey.Tests.EditMode
{
    public class Map1SystemsTestMapTests
    {
        readonly List<GameObject> _spawned = new List<GameObject>();
        ThreatConfig _threat;

        [SetUp]
        public void SetUp()
        {
            EventBus.ResetAll();
            GameEventLog.Clear();
            DuskRecallSystem.Clear();
            DarknessEvaluator.Clear();
            ThreatConfig.ClearCache();

            _threat = ScriptableObject.CreateInstance<ThreatConfig>();
            _threat.falseBellRadius = 20f;
            _threat.falseLightDistance = 5f;
            _threat.falseGuidanceFear = 0.2f;
            _threat.misdirectionLanternMultiplier = 0.5f;
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _spawned.Count; i++)
            {
                if (_spawned[i] != null)
                {
                    Object.DestroyImmediate(_spawned[i]);
                }
            }
            _spawned.Clear();
            Object.DestroyImmediate(_threat);
            EventBus.ResetAll();
            GameEventLog.Clear();
            DuskRecallSystem.Clear();
            DarknessEvaluator.Clear();
            ThreatConfig.ClearCache();
        }

        [Test]
        public void DefaultCatalog_IncludesFullForestBuildingSet()
        {
            var catalog = ScriptableObject.CreateInstance<BuildingCatalog>();
            try
            {
                string[] ids =
                {
                    "forester_hut_t1",
                    "herbalist_hut_t1",
                    "orchard_plot_t1",
                    "hunter_blind_t1",
                    "grove_shrine_t1",
                    "root_bridge_t1",
                    "charcoal_kiln_t1",
                    "stag_garden_t1",
                    "forest_watchpost_t1",
                    "abbey_cloister_repair",
                };

                for (int i = 0; i < ids.Length; i++)
                {
                    Assert.IsNotNull(catalog.Find(ids[i]), $"{ids[i]} is in the Map 1 systems-test catalog");
                }
            }
            finally
            {
                Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void DefaultEconomyRecipes_CoverForestResourceSet()
        {
            var economy = ScriptableObject.CreateInstance<EconomyConfig>();
            try
            {
                var produced = new HashSet<ResourceType>();
                string[] ids =
                {
                    "forester_hut_t1",
                    "herbalist_hut_t1",
                    "orchard_plot_t1",
                    "hunter_blind_t1",
                    "stag_garden_t1",
                    "charcoal_kiln_t1",
                };
                for (int i = 0; i < ids.Length; i++)
                {
                    var recipe = economy.RecipeFor(ids[i]);
                    Assert.IsNotNull(recipe, $"{ids[i]} has a default recipe");
                    for (int o = 0; o < recipe.outputs.Count; o++)
                    {
                        produced.Add(recipe.outputs[o].type);
                    }
                }

                Assert.IsTrue(produced.Contains(ResourceType.OldWood));
                Assert.IsTrue(produced.Contains(ResourceType.GreenWood));
                Assert.IsTrue(produced.Contains(ResourceType.Apples));
                Assert.IsTrue(produced.Contains(ResourceType.Venison));
                Assert.IsTrue(produced.Contains(ResourceType.Herbs));
                Assert.IsTrue(produced.Contains(ResourceType.Resin));
                Assert.IsTrue(produced.Contains(ResourceType.SacredSeeds));
                Assert.IsTrue(produced.Contains(ResourceType.Charcoal));
            }
            finally
            {
                Object.DestroyImmediate(economy);
            }
        }

        [Test]
        public void FalseBell_LuresVillager_AndTrueBellBreaksTheLure()
        {
            var guidance = SpawnGuidance();
            var villager = SpawnVillager(Vector3.zero);

            int affected = guidance.EmitFalseBell(new Vector3(2f, 0f, 0f), Vector3.zero, "test");

            Assert.AreEqual(1, affected);
            Assert.IsTrue(villager.IsFollowingFalseGuidance);
            Assert.AreEqual(VillagerState.ReturningToLight, villager.State);

            villager.OrderReturnToLight(bellBoosted: true);

            Assert.IsFalse(villager.IsFollowingFalseGuidance,
                "the True Bell clears the False Bell target");
            Assert.AreEqual(VillagerState.ReturningToLight, villager.State);
        }

        [Test]
        public void MisdirectionFog_HidesLanterns_ButNotSacredLights()
        {
            var guidance = SpawnGuidance();
            var lantern = SpawnLight("Lantern", Vector3.zero, sacred: false);
            var shrine = SpawnLight("Shrine", new Vector3(20f, 0f, 0f), sacred: true);

            guidance.RecordNightmareSpawn(NightmareType.RootWalker, Vector3.zero, Vector3.zero);

            Assert.IsTrue(guidance.FogActive);
            Assert.AreEqual(5f, DarknessEvaluator.EffectiveRadiusOf(lantern), 0.001f);
            Assert.AreEqual(10f, DarknessEvaluator.EffectiveRadiusOf(shrine), 0.001f);
        }

        FalseGuidanceSystem SpawnGuidance()
        {
            var go = new GameObject("FalseGuidance");
            _spawned.Add(go);
            var guidance = go.AddComponent<FalseGuidanceSystem>();
            guidance.Config = _threat;
            return guidance;
        }

        VillagerAgent SpawnVillager(Vector3 position)
        {
            var go = new GameObject("Villager");
            go.transform.position = position;
            _spawned.Add(go);
            var villager = go.AddComponent<VillagerAgent>();
            villager.autoTick = false;
            return villager;
        }

        LightSource SpawnLight(string name, Vector3 position, bool sacred)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            _spawned.Add(go);
            var light = go.AddComponent<LightSource>();
            light.radius = 10f;
            light.strength = 1f;
            light.sacred = sacred;
            light.isLit = true;
            light.fuelSeconds = -1f;
            return light;
        }
    }
}
