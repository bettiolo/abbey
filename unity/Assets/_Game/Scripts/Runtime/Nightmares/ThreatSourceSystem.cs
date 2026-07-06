using System.Collections.Generic;
using System.Globalization;
using Abbey.Core;
using UnityEngine;

namespace Abbey.Nightmares
{
    /// <summary>
    /// One exploitable place on the map: a type and a world position. Its accumulated
    /// exploitation pressure is held per-type by the owning <see cref="ThreatSourceSystem"/>
    /// (so several forest edges share the settlement's forest pressure); the position is only
    /// used to place the monsters the pressure summons.
    /// </summary>
    public sealed class ThreatSource
    {
        public readonly ThreatSourceType Type;
        public readonly Vector3 Position;

        public ThreatSource(ThreatSourceType type, Vector3 position)
        {
            Type = type;
            Position = position;
        }
    }

    /// <summary>
    /// The threat-source registry + exploitation-pressure store (P3-11, ROADMAP item 10). It
    /// is a <b>deterministic fold</b> over the append-only <see cref="GameEventLog"/> exactly
    /// like <see cref="Abbey.Morale.PressureSystem"/>: <see cref="RecomputeFromLog"/> resets
    /// every source type to zero and re-applies the whole history — economy records add
    /// pressure to their mapped source (woodcutting→forest, coal→cave, salvage→shore, grave
    /// handling→crypt, hauling→old road, the daily draw→well), mitigation records subtract,
    /// and a decay step toward zero is interleaved at each day-marker record. Same log ⇒ same
    /// pressures, always.
    ///
    /// The registered source POSITIONS (authored in the scene) let the director place spawns:
    /// <see cref="SelectWeightedSource"/> draws a source with probability proportional to its
    /// type's pressure (plus a small floor and a bonus for the arming trigger's preferred
    /// source), deterministic under a seeded <see cref="System.Random"/>. Mitigation
    /// (<see cref="Mitigate"/>) appends a log record so the reduction stays part of the fold.
    ///
    /// Singleton + [ExecuteAlways] like the other Phase 3 systems; the recompute runs at dawn
    /// off <see cref="EventBus.PhaseChanged"/>; tests call the methods directly.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class ThreatSourceSystem : MonoBehaviour
    {
        public static ThreatSourceSystem Instance { get; private set; }

        ThreatConfig _config;
        bool _isDuplicate;

        readonly List<ThreatSource> _sources = new List<ThreatSource>();
        readonly Dictionary<ThreatSourceType, float> _pressure = new Dictionary<ThreatSourceType, float>();

        static readonly ThreatSourceType[] AllTypes =
        {
            ThreatSourceType.Forest, ThreatSourceType.Well, ThreatSourceType.Cave,
            ThreatSourceType.Mountain, ThreatSourceType.Shore, ThreatSourceType.Crypt,
            ThreatSourceType.OldRoad
        };

        public ThreatConfig Config
        {
            get
            {
                if (_config == null)
                {
                    _config = ThreatConfig.LoadOrDefault();
                }
                return _config;
            }
            set { _config = value; }
        }

        public IReadOnlyList<ThreatSource> Sources => _sources;

        public float PressureFor(ThreatSourceType type) =>
            _pressure.TryGetValue(type, out var v) ? v : 0f;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                _isDuplicate = true;
                Debug.LogWarning("[ThreatSourceSystem] Duplicate instance ignored.", this);
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
            ResetToZero();
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

        /// <summary>Injects a config (tests) and recomputes from the current log.</summary>
        public void Configure(ThreatConfig config)
        {
            _config = config;
            RecomputeFromLog();
        }

        void OnPhaseChanged(DayPhase phase)
        {
            if (_isDuplicate)
            {
                return;
            }
            if (phase == DayPhase.Dawn)
            {
                RecomputeFromLog();
            }
        }

        // ------------------------------------------------------------------
        // Registry
        // ------------------------------------------------------------------

        /// <summary>Registers an authored source location. Positions drive spawn placement.</summary>
        public ThreatSource RegisterSource(ThreatSourceType type, Vector3 position)
        {
            var src = new ThreatSource(type, position);
            _sources.Add(src);
            return src;
        }

        /// <summary>Test isolation: forget every registered source (pressures are untouched).</summary>
        public void ClearSources()
        {
            _sources.Clear();
        }

        // ------------------------------------------------------------------
        // Deterministic fold
        // ------------------------------------------------------------------

        void ResetToZero()
        {
            for (int i = 0; i < AllTypes.Length; i++)
            {
                _pressure[AllTypes[i]] = 0f;
            }
        }

        /// <summary>
        /// Rebuilds every source pressure from scratch by folding the whole event log: reset
        /// to zero, add the mapped exploitation rate for each matching record, subtract each
        /// mitigation record, decay toward zero at each day marker, clamp to
        /// [0, maxSourcePressure]. Deterministic and idempotent. Public for tests.
        /// </summary>
        public void RecomputeFromLog()
        {
            if (_isDuplicate)
            {
                return;
            }
            var cfg = Config;
            ResetToZero();

            var records = GameEventLog.Records;
            for (int r = 0; r < records.Count; r++)
            {
                var rec = records[r];

                if (cfg.exploitation != null)
                {
                    for (int m = 0; m < cfg.exploitation.Count; m++)
                    {
                        var map = cfg.exploitation[m];
                        if (map == null || string.IsNullOrEmpty(map.signal))
                        {
                            continue;
                        }
                        bool hit = map.matchData
                            ? rec.Data != null && rec.Data.Contains(map.signal)
                            : rec.Type == map.signal;
                        if (hit)
                        {
                            Add(map.source, map.pressurePerEvent);
                        }
                    }
                }

                if (!string.IsNullOrEmpty(cfg.mitigationEventType) && rec.Type == cfg.mitigationEventType)
                {
                    if (TryParseMitigation(rec.Data, out var mtype, out float amount))
                    {
                        Add(mtype, -amount);
                    }
                }

                if (IsDayMarker(cfg, rec))
                {
                    ApplyDecayStep(cfg);
                }
            }

            ClampAll(cfg);
        }

        static bool IsDayMarker(ThreatConfig cfg, GameEventLog.Record rec)
        {
            if (string.IsNullOrEmpty(cfg.dayMarkerEventType) || rec.Type != cfg.dayMarkerEventType)
            {
                return false;
            }
            return string.IsNullOrEmpty(cfg.dayMarkerDataContains)
                   || (rec.Data != null && rec.Data.Contains(cfg.dayMarkerDataContains));
        }

        void ApplyDecayStep(ThreatConfig cfg)
        {
            if (cfg.sourceDecayPerDay <= 0f)
            {
                return;
            }
            for (int i = 0; i < AllTypes.Length; i++)
            {
                float v = PressureFor(AllTypes[i]);
                _pressure[AllTypes[i]] = Mathf.MoveTowards(v, 0f, cfg.sourceDecayPerDay);
            }
        }

        void Add(ThreatSourceType type, float delta)
        {
            _pressure[type] = PressureFor(type) + delta;
        }

        void ClampAll(ThreatConfig cfg)
        {
            for (int i = 0; i < AllTypes.Length; i++)
            {
                _pressure[AllTypes[i]] = Mathf.Clamp(PressureFor(AllTypes[i]), 0f, cfg.maxSourcePressure);
            }
        }

        // ------------------------------------------------------------------
        // Mitigation
        // ------------------------------------------------------------------

        /// <summary>
        /// Rests / reconsecrates a source: appends a mitigation record the fold subtracts, then
        /// recomputes. Kept in the log so the reduction is part of the deterministic history.
        /// </summary>
        public void Mitigate(ThreatSourceType type, float amount)
        {
            if (_isDuplicate || amount <= 0f)
            {
                return;
            }
            GameEventLog.Append(Config.mitigationEventType,
                $"source={type} amount={amount.ToString("F2", CultureInfo.InvariantCulture)}");
            RecomputeFromLog();
        }

        static bool TryParseMitigation(string data, out ThreatSourceType type, out float amount)
        {
            type = ThreatSourceType.Forest;
            amount = 0f;
            if (string.IsNullOrEmpty(data))
            {
                return false;
            }
            string src = ExtractToken(data, "source=");
            string amt = ExtractToken(data, "amount=");
            if (src == null || !System.Enum.TryParse(src, out type))
            {
                return false;
            }
            return float.TryParse(amt, NumberStyles.Float, CultureInfo.InvariantCulture, out amount);
        }

        static string ExtractToken(string data, string key)
        {
            int at = data.IndexOf(key, System.StringComparison.Ordinal);
            if (at < 0)
            {
                return null;
            }
            int start = at + key.Length;
            int end = start;
            while (end < data.Length && data[end] != ' ')
            {
                end++;
            }
            return data.Substring(start, end - start);
        }

        // ------------------------------------------------------------------
        // Spawn-location weighting
        // ------------------------------------------------------------------

        /// <summary>
        /// Draws a registered source weighted by its type's pressure (plus the config floor,
        /// plus a bonus when it matches <paramref name="preferred"/>), using the given seeded
        /// RNG. Returns null when nothing is registered. Deterministic for a fixed seed.
        /// </summary>
        public ThreatSource SelectWeightedSource(System.Random rng, ThreatSourceType? preferred)
        {
            if (_sources.Count == 0 || rng == null)
            {
                return null;
            }
            var cfg = Config;
            float total = 0f;
            for (int i = 0; i < _sources.Count; i++)
            {
                total += WeightOf(_sources[i], cfg, preferred);
            }
            if (total <= 0f)
            {
                return _sources[0];
            }
            double pick = rng.NextDouble() * total;
            float acc = 0f;
            for (int i = 0; i < _sources.Count; i++)
            {
                acc += WeightOf(_sources[i], cfg, preferred);
                if (pick <= acc)
                {
                    return _sources[i];
                }
            }
            return _sources[_sources.Count - 1];
        }

        float WeightOf(ThreatSource src, ThreatConfig cfg, ThreatSourceType? preferred)
        {
            float w = PressureFor(src.Type) + cfg.sourceWeightFloor;
            if (preferred.HasValue && src.Type == preferred.Value)
            {
                w += cfg.preferredSourceBonus;
            }
            return Mathf.Max(0f, w);
        }
    }
}
