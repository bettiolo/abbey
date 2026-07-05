using System.Collections.Generic;
using Abbey.Buildings;
using Abbey.Core;
using Abbey.Light;
using Abbey.Sanity;
using Abbey.Villagers;
using UnityEngine;

namespace Abbey.Combat
{
    /// <summary>Two-tier night state of a defended home.</summary>
    public enum HomeDefenseState
    {
        /// <summary>Settlers sheltering and asleep — no threat near the door (free).</summary>
        Sleeping,

        /// <summary>Woken: interior light flared, settlers firing from lit windows at a sanity cost.</summary>
        Awake,

        /// <summary>Overwhelmed and razed — light node lost, occupants dead.</summary>
        Razed
    }

    /// <summary>
    /// The settler tier of night defense (ROADMAP Phase 3 item 17, settler half).
    /// Each night, for every occupied destructible home (<see cref="Building.IsDestructibleHome"/>):
    ///
    /// <list type="number">
    /// <item><b>Sleep</b> — while no monster is within <see cref="CombatConfig.wakeRadius"/>
    /// the house stays <see cref="HomeDefenseState.Sleeping"/>: nothing happens, no
    /// sanity is spent (danger stayed away).</item>
    /// <item><b>Lit-window fire</b> — a monster reaching the door wakes the house
    /// (<see cref="EventBus.HomeWokeForDefense"/>): its interior light flares (a small
    /// Safe zone the assaulting monster is debuffed in), and the occupants fire volleys
    /// on <see cref="CombatConfig.windowShotIntervalSeconds"/> — damage routed through
    /// <see cref="LightBandCombatResolver"/> — paying
    /// <see cref="CombatConfig.sanityCostPerVolley"/> each volley through
    /// <see cref="SanitySystem"/>.</item>
    /// <item><b>Raze</b> — if monsters batter the home's hit points to zero
    /// (<see cref="Building.TakeStructuralDamage"/>, driven by the assaulting
    /// <see cref="Abbey.Nightmares.MonsterController"/>), the home razes: occupants die
    /// and the light node is lost (the door reclassifies Dark).</item>
    /// </list>
    ///
    /// All balance lives in <see cref="CombatConfig"/>; occupancy is read from the
    /// P3-03 shelter map on <see cref="SanitySystem"/>. Deterministic (no RNG):
    /// nearest-target selection and a fixed volley interval. [ExecuteAlways] so
    /// EditMode gets the lifecycle; Update only ticks in play mode with autoTick on.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class HomeDefenseSystem : MonoBehaviour
    {
        public static HomeDefenseSystem Instance { get; private set; }

        [Tooltip("Advance from Update using Time.deltaTime. Tests set false and call Tick().")]
        public bool autoTick = true;

        CombatConfig _config;
        SanitySystem _sanity;
        bool _isDuplicate;

        readonly Dictionary<Building, DefenseRecord> _records = new Dictionary<Building, DefenseRecord>();
        readonly List<Building> _razeScratch = new List<Building>();
        readonly List<VillagerAgent> _occupantScratch = new List<VillagerAgent>();

        class DefenseRecord
        {
            public HomeDefenseState State = HomeDefenseState.Sleeping;
            public float FireTimer;
            public bool Initialized;
        }

        public CombatConfig Config
        {
            get
            {
                if (_config == null)
                {
                    _config = CombatConfig.LoadOrDefault();
                }
                return _config;
            }
            set { _config = value; }
        }

        /// <summary>The sanity system (occupancy source + volley cost sink); auto-found, tests inject.</summary>
        public SanitySystem Sanity
        {
            get
            {
                if (_sanity == null)
                {
                    _sanity = SanitySystem.Instance != null
                        ? SanitySystem.Instance
                        : FindFirstObjectByType<SanitySystem>();
                }
                return _sanity;
            }
            set { _sanity = value; }
        }

        /// <summary>The tracked night state of a home (debug overlay / downstream), or Sleeping if untracked.</summary>
        public HomeDefenseState StateOf(Building home)
        {
            if (home != null && _records.TryGetValue(home, out var record))
            {
                return record.State;
            }
            return home != null && home.IsRazed ? HomeDefenseState.Razed : HomeDefenseState.Sleeping;
        }

        /// <summary>Every home this system is tracking tonight (debug overlay).</summary>
        public IEnumerable<Building> TrackedHomes => _records.Keys;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                _isDuplicate = true;
                Debug.LogWarning("[HomeDefenseSystem] Duplicate instance ignored.", this);
                if (Application.isPlaying)
                {
                    Destroy(gameObject);
                }
                return;
            }
            _isDuplicate = false;
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>Injects a config (tests) and clears all tracked home state.</summary>
        public void Configure(CombatConfig config)
        {
            _config = config;
            _records.Clear();
        }

        void Update()
        {
            if (_isDuplicate || !Application.isPlaying || !autoTick)
            {
                return;
            }
            Tick(Time.deltaTime);
        }

        /// <summary>Deterministic simulation step (autoTick = false in tests).</summary>
        public void Tick(float dt)
        {
            if (_isDuplicate || dt <= 0f)
            {
                return;
            }

            var cfg = Config;
            var phase = GameClock.Instance != null ? GameClock.Instance.Phase : DayPhase.Day;
            bool night = phase == DayPhase.Night;

            var homes = Building.Active;
            for (int i = 0; i < homes.Count; i++)
            {
                var home = homes[i];
                if (home == null || !home.IsDestructibleHome)
                {
                    continue;
                }

                var record = GetOrCreate(home, cfg);

                if (home.IsRazed)
                {
                    if (record.State != HomeDefenseState.Razed)
                    {
                        record.State = HomeDefenseState.Razed;
                        GameEventLog.Append("home_defense", $"{home.name} razed (light node lost)");
                    }
                    continue;
                }

                // Sync occupants from the shelter map so the raze kill-list and the
                // monster's "occupied home" query both see the current household.
                SyncOccupants(home);

                if (!night)
                {
                    if (record.State == HomeDefenseState.Awake)
                    {
                        StandDown(home, record);
                    }
                    continue;
                }

                TickHome(home, record, cfg, dt);
            }

            PruneRazed();
        }

        void TickHome(Building home, DefenseRecord record, CombatConfig cfg, float dt)
        {
            Vector3 pos = home.transform.position;
            bool anyOccupant = home.Occupants.Count > 0;

            var wakeThreat = NearestMonsterWithin(pos, cfg.wakeRadius);
            if (record.State == HomeDefenseState.Sleeping)
            {
                if (wakeThreat == null || !anyOccupant)
                {
                    return; // danger stayed away (or nobody home): sleep on, no cost
                }
                Wake(home, record, cfg);
            }

            // Awake: keep firing while a monster is in engage range; stand down once clear.
            var engage = NearestMonsterWithin(pos, cfg.defenseEngageRange);
            if (engage == null && wakeThreat == null)
            {
                StandDown(home, record);
                return;
            }

            record.FireTimer -= dt;
            int guard = 0;
            while (record.FireTimer <= 0f && guard++ < 64)
            {
                engage = NearestMonsterWithin(pos, cfg.defenseEngageRange);
                if (engage == null)
                {
                    record.FireTimer = 0f;
                    break;
                }
                FireVolley(home, engage, cfg);
                record.FireTimer += cfg.windowShotIntervalSeconds;
            }
        }

        void Wake(Building home, DefenseRecord record, CombatConfig cfg)
        {
            home.FlareOn(cfg.flareLightRadius, cfg.flareLightStrength);
            record.State = HomeDefenseState.Awake;
            record.FireTimer = 0f; // fire on the first defended tick
            GameEventLog.Append("home_defense", $"{home.name} woke (interior light flared)");
            EventBus.RaiseHomeWokeForDefense(home.gameObject);
        }

        void StandDown(Building home, DefenseRecord record)
        {
            home.FlareOff();
            if (record.State == HomeDefenseState.Awake)
            {
                GameEventLog.Append("home_defense", $"{home.name} stood down (threat passed)");
            }
            record.State = HomeDefenseState.Sleeping;
        }

        void FireVolley(Building home, Abbey.Nightmares.MonsterController target, CombatConfig cfg)
        {
            // The shot comes from the flared window (Safe): full friendly damage, no
            // Dark-band drain — but firing still costs the woken occupants sanity.
            var band = DarknessEvaluator.Classify(home.transform.position);
            var treatment = LightBandCombatResolver.Resolve(CombatSide.Friendly, false, band, cfg);
            float damage = cfg.windowShotDamage * treatment.DamageMultiplier;
            target.TakeDamage(damage);
            GameEventLog.Append("window_shot",
                $"{home.name} -> {target.name} band={band} dmg={damage:F1}");

            var sanity = Sanity;
            var occupants = home.Occupants;
            for (int i = 0; i < occupants.Count; i++)
            {
                var v = occupants[i];
                if (v == null || v.State == VillagerState.Dead || v.State == VillagerState.Missing)
                {
                    continue;
                }
                if (sanity != null)
                {
                    sanity.ApplySanityCost(v, cfg.sanityCostPerVolley, "lit-window defense");
                }
            }
        }

        DefenseRecord GetOrCreate(Building home, CombatConfig cfg)
        {
            if (!_records.TryGetValue(home, out var record))
            {
                record = new DefenseRecord();
                _records[home] = record;
            }
            if (!record.Initialized)
            {
                // Inject this system's config hit points (may differ from the asset default).
                home.InitializeDefense(cfg.HomeHitPointsFor(home.Type));
                record.Initialized = true;
            }
            return record;
        }

        void SyncOccupants(Building home)
        {
            var sanity = Sanity;
            if (sanity == null)
            {
                return; // no shelter map: rely on occupants assigned directly (tests)
            }
            sanity.CollectHomeOccupants(home, _occupantScratch);
            home.SetOccupants(_occupantScratch);
        }

        void PruneRazed()
        {
            _razeScratch.Clear();
            foreach (var pair in _records)
            {
                if (pair.Key == null)
                {
                    _razeScratch.Add(pair.Key);
                }
            }
            for (int i = 0; i < _razeScratch.Count; i++)
            {
                _records.Remove(_razeScratch[i]);
            }
        }

        static Abbey.Nightmares.MonsterController NearestMonsterWithin(Vector3 position, float range)
        {
            Abbey.Nightmares.MonsterController nearest = null;
            float bestDist = float.MaxValue;
            var monsters = Abbey.Nightmares.MonsterController.Active;
            for (int i = 0; i < monsters.Count; i++)
            {
                var m = monsters[i];
                if (m == null || !m.IsAlive)
                {
                    continue;
                }
                float dist = PlanarMotion.Distance(position, m.transform.position);
                if (dist <= range && dist < bestDist)
                {
                    bestDist = dist;
                    nearest = m;
                }
            }
            return nearest;
        }
    }
}
