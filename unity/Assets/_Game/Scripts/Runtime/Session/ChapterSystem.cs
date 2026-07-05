using System;
using Abbey.Core;
using Abbey.World;
using UnityEngine;

namespace Abbey.Session
{
    /// <summary>
    /// The four narrative chapters of the Phase 3 campaign (ROADMAP Phase 3 item 12):
    /// The Wreck, The Meadow, The Long Rain, The First White Night. Each chapter maps
    /// onto one of the year's four seasons (Spring/Summer/Autumn/Winter), so the chapter
    /// is a pure, deterministic read of <see cref="SeasonSystem"/> — no bounds are
    /// duplicated here; the names/flavour live in <see cref="GameSessionConfig"/>.
    ///
    /// A chapter TRANSITION (the season turning to a new chapter, or the first seed at
    /// enable) appends a "chapter" record to the shared <see cref="GameEventLog"/> with
    /// its morning-report flavour and raises <see cref="ChapterChanged"/>. Transitions
    /// are only logged when <see cref="GameSessionConfig.phase3CampaignEnabled"/> is set,
    /// so the Phase 2 slice is untouched.
    ///
    /// Singleton + [ExecuteAlways] like the other Phase 3 systems so EditMode tests get
    /// the OnEnable/OnDisable lifecycle. Season turns arrive on
    /// <see cref="EventBus.SeasonChanged"/>; tests call <see cref="Refresh"/> directly.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class ChapterSystem : MonoBehaviour
    {
        /// <summary>The log Type this system appends.</summary>
        public const string RecordType = "chapter";

        public static ChapterSystem Instance { get; private set; }

        /// <summary>Raised on each chapter transition: (index 0..3, display name).</summary>
        public static event Action<int, string> ChapterChanged;

        /// <summary>Test isolation — clear the static subscriber list in [SetUp]/[TearDown].</summary>
        public static void ResetStaticEvents()
        {
            ChapterChanged = null;
        }

        GameSessionConfig _config;
        bool _isDuplicate;
        bool _hasChapter;
        int _chapterIndex = -1;

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

        /// <summary>0..3 chapter index, keyed to the current season (Spring=0..Winter=3).</summary>
        public int CurrentChapterIndex => _chapterIndex < 0 ? 0 : _chapterIndex;

        /// <summary>Display name of the current chapter, from config.</summary>
        public string CurrentChapterName => Config.ChapterNameFor(CurrentChapterIndex);

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                _isDuplicate = true;
                Debug.LogWarning("[ChapterSystem] Duplicate instance ignored.", this);
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
            EventBus.SeasonChanged -= OnSeasonChanged;
            EventBus.SeasonChanged += OnSeasonChanged;
            // Seed the chapter for whatever season the calendar already sits on.
            Refresh();
        }

        void OnDisable()
        {
            EventBus.SeasonChanged -= OnSeasonChanged;
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>Injects a config (tests) and re-seeds the chapter.</summary>
        public void Configure(GameSessionConfig config)
        {
            _config = config;
            _hasChapter = false;
            _chapterIndex = -1;
            Refresh();
        }

        void OnSeasonChanged(Season season)
        {
            Refresh();
        }

        /// <summary>
        /// Recomputes the chapter for the current season and, on a change, logs the
        /// transition (campaign mode only) and raises <see cref="ChapterChanged"/>. Public
        /// so tests and debug tools can force it. Deterministic: the chapter is a pure
        /// function of <see cref="SeasonSystem.CurrentSeason"/>.
        /// </summary>
        public void Refresh()
        {
            if (_isDuplicate)
            {
                return;
            }
            int index = ChapterIndexForSeason();
            bool changed = !_hasChapter || index != _chapterIndex;
            _chapterIndex = index;
            _hasChapter = true;
            if (!changed)
            {
                return;
            }

            if (Config.phase3CampaignEnabled)
            {
                GameEventLog.Append(RecordType,
                    $"chapter_begins index={index} name=\"{Config.ChapterNameFor(index)}\" " +
                    $"— {Config.ChapterFlavourFor(index)}");
            }
            ChapterChanged?.Invoke(index, Config.ChapterNameFor(index));
        }

        int ChapterIndexForSeason()
        {
            var season = SeasonSystem.Instance != null
                ? SeasonSystem.Instance.CurrentSeason
                : Season.Spring;
            int i = (int)season;
            return Mathf.Clamp(i, 0, 3);
        }

        /// <summary>Resets the chapter state to before the run (test isolation).</summary>
        public void Clear()
        {
            _hasChapter = false;
            _chapterIndex = -1;
        }
    }
}
