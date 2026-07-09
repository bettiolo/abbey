using System.Collections.Generic;
using Abbey.Core;
using Abbey.Economy;
using Abbey.Hero;
using Abbey.Island;
using Abbey.Map2;
using Abbey.Nightmares;
using Abbey.Session;
using Abbey.Villagers;
using NUnit.Framework;
using UnityEngine;

namespace Abbey.Tests.EditMode
{
    public class Map2SystemsTests
    {
        readonly List<GameObject> _objects = new List<GameObject>();
        readonly List<Object> _assets = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            ClearStatics();
            var economy = ScriptableObject.CreateInstance<EconomyConfig>();
            economy.baseStorageCapacity = 1000;
            ResourceLedger.Config = economy;
            _assets.Add(economy);
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _objects.Count; i++)
                if (_objects[i] != null) Object.DestroyImmediate(_objects[i]);
            for (int i = 0; i < _assets.Count; i++)
                if (_assets[i] != null) Object.DestroyImmediate(_assets[i]);
            _objects.Clear();
            _assets.Clear();
            ClearStatics();
        }

        static void ClearStatics()
        {
            EventBus.ResetAll();
            GameEventLog.Clear();
            ResourceLedger.Clear();
            DuskRecallSystem.Clear();
            Map2Config.ClearCache();
            ThreatConfig.ClearCache();
            IslandConfig.ClearCache();
            Map2Scenario.ResetStaticEvents();
            PrototypeConfig.ClearCache();
        }

        T Spawn<T>(string name) where T : Component
        {
            var go = new GameObject(name);
            _objects.Add(go);
            return go.AddComponent<T>();
        }

        [Test]
        public void Stag_ReactsToRestraint_AndBreaksUnderExtraction()
        {
            var stag = Spawn<StagCovenantSystem>("Stag");
            Assert.AreEqual(StagState.Hidden, stag.State);

            for (int i = 0; i < 4; i++) stag.RecordWorldChoice("leave_offering");
            Assert.AreEqual(StagState.Allied, stag.State);
            Assert.GreaterOrEqual(stag.Covenant, 0.75f);

            GameEventLog.Clear();
            for (int i = 0; i < 4; i++) stag.RecordWorldChoice("old_growth_cutting");
            Assert.AreEqual(StagState.HornedAccuser, stag.State);
            Assert.IsTrue(stag.CovenantBroken);
        }

        [Test]
        public void FourForestDilemmas_AreDataDriven_AndOldTreeFeedsDebtAndStag()
        {
            var island = ScriptableObject.CreateInstance<IslandConfig>();
            _assets.Add(island);
            string[] ids = { "old_tree", "starving_deer", "lost_woodcutters", "charcoal_camp" };
            for (int i = 0; i < ids.Length; i++) Assert.IsNotNull(island.CardFor(ids[i]), ids[i]);

            var threat = Spawn<ThreatSourceSystem>("Threats");
            var stag = Spawn<StagCovenantSystem>("Stag");
            var dilemmas = Spawn<DilemmaSystem>("Dilemmas");
            dilemmas.Configure(island);

            Assert.IsTrue(dilemmas.EnqueueCard("old_tree"));
            Assert.IsTrue(dilemmas.ChooseById("fell_it"));
            threat.RecomputeFromLog();
            stag.RecomputeFromLog();

            Assert.AreEqual(8, ResourceLedger.Get(ResourceType.OldWood));
            Assert.Greater(threat.PressureFor(ThreatSourceType.Forest), 0f);
            Assert.Less(stag.Covenant, stag.Config.startingCovenant);
        }

        [Test]
        public void Carryover_DerivesExactlyOneTrait_AndCommandingVoiceExtendsBell()
        {
            var outcome = new CampaignOutcome
            {
                result = CampaignResult.ShipSailed.ToString(),
                resultCode = (int)CampaignResult.ShipSailed,
                houndPath = "War",
                abbeyForm = "Balanced",
            };
            Assert.AreEqual(BellkeeperTrait.CommandingVoice,
                CampaignCarryoverSystem.DeriveTrait(outcome));

            var config = ScriptableObject.CreateInstance<Map2Config>();
            config.commandingVoiceBellRadiusMultiplier = 1.5f;
            _assets.Add(config);
            var carry = Spawn<CampaignCarryoverSystem>("Carryover");
            var hero = Spawn<BellkeeperController>("Bellkeeper");
            var proto = ScriptableObject.CreateInstance<PrototypeConfig>();
            proto.bellRadius = 10f;
            proto.bellCooldownSeconds = 0f;
            _assets.Add(proto);
            hero.Config = proto;
            carry.Configure(outcome, config);

            float heardRadius = 0f;
            EventBus.BellRang += (_, radius) => heardRadius = radius;
            Assert.IsTrue(hero.RingBell());
            Assert.AreEqual(15f, heardRadius, 0.001f);

            outcome.houndPath = "Sacred";
            Assert.AreEqual(BellkeeperTrait.RitualAuthority,
                CampaignCarryoverSystem.DeriveTrait(outcome));
            outcome.houndPath = "Broken";
            Assert.AreEqual(BellkeeperTrait.HardLessons,
                CampaignCarryoverSystem.DeriveTrait(outcome));
            Assert.IsTrue(CampaignFlowController.CanUnlock(outcome));
            outcome.resultCode = (int)CampaignResult.InProgress;
            Assert.IsFalse(CampaignFlowController.CanUnlock(outcome));
        }

        [Test]
        public void Map2Scenario_AllowsCovenantAndExploitativeWins_AndBrokenCovenantLoses()
        {
            var config = ScriptableObject.CreateInstance<Map2Config>();
            config.minimumNightsSurvived = 0;
            _assets.Add(config);

            var threat = Spawn<ThreatSourceSystem>("Threats");
            var stag = Spawn<StagCovenantSystem>("Stag");
            stag.Config = config;
            var scenario = Spawn<Map2Scenario>("Scenario");
            scenario.Config = config;
            scenario.stag = stag;
            scenario.autoEvaluate = false;

            for (int i = 0; i < 4; i++) stag.RecordWorldChoice("leave_offering");
            ResourceLedger.Add(ResourceType.SacredSeeds, 4, "grove");
            ResourceLedger.Add(ResourceType.Herbs, 5, "grove");
            ResourceLedger.Add(ResourceType.Resin, 3, "grove");
            threat.RecomputeFromLog();
            Assert.AreEqual(Map2Result.CovenantVictory, scenario.Evaluate());

            scenario.Clear();
            GameEventLog.Clear();
            ResourceLedger.Clear();
            var econ = ScriptableObject.CreateInstance<EconomyConfig>();
            econ.baseStorageCapacity = 1000;
            ResourceLedger.Config = econ;
            _assets.Add(econ);
            ResourceLedger.Add(ResourceType.OldWood, 10, "logging");
            ResourceLedger.Add(ResourceType.Charcoal, 8, "kilns");
            ResourceLedger.Add(ResourceType.Venison, 6, "hunt");
            threat.RecomputeFromLog();
            stag.RecomputeFromLog();
            Assert.AreEqual(Map2Result.ExploitativeVictory, scenario.Evaluate());

            scenario.Clear();
            for (int i = 0; i < 4; i++) stag.RecordWorldChoice("old_growth_cutting");
            Assert.AreEqual(Map2Result.Loss, scenario.Evaluate());
            Assert.AreEqual(Map2LossReason.CovenantBroken, scenario.LossReason);
        }

        [Test]
        public void Map2Scenario_RaisesForestDilemmasOnConfiguredDays()
        {
            var config = ScriptableObject.CreateInstance<Map2Config>();
            _assets.Add(config);
            var dilemmas = Spawn<DilemmaSystem>("Dilemmas");
            var scenario = Spawn<Map2Scenario>("Scenario");
            scenario.Config = config;

            EventBus.RaiseDayChanged(2);

            Assert.IsNotNull(dilemmas.PendingCard);
            Assert.AreEqual("old_tree", dilemmas.PendingCard.id);
        }
    }
}
