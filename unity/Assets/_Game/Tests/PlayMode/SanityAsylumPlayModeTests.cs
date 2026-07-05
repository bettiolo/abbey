using System.Collections;
using System.Collections.Generic;
using Abbey.Beast;
using Abbey.Buildings;
using Abbey.Core;
using Abbey.Light;
using Abbey.Sanity;
using Abbey.Villagers;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Abbey.Tests.PlayMode
{
    /// <summary>
    /// PlayMode coverage for the asylum + home-recovery half of P3-03 (worlds built
    /// programmatically, deterministic manual ticks). An insane villager admitted to
    /// the asylum is held — parked, absent from the night's recall/defense
    /// participation — and released only by day once its cooldown has elapsed and it
    /// has recovered; without an asylum it recovers slower at home while its
    /// housemates gain dread each night; and the hound is never tracked (immunity).
    /// </summary>
    public class SanityAsylumPlayModeTests
    {
        readonly List<GameObject> _spawned = new List<GameObject>();
        readonly List<ScriptableObject> _assets = new List<ScriptableObject>();
        PrototypeConfig _proto;
        SanityConfig _sanity;
        GameClock _clock;
        SanitySystem _system;

        [SetUp]
        public void SetUp()
        {
            EventBus.ResetAll();
            GameEventLog.Clear();
            DarknessEvaluator.Clear();
            DuskRecallSystem.Clear();
            Building.ClearRegistry();
            SanityConfig.ClearCache();
            PrototypeConfig.ClearCache();

            _proto = ScriptableObject.CreateInstance<PrototypeConfig>();
            _assets.Add(_proto);
            _proto.dayDurationSeconds = 1f;
            _proto.duskDurationSeconds = 1f;
            _proto.nightDurationSeconds = 1f;
            _proto.dawnDurationSeconds = 1f;
            _proto.villagerFearPerSecondInDark = 0f;
            _proto.villagerInjuredDarkSeconds = 10000f;
            _proto.villagerMissingDarkSeconds = 20000f;

            _sanity = ScriptableObject.CreateInstance<SanityConfig>();
            _assets.Add(_sanity);
            _sanity.insanityThreshold = 0.2f;
            _sanity.breakingThreshold = 0.4f;
            _sanity.shakenThreshold = 0.7f;
            _sanity.releaseThreshold = 0.5f;
            _sanity.dreadDecayPerSecond = 0.1f;
            _sanity.asylumRecoveryPerSecond = 0.3f;
            _sanity.homeRecoveryPerSecond = 0.05f;
            _sanity.asylumCooldownDays = 1;
            _sanity.dreadSpillPerNight = 0.2f;

            DarknessEvaluator.Config = _proto;
            DuskRecallSystem.Config = _proto;

            var clockGO = new GameObject("Clock");
            _spawned.Add(clockGO);
            _clock = clockGO.AddComponent<GameClock>();
            _clock.autoTick = false;
            _clock.Configure(_proto);

            var sysGO = new GameObject("SanitySystem");
            _spawned.Add(sysGO);
            _system = sysGO.AddComponent<SanitySystem>();
            _system.autoTick = false;
            _system.Config = _sanity;
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
            EventBus.ResetAll();
            GameEventLog.Clear();
            DarknessEvaluator.Clear();
            DuskRecallSystem.Clear();
            Building.ClearRegistry();
            SanityConfig.ClearCache();
            PrototypeConfig.ClearCache();
        }

        // ---- Helpers -----------------------------------------------------

        VillagerAgent SpawnVillager(Vector3 position, int seed = 1)
        {
            var go = new GameObject($"Villager_{_spawned.Count}");
            _spawned.Add(go);
            go.transform.position = position;
            var v = go.AddComponent<VillagerAgent>();
            v.autoTick = false;
            v.Config = _proto;
            v.seed = seed;
            v.Bravery = 0.5f;
            DuskRecallSystem.Register(v);
            return v;
        }

        AsylumZone SpawnAsylum(Vector3 position)
        {
            var go = new GameObject("Asylum");
            _spawned.Add(go);
            go.transform.position = position;
            var zone = go.AddComponent<AsylumZone>();
            zone.autoTick = false;
            zone.radius = 6f;
            return zone;
        }

        Building SpawnHome(Vector3 position)
        {
            var go = new GameObject($"Home_{_spawned.Count}");
            _spawned.Add(go);
            go.transform.position = position;
            return go.AddComponent<Building>();
        }

        void ForceInsane(VillagerAgent v)
        {
            var rec = _system.RecordFor(v);
            rec.Sanity = 0.1f;
            _system.Tick(0.01f); // Day: UpdateBand flips it Insane
            Assert.IsTrue(rec.IsInsane, "setup: the villager must be insane");
        }

        void AdvanceClockTo(DayPhase target)
        {
            int guard = 10000;
            while (_clock.Phase != target && guard-- > 0)
            {
                _clock.Tick(0.25f);
            }
            Assert.Greater(guard, 0, $"clock never reached {target}");
        }

        // ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator InsaneVillager_AdmittedToAsylum_MissesNight_ReleasedByDay()
        {
            yield return null; // let play-mode wiring settle

            var asylum = SpawnAsylum(new Vector3(50f, 0f, 50f));
            _system.Asylum = asylum;
            var patient = SpawnVillager(Vector3.zero);
            var healthy = SpawnVillager(new Vector3(2f, 0f, 0f), seed: 2);
            _system.RecordFor(healthy); // healthy control, sanity 1

            bool admitted = false;
            bool released = false;
            EventBus.AsylumAdmitted += _ => admitted = true;
            EventBus.AsylumReleased += _ => released = true;

            // Day 1: go insane, then day-onset admits and parks the patient.
            ForceInsane(patient);
            _system.EvaluateDayOnset();

            Assert.IsTrue(admitted, "AsylumAdmitted must fire");
            Assert.IsTrue(_system.IsHeldInAsylum(patient), "the patient is held");
            Assert.AreEqual(1, asylum.AdmittedCount);
            Assert.Less(Vector3.Distance(patient.transform.position, asylum.transform.position), 0.1f,
                "the patient is parked at the asylum");

            // The following night: the patient misses it (not available); the healthy
            // villager is. Recovery ticks the patient's sanity back up under care.
            AdvanceClockTo(DayPhase.Night);
            Assert.IsFalse(_system.IsAvailableForNight(patient),
                "a held villager is absent from the night's recall/defense participation");
            Assert.IsTrue(_system.IsAvailableForNight(healthy), "the healthy villager takes part");

            for (int i = 0; i < 40; i++)
            {
                _system.Tick(0.05f); // asylum recovery
            }
            var patientRec = _system.RecordFor(patient);
            Assert.GreaterOrEqual(patientRec.Sanity, _sanity.releaseThreshold,
                "care restores sanity above the release band");
            Assert.IsTrue(_system.IsHeldInAsylum(patient), "still held through the night it missed");

            // Next day (day 2): cooldown elapsed + recovered -> released by day.
            AdvanceClockTo(DayPhase.Day);
            Assert.AreEqual(2, _clock.DayNumber);
            Assert.IsTrue(released, "AsylumReleased must fire on the day after the cooldown");
            Assert.IsFalse(_system.IsHeldInAsylum(patient), "the patient is discharged");
            Assert.AreEqual(0, asylum.AdmittedCount);
            Assert.AreEqual(VillagerState.Idle, patient.State);
        }

        [UnityTest]
        public IEnumerator NoAsylum_HomeRecoverySlower_AndHousematesGainDread()
        {
            yield return null;

            _system.Asylum = null; // no asylum anywhere: the home path
            var home = SpawnHome(new Vector3(5f, 0f, 5f));
            var patient = SpawnVillager(Vector3.zero);
            var mate = SpawnVillager(new Vector3(1f, 0f, 0f), seed: 2);
            _system.AssignHome(patient, home);
            _system.AssignHome(mate, home);
            var mateRec = _system.RecordFor(mate);

            bool disturbed = false;
            EventBus.HouseholdDisturbed += _ => disturbed = true;

            ForceInsane(patient);
            Assert.IsNull(_system.Asylum, "sanity: no asylum in this world");

            // Night onset: the insane settler disturbs the household — the mate gains dread.
            float mateDreadBefore = mateRec.Dread;
            _system.EvaluateNightOnset();
            Assert.IsTrue(disturbed, "HouseholdDisturbed must fire");
            Assert.AreEqual(mateDreadBefore + _sanity.dreadSpillPerNight, mateRec.Dread, 1e-4f,
                "each housemate gains a night's worth of dread");

            // Home recovery is slow — and slower than the asylum would be.
            var patientRec = _system.RecordFor(patient);
            float sanityBefore = patientRec.Sanity;
            for (int i = 0; i < 20; i++)
            {
                _system.Tick(0.05f); // 1s of daytime home recovery
            }
            float homeGain = patientRec.Sanity - sanityBefore;
            Assert.AreEqual(_sanity.homeRecoveryPerSecond, homeGain, 5e-3f,
                "home recovery accrues at the slow home rate");
            Assert.Less(homeGain, _sanity.asylumRecoveryPerSecond,
                "home recovery is slower than the asylum would be");
        }

        [UnityTest]
        public IEnumerator Hound_IsImmune_NeverInSanityRecords()
        {
            yield return null;

            var v = SpawnVillager(new Vector3(100f, 0f, 100f));
            var houndGO = new GameObject("Hound");
            _spawned.Add(houndGO);
            houndGO.AddComponent<HoundController>().autoTick = false;

            Assert.IsNull(houndGO.GetComponent<VillagerAgent>(),
                "the hound carries no villager/sanity component");

            AdvanceClockTo(DayPhase.Night);
            for (int i = 0; i < 20; i++)
            {
                _system.Tick(0.1f);
            }

            Assert.AreEqual(1, _system.Records.Count, "only the villager is tracked");
            Assert.AreSame(v, _system.Records[0].Villager, "and never the hound");
        }
    }
}
