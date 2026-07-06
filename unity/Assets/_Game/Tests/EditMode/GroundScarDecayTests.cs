using System.Collections.Generic;
using Abbey.Core;
using Abbey.Settlement;
using Abbey.World;
using NUnit.Framework;
using UnityEngine;

namespace Abbey.Tests.EditMode
{
    /// <summary>
    /// Ground scars (P3-12): the night's violence is stamped at dawn, fades to meadow
    /// before dusk in the growing seasons, and in Winter persists snow-covered instead
    /// of regrowing. Nearby stamps merge. Built programmatically; the winter branch is
    /// forced through the system's season override so no clock/calendar is needed.
    /// </summary>
    public class GroundScarDecayTests
    {
        readonly List<GameObject> _spawned = new List<GameObject>();
        PathsConfig _config;
        GroundScarSystem _scars;

        [SetUp]
        public void SetUp()
        {
            EventBus.ResetAll();
            GameEventLog.Clear();
            PathsConfig.ClearCache();

            _config = ScriptableObject.CreateInstance<PathsConfig>();
            _config.scarStampRadius = 3f;
            _config.scarInitialIntensity = 1f;
            _config.scarMergeRadius = 1.5f;
            _config.scarFadeDurationSeconds = 20f;
            _config.winterSnowCoversScars = true;

            _scars = NewGO("GroundScarSystem").AddComponent<GroundScarSystem>();
            _scars.autoTick = false;
            _scars.Config = _config;
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

        void SetSeason(Season season)
        {
            _scars.useSeasonOverride = true;
            _scars.seasonOverride = season;
        }

        [Test]
        public void BufferedViolence_StampsScarAtDawn()
        {
            SetSeason(Season.Spring);
            var pos = new Vector3(5f, 0f, -3f);
            _scars.RecordViolence(pos);
            Assert.AreEqual(1, _scars.PendingViolenceCount);
            Assert.AreEqual(0, _scars.ScarCount, "nothing is stamped until dawn");

            EventBus.RaisePhaseChanged(DayPhase.Dawn);

            Assert.AreEqual(0, _scars.PendingViolenceCount, "the buffer is drained at dawn");
            Assert.AreEqual(1, _scars.ScarCount);
            Assert.Greater(_scars.IntensityAt(pos), 0.9f, "a fresh scar sits at the fight");
        }

        [Test]
        public void HomeRazedEvent_IsBufferedAndStamped()
        {
            SetSeason(Season.Summer);
            var home = NewGO("Home");
            home.transform.position = new Vector3(-2f, 0f, 4f);

            EventBus.RaiseHomeRazed(home); // night
            Assert.AreEqual(1, _scars.PendingViolenceCount);

            EventBus.RaisePhaseChanged(DayPhase.Dawn);
            Assert.AreEqual(1, _scars.ScarCount);
            Assert.Greater(_scars.IntensityAt(home.transform.position), 0.9f);
        }

        [Test]
        public void Scar_FadesToZeroBeforeDusk_InSpring()
        {
            SetSeason(Season.Spring);
            var pos = Vector3.zero;
            _scars.StampScar(pos);
            Assert.AreEqual(1f, _scars.IntensityAt(pos), 1e-3f);

            // Half the fade window: still visible, dimmer.
            _scars.Tick(10f);
            Assert.AreEqual(0.5f, _scars.IntensityAt(pos), 1e-2f);

            // The rest of the day: fully regrown before dusk.
            _scars.Tick(10f);
            Assert.AreEqual(0f, _scars.IntensityAt(pos), 1e-4f);
            Assert.AreEqual(0, _scars.ScarCount, "a faded scar is removed");
        }

        [Test]
        public void DuskRegrowsRemainingScars_InSpring()
        {
            SetSeason(Season.Spring);
            _scars.StampScar(new Vector3(1f, 0f, 1f));
            _scars.Tick(5f); // partly faded but still present
            Assert.AreEqual(1, _scars.ScarCount);

            EventBus.RaisePhaseChanged(DayPhase.Dusk);
            Assert.AreEqual(0, _scars.ScarCount, "meadow has fully regrown by dusk");
        }

        [Test]
        public void WinterScar_PersistsSnowCovered_AndDoesNotFade()
        {
            SetSeason(Season.Winter);
            var pos = new Vector3(3f, 0f, 3f);
            _scars.StampScar(pos);

            Assert.AreEqual(1, _scars.ScarCount);
            Assert.AreEqual(1, _scars.SnowCoveredCount(), "a Winter scar is snow-covered");

            _scars.Tick(1000f); // a whole winter day of would-be fading
            Assert.AreEqual(1, _scars.ScarCount, "snow holds the scar — it does not regrow");
            Assert.Greater(_scars.IntensityAt(pos), 0.9f);

            EventBus.RaisePhaseChanged(DayPhase.Dusk);
            Assert.AreEqual(1, _scars.ScarCount, "dusk does not clear snow-covered scars");
        }

        [Test]
        public void NearbyStamps_Merge_FarStampsDoNot()
        {
            SetSeason(Season.Spring);
            var a = Vector3.zero;
            _scars.StampScar(a);
            _scars.StampScar(a + new Vector3(1f, 0f, 0f)); // within mergeRadius 1.5
            Assert.AreEqual(1, _scars.ScarCount, "close stamps merge into one scar");

            _scars.StampScar(new Vector3(10f, 0f, 10f)); // far away
            Assert.AreEqual(2, _scars.ScarCount, "a distant fight is its own scar");
        }

        [Test]
        public void MergedStamp_IntensityClampsToOne()
        {
            SetSeason(Season.Spring);
            _config.scarInitialIntensity = 0.7f;
            var pos = Vector3.zero;
            _scars.StampScar(pos);
            _scars.StampScar(pos); // 0.7 + 0.7 clamped to 1
            Assert.AreEqual(1, _scars.ScarCount);
            Assert.AreEqual(1f, _scars.IntensityAt(pos), 1e-4f);
        }

        [Test]
        public void CleanGround_HasNoScarIntensity()
        {
            SetSeason(Season.Spring);
            _scars.StampScar(Vector3.zero);
            Assert.AreEqual(0f, _scars.IntensityAt(new Vector3(50f, 0f, 50f)),
                "far from any scar the ground is clean");
        }
    }
}
