using System.Collections.Generic;
using Abbey.Core;
using Abbey.Light;
using Abbey.Settlement;
using NUnit.Framework;
using UnityEngine;

namespace Abbey.Tests.EditMode
{
    /// <summary>
    /// Desire-path emergence (P3-12): repeated traversals wear cells into path tiers,
    /// untrodden paths decay, worn roads grant a per-tier speed bonus, and the dusk
    /// scan makes lanterns over important paths burn extra fuel while unlit important
    /// paths add light debt. Everything is built programmatically with injected
    /// configs and manual ticks — no scene assets, deterministic.
    /// </summary>
    public class DesirePathTests
    {
        readonly List<GameObject> _spawned = new List<GameObject>();
        PathsConfig _config;
        TrafficGrid _grid;
        DesirePathSystem _paths;

        [SetUp]
        public void SetUp()
        {
            EventBus.ResetAll();
            GameEventLog.Clear();
            DarknessEvaluator.Clear();
            PlanarMotion.ResetHooks();
            PathsConfig.ClearCache();

            _config = ScriptableObject.CreateInstance<PathsConfig>();
            _config.cellSize = 2f;
            _config.gridOrigin = new Vector2(-20f, -20f);
            _config.gridColumns = 20;
            _config.gridRows = 20;
            _config.wearPerDistanceUnit = 1f;
            _config.maxWearPerCell = 100f;
            _config.wearDecayPerDay = 0.5f;
            _config.wearDecayFloor = 0.25f;
            _config.tierWearThresholds = new[] { 3f, 9f, 18f };
            _config.tierSpeedMultipliers = new[] { 1.1f, 1.2f, 1.3f };
            _config.importantTier = 2;
            _config.importantPathFuelMultiplier = 2f;
            _config.unlitImportantPathLightDebtPerCell = 1.5f;

            _grid = NewGO("TrafficGrid").AddComponent<TrafficGrid>();
            _grid.Config = _config;
            _paths = NewGO("DesirePathSystem").AddComponent<DesirePathSystem>();
            _paths.Config = _config;
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
            if (_config != null)
            {
                Object.DestroyImmediate(_config);
            }
            PlanarMotion.ResetHooks();
            DarknessEvaluator.Clear();
            PathsConfig.ClearCache();
            EventBus.ResetAll();
            GameEventLog.Clear();
        }

        GameObject NewGO(string name)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            return go;
        }

        [Test]
        public void RepeatedTraversals_PromoteCellsToPathTier()
        {
            var a = new Vector3(-6f, 0f, 0f);
            var b = new Vector3(6f, 0f, 0f);
            var mid = Vector3.zero;

            Assert.AreEqual(0, _grid.TierAt(mid), "clean ground is tier 0");

            // Each full pass deposits wearPerDistanceUnit * cellSize = 2 wear per cell.
            _grid.ReportTraversal(a, b); // 2 wear, under the tier-1 threshold (3)
            Assert.AreEqual(0, _grid.TierAt(mid), "one pass is not yet a path");

            _grid.ReportTraversal(a, b); // 4 wear >= 3
            Assert.AreEqual(1, _grid.TierAt(mid), "a second pass crosses the tier-1 threshold");

            for (int i = 0; i < 7; i++)
            {
                _grid.ReportTraversal(a, b); // climb well past the important threshold (18)
            }
            Assert.GreaterOrEqual(_grid.TierAt(mid), 2, "heavy traffic reaches an important tier");
        }

        [Test]
        public void UnusedPaths_DecayEachDay()
        {
            var mid = Vector3.zero;
            _grid.AddWear(mid, 6f); // tier 1 (>= 3, < 9)
            Assert.AreEqual(1, _grid.TierAt(mid));

            _grid.DecayDay(); // 6 * 0.5 = 3, still exactly tier 1
            Assert.AreEqual(3f, _grid.WearAt(mid), 1e-4f);
            Assert.AreEqual(1, _grid.TierAt(mid));

            _grid.DecayDay(); // 1.5 -> below tier-1
            Assert.AreEqual(0, _grid.TierAt(mid));

            // Keep decaying: once below the floor it snaps fully to zero.
            for (int i = 0; i < 6; i++)
            {
                _grid.DecayDay();
            }
            Assert.AreEqual(0f, _grid.WearAt(mid), "faint scuffs heal to clean ground");
        }

        [Test]
        public void SpeedBonus_ReturnsPerTierValue()
        {
            var mid = Vector3.zero;
            Assert.AreEqual(1f, _paths.SpeedMultiplierAt(mid), 1e-4f);

            _grid.AddWear(mid, 4f); // tier 1
            Assert.AreEqual(1.1f, _paths.SpeedMultiplierAt(mid), 1e-4f);

            _grid.AddWear(mid, 6f); // total 10 -> tier 2
            Assert.AreEqual(1.2f, _paths.SpeedMultiplierAt(mid), 1e-4f);

            _grid.AddWear(mid, 10f); // total 20 -> tier 3
            Assert.AreEqual(1.3f, _paths.SpeedMultiplierAt(mid), 1e-4f);
        }

        [Test]
        public void StepWorn_AppliesSpeedBonus_OnAWornCell()
        {
            // Wire the provider exactly as DesirePathSystem does on enable.
            PlanarMotion.PathSpeedProvider = _paths.SpeedMultiplierAt;
            var origin = Vector3.zero;
            _grid.AddWear(origin, 20f); // tier 3 -> 1.3x

            var slow = PlanarMotion.Step(origin, new Vector3(100f, 0f, 0f), 1f, 1f, 0.01f, out _);
            var fast = PlanarMotion.StepWorn(origin, new Vector3(100f, 0f, 0f), 1f, 1f, 0.01f, out _);

            Assert.Greater(fast.x, slow.x + 0.2f, "the worn road moves the walker further per tick");
            Assert.AreEqual(1.3f, fast.x / slow.x, 1e-3f, "exactly the tier-3 multiplier");
        }

        [Test]
        public void ImportantPath_LanternBurnsFuelFaster_OffPathLanternDoesNot()
        {
            // An important path down the X axis around the origin.
            for (int i = 0; i < 12; i++)
            {
                _grid.ReportTraversal(new Vector3(-8f, 0f, 0f), new Vector3(8f, 0f, 0f));
            }
            Assert.GreaterOrEqual(_grid.TierAt(Vector3.zero), _config.importantTier,
                "the route is now an important path");

            var onPath = SpawnLantern(Vector3.zero, radius: 4f);
            var offPath = SpawnLantern(new Vector3(0f, 0f, 15f), radius: 4f);

            _paths.ScanImportantPaths(applyFuelDebt: true); // dusk

            Assert.AreEqual(_config.importantPathFuelMultiplier, onPath.PathFuelMultiplier, 1e-4f,
                "a lantern covering the important path is set to burn extra fuel");
            Assert.AreEqual(1f, offPath.PathFuelMultiplier, 1e-4f,
                "an off-path lantern burns at its normal rate");

            float before = onPath.fuelSeconds;
            onPath.Tick(1f);
            Assert.AreEqual(before - onPath.fuelConsumptionPerSecond * _config.importantPathFuelMultiplier,
                onPath.fuelSeconds, 1e-3f, "the fuel debt is actually charged on tick");

            _paths.ClearImportantPathCoverage(); // dawn
            Assert.AreEqual(1f, onPath.PathFuelMultiplier, 1e-4f, "dawn restores the normal rate");
        }

        [Test]
        public void UnlitImportantPath_AddsLightDebt()
        {
            for (int i = 0; i < 12; i++)
            {
                _grid.ReportTraversal(new Vector3(-8f, 0f, 0f), new Vector3(8f, 0f, 0f));
            }
            int importantCells = _grid.CountCellsAtTier(_config.importantTier);
            Assert.Greater(importantCells, 0);

            // No lights anywhere: every important cell is unlit.
            float debt = _paths.ComputePathLightDebt();
            Assert.Greater(debt, 0f, "unlit important paths add light debt");
            Assert.AreEqual(importantCells * _config.unlitImportantPathLightDebtPerCell, debt, 1e-3f);
            Assert.AreEqual(importantCells, _paths.UnlitImportantCellCount);

            // Cover the whole line in light: debt collapses.
            SpawnLantern(Vector3.zero, radius: 30f);
            float litDebt = _paths.ComputePathLightDebt();
            Assert.AreEqual(0f, litDebt, 1e-4f, "a lit important path owes no light debt");
        }

        [Test]
        public void SerializeState_RoundTripsWearField()
        {
            _grid.AddWear(new Vector3(2f, 0f, 2f), 12f);
            _grid.AddWear(new Vector3(-4f, 0f, 6f), 5f);

            var state = _grid.SerializeState();
            _grid.ClearWear();
            Assert.AreEqual(0f, _grid.WearAt(new Vector3(2f, 0f, 2f)));

            _grid.LoadState(state);
            Assert.AreEqual(12f, _grid.WearAt(new Vector3(2f, 0f, 2f)), 1e-4f);
            Assert.AreEqual(5f, _grid.WearAt(new Vector3(-4f, 0f, 6f)), 1e-4f);
        }

        [Test]
        public void OffGridTraversal_IsIgnored()
        {
            var far = new Vector3(500f, 0f, 500f);
            _grid.ReportTraversal(far, far + new Vector3(4f, 0f, 0f));
            Assert.AreEqual(0f, _grid.TotalWear(), "traffic outside the grid bounds is dropped");
        }

        LightSource SpawnLantern(Vector3 position, float radius)
        {
            var go = NewGO($"Lantern_{_spawned.Count}");
            go.transform.position = position;
            var light = go.AddComponent<LightSource>();
            light.radius = radius;
            light.strength = 1f;
            light.isLit = true;
            light.autoTick = false;
            light.fuelSeconds = 100f;
            light.fuelConsumptionPerSecond = 1f;
            DarknessEvaluator.Register(light);
            return light;
        }
    }
}
