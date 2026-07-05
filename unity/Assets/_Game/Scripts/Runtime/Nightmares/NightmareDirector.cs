using System.Collections.Generic;
using Abbey.Combat;
using Abbey.Core;
using Abbey.Light;
using Abbey.Villagers;
using Abbey.World;
using UnityEngine;

namespace Abbey.Nightmares
{
    /// <summary>
    /// Night director. Two modes, chosen by <see cref="PrototypeConfig.phase2NightsEnabled"/>:
    ///
    /// Legacy (default, off): on PhaseChanged(Night) it spawns
    /// config.firstNightMonsterCount pale hounds at deterministic dark points on a
    /// ring beyond the map center (its own transform) — the 0.1 first night that
    /// FirstNightTests script against.
    ///
    /// Phase 2 (opt-in): the intimate-not-wave scripted night
    /// (VERTICAL_SLICE_SPEC §10). BeginNight parses the data-driven schedule
    /// (<see cref="PrototypeConfig.phase2NightSchedule"/>, "fraction:kind" lines)
    /// and <see cref="Tick"/> fires entries as time-into-night crosses each
    /// fraction: staggered pale hounds, a lantern moth, whispers and a shadow on
    /// the unlit road (log + static <see cref="WhisperEmitted"/> event for future
    /// audio — no audio here), a possible villager panic beat, and the drowned
    /// sailor near the wreck ONLY when the event log holds a died-by-water record
    /// ("villager_died_at" with nearWater=True — the director itself observes
    /// villager deaths and writes those records, since VillagerAgent logs deaths
    /// without location). Fully deterministic: all randomness is System.Random
    /// seeded from config.simulationSeed; identical seed = identical night.
    ///
    /// On Dawn it despawns everything and writes a NightSummary record — villagers
    /// dead/injured/missing, monsters killed, whether the hound helped — into the
    /// shared <see cref="GameEventLog"/> (one log, many consumers).
    /// </summary>
    [DisallowMultipleComponent]
    public class NightmareDirector : MonoBehaviour
    {
        /// <summary>
        /// A whisper rose in the dark (position included). Static hook for a
        /// future audio layer; the same moment is also logged as "whisper".
        /// </summary>
        public static event System.Action<Vector3> WhisperEmitted;

        /// <summary>Test isolation for the static audio hook.</summary>
        public static void ResetStaticEvents()
        {
            WhisperEmitted = null;
        }

        [Tooltip("autoTick value handed to every spawned monster (tests set false).")]
        public bool monstersAutoTick = true;

        [Tooltip("Advance the night script from Update using Time.deltaTime. Tests set false and call Tick().")]
        public bool autoTick = true;

        [Tooltip("Where the sea took them: deaths near this anchor count as died-by-water and the drowned sailor rises beside it. Unset = no water deaths are ever recorded.")]
        public Transform shipwreckAnchor;

        [Tooltip("Phase 3 escalation system (optional). Auto-found when phase3NightsEnabled; drives wave budget + the nightly dark objective.")]
        public NightEscalationSystem escalation;

        PrototypeConfig _config;
        readonly List<MonsterController> _spawned = new List<MonsterController>();
        readonly HashSet<VillagerAgent> _observedDead = new HashSet<VillagerAgent>();
        List<NightmareSchedule.Entry> _schedule;
        int _nextEventIndex;
        float _timeIntoNight;
        bool _nightActive;
        int _nightStartLogIndex;
        int _nightNumber;

        public IReadOnlyList<MonsterController> SpawnedMonsters => _spawned;

        // ------------------------------------------------------------------
        // Debug-panel state (display only; the panel never tunes anything)
        // ------------------------------------------------------------------

        /// <summary>The parsed Phase 2 script for the current night (null in legacy mode).</summary>
        public IReadOnlyList<NightmareSchedule.Entry> Schedule => _schedule;

        /// <summary>Index of the next unfired schedule entry.</summary>
        public int NextEventIndex => _nextEventIndex;

        public float TimeIntoNight => _timeIntoNight;

        public bool NightActive => _nightActive;

        public int NightNumber => _nightNumber;

        /// <summary>Seconds until the next scheduled event, or null when none remain.</summary>
        public float? SecondsUntilNextEvent
        {
            get
            {
                if (!_nightActive || _schedule == null || _nextEventIndex >= _schedule.Count)
                {
                    return null;
                }
                float due = _schedule[_nextEventIndex].Fraction * NightDuration;
                return Mathf.Max(0f, due - _timeIntoNight);
            }
        }

        public PrototypeConfig Config
        {
            get
            {
                if (_config == null)
                {
                    _config = PrototypeConfig.LoadOrDefault();
                }
                return _config;
            }
            set { _config = value; }
        }

        /// <summary>The escalation system (Phase 3), from the serialized field, the singleton, or the scene.</summary>
        public NightEscalationSystem Escalation
        {
            get
            {
                if (escalation == null)
                {
                    escalation = NightEscalationSystem.Instance != null
                        ? NightEscalationSystem.Instance
                        : FindFirstObjectByType<NightEscalationSystem>();
                }
                return escalation;
            }
            set { escalation = value; }
        }

        float NightDuration => Mathf.Max(0.01f, Config.nightDurationSeconds);

        void OnEnable()
        {
            EventBus.PhaseChanged += OnPhaseChanged;
        }

        void OnDisable()
        {
            EventBus.PhaseChanged -= OnPhaseChanged;
        }

        void Update()
        {
            if (!Application.isPlaying || !autoTick)
            {
                return;
            }
            Tick(Time.deltaTime);
        }

        void OnPhaseChanged(DayPhase phase)
        {
            if (phase == DayPhase.Night)
            {
                BeginNight();
            }
            else if (phase == DayPhase.Dawn)
            {
                EndNight();
            }
        }

        /// <summary>Starts the night. Public so tests/debug tools can force it.</summary>
        public void BeginNight()
        {
            var cfg = Config;
            _nightNumber++;
            _nightStartLogIndex = GameEventLog.Count;
            _timeIntoNight = 0f;
            _nextEventIndex = 0;
            _nightActive = true;
            GameEventLog.Append("night_begins", $"night={_nightNumber}");

            if (cfg.phase3NightsEnabled)
            {
                BeginPhase3Night(cfg);
                return; // Phase 3 spawns the escalation wave up front, no schedule
            }

            if (cfg.phase2NightsEnabled)
            {
                var errors = new List<string>();
                _schedule = NightmareSchedule.Parse(cfg.phase2NightSchedule, errors);
                for (int i = 0; i < errors.Count; i++)
                {
                    GameEventLog.Append("nightmare", $"schedule_parse_error entry='{errors[i]}'");
                }
                GameEventLog.Append("nightmare",
                    $"schedule night={_nightNumber} entries={_schedule.Count}");
                return; // Phase 2 spawns are staggered by Tick, never a wave
            }

            // Legacy 0.1 night: everything spawns at the boundary.
            _schedule = null;
            for (int i = 0; i < cfg.firstNightMonsterCount; i++)
            {
                Vector3? point = FindDarkSpawnPoint(
                    transform.position,
                    cfg.monsterSpawnMinRadius,
                    cfg.monsterSpawnMaxRadius,
                    cfg.simulationSeed + _nightNumber * 977 + i * 131,
                    cfg.monsterSpawnAttempts);
                if (!point.HasValue)
                {
                    GameEventLog.Append("monster_spawn_failed", $"night={_nightNumber} index={i}");
                    continue;
                }
                SpawnMonster(NightmareType.PaleHound, point.Value,
                    $"PaleHound_{_nightNumber}_{i}", cfg);
            }
        }

        /// <summary>
        /// Deterministic director step: observes villager deaths (for the
        /// died-by-water gate) and, in Phase 2 mode, fires every schedule entry
        /// whose time has come. autoTick = false in tests.
        /// </summary>
        public void Tick(float dt)
        {
            if (dt <= 0f)
            {
                return;
            }

            ObserveVillagerDeaths();

            if (!_nightActive || _schedule == null)
            {
                return;
            }

            _timeIntoNight += dt;
            float duration = NightDuration;
            while (_nextEventIndex < _schedule.Count
                   && _schedule[_nextEventIndex].Fraction * duration <= _timeIntoNight)
            {
                FireScheduledEvent(_schedule[_nextEventIndex], _nextEventIndex);
                _nextEventIndex++;
            }
        }

        /// <summary>
        /// The Phase 3 escalating night (P3-06): the <see cref="NightEscalationSystem"/>
        /// turns tonight's night index + season into a wave budget and generates the
        /// dark objective; the director spawns that many monsters on the dark ring (a
        /// set-piece stand mixes in a lantern moth for variety). Fully deterministic:
        /// spawn seeds derive from the config seed + night index like the legacy path.
        /// </summary>
        void BeginPhase3Night(PrototypeConfig cfg)
        {
            _schedule = null;
            var esc = Escalation;
            var season = SeasonSystem.Instance != null
                ? SeasonSystem.Instance.CurrentSeason
                : Season.Spring;

            int count;
            bool setPiece;
            if (esc != null)
            {
                count = esc.BeginNight(_nightNumber, season, transform.position);
                setPiece = esc.IsSetPieceTonight;
            }
            else
            {
                var combat = CombatConfig.LoadOrDefault();
                count = NightEscalationSystem.WaveMonsterCount(combat, _nightNumber, season);
                setPiece = NightEscalationSystem.IsSetPieceNight(combat, _nightNumber);
            }

            // P3-08: earlier nights' overdrive levers booked a nightmare debt; the
            // director cashes it in now, spawning extra monsters on top of the season
            // wave (P3-11 nightmares extend this hook).
            var overdrive = Abbey.Decrees.OverdriveSystem.Instance;
            int debtExtra = overdrive != null ? overdrive.ConsumeNightmareDebtForNight() : 0;
            if (debtExtra > 0)
            {
                GameEventLog.Append("night_escalation",
                    $"debt_monsters night={_nightNumber} extra={debtExtra}");
            }
            count += debtExtra;

            for (int i = 0; i < count; i++)
            {
                int seed = cfg.simulationSeed + _nightNumber * 977 + i * 131;
                // Set-piece stands mix in a lantern moth every third slot for variety.
                var type = (setPiece && i % 3 == 2)
                    ? NightmareType.LanternMoth
                    : NightmareType.PaleHound;
                SpawnOnDarkRing(type, $"{type}_{_nightNumber}_{i}", seed, cfg);
            }
        }

        /// <summary>Cleans up the night and writes the summary. Public for tests/debug tools.</summary>
        public void EndNight()
        {
            if (Config.phase3NightsEnabled)
            {
                var esc = Escalation;
                if (esc != null)
                {
                    esc.ResolveNight();
                }
            }

            _nightActive = false;
            int dead = 0, injured = 0, missing = 0;
            var villagers = DuskRecallSystem.Villagers;
            for (int i = 0; i < villagers.Count; i++)
            {
                var v = villagers[i];
                if (v == null)
                {
                    continue;
                }
                switch (v.State)
                {
                    case VillagerState.Dead: dead++; break;
                    case VillagerState.Injured: injured++; break;
                    case VillagerState.Missing: missing++; break;
                }
            }

            int monstersKilled = 0;
            for (int i = 0; i < _spawned.Count; i++)
            {
                if (_spawned[i] == null || !_spawned[i].IsAlive)
                {
                    monstersKilled++;
                }
            }

            bool houndHelped = HoundHelpedSince(_nightStartLogIndex);

            GameEventLog.Append("NightSummary",
                $"night={_nightNumber} villagersDead={dead} villagersInjured={injured} " +
                $"villagersMissing={missing} monstersKilled={monstersKilled} houndHelped={houndHelped}");

            for (int i = 0; i < _spawned.Count; i++)
            {
                var monster = _spawned[i];
                if (monster == null)
                {
                    continue;
                }
                GameEventLog.Append("monster_despawned", monster.name);
                GameEventLog.Append("nightmare",
                    $"despawn type={monster.Type} name={monster.name}");
                if (Application.isPlaying)
                {
                    Destroy(monster.gameObject);
                }
                else
                {
                    DestroyImmediate(monster.gameObject);
                }
            }
            _spawned.Clear();
        }

        // ------------------------------------------------------------------
        // Phase 2 script
        // ------------------------------------------------------------------

        void FireScheduledEvent(NightmareSchedule.Entry entry, int index)
        {
            var cfg = Config;
            // Same derivation shape as the legacy spawns: unique per night + entry.
            int seed = cfg.simulationSeed + _nightNumber * 977 + index * 131;

            switch (entry.Kind)
            {
                case NightmareEventKind.SpawnPaleHound:
                    SpawnOnDarkRing(NightmareType.PaleHound,
                        $"PaleHound_{_nightNumber}_{index}", seed, cfg);
                    break;

                case NightmareEventKind.SpawnLanternMoth:
                    SpawnOnDarkRing(NightmareType.LanternMoth,
                        $"LanternMoth_{_nightNumber}_{index}", seed, cfg);
                    break;

                case NightmareEventKind.SpawnDrownedSailor:
                    TrySpawnDrownedSailor(index, seed, cfg);
                    break;

                case NightmareEventKind.Whisper:
                    EmitWhisper(seed, cfg);
                    break;

                case NightmareEventKind.Shadow:
                    EmitShadow(seed, cfg);
                    break;

                case NightmareEventKind.Panic:
                    FirePanicEvent();
                    break;
            }
        }

        void SpawnOnDarkRing(NightmareType type, string name, int seed, PrototypeConfig cfg)
        {
            Vector3? point = FindDarkSpawnPoint(
                transform.position,
                cfg.monsterSpawnMinRadius,
                cfg.monsterSpawnMaxRadius,
                seed,
                cfg.monsterSpawnAttempts);
            if (!point.HasValue)
            {
                GameEventLog.Append("monster_spawn_failed",
                    $"night={_nightNumber} name={name}");
                return;
            }
            SpawnMonster(type, point.Value, name, cfg);
        }

        void TrySpawnDrownedSailor(int index, int seed, PrototypeConfig cfg)
        {
            if (!HasWaterDeathRecord())
            {
                GameEventLog.Append("nightmare",
                    $"drowned_sailor_skipped night={_nightNumber} reason=no_water_death");
                return;
            }

            // It rises beside the wreck; without an anchor it walks in from the
            // dark ring like everything else.
            Vector3? point;
            if (shipwreckAnchor != null)
            {
                point = FindDarkSpawnPoint(
                    shipwreckAnchor.position, 0.5f, cfg.drownedSailorSpawnRadius,
                    seed, cfg.monsterSpawnAttempts) ?? shipwreckAnchor.position;
            }
            else
            {
                point = FindDarkSpawnPoint(
                    transform.position, cfg.monsterSpawnMinRadius,
                    cfg.monsterSpawnMaxRadius, seed, cfg.monsterSpawnAttempts);
            }
            if (!point.HasValue)
            {
                GameEventLog.Append("monster_spawn_failed",
                    $"night={_nightNumber} name=DrownedSailor_{_nightNumber}_{index}");
                return;
            }
            SpawnMonster(NightmareType.DrownedSailor, point.Value,
                $"DrownedSailor_{_nightNumber}_{index}", cfg);
        }

        MonsterController SpawnMonster(
            NightmareType type, Vector3 position, string name, PrototypeConfig cfg)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            MonsterController monster;
            switch (type)
            {
                case NightmareType.DrownedSailor:
                    monster = go.AddComponent<DrownedSailorController>();
                    break;
                case NightmareType.LanternMoth:
                    monster = go.AddComponent<LanternMothController>();
                    break;
                default:
                    // Legacy nights keep the base class (the pale hound behaviour)
                    // so nothing the 0.1 tests touch changes type.
                    monster = cfg.phase2NightsEnabled
                        ? go.AddComponent<PaleHoundController>()
                        : go.AddComponent<MonsterController>();
                    break;
            }
            // Configure (not just Config =): OnEnable already initialized health
            // from the default config during AddComponent.
            monster.Configure(cfg);
            monster.autoTick = monstersAutoTick;
            _spawned.Add(monster);
            GameEventLog.Append("nightmare",
                $"spawn type={type} name={name} pos=({position.x:F1},{position.z:F1})");
            EventBus.RaiseMonsterSpawned(go);
            return monster;
        }

        void EmitWhisper(int seed, PrototypeConfig cfg)
        {
            // "Whispers near the unlit road": a dark point on an inner ring,
            // closer to camp than the monster spawn ring.
            float outer = Mathf.Max(1f, cfg.monsterSpawnMinRadius * cfg.whisperRingFraction);
            Vector3 pos = FindDarkSpawnPoint(
                              transform.position, outer * 0.5f, outer, seed,
                              cfg.monsterSpawnAttempts)
                          ?? transform.position + Vector3.forward * outer;
            GameEventLog.Append("whisper",
                $"night={_nightNumber} pos=({pos.x:F1},{pos.z:F1})");
            WhisperEmitted?.Invoke(pos);
        }

        void EmitShadow(int seed, PrototypeConfig cfg)
        {
            // Pure dread: a shape at the forest edge (the far spawn ring), logged
            // for the morning report and future presentation. Nothing spawns.
            Vector3 pos = FindDarkSpawnPoint(
                              transform.position, cfg.monsterSpawnMaxRadius,
                              cfg.monsterSpawnMaxRadius, seed, cfg.monsterSpawnAttempts)
                          ?? transform.position + Vector3.forward * cfg.monsterSpawnMaxRadius;
            GameEventLog.Append("nightmare",
                $"shadow night={_nightNumber} pos=({pos.x:F1},{pos.z:F1})");
        }

        /// <summary>
        /// The panic beat: the most fearful villager outside Safe light breaks
        /// into panic (via the public ForceState API). Skipped — and logged as
        /// skipped — when everyone is tucked inside the light.
        /// </summary>
        void FirePanicEvent()
        {
            VillagerAgent target = null;
            float bestFear = -1f;
            var villagers = DuskRecallSystem.Villagers;
            for (int i = 0; i < villagers.Count; i++)
            {
                var v = villagers[i];
                if (v == null)
                {
                    continue;
                }
                switch (v.State)
                {
                    case VillagerState.Dead:
                    case VillagerState.Missing:
                    case VillagerState.Injured:
                    case VillagerState.Resting:
                    case VillagerState.Panicking:
                        continue;
                }
                if (v.CurrentZone == LightZone.Safe)
                {
                    continue; // panic strikes the exposed, not the sheltered
                }
                if (v.Fear > bestFear)
                {
                    bestFear = v.Fear;
                    target = v;
                }
            }

            if (target == null)
            {
                GameEventLog.Append("panic_event",
                    $"night={_nightNumber} skipped=no_exposed_villager");
                return;
            }

            GameEventLog.Append("panic_event",
                $"night={_nightNumber} villager={target.name} fear={target.Fear:F2}");
            target.ForceState(VillagerState.Panicking);
        }

        // ------------------------------------------------------------------
        // Death observation (drowned-sailor gate)
        // ------------------------------------------------------------------

        /// <summary>
        /// VillagerAgent logs "villager_died" without location, so the director
        /// watches registered villagers and writes a located record —
        /// "villager_died_at" with a nearWater flag measured against the wreck
        /// anchor — the moment each death is first seen.
        /// </summary>
        void ObserveVillagerDeaths()
        {
            var villagers = DuskRecallSystem.Villagers;
            for (int i = 0; i < villagers.Count; i++)
            {
                var v = villagers[i];
                if (v == null || v.State != VillagerState.Dead || _observedDead.Contains(v))
                {
                    continue;
                }
                _observedDead.Add(v);
                Vector3 pos = v.transform.position;
                bool nearWater = shipwreckAnchor != null
                                 && PlanarMotion.Distance(pos, shipwreckAnchor.position)
                                    <= Config.waterDeathRadius;
                GameEventLog.Append("villager_died_at",
                    $"name={v.name} pos=({pos.x:F1},{pos.z:F1}) nearWater={nearWater}");
            }
        }

        /// <summary>True when the log holds any died-by-water record.</summary>
        public static bool HasWaterDeathRecord()
        {
            var records = GameEventLog.Records;
            for (int i = 0; i < records.Count; i++)
            {
                if (records[i].Type == "villager_died_at"
                    && records[i].Data.Contains("nearWater=True"))
                {
                    return true;
                }
            }
            return false;
        }

        static bool HoundHelpedSince(int logIndex)
        {
            var records = GameEventLog.Records;
            for (int i = logIndex; i < records.Count; i++)
            {
                string type = records[i].Type;
                if (type == "hound_engaged_monster"
                    || type == "hound_attacked_monster"
                    || type == "hound_killed_monster")
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Deterministically samples ring points (minRadius..maxRadius from center)
        /// until one classifies Dark — monsters are born outside all light. Returns
        /// null when every attempt landed in light. Static so EditMode tests can
        /// exercise spawn-point selection without a director instance.
        /// </summary>
        public static Vector3? FindDarkSpawnPoint(
            Vector3 center, float minRadius, float maxRadius, int seed, int attempts)
        {
            var rng = new System.Random(seed);
            float max = Mathf.Max(minRadius, maxRadius);
            for (int i = 0; i < attempts; i++)
            {
                float angle = (float)(rng.NextDouble() * Mathf.PI * 2.0);
                float radius = Mathf.Lerp(minRadius, max, (float)rng.NextDouble());
                var candidate = center + new Vector3(
                    Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                if (DarknessEvaluator.Classify(candidate) == LightZone.Dark)
                {
                    return candidate;
                }
            }
            return null;
        }
    }
}
