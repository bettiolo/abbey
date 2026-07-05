using Abbey.Core;
using UnityEngine;

namespace Abbey.Beast
{
    /// <summary>
    /// A read-only snapshot of the hound's evolution for downstream consumers — the
    /// moral pressures in P3-10, the abbey transformation in P3-10/P3-11 and the
    /// who-sails/who-stays end summary in P3-14. Pure data.
    /// </summary>
    public readonly struct BeastStatusReport
    {
        /// <summary>The path the hound has settled onto (Unevolved before any locks in).</summary>
        public readonly HoundPath Path;

        /// <summary>Standing toward the beast: -1 feared … +1 beloved.</summary>
        public readonly float BeastStatus;

        /// <summary>True once the path is permanent (the year has hardened it).</summary>
        public readonly bool Locked;

        /// <summary>Dominant path score at the last dawn evaluation.</summary>
        public readonly float DominantScore;

        public BeastStatusReport(HoundPath path, float beastStatus, bool locked, float dominantScore)
        {
            Path = path;
            BeastStatus = beastStatus;
            Locked = locked;
            DominantScore = dominantScore;
        }

        public override string ToString()
        {
            return $"{Path} status={BeastStatus:F2}{(Locked ? " (locked)" : "")}";
        }
    }

    /// <summary>
    /// The hound-evolution system (P3-07). Once per dawn it reads the
    /// <see cref="HoundController"/>'s accumulated treatment counters + bond averages
    /// and the <see cref="Doctrine"/>, scores the five paths through
    /// <see cref="HoundEvolutionConfig"/> and adopts the dominant path once it clears
    /// the adopt threshold — locking it permanently past the lock threshold. Adopting a
    /// path pushes its behaviour parameters (aggression, bell response, villager
    /// comfort, hunt-alone / abbey-only / unreliable flags) onto the controller and
    /// updates the exposed <see cref="BeastStatus"/>. Every transition is event-logged
    /// (<see cref="EventBus.HoundEvolved"/>, "hound_evolved" from→to).
    ///
    /// Deterministic: no RNG — the path is a pure function of accumulated treatment,
    /// bond and doctrine. Singleton + [ExecuteAlways] like the other Phase 3 systems so
    /// EditMode tests get the OnEnable/OnDisable lifecycle. The evaluation runs off
    /// <see cref="EventBus.PhaseChanged"/> at Dawn; tests call
    /// <see cref="EvaluateAtDawn"/> directly.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class HoundEvolutionSystem : MonoBehaviour
    {
        public static HoundEvolutionSystem Instance { get; private set; }

        [Tooltip("The standing Hound doctrine (written by the P3-09 Hound law; Neutral until then).")]
        public HoundDoctrine doctrine = HoundDoctrine.Neutral;

        HoundEvolutionConfig _config;
        HoundController _hound;
        bool _isDuplicate;
        float[] _scores = new float[6];

        public HoundPath CurrentPath { get; private set; } = HoundPath.Unevolved;

        /// <summary>True once the path is locked in permanently.</summary>
        public bool PathLocked { get; private set; }

        /// <summary>Dominant path score at the last dawn evaluation (debug/overlay).</summary>
        public float LastDominantScore { get; private set; }

        /// <summary>Beast standing: -1 feared … +1 beloved (P3-10 pressures, P3-14 summary).</summary>
        public float BeastStatus { get; private set; }

        /// <summary>The doctrine input the Hound law writes (P3-09).</summary>
        public HoundDoctrine Doctrine
        {
            get { return doctrine; }
            set { doctrine = value; }
        }

        public HoundEvolutionConfig Config
        {
            get
            {
                if (_config == null)
                {
                    _config = HoundEvolutionConfig.LoadOrDefault();
                }
                return _config;
            }
            set { _config = value; }
        }

        /// <summary>The hound this system evolves. Auto-found in the scene; tests inject one.</summary>
        public HoundController Hound
        {
            get
            {
                if (_hound == null)
                {
                    _hound = FindFirstObjectByType<HoundController>();
                }
                return _hound;
            }
            set { _hound = value; }
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                _isDuplicate = true;
                Debug.LogWarning("[HoundEvolutionSystem] Duplicate instance ignored.", this);
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

        /// <summary>Injects a config (tests) and resets the evolution to Unevolved.</summary>
        public void Configure(HoundEvolutionConfig config, HoundController hound = null)
        {
            _config = config;
            if (hound != null)
            {
                _hound = hound;
            }
            CurrentPath = HoundPath.Unevolved;
            PathLocked = false;
            LastDominantScore = 0f;
            BeastStatus = 0f;
        }

        void OnPhaseChanged(DayPhase phase)
        {
            if (_isDuplicate)
            {
                return;
            }
            if (phase == DayPhase.Dawn)
            {
                EvaluateAtDawn();
            }
        }

        /// <summary>The per-path scores from the last evaluation, indexed by <see cref="HoundPath"/>.</summary>
        public float ScoreFor(HoundPath path)
        {
            int i = (int)path;
            return i >= 0 && i < _scores.Length ? _scores[i] : 0f;
        }

        /// <summary>Full downstream snapshot (P3-10 pressures, P3-14 end summary).</summary>
        public BeastStatusReport Report()
        {
            return new BeastStatusReport(CurrentPath, BeastStatus, PathLocked, LastDominantScore);
        }

        /// <summary>
        /// The daily dawn evaluation: score the paths from the hound's treatment history
        /// + bond + doctrine, adopt/lock the dominant path and apply its behaviour and
        /// beast status. Deterministic. Public so tests can drive it. Safe with no hound.
        /// </summary>
        public void EvaluateAtDawn()
        {
            if (_isDuplicate)
            {
                return;
            }
            var hound = Hound;
            if (hound == null)
            {
                return;
            }
            var cfg = Config;
            var sample = hound.BuildTreatmentSample();

            // Score every path (for the overlay) and find the dominant one.
            for (int i = 0; i < _scores.Length; i++)
            {
                _scores[i] = cfg.ScoreFor((HoundPath)i, sample, doctrine);
            }
            var dominant = cfg.DominantPath(sample, doctrine, out float dominantScore);
            LastDominantScore = dominantScore;

            if (!PathLocked)
            {
                HoundPath next = dominantScore >= cfg.pathAdoptThreshold
                    ? dominant : HoundPath.Unevolved;
                if (next != CurrentPath)
                {
                    var previous = CurrentPath;
                    CurrentPath = next;
                    ApplyPath(hound, next);
                    EventBus.RaiseHoundEvolved(previous, next);
                }
                if (next != HoundPath.Unevolved && dominantScore >= cfg.pathLockThreshold)
                {
                    PathLocked = true;
                    GameEventLog.Append("hound_evolved",
                        $"locked path={next} score={dominantScore:F1}");
                }
            }

            // Beast standing follows the (locked or current) path + live bond every dawn.
            var profile = cfg.ProfileFor(CurrentPath);
            BeastStatus = profile.BeastStatus(hound.AverageTrust, hound.AverageFear);
        }

        void ApplyPath(HoundController hound, HoundPath path)
        {
            var profile = Config.ProfileFor(path);
            hound.ApplyEvolution(path, profile.behaviour);
        }
    }
}
