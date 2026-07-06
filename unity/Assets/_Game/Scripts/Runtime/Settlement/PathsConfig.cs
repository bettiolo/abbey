using UnityEngine;

namespace Abbey.Settlement
{
    /// <summary>
    /// Every tunable for the two ground-memory systems (P3-12), in one
    /// ScriptableObject so no balance value hides inside a MonoBehaviour
    /// (AGENTS.md). <see cref="TrafficGrid"/>, <see cref="DesirePathSystem"/> and
    /// <see cref="GroundScarSystem"/> all fetch it via <see cref="LoadOrDefault"/>,
    /// so tests and CI never need an asset file to exist. An optional asset at
    /// Resources/PathsConfig overrides the coded defaults. Mirrors
    /// <see cref="SettlementGrowthConfig"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "PathsConfig", menuName = "Abbey/Paths Config")]
    public class PathsConfig : ScriptableObject
    {
        public const string ResourcePath = "PathsConfig";

        [Header("Traffic grid (world-space, XZ)")]
        [Tooltip("World units per grid cell.")]
        [Min(0.1f)] public float cellSize = 2f;
        [Tooltip("World XZ position of the (0,0) cell's minimum corner.")]
        public Vector2 gridOrigin = new Vector2(-48f, -48f);
        [Tooltip("Number of cell columns (X).")]
        [Min(1)] public int gridColumns = 48;
        [Tooltip("Number of cell rows (Z).")]
        [Min(1)] public int gridRows = 48;

        [Header("Wear accumulation + decay")]
        [Tooltip("Wear added to a cell per world-unit a path-wearing agent travels across it.")]
        [Min(0f)] public float wearPerDistanceUnit = 0.6f;
        [Tooltip("A cell's wear never climbs above this (a saturated road).")]
        [Min(0f)] public float maxWearPerCell = 60f;
        [Tooltip("Fraction of each cell's wear shed at every day marker (unused paths fade).")]
        [Range(0f, 1f)] public float wearDecayPerDay = 0.2f;
        [Tooltip("Wear below this after a decay pass is dropped to zero (a faint scuff heals).")]
        [Min(0f)] public float wearDecayFloor = 0.25f;

        [Header("Path tiers (ascending wear thresholds)")]
        [Tooltip("Wear needed to reach tier 1, 2, 3, ... A cell below the first threshold is tier 0 (untrodden ground).")]
        public float[] tierWearThresholds = new float[] { 3f, 9f, 18f };
        [Tooltip("Walk-speed multiplier granted on tier 1, 2, 3, ... (parallel to the thresholds). Tier 0 is always 1.")]
        public float[] tierSpeedMultipliers = new float[] { 1.1f, 1.2f, 1.3f };
        [Tooltip("Tiers at or above this are 'important paths': they expect lantern coverage at night.")]
        [Min(1)] public int importantTier = 2;

        [Header("Lantern fuel debt / light debt on important paths (night)")]
        [Tooltip("A lantern covering an important-path cell at night burns fuel this many times faster.")]
        [Min(1f)] public float importantPathFuelMultiplier = 1.6f;
        [Tooltip("Light debt added per important-path cell that sits unlit at dusk (feeds P3-02 pressure).")]
        [Min(0f)] public float unlitImportantPathLightDebtPerCell = 1.5f;

        [Header("Ground scars (transient night violence)")]
        [Tooltip("Radius of a stamped scar in world units.")]
        [Min(0f)] public float scarStampRadius = 3f;
        [Tooltip("Intensity a fresh scar is stamped at (0..1).")]
        [Range(0f, 1f)] public float scarInitialIntensity = 1f;
        [Tooltip("Two stamps within this planar distance merge into one (intensities combine, clamped to 1).")]
        [Min(0f)] public float scarMergeRadius = 1.5f;
        [Tooltip("Seconds for a scar to fade fully to meadow. Fade runs only outside Winter.")]
        [Min(0.01f)] public float scarFadeDurationSeconds = 20f;
        [Tooltip("When true, a scar stamped in Winter is snow-covered and does NOT fade; it persists until thaw.")]
        public bool winterSnowCoversScars = true;

        // ------------------------------------------------------------------
        // Derived helpers (pure)
        // ------------------------------------------------------------------

        /// <summary>Highest tier whose wear threshold this wear meets (0 = below every threshold).</summary>
        public int TierForWear(float wear)
        {
            int tier = 0;
            if (tierWearThresholds == null)
            {
                return 0;
            }
            for (int i = 0; i < tierWearThresholds.Length; i++)
            {
                if (wear >= tierWearThresholds[i])
                {
                    tier = i + 1;
                }
            }
            return tier;
        }

        /// <summary>Walk-speed multiplier for a tier (tier 0 = 1; clamps to the last entry).</summary>
        public float SpeedMultiplierForTier(int tier)
        {
            if (tier <= 0 || tierSpeedMultipliers == null || tierSpeedMultipliers.Length == 0)
            {
                return 1f;
            }
            int index = Mathf.Clamp(tier - 1, 0, tierSpeedMultipliers.Length - 1);
            return Mathf.Max(0.01f, tierSpeedMultipliers[index]);
        }

        /// <summary>Whether a tier counts as an important (lantern-expecting) path.</summary>
        public bool IsImportantTier(int tier)
        {
            return tier >= Mathf.Max(1, importantTier);
        }

        static PathsConfig _cached;

        /// <summary>
        /// Returns the config asset from Resources/PathsConfig if one exists,
        /// otherwise an in-memory instance with the coded defaults. Never null.
        /// </summary>
        public static PathsConfig LoadOrDefault()
        {
            if (_cached != null)
            {
                return _cached;
            }

            _cached = Resources.Load<PathsConfig>(ResourcePath);
            if (_cached == null)
            {
                _cached = CreateInstance<PathsConfig>();
                _cached.name = "PathsConfig (defaults)";
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
