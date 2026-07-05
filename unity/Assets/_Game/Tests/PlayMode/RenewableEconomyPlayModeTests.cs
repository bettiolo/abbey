using System.Collections;
using System.Collections.Generic;
using Abbey.Core;
using Abbey.Economy;
using Abbey.Villagers;
using Abbey.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Abbey.Tests.PlayMode
{
    /// <summary>
    /// The renewable economy under the play loop (P3-04), built programmatically (no
    /// scenes): a Farmer villager walks to a nearby field, staffs its work slot so the
    /// building counts it as a live worker, and the daily cycle deposits grain; and a
    /// field auto-advances its cycle off <see cref="EventBus.DayChanged"/> using the
    /// live <see cref="SeasonSystem"/> season. Components run with autoTick = false and
    /// are stepped with fixed dt; yields only prove it runs in Play mode.
    /// </summary>
    public class RenewableEconomyPlayModeTests
    {
        const float Dt = 0.05f;

        readonly List<GameObject> _spawned = new List<GameObject>();
        readonly List<ScriptableObject> _assets = new List<ScriptableObject>();

        [SetUp]
        public void SetUp()
        {
            EventBus.ResetAll();
            GameEventLog.Clear();
            ResourceLedger.Clear();
            ProductionBuilding.ClearRegistry();
            DuskRecallSystem.Clear();
            EconomyConfig.ClearCache();
            JobsConfig.ClearCache();
            SeasonConfig.ClearCache();
            PrototypeConfig.ClearCache();
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
            ProductionBuilding.ClearRegistry();
            ResourceLedger.Clear();
            DuskRecallSystem.Clear();
            EconomyConfig.ClearCache();
            JobsConfig.ClearCache();
            SeasonConfig.ClearCache();
            PrototypeConfig.ClearCache();
            EventBus.ResetAll();
            GameEventLog.Clear();
        }

        GameObject Track(GameObject go)
        {
            _spawned.Add(go);
            return go;
        }

        PrototypeConfig CreatePrototypeConfig()
        {
            var config = ScriptableObject.CreateInstance<PrototypeConfig>();
            _assets.Add(config);
            config.dayDurationSeconds = 10f;
            config.duskDurationSeconds = 5f;
            config.nightDurationSeconds = 10f;
            config.dawnDurationSeconds = 5f;
            config.arrivalRadius = 0.3f;
            config.villagerWalkSpeed = 3f;
            config.villagerFearPerSecondInDark = 0f;
            config.villagerFearPerSecondInEdge = 0f;
            DuskRecallSystem.Config = config;
            return config;
        }

        EconomyConfig CreateEconomyConfig()
        {
            var config = ScriptableObject.CreateInstance<EconomyConfig>();
            _assets.Add(config);
            config.baseStorageCapacity = 1000;
            config.springGrowthYield = 1f;
            config.summerGrowthYield = 1.5f;
            config.autumnGrowthYield = 2f;
            config.winterGrowthYield = 0f;
            config.productionRecipes = new List<ProductionRecipe>
            {
                new ProductionRecipe
                {
                    buildingId = "field_plot_t1",
                    seasonal = true,
                    workersRequired = 1,
                    cycleDays = 2f,
                    inputs = new List<ResourceStack>(),
                    outputs = new List<ResourceStack> { new ResourceStack(ResourceType.Grain, 3) },
                },
            };
            ResourceLedger.Config = config;
            return config;
        }

        ProductionBuilding CreateField(Vector3 pos, EconomyConfig econ, bool autoAdvance)
        {
            var go = Track(new GameObject("GrainField"));
            go.transform.position = pos;
            var pb = go.AddComponent<ProductionBuilding>();
            pb.autoAdvanceOnDayChanged = autoAdvance;
            pb.Initialize("field_plot_t1", econ);
            return pb;
        }

        VillagerJobAgent CreateFarmer(Vector3 pos, PrototypeConfig proto, JobsConfig jobs)
        {
            var go = Track(new GameObject($"Farmer_{_spawned.Count}"));
            go.transform.position = pos;
            var villager = go.AddComponent<VillagerAgent>();
            villager.autoTick = false;
            villager.Config = proto;
            villager.seed = 1;
            villager.Bravery = 0.5f;
            var agent = go.AddComponent<VillagerJobAgent>();
            agent.autoTick = false;
            agent.Config = jobs;
            agent.SetJob(VillagerJob.Farmer);
            return agent;
        }

        [UnityTest]
        public IEnumerator Farmer_WalksToField_StaffsIt_AndGrainHarvests()
        {
            var proto = CreatePrototypeConfig();
            var econ = CreateEconomyConfig();
            var jobs = ScriptableObject.CreateInstance<JobsConfig>();
            _assets.Add(jobs);

            var clockGO = Track(new GameObject("Clock"));
            var clock = clockGO.AddComponent<GameClock>();
            clock.autoTick = false;
            clock.Configure(proto); // Day, never ticked -> a work phase

            var field = CreateField(new Vector3(0f, 0f, 6f), econ, autoAdvance: false);
            var farmer = CreateFarmer(Vector3.zero, proto, jobs);
            var villager = farmer.Villager;

            float maxDistance = 0f;
            for (int i = 0; i < 600 && !farmer.IsStaffingProduction; i++)
            {
                villager.Tick(Dt);
                farmer.Tick(Dt);
                maxDistance = Mathf.Max(maxDistance,
                    PlanarMotion.Distance(villager.transform.position, Vector3.zero));
                if (i % 25 == 0)
                {
                    yield return null;
                }
            }

            Assert.IsTrue(farmer.IsStaffingProduction, "the farmer reached and staffed the field");
            Assert.AreSame(field, farmer.ProductionTarget);
            Assert.AreEqual(1, field.StaffedWorkers, "the building counts the farmer as a worker");
            Assert.Greater(maxDistance, 5f, "the farmer visibly walked out to the field");

            // Two staffed days complete the 2-day cycle and deposit grain.
            Assert.IsFalse(field.AdvanceDay(Season.Spring));
            Assert.IsTrue(field.AdvanceDay(Season.Spring));
            Assert.AreEqual(3, ResourceLedger.Get(ResourceType.Grain));

            // Unassigning the farmer empties the work slot.
            farmer.SetJob(VillagerJob.None);
            Assert.AreEqual(0, field.StaffedWorkers, "leaving the job vacates the field");
            Assert.IsFalse(field.AdvanceDay(Season.Spring), "an empty field produces nothing");
        }

        [UnityTest]
        public IEnumerator Field_AutoAdvances_OnDayChange_UsingLiveSeason()
        {
            var proto = CreatePrototypeConfig();
            var econ = CreateEconomyConfig();

            var clockGO = Track(new GameObject("Clock"));
            var clock = clockGO.AddComponent<GameClock>();
            clock.autoTick = false;
            clock.Configure(proto);

            var seasonCfg = ScriptableObject.CreateInstance<SeasonConfig>();
            _assets.Add(seasonCfg);
            var seasonGO = Track(new GameObject("SeasonSystem"));
            var season = seasonGO.AddComponent<SeasonSystem>();
            season.Configure(seasonCfg);
            Assert.AreEqual(Season.Spring, season.CurrentSeason);

            var field = CreateField(new Vector3(0f, 0f, 2f), econ, autoAdvance: true);
            field.SetStaff(1);

            // Each simulated day-change advances the cycle at the live (Spring) season.
            EventBus.RaiseDayChanged(2);
            yield return null;
            Assert.AreEqual(0, ResourceLedger.Get(ResourceType.Grain), "half a 2-day cycle");
            EventBus.RaiseDayChanged(3);
            yield return null;
            Assert.AreEqual(3, ResourceLedger.Get(ResourceType.Grain), "the field auto-harvested");
            Assert.AreEqual(1, field.CompletedCycles);
        }
    }
}
