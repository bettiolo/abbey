using Abbey.Core;
using Abbey.Nightmares;
using Abbey.Session;
using NUnit.Framework;
using UnityEngine;

namespace Abbey.Tests.EditMode
{
    public class Phase3SceneModeTests
    {
        GameObject _go;
        PrototypeConfig _prototype;
        GameSessionConfig _session;

        [SetUp]
        public void SetUp()
        {
            EventBus.ResetAll();
            GameEventLog.Clear();
            FirstWhiteNightScenario.ResetStaticEvents();
            PrototypeConfig.ClearCache();
            GameSessionConfig.ClearCache();

            _prototype = ScriptableObject.CreateInstance<PrototypeConfig>();
            _session = ScriptableObject.CreateInstance<GameSessionConfig>();
            _go = new GameObject("test");
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
            {
                Object.DestroyImmediate(_go);
            }
            if (_prototype != null)
            {
                Object.DestroyImmediate(_prototype);
            }
            if (_session != null)
            {
                Object.DestroyImmediate(_session);
            }
            EventBus.ResetAll();
            GameEventLog.Clear();
            FirstWhiteNightScenario.ResetStaticEvents();
            PrototypeConfig.ClearCache();
            GameSessionConfig.ClearCache();
        }

        [Test]
        public void Bootstrap_EnablesPhase3NightAndCampaignFlags()
        {
            Assert.IsFalse(_prototype.phase3NightsEnabled);
            Assert.IsFalse(_session.phase3CampaignEnabled);

            PrototypePhase3Bootstrap.ApplyTo(_prototype, _session,
                enableNights: true, enableCampaign: true);

            Assert.IsTrue(_prototype.phase3NightsEnabled);
            Assert.IsTrue(_session.phase3CampaignEnabled);
        }

        [Test]
        public void CampaignMode_SkipsLegacyWhiteNightScenario()
        {
            _session.phase3CampaignEnabled = true;
            _session.whiteNightIndex = 1;

            var director = _go.AddComponent<NightmareDirector>();
            director.Config = _prototype;

            var scenario = _go.AddComponent<FirstWhiteNightScenario>();
            scenario.Config = _session;
            scenario.director = director;

            scenario.ArmWhiteNight();

            Assert.IsFalse(_prototype.phase2NightsEnabled,
                "campaign mode must not switch the director into the old Phase 2 White Night schedule");
            Assert.IsFalse(scenario.IsArmed);
            Assert.IsTrue(LogContains("white_night_legacy_scenario_skipped"));
        }

        static bool LogContains(string fragment)
        {
            var records = GameEventLog.Records;
            for (int i = 0; i < records.Count; i++)
            {
                if (records[i].Data.Contains(fragment))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
