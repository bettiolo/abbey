using System.Collections;
using System.Collections.Generic;
using Abbey.Core;
using Abbey.Economy;
using Abbey.Light;
using Abbey.Map2;
using Abbey.Nightmares;
using Abbey.Villagers;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Abbey.Tests.PlayMode
{
    public class Map2FullLoopPlayModeTests
    {
        readonly List<GameObject> _objects = new List<GameObject>();
        readonly List<Object> _assets = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            Clear();
            var economy = ScriptableObject.CreateInstance<EconomyConfig>();
            economy.baseStorageCapacity = 1000;
            ResourceLedger.Config = economy;
            _assets.Add(economy);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _objects) if (go != null) Object.DestroyImmediate(go);
            foreach (var asset in _assets) if (asset != null) Object.DestroyImmediate(asset);
            _objects.Clear();
            _assets.Clear();
            Clear();
        }

        static void Clear()
        {
            EventBus.ResetAll();
            GameEventLog.Clear();
            ResourceLedger.Clear();
            DuskRecallSystem.Clear();
            DarknessEvaluator.Clear();
            Map2Config.ClearCache();
            ThreatConfig.ClearCache();
            Map2Scenario.ResetStaticEvents();
        }

        T Spawn<T>(string name) where T : Component
        {
            var go = new GameObject(name);
            _objects.Add(go);
            return go.AddComponent<T>();
        }

        [UnityTest]
        public IEnumerator ForestStory_FalseBellIsBroken_ThenRestorationWinsCovenant()
        {
            var config = ScriptableObject.CreateInstance<Map2Config>();
            config.minimumNightsSurvived = 1;
            _assets.Add(config);

            var threatConfig = ScriptableObject.CreateInstance<ThreatConfig>();
            threatConfig.falseBellRadius = 30f;
            threatConfig.falseLightDistance = 8f;
            _assets.Add(threatConfig);

            var threat = Spawn<ThreatSourceSystem>("Threats");
            threat.Config = threatConfig;
            var guidance = Spawn<FalseGuidanceSystem>("FalseGuidance");
            guidance.Config = threatConfig;
            var stag = Spawn<StagCovenantSystem>("Stag");
            stag.Config = config;
            var villager = Spawn<VillagerAgent>("Villager");
            villager.autoTick = false;
            var scenario = Spawn<Map2Scenario>("Map2Scenario");
            scenario.Config = config;
            scenario.stag = stag;
            scenario.autoEvaluate = false;

            stag.RecordWorldChoice("old_growth_cutting");
            threat.RecomputeFromLog();
            guidance.RecordNightmareSpawn(NightmareType.BellMimic,
                new Vector3(2f, 0f, 0f), Vector3.zero);
            Assert.IsTrue(villager.IsFollowingFalseGuidance);

            villager.OrderReturnToLight(bellBoosted: true);
            Assert.IsFalse(villager.IsFollowingFalseGuidance, "the True Bell breaks the lure");

            GameEventLog.Clear();
            for (int i = 0; i < 4; i++) stag.RecordWorldChoice("leave_offering");
            stag.RecordWorldChoice("replanting");
            ResourceLedger.Add(ResourceType.SacredSeeds, 4, "stag garden");
            ResourceLedger.Add(ResourceType.Herbs, 5, "herbalist");
            ResourceLedger.Add(ResourceType.Resin, 3, "forester");
            threat.RecomputeFromLog();

            EventBus.RaisePhaseChanged(DayPhase.Dawn);
            yield return null;

            Assert.AreEqual(StagState.Allied, stag.State);
            Assert.AreEqual(Map2Result.CovenantVictory, scenario.Evaluate());
            StringAssert.Contains("marked trees stand", scenario.Chronicle);
        }
    }
}
