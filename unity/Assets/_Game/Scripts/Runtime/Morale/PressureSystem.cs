using System.Collections.Generic;
using System.Globalization;
using Abbey.Beast;
using Abbey.Core;
using Abbey.Decrees;
using Abbey.Sanity;
using UnityEngine;

namespace Abbey.Morale
{
    /// <summary>
    /// An immutable read of the settlement's moral state at one instant: the seven pressures,
    /// the two external inputs (beast status from P3-07, household sanity from P3-03) and the
    /// active standing law tags. <see cref="PressureSystem.Snapshot"/> builds one; the
    /// <see cref="AbbeyTransformationSystem"/> scores forms from it. Pure data — deterministic.
    /// </summary>
    public readonly struct PressureSnapshot
    {
        public readonly float Trust;
        public readonly float Sanctity;
        public readonly float Mercy;
        public readonly float Fear;
        public readonly float Reason;
        public readonly float Hunger;
        public readonly float OldFaith;
        public readonly float BeastStatus;
        public readonly float HouseholdSanity;
        public readonly string[] ActiveTags;

        public PressureSnapshot(float trust, float sanctity, float mercy, float fear,
            float reason, float hunger, float oldFaith, float beastStatus,
            float householdSanity, string[] activeTags)
        {
            Trust = trust;
            Sanctity = sanctity;
            Mercy = mercy;
            Fear = fear;
            Reason = reason;
            Hunger = hunger;
            OldFaith = oldFaith;
            BeastStatus = beastStatus;
            HouseholdSanity = householdSanity;
            ActiveTags = activeTags ?? System.Array.Empty<string>();
        }

        public float Get(PressureId id)
        {
            switch (id)
            {
                case PressureId.Trust: return Trust;
                case PressureId.Sanctity: return Sanctity;
                case PressureId.Mercy: return Mercy;
                case PressureId.Fear: return Fear;
                case PressureId.Reason: return Reason;
                case PressureId.Hunger: return Hunger;
                case PressureId.OldFaith: return OldFaith;
                default: return 0f;
            }
        }

        public bool HasTag(string tag)
        {
            if (string.IsNullOrEmpty(tag))
            {
                return false;
            }
            for (int i = 0; i < ActiveTags.Length; i++)
            {
                if (ActiveTags[i] == tag)
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>
    /// The moral-pressure store (P3-10). It is the single, authoritative implementation of
    /// the settlement's Trust / Sanctity / Mercy / Fear / Reason / Hunger / Old-faith
    /// pressures, replacing the ad-hoc trust stubs of earlier phases. The values are a
    /// <b>deterministic fold</b> over the append-only <see cref="GameEventLog"/> plus the
    /// active law tags: <see cref="RecomputeFromLog"/> resets every channel to its
    /// <see cref="PressuresConfig"/> baseline and re-applies the whole history in order, so
    /// the <i>same log ⇒ the same pressures</i>, always. Decay toward baseline is interleaved
    /// at each day-marker record, so it too is a pure function of the log.
    ///
    /// Household sanity (P3-03) and beast status (P3-07) are read live as transformation
    /// inputs, not stored channels. Trust exposes tier bands (<see cref="TrustTier"/>) that
    /// gate volunteer behaviours (<see cref="IsVolunteerEligible"/>).
    ///
    /// Singleton + [ExecuteAlways] like the other Phase 3 systems so EditMode tests get the
    /// OnEnable/OnDisable lifecycle. The recompute runs at dawn off
    /// <see cref="EventBus.PhaseChanged"/>; tests call <see cref="RecomputeFromLog"/> directly.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class PressureSystem : MonoBehaviour
    {
        public static PressureSystem Instance { get; private set; }

        PressuresConfig _config;
        bool _isDuplicate;

        readonly Dictionary<PressureId, float> _pressures = new Dictionary<PressureId, float>();

        static readonly PressureId[] Ids =
        {
            PressureId.Trust, PressureId.Sanctity, PressureId.Mercy, PressureId.Fear,
            PressureId.Reason, PressureId.Hunger, PressureId.OldFaith
        };

        public PressuresConfig Config
        {
            get
            {
                if (_config == null)
                {
                    _config = PressuresConfig.LoadOrDefault();
                }
                return _config;
            }
            set { _config = value; }
        }

        // ---- Typed getters -------------------------------------------------

        public float Get(PressureId id) => _pressures.TryGetValue(id, out var v) ? v : 0f;
        public float Trust => Get(PressureId.Trust);
        public float Sanctity => Get(PressureId.Sanctity);
        public float Mercy => Get(PressureId.Mercy);
        public float Fear => Get(PressureId.Fear);
        public float Reason => Get(PressureId.Reason);
        public float Hunger => Get(PressureId.Hunger);
        public float OldFaith => Get(PressureId.OldFaith);

        /// <summary>Beast standing from the P3-07 evolution system (0 when absent).</summary>
        public float BeastStatus =>
            HoundEvolutionSystem.Instance != null ? HoundEvolutionSystem.Instance.BeastStatus : 0f;

        /// <summary>Mean household sanity from P3-03 (1 when no sanity system / villagers).</summary>
        public float HouseholdSanity =>
            SanitySystem.Instance != null ? SanitySystem.Instance.AverageHouseholdSanity() : 1f;

        /// <summary>The Bellkeeper trust tier for the current Trust value.</summary>
        public TrustTier TrustTier => Config.TierFor(Trust);

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                _isDuplicate = true;
                Debug.LogWarning("[PressureSystem] Duplicate instance ignored.", this);
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
            ResetToBaseline();
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
        public void Configure(PressuresConfig config)
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
                var transform = AbbeyTransformationSystem.Instance;
                if (transform != null)
                {
                    transform.EvaluateAtDawn();
                }
            }
        }

        // ------------------------------------------------------------------
        // Deterministic fold
        // ------------------------------------------------------------------

        void ResetToBaseline()
        {
            var cfg = Config;
            for (int i = 0; i < Ids.Length; i++)
            {
                _pressures[Ids[i]] = cfg.ChannelFor(Ids[i]).baseline;
            }
        }

        /// <summary>
        /// Rebuilds every pressure from scratch by folding the entire event log in order:
        /// reset to baselines, then for each record apply the matching weights and — when the
        /// record is a day-marker — one decay step toward baseline. Trust-pressure records
        /// (signed overdrive costs) fold their parsed delta directly. Deterministic and
        /// idempotent: the same log always yields the same pressures. Public for tests.
        /// </summary>
        public void RecomputeFromLog()
        {
            if (_isDuplicate)
            {
                return;
            }
            var cfg = Config;
            ResetToBaseline();

            var records = GameEventLog.Records;
            for (int r = 0; r < records.Count; r++)
            {
                var rec = records[r];

                // Overdrive trust / beast costs carry a signed delta in the record data.
                if (rec.Type == "trust_pressure")
                {
                    Add(PressureId.Trust, ParseLeadingFloat(rec.Data));
                    continue;
                }

                if (cfg.weights != null)
                {
                    for (int w = 0; w < cfg.weights.Count; w++)
                    {
                        var weight = cfg.weights[w];
                        if (weight == null || string.IsNullOrEmpty(weight.signal))
                        {
                            continue;
                        }
                        bool hit = weight.matchData
                            ? rec.Data != null && rec.Data.Contains(weight.signal)
                            : rec.Type == weight.signal;
                        if (!hit)
                        {
                            continue;
                        }
                        for (int i = 0; i < Ids.Length; i++)
                        {
                            float d = weight.For(Ids[i]);
                            if (d != 0f)
                            {
                                Add(Ids[i], d);
                            }
                        }
                    }
                }

                if (IsDayMarker(cfg, rec))
                {
                    ApplyDecayStep(cfg);
                }
            }

            ClampAll(cfg);
        }

        static bool IsDayMarker(PressuresConfig cfg, GameEventLog.Record rec)
        {
            if (string.IsNullOrEmpty(cfg.dayMarkerEventType) || rec.Type != cfg.dayMarkerEventType)
            {
                return false;
            }
            return string.IsNullOrEmpty(cfg.dayMarkerDataContains)
                || (rec.Data != null && rec.Data.Contains(cfg.dayMarkerDataContains));
        }

        void ApplyDecayStep(PressuresConfig cfg)
        {
            for (int i = 0; i < Ids.Length; i++)
            {
                var ch = cfg.ChannelFor(Ids[i]);
                if (ch.decayToBaselinePerDay <= 0f)
                {
                    continue;
                }
                float v = Get(Ids[i]);
                _pressures[Ids[i]] = Mathf.MoveTowards(v, ch.baseline, ch.decayToBaselinePerDay);
            }
        }

        void Add(PressureId id, float delta)
        {
            _pressures[id] = Get(id) + delta;
        }

        void ClampAll(PressuresConfig cfg)
        {
            for (int i = 0; i < Ids.Length; i++)
            {
                var ch = cfg.ChannelFor(Ids[i]);
                _pressures[Ids[i]] = Mathf.Clamp(Get(Ids[i]), ch.min, ch.max);
            }
        }

        static float ParseLeadingFloat(string data)
        {
            if (string.IsNullOrEmpty(data))
            {
                return 0f;
            }
            int i = 0;
            while (i < data.Length && data[i] == ' ')
            {
                i++;
            }
            int start = i;
            while (i < data.Length && data[i] != ' ')
            {
                i++;
            }
            string token = data.Substring(start, i - start);
            return float.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
                ? v : 0f;
        }

        // ------------------------------------------------------------------
        // Trust tiers / volunteer gates
        // ------------------------------------------------------------------

        /// <summary>Whether a volunteer behaviour is available at the current trust tier.</summary>
        public bool IsVolunteerEligible(VolunteerRole role)
        {
            return TrustTier >= Config.MinTierFor(role);
        }

        // ------------------------------------------------------------------
        // Snapshot
        // ------------------------------------------------------------------

        /// <summary>An immutable read of all pressures + inputs for the transformation eval.</summary>
        public PressureSnapshot Snapshot()
        {
            var laws = LawSystem.Instance;
            string[] tags = laws != null ? laws.ActiveTags() : System.Array.Empty<string>();
            return new PressureSnapshot(Trust, Sanctity, Mercy, Fear, Reason, Hunger, OldFaith,
                BeastStatus, HouseholdSanity, tags);
        }
    }
}
