using System.Collections;
using System.Collections.Generic;
using Abbey.Core;
using Abbey.Economy;
using Abbey.Island;
using Abbey.Light;
using Abbey.Settlement;
using Abbey.Villagers;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Abbey.Tests.PlayMode
{
    /// <summary>
    /// Island exploration under the play loop (P3-13), built programmatically (no scenes): a
    /// daytime expedition walks its party out to a hidden POI, reveals it and lands the reward
    /// (ledger + unlocked seed slots), then walks the party back to the muster point; and an
    /// expedition still out when dusk falls is caught out via the real
    /// <see cref="EventBus.PhaseChanged"/> hook. Logic is synchronous; the frame yields prove
    /// it runs in Play mode.
    /// </summary>
    public class IslandExplorationPlayModeTests
    {
        readonly List<GameObject> _spawned = new List<GameObject>();
        readonly List<ScriptableObject> _assets = new List<ScriptableObject>();
        ExplorationSystem _system;
        SeedSlotSystem _seeds;

        [SetUp]
        public void SetUp()
        {
            EventBus.ResetAll();
            GameEventLog.Clear();
            DarknessEvaluator.Clear();
            DuskRecallSystem.Clear();
            ResourceLedger.Clear();
            IslandConfig.ClearCache();
            PrototypeConfig.ClearCache();
            EconomyConfig.ClearCache();
            SettlementGrowthConfig.ClearCache();

            var island = ScriptableObject.CreateInstance<IslandConfig>();
            island.expeditionTravelSpeed = 30f;
            island.poiResolveSeconds = 0.2f;
            island.arrivalRadius = 0.5f;
            island.poiRewards = new List<PoiRewardRule>
            {
                new PoiRewardRule
                {
                    type = PoiType.ResourceCache,
                    resourceYields = new List<ResourceStack> { new ResourceStack(ResourceType.Wood, 4) },
                    seedSlotsUnlocked = 1,
                    unlockedSlotSize = SlotSizeClass.Small,
                },
            };
            _assets.Add(island);

            var econ = ScriptableObject.CreateInstance<EconomyConfig>();
            econ.baseStorageCapacity = 1000;
            ResourceLedger.Config = econ;
            _assets.Add(econ);

            var growth = ScriptableObject.CreateInstance<SettlementGrowthConfig>();
            growth.minSlotSeparation = 1f;
            growth.childSlotRingRadius = 2f;
            _assets.Add(growth);

            var proto = ScriptableObject.CreateInstance<PrototypeConfig>();
            proto.arrivalRadius = 0.5f;
            _assets.Add(proto);
            DarknessEvaluator.Config = proto;
            DuskRecallSystem.Config = proto;

            var seedGO = new GameObject("SeedSlotSystem");
            _seeds = seedGO.AddComponent<SeedSlotSystem>();
            _seeds.Config = growth;
            _spawned.Add(seedGO);

            var sysGO = new GameObject("ExplorationSystem");
            sysGO.transform.position = Vector3.zero;
            _system = sysGO.AddComponent<ExplorationSystem>();
            _system.autoTick = false;
            _system.Configure(island);
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
            foreach (var a in _assets)
            {
                if (a != null)
                {
                    Object.DestroyImmediate(a);
                }
            }
            _assets.Clear();
            _system = null;
            _seeds = null;

            EventBus.ResetAll();
            GameEventLog.Clear();
            DarknessEvaluator.Clear();
            DuskRecallSystem.Clear();
            ResourceLedger.Clear();
        }

        VillagerAgent MakeVillager(int seed)
        {
            var go = new GameObject($"Villager_{seed}");
            go.transform.position = Vector3.zero;
            var v = go.AddComponent<VillagerAgent>();
            v.autoTick = false;
            v.seed = seed;
            _spawned.Add(go);
            return v;
        }

        [UnityTest]
        public IEnumerator Expedition_LeavesCamp_DiscoversPoi_AndReturns()
        {
            int slotsBefore = _seeds.CountByState(SlotState.Open);
            var poi = _system.AddPoi(PoiType.ResourceCache, new Vector3(20f, 0f, 0f));
            var v = MakeVillager(1);

            Assert.IsNotNull(_system.LaunchExpedition(poi, new List<VillagerAgent> { v }));
            yield return null;

            // The party walks off toward the POI during the day.
            _system.Tick(0.2f);
            Assert.Greater(v.transform.position.x, 1f, "the party heads out of camp toward the POI");
            yield return null;

            for (int i = 0; i < 60 && _system.Expeditions.Count > 0; i++)
            {
                _system.Tick(0.2f);
                yield return null;
            }

            Assert.IsTrue(poi.discovered, "the POI flips to discovered on the debug/panel state");
            Assert.AreEqual(4, ResourceLedger.Get(ResourceType.Wood), "the reward landed");
            Assert.AreEqual(slotsBefore + 1, _seeds.CountByState(SlotState.Open),
                "a seed slot was unlocked beside the POI");
            Assert.Less(Vector3.Distance(v.transform.position, Vector3.zero), 1f,
                "the party returns to the muster point before the day ends");
        }

        [UnityTest]
        public IEnumerator Expedition_CaughtByDusk_ViaPhaseHook()
        {
            var poi = _system.AddPoi(PoiType.ResourceCache, new Vector3(40f, 0f, 0f));
            var v = MakeVillager(2);
            _system.LaunchExpedition(poi, new List<VillagerAgent> { v });
            yield return null;

            _system.Tick(0.2f); // still far from a distant POI
            Assert.AreEqual(1, _system.Expeditions.Count);

            // Dusk falls: the real phase hook catches the party out.
            EventBus.RaisePhaseChanged(DayPhase.Dusk);
            yield return null;

            Assert.AreEqual(0, _system.Expeditions.Count, "the expedition is stranded at dusk");
            Assert.IsFalse(_system.IsAway(v), "the villager is handed back to the dusk recall");
            Assert.IsFalse(poi.discovered);
        }
    }
}
