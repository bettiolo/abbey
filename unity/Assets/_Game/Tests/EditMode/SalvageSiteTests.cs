using System.Collections.Generic;
using Abbey.Core;
using Abbey.Economy;
using NUnit.Framework;
using UnityEngine;

namespace Abbey.Tests.EditMode
{
    /// <summary>
    /// Salvage node behaviour: deterministic harvest order, pool clamping, the
    /// Intact/Picked/Stripped depletion stages (event + log + optional visuals)
    /// and exhaustion. Config is injected; no default balance value matters.
    /// </summary>
    public class SalvageSiteTests
    {
        readonly List<GameObject> _spawned = new List<GameObject>();
        EconomyConfig _config;

        [SetUp]
        public void SetUp()
        {
            GameEventLog.Clear();
            ResourceLedger.Clear();
            SalvageSite.ClearRegistry();
            EconomyConfig.ClearCache();

            _config = ScriptableObject.CreateInstance<EconomyConfig>();
            _config.salvageSiteWood = 6;
            _config.salvageSiteFood = 3;
            _config.salvageSiteOil = 2;
            _config.salvageSiteMedicine = 1; // total pool = 12
            _config.salvageYieldPerCycle = 2;
            _config.salvagePickedFraction = 0.66f;
            _config.salvageStrippedFraction = 0.25f;
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
            SalvageSite.ClearRegistry();
            ResourceLedger.Clear();
            EconomyConfig.ClearCache();
            GameEventLog.Clear();
        }

        SalvageSite SpawnSite(string name = "TestWreck")
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            var site = go.AddComponent<SalvageSite>();
            site.Configure(_config);
            return site;
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
        public void Configure_FillsThePoolFromConfig()
        {
            var site = SpawnSite();

            Assert.AreEqual(6, site.Remaining(ResourceType.Wood));
            Assert.AreEqual(3, site.Remaining(ResourceType.Food));
            Assert.AreEqual(2, site.Remaining(ResourceType.Oil));
            Assert.AreEqual(1, site.Remaining(ResourceType.Medicine));
            Assert.AreEqual(0, site.Remaining(ResourceType.Stone), "wrecks hold no stone");
            Assert.AreEqual(12, site.TotalRemaining);
            Assert.AreEqual(1f, site.RemainingFraction, 1e-6f);
            Assert.AreEqual(SalvageStage.Intact, site.Stage);
            Assert.IsFalse(site.IsExhausted);
        }

        [Test]
        public void Harvest_ClampsToTheRemainingPool()
        {
            var site = SpawnSite();

            Assert.AreEqual(2, site.Harvest(ResourceType.Oil, 2));
            Assert.AreEqual(0, site.Harvest(ResourceType.Oil, 5), "the oil is gone");
            Assert.AreEqual(1, site.Harvest(ResourceType.Medicine, 99), "partial grant, clamped");
            Assert.AreEqual(0, site.Harvest(ResourceType.Wood, 0), "zero requests take nothing");
            Assert.AreEqual(9, site.TotalRemaining);
        }

        [Test]
        public void TryHarvestCycle_TakesEnumOrder_UntilExhausted()
        {
            var site = SpawnSite();
            var harvested = new List<ResourceStack>();

            int guard = 0;
            while (site.TryHarvestCycle(out var type, out int amount) && guard++ < 20)
            {
                harvested.Add(new ResourceStack(type, amount));
            }

            // 6 wood in 2s, 3 food as 2+1, 2 oil, 1 medicine — fixed order, no RNG.
            var expected = new[]
            {
                new ResourceStack(ResourceType.Wood, 2),
                new ResourceStack(ResourceType.Wood, 2),
                new ResourceStack(ResourceType.Wood, 2),
                new ResourceStack(ResourceType.Food, 2),
                new ResourceStack(ResourceType.Food, 1),
                new ResourceStack(ResourceType.Oil, 2),
                new ResourceStack(ResourceType.Medicine, 1),
            };
            Assert.AreEqual(expected.Length, harvested.Count);
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i].type, harvested[i].type, $"cycle {i} type");
                Assert.AreEqual(expected[i].amount, harvested[i].amount, $"cycle {i} amount");
            }

            Assert.IsTrue(site.IsExhausted);
            Assert.AreEqual(0f, site.RemainingFraction);
            Assert.IsFalse(site.TryHarvestCycle(out _, out int none), "stripped wrecks give nothing");
            Assert.AreEqual(0, none);
        }

        [Test]
        public void DepletionStages_Progress_RaiseEvents_AndHitTheLog()
        {
            var site = SpawnSite();
            var stages = new List<SalvageStage>();
            site.StageChanged += (s, stage) => stages.Add(stage);

            site.Harvest(ResourceType.Wood, 4); // 8/12 = 0.667 > 0.66 → still Intact
            Assert.AreEqual(SalvageStage.Intact, site.Stage);

            site.Harvest(ResourceType.Wood, 1); // 7/12 = 0.583 → Picked
            Assert.AreEqual(SalvageStage.Picked, site.Stage);

            site.Harvest(ResourceType.Food, 3); // 4/12 = 0.333 → still Picked
            Assert.AreEqual(SalvageStage.Picked, site.Stage);

            site.Harvest(ResourceType.Oil, 2); // 2/12 = 0.167 → Stripped
            Assert.AreEqual(SalvageStage.Stripped, site.Stage);

            CollectionAssert.AreEqual(
                new[] { SalvageStage.Picked, SalvageStage.Stripped }, stages,
                "one event per stage transition, in order");
            Assert.IsTrue(LogContains("salvage", "TestWreck stage Intact->Picked"));
            Assert.IsTrue(LogContains("salvage", "TestWreck stage Picked->Stripped"));
        }

        [Test]
        public void StageVisuals_SwapWithTheStage_WhenAssigned()
        {
            var site = SpawnSite();
            var intact = new GameObject("VisIntact");
            var picked = new GameObject("VisPicked");
            var stripped = new GameObject("VisStripped");
            _spawned.Add(intact);
            _spawned.Add(picked);
            _spawned.Add(stripped);
            site.stageVisuals = new[] { intact, picked, stripped };
            site.Configure(_config); // re-applies visuals for the fresh pool

            Assert.IsTrue(intact.activeSelf);
            Assert.IsFalse(picked.activeSelf);
            Assert.IsFalse(stripped.activeSelf);

            site.Harvest(ResourceType.Wood, 6); // 6/12 = 0.5 → Picked
            Assert.IsFalse(intact.activeSelf);
            Assert.IsTrue(picked.activeSelf);
            Assert.IsFalse(stripped.activeSelf);

            site.Harvest(ResourceType.Food, 3);
            site.Harvest(ResourceType.Oil, 1); // 2/12 = 0.167 → Stripped
            Assert.IsFalse(intact.activeSelf);
            Assert.IsFalse(picked.activeSelf);
            Assert.IsTrue(stripped.activeSelf);
        }

        [Test]
        public void Registry_TracksEnabledSites()
        {
            var site = SpawnSite();
            SalvageSite.ClearRegistry();
            site.gameObject.SetActive(false);
            site.gameObject.SetActive(true); // OnEnable registers

            Assert.AreEqual(1, SalvageSite.Active.Count);
            Assert.AreSame(site, SalvageSite.Active[0]);

            site.gameObject.SetActive(false); // OnDisable unregisters
            Assert.AreEqual(0, SalvageSite.Active.Count);
        }

        [Test]
        public void HarvestIntoLedger_TheFirstLoop_WreckFeedsTheStockpile()
        {
            // GAME_DESIGN §7: shipwreck crates → wood/food/oil/medicine.
            _config.baseStorageCapacity = 50;
            ResourceLedger.Config = _config;
            var site = SpawnSite();

            while (site.TryHarvestCycle(out var type, out int amount))
            {
                ResourceLedger.Add(type, amount, "salvage");
            }

            Assert.AreEqual(6, ResourceLedger.Get(ResourceType.Wood));
            Assert.AreEqual(3, ResourceLedger.Get(ResourceType.Food));
            Assert.AreEqual(2, ResourceLedger.Get(ResourceType.Oil));
            Assert.AreEqual(1, ResourceLedger.Get(ResourceType.Medicine));
            Assert.IsTrue(site.IsExhausted, "the head start is temporary by design");
            Assert.IsTrue(LogContains("resource", "wood +2 (salvage)"));
        }
    }
}
