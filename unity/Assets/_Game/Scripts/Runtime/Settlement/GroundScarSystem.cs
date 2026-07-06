using System.Collections.Generic;
using Abbey.Core;
using Abbey.World;
using UnityEngine;

namespace Abbey.Settlement
{
    /// <summary>
    /// Ground scars (P3-12, ROADMAP Phase 3 item 19): the transient half of ground
    /// memory. Through the night, violence — razed homes, settlers killed in their
    /// beds — is buffered by position (from the P3-05 <see cref="EventBus"/> signals,
    /// which carry the home GameObject). At Dawn those positions are stamped as dark
    /// scars; across the day they fade back to meadow and are gone before Dusk. In
    /// Winter (P3-01) they do not regrow — snow covers them instead, so a
    /// snow-covered scar persists (with <c>snowCovered = true</c>) until the thaw.
    /// Nearby stamps merge so a running battle leaves one wide scar, not a stack.
    ///
    /// Placeholder visuals only (dark ground gizmo / decal tint). Singleton,
    /// [ExecuteAlways] so EditMode tests get the lifecycle; Update fades only in play
    /// with autoTick on. Reads <see cref="PathsConfig"/>; the winter branch reads
    /// <see cref="SeasonSystem"/> (overridable for tests).
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class GroundScarSystem : MonoBehaviour
    {
        public struct Scar
        {
            public Vector3 position;
            public float intensity;   // 0..1
            public float radius;
            public bool snowCovered;
        }

        [Tooltip("Advance the fade from Update using Time.deltaTime. Tests set false and call Tick().")]
        public bool autoTick = true;

        [Tooltip("Test hook: forces the winter branch regardless of SeasonSystem when set.")]
        public bool useSeasonOverride;
        [Tooltip("Season used when useSeasonOverride is true.")]
        public Season seasonOverride = Season.Winter;

        PathsConfig _config;
        bool _isDuplicate;
        readonly List<Scar> _scars = new List<Scar>();
        readonly List<Vector3> _pendingViolence = new List<Vector3>();

        /// <summary>Live scars (mutates each Tick).</summary>
        public IReadOnlyList<Scar> Scars => _scars;

        public int ScarCount => _scars.Count;

        /// <summary>Positions buffered this night, awaiting the dawn stamp.</summary>
        public int PendingViolenceCount => _pendingViolence.Count;

        public PathsConfig Config
        {
            get
            {
                if (_config == null)
                {
                    _config = PathsConfig.LoadOrDefault();
                }
                return _config;
            }
            set { _config = value; }
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                _isDuplicate = true;
                Debug.LogWarning("[GroundScarSystem] Duplicate instance ignored.", this);
                if (Application.isPlaying)
                {
                    Destroy(gameObject);
                }
                return;
            }
            _isDuplicate = false;
            Instance = this;
        }

        public static GroundScarSystem Instance { get; private set; }

        void OnEnable()
        {
            if (_isDuplicate)
            {
                return;
            }
            EventBus.PhaseChanged -= OnPhaseChanged;
            EventBus.PhaseChanged += OnPhaseChanged;
            EventBus.HomeRazed -= OnHomeRazed;
            EventBus.HomeRazed += OnHomeRazed;
            EventBus.SettlersKilledInHome -= OnSettlersKilled;
            EventBus.SettlersKilledInHome += OnSettlersKilled;
        }

        void OnDisable()
        {
            EventBus.PhaseChanged -= OnPhaseChanged;
            EventBus.HomeRazed -= OnHomeRazed;
            EventBus.SettlersKilledInHome -= OnSettlersKilled;
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        void Update()
        {
            if (!Application.isPlaying || !autoTick)
            {
                return;
            }
            Tick(Time.deltaTime);
        }

        // ------------------------------------------------------------------
        // Violence buffering (night) + dawn stamp
        // ------------------------------------------------------------------

        void OnHomeRazed(GameObject home)
        {
            if (home != null)
            {
                RecordViolence(home.transform.position);
            }
        }

        void OnSettlersKilled(GameObject home)
        {
            if (home != null)
            {
                RecordViolence(home.transform.position);
            }
        }

        /// <summary>Buffers a violence position to be stamped as a scar at the next dawn.</summary>
        public void RecordViolence(Vector3 worldPos)
        {
            _pendingViolence.Add(new Vector3(worldPos.x, 0f, worldPos.z));
        }

        void OnPhaseChanged(DayPhase phase)
        {
            if (phase == DayPhase.Dawn)
            {
                StampPendingScars();
            }
            else if (phase == DayPhase.Dusk)
            {
                // Meadow finishes regrowing before dusk — outside Winter, the day's
                // scars are gone. Winter's snow-covered scars persist.
                if (!IsWinter)
                {
                    _scars.RemoveAll(s => !s.snowCovered);
                }
            }
        }

        /// <summary>Stamps every buffered violence position as a scar, then clears the buffer.</summary>
        public void StampPendingScars()
        {
            for (int i = 0; i < _pendingViolence.Count; i++)
            {
                StampScar(_pendingViolence[i]);
            }
            _pendingViolence.Clear();
        }

        /// <summary>
        /// Stamps a scar at a position: merges into a nearby scar when one is within
        /// the config merge radius (intensities combine, clamped to 1), otherwise adds
        /// a fresh one. Winter stamps are snow-covered and never fade.
        /// </summary>
        public void StampScar(Vector3 worldPos)
        {
            var cfg = Config;
            bool snow = IsWinter && cfg.winterSnowCoversScars;
            Vector3 pos = new Vector3(worldPos.x, 0f, worldPos.z);

            for (int i = 0; i < _scars.Count; i++)
            {
                if (PlanarDistance(_scars[i].position, pos) <= cfg.scarMergeRadius)
                {
                    var merged = _scars[i];
                    merged.intensity = Mathf.Clamp01(merged.intensity + cfg.scarInitialIntensity);
                    merged.radius = Mathf.Max(merged.radius, cfg.scarStampRadius);
                    merged.snowCovered = merged.snowCovered || snow;
                    _scars[i] = merged;
                    GameEventLog.Append("ground_scar",
                        $"merge at ({pos.x:F1},{pos.z:F1}) snow={merged.snowCovered}");
                    return;
                }
            }

            _scars.Add(new Scar
            {
                position = pos,
                intensity = Mathf.Clamp01(cfg.scarInitialIntensity),
                radius = cfg.scarStampRadius,
                snowCovered = snow,
            });
            GameEventLog.Append("ground_scar",
                $"stamp at ({pos.x:F1},{pos.z:F1}) snow={snow}");
        }

        // ------------------------------------------------------------------
        // Fade
        // ------------------------------------------------------------------

        /// <summary>
        /// Fades non-snow scars toward meadow. Snow-covered (Winter) scars are held.
        /// Deterministic: intensity drops by initialIntensity / fadeDurationSeconds
        /// per second, and a scar at zero is removed.
        /// </summary>
        public void Tick(float dt)
        {
            if (dt <= 0f || _scars.Count == 0)
            {
                return;
            }
            var cfg = Config;
            float rate = cfg.scarInitialIntensity / Mathf.Max(0.01f, cfg.scarFadeDurationSeconds);
            for (int i = _scars.Count - 1; i >= 0; i--)
            {
                var scar = _scars[i];
                if (scar.snowCovered)
                {
                    continue; // snow holds the scar through Winter
                }
                scar.intensity -= rate * dt;
                if (scar.intensity <= 0f)
                {
                    _scars.RemoveAt(i);
                }
                else
                {
                    _scars[i] = scar;
                }
            }
        }

        // ------------------------------------------------------------------
        // Queries
        // ------------------------------------------------------------------

        /// <summary>Strongest scar intensity covering a position (0 = clean ground).</summary>
        public float IntensityAt(Vector3 worldPos)
        {
            float best = 0f;
            for (int i = 0; i < _scars.Count; i++)
            {
                var scar = _scars[i];
                if (PlanarDistance(scar.position, worldPos) <= scar.radius && scar.intensity > best)
                {
                    best = scar.intensity;
                }
            }
            return best;
        }

        /// <summary>Number of snow-covered (persisting) scars.</summary>
        public int SnowCoveredCount()
        {
            int count = 0;
            for (int i = 0; i < _scars.Count; i++)
            {
                if (_scars[i].snowCovered)
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>The current season, honouring the test override.</summary>
        public Season CurrentSeason
        {
            get
            {
                if (useSeasonOverride)
                {
                    return seasonOverride;
                }
                var s = SeasonSystem.Instance;
                return s != null ? s.CurrentSeason : Season.Spring;
            }
        }

        public bool IsWinter => CurrentSeason == Season.Winter;

        /// <summary>Clears scars and pending violence (test isolation / new game).</summary>
        public void ClearScars()
        {
            _scars.Clear();
            _pendingViolence.Clear();
        }

        static float PlanarDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        void OnDrawGizmos()
        {
            for (int i = 0; i < _scars.Count; i++)
            {
                var scar = _scars[i];
                Gizmos.color = scar.snowCovered
                    ? new Color(0.85f, 0.9f, 1f, 0.5f)                    // snow-white patch
                    : new Color(0.15f, 0.08f, 0.05f, 0.35f + 0.4f * scar.intensity); // dark burn
                Gizmos.DrawCube(scar.position + new Vector3(0f, 0.03f, 0f),
                    new Vector3(scar.radius * 2f, 0.03f, scar.radius * 2f));
            }
        }
    }
}
