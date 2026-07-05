using UnityEngine;

namespace Abbey.Sanity
{
    /// <summary>
    /// Single ScriptableObject holding ALL sanity/dread/asylum tunables (AGENTS.md
    /// rule: no balance values inside MonoBehaviours). Systems fetch it via
    /// <see cref="LoadOrDefault"/> so tests and CI never need an asset file to exist.
    /// An optional asset at Resources/SanityConfig overrides the coded defaults.
    /// Mirrors <see cref="Abbey.Core.PrototypeConfig"/> /
    /// <see cref="Abbey.World.SeasonConfig"/>.
    ///
    /// All sanity/dread values are 0..1. Sanity is the persistent damage track
    /// (1 = sound of mind); dread is the transient meter that fills in the dark at
    /// night and, once past <see cref="dreadDamageThreshold"/>, eats sanity.
    /// </summary>
    [CreateAssetMenu(fileName = "SanityConfig", menuName = "Abbey/Sanity Config")]
    public class SanityConfig : ScriptableObject
    {
        public const string ResourcePath = "SanityConfig";

        [Header("Sanity bands (0..1, 1 = sound of mind)")]
        [Tooltip("Below this sanity a villager reads Shaken.")]
        [Range(0f, 1f)] public float shakenThreshold = 0.7f;
        [Tooltip("Below this sanity a villager reads Breaking.")]
        [Range(0f, 1f)] public float breakingThreshold = 0.4f;
        [Tooltip("Below this sanity a villager goes Insane: it stops working and needs care.")]
        [Range(0f, 1f)] public float insanityThreshold = 0.2f;
        [Tooltip("Sanity a recovering (insane) villager must reach before it is released.")]
        [Range(0f, 1f)] public float releaseThreshold = 0.5f;

        [Header("Dread (0..1) — the dark-exposure meter")]
        [Tooltip("Dread gained per second standing in the Dark at Night.")]
        [Min(0f)] public float dreadGainPerSecondInDark = 0.2f;
        [Tooltip("Dread lost per second when not caught in the dark at night (calm).")]
        [Min(0f)] public float dreadDecayPerSecond = 0.1f;
        [Tooltip("Dread above this level starts damaging sanity.")]
        [Range(0f, 1f)] public float dreadDamageThreshold = 0.6f;
        [Tooltip("Sanity lost per second while dread sits above the damage threshold.")]
        [Min(0f)] public float sanityDamagePerSecondAtHighDread = 0.05f;

        [Header("Recovery (sanity regained per second while recovering)")]
        [Tooltip("Fast recovery inside the asylum.")]
        [Min(0f)] public float asylumRecoveryPerSecond = 0.06f;
        [Tooltip("Slow recovery at home when no asylum exists.")]
        [Min(0f)] public float homeRecoveryPerSecond = 0.02f;

        [Header("Asylum")]
        [Tooltip("Days an admitted villager is held before it may be released (>=1 misses the next night).")]
        [Min(1)] public int asylumCooldownDays = 1;

        [Header("Home disturbance (no asylum)")]
        [Tooltip("Dread each housemate gains per night an insane settler recovers at home (screaming, nightmares).")]
        [Min(0f)] public float dreadSpillPerNight = 0.15f;

        [Header("Work-efficiency curve (daytime output vs sanity)")]
        [Tooltip("Work efficiency just above the insanity threshold (0 at/below it: insane villagers stop working).")]
        [Range(0f, 1f)] public float workEfficiencyFloor = 0.3f;
        [Tooltip("Sanity at or above which work efficiency is full (1).")]
        [Range(0f, 1f)] public float workFullSanity = 0.8f;

        /// <summary>
        /// Daytime work-efficiency multiplier for a sanity value. Zero at or below
        /// the insanity threshold (an insane villager stops working until recovered),
        /// then ramps from <see cref="workEfficiencyFloor"/> up to 1 at
        /// <see cref="workFullSanity"/>.
        /// </summary>
        public float WorkEfficiency(float sanity)
        {
            if (sanity <= insanityThreshold)
            {
                return 0f;
            }
            float upper = Mathf.Max(insanityThreshold + 0.0001f, workFullSanity);
            float t = Mathf.Clamp01(Mathf.InverseLerp(insanityThreshold, upper, sanity));
            return Mathf.Lerp(Mathf.Clamp01(workEfficiencyFloor), 1f, t);
        }

        /// <summary>The band a sanity value falls into (ignoring recovery hysteresis).</summary>
        public SanityState BandFor(float sanity)
        {
            if (sanity < insanityThreshold)
            {
                return SanityState.Insane;
            }
            if (sanity < breakingThreshold)
            {
                return SanityState.Breaking;
            }
            if (sanity < shakenThreshold)
            {
                return SanityState.Shaken;
            }
            return SanityState.Stable;
        }

        static SanityConfig _cached;

        /// <summary>
        /// Returns the config asset from Resources/SanityConfig if one exists,
        /// otherwise an in-memory instance with the coded defaults. Never returns null.
        /// </summary>
        public static SanityConfig LoadOrDefault()
        {
            if (_cached != null)
            {
                return _cached;
            }

            _cached = Resources.Load<SanityConfig>(ResourcePath);
            if (_cached == null)
            {
                _cached = CreateInstance<SanityConfig>();
                _cached.name = "SanityConfig (defaults)";
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
