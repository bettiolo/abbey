using System.Collections;
using System.Collections.Generic;
using Abbey.Buildings;
using Abbey.Core;
using Abbey.Economy;
using Abbey.Light;
using Abbey.Settlement;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Abbey.Tests.PlayMode
{
    /// <summary>
    /// Seed-slot settlement growth under the play loop (P3-02), built programmatically
    /// (no scenes): off-slot placement is refused, on-slot placement occupies its slot
    /// and completing the building opens child slots beside it on lit ground, and light
    /// debt falls when a lantern covers a dark slot. Logic is synchronous; the frame
    /// yields only prove it runs in Play mode.
    /// </summary>
    public class SeedSlotGrowthPlayModeTests
    {
        readonly List<GameObject> _spawned = new List<GameObject>();
        readonly List<ScriptableObject> _assets = new List<ScriptableObject>();
        SeedSlotSystem _system;

        [SetUp]
        public void SetUp()
        {
            EventBus.ResetAll();
            GameEventLog.Clear();
            DarknessEvaluator.Clear();
            ResourceLedger.Clear();
            ConstructionSite.ClearRegistry();
            Building.ClearRegistry();
            BuildingPlacer.Clear();
            AbbeyState.Clear();
            EconomyConfig.ClearCache();
            BuildingCatalog.ClearCache();
            PrototypeConfig.ClearCache();
            SettlementGrowthConfig.ClearCache();

            var economy = ScriptableObject.CreateInstance<EconomyConfig>();
            economy.baseStorageCapacity = 9999;
            ResourceLedger.Config = economy;
            _assets.Add(economy);

            var catalog = ScriptableObject.CreateInstance<BuildingCatalog>();
            catalog.buildings = new List<BuildingType>
            {
                new BuildingType
                {
                    id = "hut",
                    displayName = "Test Hut",
                    footprint = new Vector2(2f, 2f),
                    cost = new List<ResourceStack> { new ResourceStack(ResourceType.Wood, 2) },
                    buildWorkSeconds = 1f,
                    function = FunctionKind.Shelter,
                },
            };
            BuildingPlacer.Catalog = catalog;
            _assets.Add(catalog);

            var growth = ScriptableObject.CreateInstance<SettlementGrowthConfig>();
            growth.childSlotsPerBuilding = 3;
            growth.childSlotRingRadius = 3.5f;
            growth.requireHug = true;
            _assets.Add(growth);

            var sysGO = new GameObject("SeedSlotSystem");
            _system = sysGO.AddComponent<SeedSlotSystem>();
            _system.Config = growth;
            _spawned.Add(sysGO);
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
            foreach (var building in Object.FindObjectsByType<Building>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(building.gameObject);
            }
            foreach (var asset in _assets)
            {
                if (asset != null)
                {
                    Object.DestroyImmediate(asset);
                }
            }
            _assets.Clear();
            _system = null;

            ConstructionSite.ClearRegistry();
            Building.ClearRegistry();
            BuildingPlacer.Clear();
            AbbeyState.Clear();
            DarknessEvaluator.Clear();
            ResourceLedger.Clear();
            SettlementGrowthConfig.ClearCache();
            EventBus.ResetAll();
            GameEventLog.Clear();
        }

        static void FundWood(int amount)
        {
            ResourceLedger.Add(ResourceType.Wood, amount, "test");
        }

        GameObject SpawnBroadLight(Vector3 position)
        {
            var go = new GameObject("BroadLight");
            go.transform.position = position;
            var light = go.AddComponent<LightSource>();
            light.radius = 12f;
            light.strength = 1f;
            light.fuelSeconds = -1f;
            light.isLit = true;
            _spawned.Add(go);
            return go;
        }

        [UnityTest]
        public IEnumerator OffSlotRejected_OnSlotPlacement_GrowsChildrenOnCompletion()
        {
            var slot = _system.AddAuthoredSlot(Vector3.zero, SlotSizeClass.Medium);
            SpawnBroadLight(Vector3.zero);
            FundWood(50);
            yield return null;

            // Off-slot is refused.
            Assert.IsNull(BuildingPlacer.PlaceConstructionSite("hut", new Vector3(25f, 0f, 25f)),
                "placement away from any open slot is refused");

            // On-slot occupies the slot.
            var site = BuildingPlacer.PlaceConstructionSite("hut", Vector3.zero);
            Assert.IsNotNull(site);
            Assert.AreEqual(SlotState.Occupied, slot.state);
            yield return null;

            // Deliver + work to completion.
            foreach (var stack in site.Type.cost)
            {
                site.DeliverResource(stack.type, stack.amount);
            }
            site.ApplyWork(site.Type.buildWorkSeconds);
            Assert.IsTrue(site.IsComplete);
            yield return null;

            Assert.AreEqual(3, _system.CountByState(SlotState.Open),
                "completing the building opened 3 child slots on lit ground");
        }

        [UnityTest]
        public IEnumerator LightDebt_FallsWhenLanternCoversDarkSlot()
        {
            var slotPos = new Vector3(30f, 0f, 30f);
            _system.AddAuthoredSlot(slotPos, SlotSizeClass.Medium);
            yield return null;

            float darkDebt = _system.ComputeLightDebt();
            Assert.Greater(darkDebt, 0f, "an open slot in the dark carries light debt");

            SpawnBroadLight(slotPos);
            yield return null;

            Assert.Less(_system.ComputeLightDebt(), darkDebt,
                "a lantern over the slot lowers the light debt");
        }
    }
}
