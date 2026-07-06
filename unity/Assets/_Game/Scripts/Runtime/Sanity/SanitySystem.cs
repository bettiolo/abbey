using System.Collections.Generic;
using Abbey.Buildings;
using Abbey.Core;
using Abbey.Light;
using Abbey.Villagers;
using UnityEngine;

namespace Abbey.Sanity
{
    /// <summary>
    /// The persistent-damage track of the game (ROADMAP Phase 3 items 3 + 18). Keeps
    /// one <see cref="SanityRecord"/> per <see cref="VillagerAgent"/> (found through
    /// the <see cref="DuskRecallSystem"/> registry, so the hound — not a villager —
    /// is never tracked and stays immune) and runs the dark-exposure pipeline:
    ///
    ///  * Night + Dark  → dread rises; sustained dread past the config threshold eats
    ///    into sanity; sanity below the insanity threshold flips the villager Insane
    ///    (raising <see cref="EventBus.VillagerWentInsane"/>).
    ///  * Morning (Dawn) → health resets (Injured/Resting villagers return to Idle)
    ///    but sanity persists — that is the whole point of the second track.
    ///  * Day → an insane villager is admitted to the <see cref="AsylumZone"/> (parked,
    ///    missing the coming night) and recovers fast; it is released only by day once
    ///    the cooldown has elapsed and sanity has climbed back to the release band.
    ///  * With no asylum → the insane settler recovers slowly at home and, each night,
    ///    spreads dread to its housemates (screaming, nightmares — the spill).
    ///  * Daytime work efficiency scales with sanity (insane ⇒ zero: they stop working).
    ///
    /// All thresholds/rates live in <see cref="SanityConfig"/> (no balance here).
    /// Singleton like <see cref="Abbey.World.SeasonSystem"/>; [ExecuteAlways] so
    /// EditMode tests get the OnEnable/OnDisable lifecycle. Discrete beats
    /// (morning reset, night spill, day admission/release) run off
    /// <see cref="EventBus.PhaseChanged"/>; the per-second dread/recovery math runs in
    /// <see cref="Tick"/> (autoTick = false in tests).
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class SanitySystem : MonoBehaviour
    {
        public static SanitySystem Instance { get; private set; }

        [Tooltip("Advance from Update using Time.deltaTime. Tests set false and call Tick().")]
        public bool autoTick = true;

        SanityConfig _config;
        AsylumZone _asylum;
        bool _isDuplicate;

        readonly List<SanityRecord> _records = new List<SanityRecord>();
        readonly Dictionary<VillagerAgent, SanityRecord> _byVillager =
            new Dictionary<VillagerAgent, SanityRecord>();
        readonly Dictionary<VillagerAgent, Building> _homes =
            new Dictionary<VillagerAgent, Building>();
        readonly HashSet<Building> _homeScratch = new HashSet<Building>();

        /// <summary>Every tracked villager's live sanity record (debug overlay / downstream).</summary>
        public IReadOnlyList<SanityRecord> Records => _records;

        public SanityConfig Config
        {
            get
            {
                if (_config == null)
                {
                    _config = SanityConfig.LoadOrDefault();
                }
                return _config;
            }
            set { _config = value; }
        }

        /// <summary>
        /// The asylum this settlement has (if any). Auto-found in the scene; tests
        /// inject one. When null, insane settlers recover slowly at home instead.
        /// </summary>
        public AsylumZone Asylum
        {
            get
            {
                if (_asylum == null)
                {
                    _asylum = FindFirstObjectByType<AsylumZone>();
                }
                return _asylum;
            }
            set { _asylum = value; }
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                _isDuplicate = true;
                Debug.LogWarning("[SanitySystem] Duplicate instance ignored.", this);
                if (Application.isPlaying)
                {
                    Destroy(gameObject);
                }
                return;
            }
            _isDuplicate = false;
            Instance = this;
        }

        void OnEnable()
        {
            if (_isDuplicate)
            {
                return;
            }
            EventBus.PhaseChanged -= OnPhaseChanged;
            EventBus.PhaseChanged += OnPhaseChanged;
        }

        void OnDisable()
        {
            EventBus.PhaseChanged -= OnPhaseChanged;
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>Injects a config (tests) and clears all records/homes.</summary>
        public void Configure(SanityConfig config)
        {
            _config = config;
            _records.Clear();
            _byVillager.Clear();
            _homes.Clear();
        }

        void Update()
        {
            if (_isDuplicate || !Application.isPlaying || !autoTick)
            {
                return;
            }
            Tick(Time.deltaTime);
        }

        // ------------------------------------------------------------------
        // Records
        // ------------------------------------------------------------------

        /// <summary>The record for a villager, creating one if it is not tracked yet.</summary>
        public SanityRecord RecordFor(VillagerAgent villager)
        {
            if (villager == null)
            {
                return null;
            }
            if (!_byVillager.TryGetValue(villager, out var record))
            {
                record = new SanityRecord(villager);
                _byVillager[villager] = record;
                _records.Add(record);
            }
            return record;
        }

        /// <summary>Live record lookup (false when the villager is not tracked).</summary>
        public bool TryGetRecord(VillagerAgent villager, out SanityRecord record)
        {
            record = null;
            return villager != null && _byVillager.TryGetValue(villager, out record);
        }

        /// <summary>Ensures every registered villager has a record and prunes dead ones.</summary>
        public void RefreshRecords()
        {
            var villagers = DuskRecallSystem.Villagers;
            for (int i = 0; i < villagers.Count; i++)
            {
                var v = villagers[i];
                if (v != null)
                {
                    RecordFor(v);
                }
            }
            for (int i = _records.Count - 1; i >= 0; i--)
            {
                if (_records[i].Villager == null)
                {
                    _byVillager.Remove(_records[i].Villager);
                    _records.RemoveAt(i);
                }
            }
        }

        // ------------------------------------------------------------------
        // Homes (shelter occupancy — P3-05 reuses this map)
        // ------------------------------------------------------------------

        /// <summary>Assigns the villager's home shelter (used for the home-recovery dread spill).</summary>
        public void AssignHome(VillagerAgent villager, Building home)
        {
            if (villager == null)
            {
                return;
            }
            if (home == null)
            {
                _homes.Remove(villager);
            }
            else
            {
                _homes[villager] = home;
            }
        }

        /// <summary>The villager's assigned home, or null.</summary>
        public Building HomeOf(VillagerAgent villager)
        {
            return villager != null && _homes.TryGetValue(villager, out var home) ? home : null;
        }

        /// <summary>
        /// Fills <paramref name="buffer"/> with the villagers assigned to a home (the
        /// shelter map is the occupancy source P3-05 home defense reads). Clears the
        /// buffer first; skips null villagers.
        /// </summary>
        public void CollectHomeOccupants(Building home, List<VillagerAgent> buffer)
        {
            if (buffer == null)
            {
                return;
            }
            buffer.Clear();
            if (home == null)
            {
                return;
            }
            foreach (var pair in _homes)
            {
                if (pair.Value == home && pair.Key != null)
                {
                    buffer.Add(pair.Key);
                }
            }
        }

        // ------------------------------------------------------------------
        // Queries (recall / defense participation, downstream aggregation)
        // ------------------------------------------------------------------

        /// <summary>True while the asylum is holding this villager (missing the night).</summary>
        public bool IsHeldInAsylum(VillagerAgent villager)
        {
            return TryGetRecord(villager, out var record) && record.HeldInAsylum;
        }

        /// <summary>
        /// Whether a villager can take part in the night (recall, defense, jobs). An
        /// insane villager — held in the asylum or breaking down at home — cannot.
        /// </summary>
        public bool IsAvailableForNight(VillagerAgent villager)
        {
            return TryGetRecord(villager, out var record) && !record.IsInsane;
        }

        /// <summary>Mean sanity across all tracked villagers (1 when none). P3-10 reads this.</summary>
        public float AverageSanity()
        {
            if (_records.Count == 0)
            {
                return 1f;
            }
            float sum = 0f;
            for (int i = 0; i < _records.Count; i++)
            {
                sum += _records[i].Sanity;
            }
            return sum / _records.Count;
        }

        /// <summary>
        /// Mean sanity of the villagers assigned to a home (1 when the home is empty /
        /// untracked). The household aggregate P3-10 folds into the abbey-transformation
        /// Broken score.
        /// </summary>
        public float HouseholdSanity(Building home)
        {
            if (home == null)
            {
                return 1f;
            }
            float sum = 0f;
            int count = 0;
            foreach (var pair in _homes)
            {
                if (pair.Value != home || pair.Key == null)
                {
                    continue;
                }
                if (_byVillager.TryGetValue(pair.Key, out var record))
                {
                    sum += record.Sanity;
                    count++;
                }
            }
            return count == 0 ? 1f : sum / count;
        }

        /// <summary>Lowest sanity in a home (1 when empty) — the most-broken housemate.</summary>
        public float HouseholdMinSanity(Building home)
        {
            if (home == null)
            {
                return 1f;
            }
            float min = 1f;
            bool any = false;
            foreach (var pair in _homes)
            {
                if (pair.Value != home || pair.Key == null)
                {
                    continue;
                }
                if (_byVillager.TryGetValue(pair.Key, out var record))
                {
                    any = true;
                    if (record.Sanity < min)
                    {
                        min = record.Sanity;
                    }
                }
            }
            return any ? min : 1f;
        }

        /// <summary>
        /// Mean of each household's mean sanity across all assigned homes (P3-10
        /// transformation input). Falls back to <see cref="AverageSanity"/> when no homes are
        /// assigned so a home-less test world still reports a sensible aggregate.
        /// </summary>
        public float AverageHouseholdSanity()
        {
            float sum = 0f;
            int homes = 0;
            _homeScratch.Clear();
            foreach (var pair in _homes)
            {
                if (pair.Value == null || _homeScratch.Contains(pair.Value))
                {
                    continue;
                }
                _homeScratch.Add(pair.Value);
                sum += HouseholdSanity(pair.Value);
                homes++;
            }
            return homes == 0 ? AverageSanity() : sum / homes;
        }

        // ------------------------------------------------------------------
        // Downstream damage hooks (P3-05 defense sanity cost, P3-08 nightmare debt)
        // ------------------------------------------------------------------

        /// <summary>Charges a direct sanity cost (e.g. P3-05 lit-window home defense).</summary>
        public void ApplySanityCost(VillagerAgent villager, float amount, string reason)
        {
            var record = RecordFor(villager);
            if (record == null || amount <= 0f)
            {
                return;
            }
            record.Sanity = Mathf.Clamp01(record.Sanity - amount);
            GameEventLog.Append("sanity_cost", $"{villager.name} -{amount:F2} ({reason})");
            UpdateBand(record, villager);
        }

        /// <summary>Adds dread directly (e.g. P3-08 overdrive nightmare debt).</summary>
        public void AddDread(VillagerAgent villager, float amount, string reason)
        {
            var record = RecordFor(villager);
            if (record == null || amount <= 0f)
            {
                return;
            }
            record.Dread = Mathf.Clamp01(record.Dread + amount);
            GameEventLog.Append("dread_added", $"{villager.name} +{amount:F2} ({reason})");
        }

        // ------------------------------------------------------------------
        // Per-second simulation (dread, sanity damage, recovery, efficiency)
        // ------------------------------------------------------------------

        /// <summary>Deterministic simulation step (autoTick = false in tests).</summary>
        public void Tick(float dt)
        {
            if (_isDuplicate || dt <= 0f)
            {
                return;
            }
            RefreshRecords();

            var cfg = Config;
            var phase = GameClock.Instance != null ? GameClock.Instance.Phase : DayPhase.Day;
            bool night = phase == DayPhase.Night;

            for (int i = 0; i < _records.Count; i++)
            {
                var record = _records[i];
                var v = record.Villager;
                if (v == null)
                {
                    continue;
                }

                if (record.HeldInAsylum)
                {
                    // Fast recovery under care; kept parked at the zone.
                    record.Sanity = Mathf.Clamp01(record.Sanity + cfg.asylumRecoveryPerSecond * dt);
                    record.Dread = Mathf.Max(0f, record.Dread - cfg.dreadDecayPerSecond * dt);
                    var asylum = Asylum;
                    if (asylum != null)
                    {
                        asylum.ParkOccupant(v);
                    }
                    ApplyEfficiency(record, v, cfg);
                    continue;
                }

                if (night)
                {
                    var zone = DarknessEvaluator.Classify(v.transform.position);
                    if (zone == LightZone.Dark)
                    {
                        record.Dread = Mathf.Min(1f, record.Dread + cfg.dreadGainPerSecondInDark * dt);
                        if (record.Dread >= cfg.dreadDamageThreshold)
                        {
                            record.Sanity = Mathf.Max(0f,
                                record.Sanity - cfg.sanityDamagePerSecondAtHighDread * dt);
                        }
                    }
                    else
                    {
                        record.Dread = Mathf.Max(0f, record.Dread - cfg.dreadDecayPerSecond * dt);
                    }
                }
                else
                {
                    // Day/Dusk/Dawn: dread eases; a recovering settler heals slowly at home.
                    record.Dread = Mathf.Max(0f, record.Dread - cfg.dreadDecayPerSecond * dt);
                    if (record.Recovering)
                    {
                        record.Sanity = Mathf.Clamp01(record.Sanity + cfg.homeRecoveryPerSecond * dt);
                    }
                }

                UpdateBand(record, v);
                ApplyEfficiency(record, v, cfg);
            }
        }

        /// <summary>
        /// Recomputes the sanity band with recovery hysteresis and raises the
        /// insanity event on entry. Insane latches <see cref="SanityRecord.Recovering"/>
        /// until sanity climbs back to the release threshold.
        /// </summary>
        void UpdateBand(SanityRecord record, VillagerAgent villager)
        {
            var cfg = Config;
            var previous = record.State;

            if (record.Sanity < cfg.insanityThreshold)
            {
                record.Recovering = true;
            }
            else if (record.Recovering && record.Sanity >= cfg.releaseThreshold)
            {
                record.Recovering = false;
            }

            SanityState next = record.Recovering ? SanityState.Insane : cfg.BandFor(record.Sanity);
            if (next == previous)
            {
                return;
            }

            record.State = next;
            GameEventLog.Append("sanity_state",
                $"{villager.name} {previous}->{next} (sanity={record.Sanity:F2})");
            if (next == SanityState.Insane && previous != SanityState.Insane)
            {
                EventBus.RaiseVillagerWentInsane(villager.gameObject);
            }
        }

        void ApplyEfficiency(SanityRecord record, VillagerAgent villager, SanityConfig cfg)
        {
            villager.SanityWorkEfficiency = record.IsInsane ? 0f : cfg.WorkEfficiency(record.Sanity);
        }

        // ------------------------------------------------------------------
        // Discrete phase beats
        // ------------------------------------------------------------------

        void OnPhaseChanged(DayPhase phase)
        {
            if (_isDuplicate)
            {
                return;
            }
            switch (phase)
            {
                case DayPhase.Day:
                    EvaluateDayOnset();
                    break;
                case DayPhase.Night:
                    EvaluateNightOnset();
                    break;
                case DayPhase.Dawn:
                    MorningReset();
                    break;
            }
        }

        /// <summary>
        /// Morning: health resets — Injured/Resting villagers return to Idle — but
        /// sanity is untouched (the persistent track). Held asylum villagers are left
        /// alone. Public so tests can force it.
        /// </summary>
        public void MorningReset()
        {
            RefreshRecords();
            for (int i = 0; i < _records.Count; i++)
            {
                var record = _records[i];
                var v = record.Villager;
                if (v == null || record.HeldInAsylum)
                {
                    continue;
                }
                if (v.State == VillagerState.Injured || v.State == VillagerState.Resting)
                {
                    GameEventLog.Append("morning_health_reset", v.name);
                    v.ForceState(VillagerState.Idle);
                }
            }
        }

        /// <summary>
        /// Day onset: admit insane villagers to the asylum (parked, held) and release
        /// those whose cooldown has elapsed and who have recovered. Public for tests.
        /// </summary>
        public void EvaluateDayOnset()
        {
            RefreshRecords();
            var cfg = Config;
            var asylum = Asylum;
            int currentDay = GameClock.Instance != null ? GameClock.Instance.DayNumber : 1;

            for (int i = 0; i < _records.Count; i++)
            {
                var record = _records[i];
                var v = record.Villager;
                if (v == null)
                {
                    continue;
                }

                if (record.HeldInAsylum)
                {
                    if (asylum != null
                        && asylum.CooldownElapsed(v, currentDay, cfg.asylumCooldownDays)
                        && record.Sanity >= cfg.releaseThreshold)
                    {
                        asylum.Release(v);
                        record.HeldInAsylum = false;
                        record.Recovering = false;
                        v.ForceState(VillagerState.Idle);
                        UpdateBand(record, v);
                        ApplyEfficiency(record, v, cfg);
                        EventBus.RaiseAsylumReleased(v.gameObject);
                    }
                    continue;
                }

                // A fresh insanity with an asylum available: admit and hold it.
                if (record.IsInsane && asylum != null)
                {
                    asylum.Admit(v, currentDay);
                    record.HeldInAsylum = true;
                    record.AdmitDay = currentDay;
                    v.CancelWork();
                    ApplyEfficiency(record, v, cfg);
                    EventBus.RaiseAsylumAdmitted(v.gameObject);
                }
            }
        }

        /// <summary>
        /// Night onset: with no asylum, every insane settler recovering at home
        /// disturbs its household — each housemate gains a night's worth of dread
        /// (screaming, crying, sleeplessness, nightmares). Public for tests.
        /// </summary>
        public void EvaluateNightOnset()
        {
            RefreshRecords();
            var cfg = Config;

            for (int i = 0; i < _records.Count; i++)
            {
                var record = _records[i];
                var v = record.Villager;
                if (v == null || record.HeldInAsylum || !record.IsInsane)
                {
                    continue;
                }
                var home = HomeOf(v);
                if (home == null)
                {
                    continue;
                }

                bool disturbedAnyone = false;
                for (int j = 0; j < _records.Count; j++)
                {
                    if (j == i)
                    {
                        continue;
                    }
                    var mate = _records[j];
                    if (mate.Villager == null || mate.HeldInAsylum)
                    {
                        continue;
                    }
                    if (HomeOf(mate.Villager) == home)
                    {
                        mate.Dread = Mathf.Min(1f, mate.Dread + cfg.dreadSpillPerNight);
                        disturbedAnyone = true;
                    }
                }
                if (disturbedAnyone)
                {
                    EventBus.RaiseHouseholdDisturbed(v.gameObject);
                }
            }
        }
    }
}
