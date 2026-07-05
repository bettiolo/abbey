using System.Collections.Generic;
using Abbey.Economy;
using UnityEngine;

namespace Abbey.Session
{
    /// <summary>
    /// Single ScriptableObject holding the win/loss + First-White-Night tunables
    /// (AGENTS.md rule: no balance values inside MonoBehaviours). Systems fetch it
    /// via <see cref="LoadOrDefault"/> so tests and CI never need an asset file to
    /// exist. An optional asset at Resources/GameSessionConfig overrides the coded
    /// defaults. Mirrors <see cref="Abbey.Core.PrototypeConfig"/> /
    /// <see cref="Abbey.Economy.EconomyConfig"/>.
    ///
    /// One source of truth for the climax: both <see cref="GameSession"/> (the
    /// outcome authority) and <see cref="FirstWhiteNightScenario"/> (the scripted
    /// climax) read <see cref="whiteNightIndex"/> from here so they can never
    /// disagree about which night is the White Night.
    /// </summary>
    [CreateAssetMenu(fileName = "GameSessionConfig", menuName = "Abbey/Game Session Config")]
    public class GameSessionConfig : ScriptableObject
    {
        public const string ResourcePath = "GameSessionConfig";

        [Header("Win condition (VERTICAL_SLICE_SPEC §11)")]
        [Tooltip("Villagers that must be alive (not Dead/Missing) at dawn after the White Night to win.")]
        [Min(0)] public int villagerWinThreshold = 6;

        [Header("The First White Night (climax)")]
        [Tooltip("1-based night number that IS the White Night. Default 2: one ordinary first night, then the climax. Win is only possible on the dawn after this night survives.")]
        [Min(1)] public int whiteNightIndex = 2;

        [Tooltip("The harder scripted schedule the director runs on the White Night (PrototypeConfig.phase2 'fraction:kind' vocabulary). Applied to the nightmare config the dusk before, so the director's Night boundary reads it — the director itself is never edited.")]
        public string[] whiteNightSchedule =
        {
            "0.05:whisper",
            "0.10:pale_hound",
            "0.18:shadow",
            "0.25:pale_hound",
            "0.32:lantern_moth",
            "0.40:pale_hound",
            "0.48:whisper",
            "0.55:pale_hound",
            "0.62:drowned_sailor",
            "0.70:panic",
            "0.78:pale_hound",
            "0.86:shadow",
            "0.94:pale_hound",
        };

        // ------------------------------------------------------------------
        // Phase 3 campaign (P3-14): four chapters + spring-ship win
        // ------------------------------------------------------------------

        [Header("Phase 3 campaign (P3-14)")]
        [Tooltip("Opt-in for the Phase 3 campaign: four calendar chapters and the spring-ship "
                 + "launch win. When OFF the run behaves exactly as the Phase 2 slice (the White "
                 + "Night is the campaign end). Mirrors the P2-06 phase2NightsEnabled precedent.")]
        public bool phase3CampaignEnabled;

        [Tooltip("The four narrative chapters keyed to the year's four seasons, in season order "
                 + "(Spring/Summer/Autumn/Winter). Length 4; data only — ChapterSystem reads it.")]
        public string[] chapterNames =
        {
            "The Wreck",
            "The Meadow",
            "The Long Rain",
            "The First White Night",
        };

        [Tooltip("One-line morning-report flavour logged when each chapter begins. Length 4, "
                 + "aligned with chapterNames.")]
        public string[] chapterFlavour =
        {
            "Salvage from the wreck litters the shore; the abbey is a ruin, and the year is young.",
            "The meadow greens and the fields take; for a little while the light feels like enough.",
            "The long rain sets in from the sea, and something in the dark grows bolder each night.",
            "Winter closes its hand; the first White Night is coming, and after it — if we live — the spring tide.",
        };

        [Header("Spring-ship launch window")]
        [Tooltip("Year (1-based, SeasonSystem.YearNumber) whose SPRING opens the launch window. "
                 + "Default 2: survive the first year, then sail the following spring.")]
        [Min(1)] public int springLaunchYear = 2;

        [Header("Manifest — part 1: settlers (willing sailors)")]
        [Tooltip("Minimum willing sailors (volunteers + spring departure intents from P3-13) that "
                 + "must be aboard for the settlers part of the manifest to be complete.")]
        [Min(1)] public int manifestSettlers = 4;

        [Header("Manifest — part 2: provisions (ledger thresholds)")]
        [Tooltip("Stockpile the ship must carry to sail. Grain counts toward Food at "
                 + "EconomyConfig.GrainToFood before the check. Candles + Tools are per-manifest "
                 + "totals, not per-head. Data only.")]
        public List<ResourceStack> manifestProvisions = new List<ResourceStack>
        {
            new ResourceStack(ResourceType.Food, 24),
            new ResourceStack(ResourceType.Candles, 6),
            new ResourceStack(ResourceType.Tools, 3),
        };

        [Header("Manifest — part 3: hull / rigging (ship reconstruction)")]
        [Tooltip("BuildingCatalog id of the staged ship reconstruction site whose completion "
                 + "satisfies the hull/rigging part of the manifest (includes sailcloth in its "
                 + "build cost, woven from wool).")]
        public string shipBuildingId = "spring_ship_t1";

        /// <summary>Chapter display name for a season (Spring..Winter), safe-indexed.</summary>
        public string ChapterNameFor(int seasonIndex)
        {
            if (chapterNames == null || chapterNames.Length == 0)
            {
                return $"Chapter {seasonIndex + 1}";
            }
            int i = Mathf.Clamp(seasonIndex, 0, chapterNames.Length - 1);
            return chapterNames[i];
        }

        /// <summary>Chapter flavour line for a season index, safe-indexed (empty when none).</summary>
        public string ChapterFlavourFor(int seasonIndex)
        {
            if (chapterFlavour == null || chapterFlavour.Length == 0)
            {
                return string.Empty;
            }
            int i = Mathf.Clamp(seasonIndex, 0, chapterFlavour.Length - 1);
            return chapterFlavour[i];
        }

        static GameSessionConfig _cached;

        /// <summary>
        /// Returns the config asset from Resources/GameSessionConfig if one exists,
        /// otherwise an in-memory instance with the coded defaults. Never returns null.
        /// </summary>
        public static GameSessionConfig LoadOrDefault()
        {
            if (_cached != null)
            {
                return _cached;
            }

            _cached = Resources.Load<GameSessionConfig>(ResourcePath);
            if (_cached == null)
            {
                _cached = CreateInstance<GameSessionConfig>();
                _cached.name = "GameSessionConfig (defaults)";
                _cached.hideFlags = HideFlags.HideAndDontSave;
            }
            return _cached;
        }

        /// <summary>Drops the cached instance (test isolation).</summary>
        public static void ClearCache()
        {
            _cached = null;
        }
    }
}
