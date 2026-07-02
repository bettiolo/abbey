using System.Collections.Generic;
using Abbey.Core;
using Abbey.Economy;
using NUnit.Framework;
using UnityEngine;

namespace Abbey.Tests.EditMode
{
    /// <summary>
    /// Stockpile accounting: add/consume/afford, the storage-pile capacity ceiling,
    /// the StockChanged event and the "resource" event-log trail. Everything is
    /// constructed programmatically; the config is injected so no default balance
    /// value matters.
    /// </summary>
    public class ResourceLedgerTests
    {
        readonly List<GameObject> _spawned = new List<GameObject>();
        EconomyConfig _config;

        [SetUp]
        public void SetUp()
        {
            GameEventLog.Clear();
            ResourceLedger.Clear();
            EconomyConfig.ClearCache();

            _config = ScriptableObject.CreateInstance<EconomyConfig>();
            _config.startingWood = 4;
            _config.startingFood = 3;
            _config.startingOil = 2;
            _config.startingMedicine = 1;
            _config.baseStorageCapacity = 10;
            _config.storagePileCapacity = 5;

            ResourceLedger.Config = _config;
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
            Object.DestroyImmediate(_config);
            ResourceLedger.Clear();
            EconomyConfig.ClearCache();
            GameEventLog.Clear();
        }

        StoragePile SpawnPile()
        {
            var go = new GameObject($"TestStoragePile_{_spawned.Count}");
            _spawned.Add(go);
            var pile = go.AddComponent<StoragePile>();
            ResourceLedger.RegisterStorage(pile); // defensive, mirrors OnEnable
            return pile;
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

        [Test]
        public void Add_StoresUnits_RaisesEvent_AndLogsTransaction()
        {
            ResourceType eventType = default;
            int eventDelta = 0;
            int eventTotal = 0;
            ResourceLedger.StockChanged += (type, delta, total) =>
            {
                eventType = type;
                eventDelta = delta;
                eventTotal = total;
            };

            int stored = ResourceLedger.Add(ResourceType.Wood, 3, "salvage");

            Assert.AreEqual(3, stored);
            Assert.AreEqual(3, ResourceLedger.Get(ResourceType.Wood));
            Assert.AreEqual(ResourceType.Wood, eventType);
            Assert.AreEqual(3, eventDelta);
            Assert.AreEqual(3, eventTotal);
            Assert.IsTrue(LogContains("resource", "wood +3 (salvage)"),
                "every transaction must land in the shared event log");
        }

        [Test]
        public void Add_ClampsToCapacity_AndLogsOverflow()
        {
            // Base capacity 10, no piles.
            int stored = ResourceLedger.Add(ResourceType.Wood, 12, "salvage");

            Assert.AreEqual(10, stored);
            Assert.AreEqual(10, ResourceLedger.Get(ResourceType.Wood));
            Assert.AreEqual(ResourceLedger.Capacity, ResourceLedger.TotalStored);
            Assert.IsTrue(LogContains("resource", "wood overflow 2 (salvage, storage full)"));

            Assert.AreEqual(0, ResourceLedger.Add(ResourceType.Food, 1, "salvage"),
                "a full stockpile accepts nothing");
            Assert.AreEqual(0, ResourceLedger.Get(ResourceType.Food));
        }

        [Test]
        public void CapacityIsShared_AcrossResourceTypes()
        {
            ResourceLedger.Add(ResourceType.Wood, 6, "test");
            int stored = ResourceLedger.Add(ResourceType.Food, 6, "test");

            Assert.AreEqual(4, stored, "wood and food share the same 10-unit ceiling");
            Assert.AreEqual(10, ResourceLedger.TotalStored);
        }

        [Test]
        public void TryConsume_SpendsWhenAffordable_RefusesAndChangesNothingOtherwise()
        {
            ResourceLedger.Add(ResourceType.Food, 5, "test");
            GameEventLog.Clear();

            Assert.IsTrue(ResourceLedger.TryConsume(ResourceType.Food, 3, "campfire"));
            Assert.AreEqual(2, ResourceLedger.Get(ResourceType.Food));
            Assert.IsTrue(LogContains("resource", "food -3 (campfire)"));

            Assert.IsFalse(ResourceLedger.TryConsume(ResourceType.Food, 3, "campfire"));
            Assert.AreEqual(2, ResourceLedger.Get(ResourceType.Food), "failed spends change nothing");
            Assert.IsFalse(ResourceLedger.TryConsume(ResourceType.Oil, 1, "lantern"),
                "cannot spend a resource that was never stored");
        }

        [Test]
        public void CanAfford_AccumulatesDuplicateTypesInCostLists()
        {
            ResourceLedger.Add(ResourceType.Wood, 5, "test");

            var affordable = new List<ResourceStack>
            {
                new ResourceStack(ResourceType.Wood, 3),
                new ResourceStack(ResourceType.Wood, 2),
            };
            var tooMuch = new List<ResourceStack>
            {
                new ResourceStack(ResourceType.Wood, 3),
                new ResourceStack(ResourceType.Wood, 3),
            };

            Assert.IsTrue(ResourceLedger.CanAfford(affordable));
            Assert.IsFalse(ResourceLedger.CanAfford(tooMuch),
                "duplicate stacks of one type must accumulate, not be checked separately");
        }

        [Test]
        public void TryConsume_CostList_IsAtomic()
        {
            ResourceLedger.Add(ResourceType.Wood, 4, "test");
            ResourceLedger.Add(ResourceType.Stone, 1, "test");

            var cost = new List<ResourceStack>
            {
                new ResourceStack(ResourceType.Wood, 2),
                new ResourceStack(ResourceType.Stone, 2),
            };

            Assert.IsFalse(ResourceLedger.TryConsume(cost, "abbey gate"));
            Assert.AreEqual(4, ResourceLedger.Get(ResourceType.Wood), "nothing spent on failure");
            Assert.AreEqual(1, ResourceLedger.Get(ResourceType.Stone));

            ResourceLedger.Add(ResourceType.Stone, 1, "test");
            Assert.IsTrue(ResourceLedger.TryConsume(cost, "abbey gate"));
            Assert.AreEqual(2, ResourceLedger.Get(ResourceType.Wood));
            Assert.AreEqual(0, ResourceLedger.Get(ResourceType.Stone));
        }

        [Test]
        public void StoragePiles_RaiseAndLowerTheCapacityCeiling()
        {
            Assert.AreEqual(10, ResourceLedger.Capacity, "base capacity with no piles");

            var pileA = SpawnPile();
            var pileB = SpawnPile();
            Assert.AreEqual(20, ResourceLedger.Capacity, "each pile adds storagePileCapacity");

            ResourceLedger.Add(ResourceType.Wood, 20, "test");
            Assert.AreEqual(20, ResourceLedger.TotalStored);

            pileB.gameObject.SetActive(false); // OnDisable unregisters
            Assert.AreEqual(15, ResourceLedger.Capacity);
            Assert.AreEqual(20, ResourceLedger.TotalStored,
                "shrinking capacity never destroys stored goods");
            Assert.AreEqual(0, ResourceLedger.Add(ResourceType.Wood, 1, "test"),
                "but nothing more fits until stock drops below the ceiling");
            Assert.IsNotNull(pileA);
        }

        [Test]
        public void GrantStartingStock_AddsTheWreckCrateHeadStart()
        {
            SpawnPile(); // room for all 10 starting units

            ResourceLedger.GrantStartingStock();

            Assert.AreEqual(4, ResourceLedger.Get(ResourceType.Wood));
            Assert.AreEqual(3, ResourceLedger.Get(ResourceType.Food));
            Assert.AreEqual(2, ResourceLedger.Get(ResourceType.Oil));
            Assert.AreEqual(1, ResourceLedger.Get(ResourceType.Medicine));
            Assert.AreEqual(0, ResourceLedger.Get(ResourceType.Stone), "the wreck gives no stone");
            Assert.IsTrue(LogContains("resource", "wood +4 (wreck crates)"));
            Assert.IsTrue(LogContains("resource", "medicine +1 (wreck crates)"));
        }

        [Test]
        public void ResourceIds_MatchTheDesignVocabulary()
        {
            Assert.AreEqual("scrap_iron", ResourceTypes.Id(ResourceType.ScrapIron));
            Assert.AreEqual("relic_fragment", ResourceTypes.Id(ResourceType.RelicFragments));
            Assert.AreEqual(8, ResourceTypes.Count);
        }
    }
}
