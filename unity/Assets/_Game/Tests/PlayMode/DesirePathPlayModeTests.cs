using System.Collections;
using System.Collections.Generic;
using Abbey.Core;
using Abbey.Economy;
using Abbey.Light;
using Abbey.Settlement;
using Abbey.Villagers;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Abbey.Tests.PlayMode
{
    /// <summary>
    /// Emergent roads end-to-end (P3-12), built programmatically: a salvager runs its
    /// site -> storage loop for several fast days with no player input, and the
    /// TrafficGrid wears a desire path (tier &gt; 0) along the commute while untouched
    /// ground beside it stays clean. Manual fixed-dt ticks, bounded loop counters.
    /// </summary>
    public class DesirePathPlayModeTests
    {
        const float Dt = 0.05f;

        readonly List<GameObject> _spawned = new List<GameObject>();
        readonly List<ScriptableObject> _assets = new List<ScriptableObject>();

        [SetUp]
        public void SetUp()
        {
            EventBus.ResetAll();
            GameEventLog.Clear();
            DarknessEvaluator.Clear();
            DuskRecallSystem.Clear();
            ResourceLedger.Clear();
            SalvageSite.ClearRegistry();
            JobWorkPoint.ClearRegistry();
            PlanarMotion.ResetHooks();
            EconomyConfig.ClearCache();
            JobsConfig.ClearCache();
            PrototypeConfig.ClearCache();
            PathsConfig.ClearCache();
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
            SalvageSite.ClearRegistry();
            JobWorkPoint.ClearRegistry();
            ResourceLedger.Clear();
            DuskRecallSystem.Clear();
            DarknessEvaluator.Clear();
            PlanarMotion.ResetHooks();
            EconomyConfig.ClearCache();
            JobsConfig.ClearCache();
            PrototypeConfig.ClearCache();
            PathsConfig.ClearCache();
            EventBus.ResetAll();
            GameEventLog.Clear();
        }

        T Track<T>(T obj) where T : Object
        {
            if (obj is GameObject go)
            {
                _spawned.Add(go);
            }
            else if (obj is ScriptableObject so)
            {
                _assets.Add(so);
            }
            return obj;
        }

        [UnityTest]
        public IEnumerator VillagerCommute_WearsADesirePath_NoPlayerAction()
        {
            var proto = Track(ScriptableObject.CreateInstance<PrototypeConfig>());
            proto.dayDurationSeconds = 1000f; // stay in Day; the loop never recalls
            proto.arrivalRadius = 0.3f;
            proto.villagerWalkSpeed = 3f;
            proto.villagerFearPerSecondInDark = 0f;
            proto.villagerFearPerSecondInEdge = 0f;
            proto.villagerInjuredDarkSeconds = 10000f;
            proto.villagerMissingDarkSeconds = 20000f;
            proto.villagerPickupDurationSeconds = 0.1f;
            DarknessEvaluator.Config = proto;
            DuskRecallSystem.Config = proto;

            var econ = Track(ScriptableObject.CreateInstance<EconomyConfig>());
            econ.baseStorageCapacity = 999;
            econ.salvageSiteWood = 999;
            econ.salvageYieldPerCycle = 2;
            econ.salvageWorkDurationSeconds = 0.2f;
            ResourceLedger.Config = econ;

            var jobs = Track(ScriptableObject.CreateInstance<JobsConfig>());
            jobs.carryCapacity = 4;

            var paths = Track(ScriptableObject.CreateInstance<PathsConfig>());
            paths.cellSize = 2f;
            paths.gridOrigin = new Vector2(-20f, -20f);
            paths.gridColumns = 20;
            paths.gridRows = 20;
            paths.wearPerDistanceUnit = 1f;
            paths.maxWearPerCell = 100f;
            paths.tierWearThresholds = new[] { 3f, 9f, 18f };
            paths.tierSpeedMultipliers = new[] { 1.1f, 1.2f, 1.3f };
            paths.importantTier = 2;

            // Clock (Day, never ticked) so the job layer treats it as a work phase.
            var clock = Track(new GameObject("Clock")).AddComponent<GameClock>();
            clock.autoTick = false;
            clock.Configure(proto);

            // Path systems: OnEnable wires PlanarMotion hooks in play mode.
            var world = Track(new GameObject("WorldSystems"));
            var grid = world.AddComponent<TrafficGrid>();
            grid.Configure(paths);
            var desire = world.AddComponent<DesirePathSystem>();
            desire.Config = paths;

            // Storage at origin, wreck straight up the +Z axis.
            var pileGO = Track(new GameObject("StoragePile"));
            pileGO.transform.position = Vector3.zero;
            pileGO.AddComponent<StoragePile>();

            var siteGO = Track(new GameObject("WreckSite"));
            var sitePos = new Vector3(0f, 0f, 8f);
            siteGO.transform.position = sitePos;
            var site = siteGO.AddComponent<SalvageSite>();
            site.Configure(econ);

            var villGO = Track(new GameObject("Villager"));
            villGO.transform.position = Vector3.zero;
            var villager = villGO.AddComponent<VillagerAgent>();
            villager.autoTick = false;
            villager.Config = proto;
            villager.seed = 1;
            villager.Bravery = 0.5f;
            var agent = villGO.AddComponent<VillagerJobAgent>();
            agent.autoTick = false;
            agent.Config = jobs;
            agent.SetJob(VillagerJob.Salvager);

            var midRoute = new Vector3(0f, 0f, 4f);
            var offRoute = new Vector3(12f, 0f, 4f);

            for (int i = 0; i < 4000 && ResourceLedger.Get(ResourceType.Wood) < 8; i++)
            {
                villager.Tick(Dt);
                agent.Tick(Dt);
                if (i % 40 == 0)
                {
                    yield return null;
                }
            }

            Assert.GreaterOrEqual(ResourceLedger.Get(ResourceType.Wood), 8,
                "the salvager completed several unattended round trips");
            Assert.GreaterOrEqual(grid.TierAt(midRoute), 1,
                "traffic wore a desire path along the commute");
            Assert.AreEqual(0, grid.TierAt(offRoute),
                "ground the villager never walked stays untrodden");
        }
    }
}
