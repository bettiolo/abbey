using UnityEngine;

namespace Abbey.Core
{
    /// <summary>The four phases of the day/night cycle, in order.</summary>
    public enum DayPhase
    {
        Day,
        Dusk,
        Night,
        Dawn
    }

    /// <summary>
    /// Singleton clock driving the day/night cycle. Phase durations come from
    /// <see cref="PrototypeConfig"/> (never hard-coded here). Two drive modes:
    /// Update-driven (<see cref="autoTick"/>, default) for play, and manual
    /// <see cref="Tick"/> for deterministic tests (set autoTick = false).
    /// [ExecuteAlways] so EditMode tests get Awake/OnEnable lifecycle.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class GameClock : MonoBehaviour
    {
        public static GameClock Instance { get; private set; }

        [Tooltip("Advance from Update using Time.deltaTime. Tests set false and call Tick().")]
        public bool autoTick = true;

        PrototypeConfig _config;
        float _timeInPhase;
        float _totalTime;

        public DayPhase Phase { get; private set; } = DayPhase.Day;

        /// <summary>1-based day counter; increments each time Dawn wraps back to Day.</summary>
        public int DayNumber { get; private set; } = 1;

        /// <summary>Total simulated seconds since the clock started.</summary>
        public float TotalTime => _totalTime;

        /// <summary>Seconds spent in the current phase.</summary>
        public float TimeInPhase => _timeInPhase;

        /// <summary>Normalized progress through the current phase, 0..1.</summary>
        public float PhaseProgress
        {
            get
            {
                float duration = GetPhaseDuration(Phase);
                return duration <= 0f ? 1f : Mathf.Clamp01(_timeInPhase / duration);
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
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[GameClock] Duplicate instance destroyed.", this);
                if (Application.isPlaying)
                {
                    Destroy(gameObject);
                }
                else
                {
                    DestroyImmediate(gameObject);
                }
                return;
            }
            Instance = this;
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

        /// <summary>Injects a config (tests) and resets the clock to Day 1, phase start.</summary>
        public void Configure(PrototypeConfig config)
        {
            _config = config;
            ResetClock();
        }

        /// <summary>Back to Day 1, Day phase, zero time. Does not raise events.</summary>
        public void ResetClock()
        {
            Phase = DayPhase.Day;
            DayNumber = 1;
            _timeInPhase = 0f;
            _totalTime = 0f;
        }

        /// <summary>
        /// Advances the clock by dt seconds, crossing as many phase boundaries as needed.
        /// Raises <see cref="EventBus.RaisePhaseChanged"/> once per boundary crossed.
        /// </summary>
        public void Tick(float dt)
        {
            if (dt <= 0f)
            {
                return;
            }

            _totalTime += dt;
            _timeInPhase += dt;

            // Cross boundaries one at a time so every phase raises its event in order.
            float duration = GetPhaseDuration(Phase);
            int safety = 64; // durations are Min(0.01) so this never spins forever
            while (_timeInPhase >= duration && safety-- > 0)
            {
                _timeInPhase -= duration;
                AdvancePhase();
                duration = GetPhaseDuration(Phase);
            }
        }

        public float GetPhaseDuration(DayPhase phase)
        {
            var cfg = Config;
            switch (phase)
            {
                case DayPhase.Day: return cfg.dayDurationSeconds;
                case DayPhase.Dusk: return cfg.duskDurationSeconds;
                case DayPhase.Night: return cfg.nightDurationSeconds;
                case DayPhase.Dawn: return cfg.dawnDurationSeconds;
                default: return cfg.dayDurationSeconds;
            }
        }

        void AdvancePhase()
        {
            var next = (DayPhase)(((int)Phase + 1) % 4);
            if (next == DayPhase.Day)
            {
                DayNumber++;
            }
            Phase = next;
            EventBus.RaisePhaseChanged(Phase);
        }
    }
}
