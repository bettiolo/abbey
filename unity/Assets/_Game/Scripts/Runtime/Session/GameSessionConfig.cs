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
