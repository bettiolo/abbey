using System;
using Abbey.Core;
using Abbey.Nightmares;
using UnityEngine;

namespace Abbey.Session
{
    /// <summary>
    /// The scripted climax (VERTICAL_SLICE_SPEC §3 opening + §10 close: "the First
    /// White Night as the climax special event"). Data-driven and decoupled from the
    /// outcome authority — both read <see cref="GameSessionConfig.whiteNightIndex"/>
    /// so they can never disagree about which night is special.
    ///
    /// It reaches the White Night by counting phase transitions, and makes that one
    /// night harder WITHOUT editing <see cref="NightmareDirector"/>: on the DUSK
    /// before the White Night it flips the director's config to the Phase 2 scripted
    /// mode and installs <see cref="GameSessionConfig.whiteNightSchedule"/>. Because
    /// Dusk strictly precedes Night, the director reads the already-armed config when
    /// its own Night boundary calls BeginNight — no assumption about subscriber order,
    /// no new director hook needed.
    ///
    /// [ExecuteAlways] so EditMode tests get the OnEnable phase subscription.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class FirstWhiteNightScenario : MonoBehaviour
    {
        /// <summary>The log Type this system appends.</summary>
        public const string RecordType = "scenario";

        /// <summary>Raised when the White Night's Night boundary is crossed. Static for UI/audio.</summary>
        public static event Action<int> WhiteNightBegan;

        /// <summary>Test isolation — clear the static subscriber list in [SetUp]/[TearDown].</summary>
        public static void ResetStaticEvents()
        {
            WhiteNightBegan = null;
        }

        [Tooltip("The night director this scenario arms for the White Night. Required to actually raise the difficulty.")]
        public NightmareDirector director;

        GameSessionConfig _config;
        int _nightsSeen;
        bool _armed;      // the White Night's schedule is installed on the director
        bool _began;      // the White Night's Night boundary has been crossed

        public GameSessionConfig Config
        {
            get
            {
                if (_config == null)
                {
                    _config = GameSessionConfig.LoadOrDefault();
                }
                return _config;
            }
            set { _config = value; }
        }

        /// <summary>1-based night number that is the White Night.</summary>
        public int WhiteNightIndex => Config.whiteNightIndex;

        /// <summary>True once the director has been armed with the White Night schedule.</summary>
        public bool IsArmed => _armed;

        /// <summary>True once the White Night itself has begun.</summary>
        public bool HasBegun => _began;

        void OnEnable()
        {
            EventBus.PhaseChanged += OnPhaseChanged;
        }

        void OnDisable()
        {
            EventBus.PhaseChanged -= OnPhaseChanged;
        }

        void OnPhaseChanged(DayPhase phase)
        {
            if (Config.phase3CampaignEnabled)
            {
                return;
            }

            switch (phase)
            {
                case DayPhase.Dusk:
                    // The upcoming night is _nightsSeen + 1. Arm the climax the dusk
                    // before, so the director reads the harder config at its Night
                    // boundary regardless of handler order.
                    if (_nightsSeen + 1 == WhiteNightIndex)
                    {
                        ArmWhiteNight();
                    }
                    break;

                case DayPhase.Night:
                    _nightsSeen++;
                    if (_nightsSeen == WhiteNightIndex && !_began)
                    {
                        _began = true;
                        GameEventLog.Append(RecordType,
                            $"white_night_begins night={_nightsSeen}");
                        WhiteNightBegan?.Invoke(_nightsSeen);
                    }
                    break;
            }
        }

        /// <summary>
        /// Installs the White Night difficulty on the director's config: Phase 2
        /// scripted mode with the harder schedule. Idempotent; safe to call directly
        /// (tests) as well as from the dusk trigger.
        /// </summary>
        public void ArmWhiteNight()
        {
            if (Config.phase3CampaignEnabled)
            {
                GameEventLog.Append(RecordType,
                    "white_night_legacy_scenario_skipped reason=phase3_campaign");
                return;
            }

            if (_armed)
            {
                return;
            }
            _armed = true;

            if (director != null)
            {
                var cfg = director.Config;
                cfg.phase2NightsEnabled = true;
                if (Config.whiteNightSchedule != null && Config.whiteNightSchedule.Length > 0)
                {
                    cfg.phase2NightSchedule = Config.whiteNightSchedule;
                }
                GameEventLog.Append(RecordType,
                    $"white_night_armed night={WhiteNightIndex} " +
                    $"events={cfg.phase2NightSchedule?.Length ?? 0}");
            }
            else
            {
                GameEventLog.Append(RecordType,
                    $"white_night_armed night={WhiteNightIndex} events=0 (no director)");
            }
        }

        /// <summary>Resets the scenario to before the run (test isolation).</summary>
        public void Clear()
        {
            _nightsSeen = 0;
            _armed = false;
            _began = false;
        }
    }
}
