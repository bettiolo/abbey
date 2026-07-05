using UnityEngine;

namespace Abbey.Villagers
{
    /// <summary>
    /// Single ScriptableObject holding ALL villager-job tunables (AGENTS.md rule:
    /// no balance values inside MonoBehaviours). Systems fetch it via
    /// <see cref="LoadOrDefault"/> so tests and CI never need an asset file to
    /// exist. An optional asset at Resources/JobsConfig overrides the coded
    /// defaults. Mirrors <see cref="Abbey.Economy.EconomyConfig"/>.
    ///
    /// The salvager's work-cycle duration and per-cycle yield deliberately live in
    /// EconomyConfig (salvageWorkDurationSeconds / salvageYieldPerCycle) — economy
    /// owns salvage tuning; this asset holds everything job-side.
    /// </summary>
    [CreateAssetMenu(fileName = "JobsConfig", menuName = "Abbey/Jobs Config")]
    public class JobsConfig : ScriptableObject
    {
        public const string ResourcePath = "JobsConfig";

        [Header("Default roster (JobAssigner order: fill each quota, rest get None)")]
        [Min(0)] public int defaultSalvagers = 3;
        [Min(0)] public int defaultBuilders = 1;
        [Min(0)] public int defaultWoodcutters = 2;
        [Min(0)] public int defaultTenders = 1;
        [Min(0)] public int defaultGuards = 1;

        [Header("Renewable production roster (P3-04; assigned explicitly, default 0)")]
        [Min(0)] public int defaultFarmers = 0;
        [Min(0)] public int defaultHerders = 0;
        [Min(0)] public int defaultCharcoalers = 0;
        [Min(0)] public int defaultSmiths = 0;

        [Header("Production work")]
        [Tooltip("Standing within this radius of the production building's work slot "
                 + "counts as staffing it (mirrors the guard post radius).")]
        [Min(0.1f)] public float productionStaffRadius = 1.5f;

        [Header("Hauling")]
        [Tooltip("Units of one resource a villager can physically carry per trip.")]
        [Min(1)] public int carryCapacity = 4;

        [Header("Builder")]
        [Tooltip("Construction-work seconds a builder applies per real second standing at a site.")]
        [Min(0f)] public float builderWorkPerSecond = 1f;

        [Header("Woodcutter")]
        [Tooltip("Seconds of Working one felling cycle takes.")]
        [Min(0.01f)] public float woodcutterWorkDurationSeconds = 5f;
        [Tooltip("Wood units produced by one completed felling cycle.")]
        [Min(0)] public int woodcutterYieldPerCycle = 2;

        [Header("Tender")]
        [Tooltip("A light whose fuel fraction (fuelSeconds / tenderTargetFuelSeconds) is below this gets refueled.")]
        [Range(0f, 1f)] public float tenderRefuelThresholdFraction = 0.35f;
        [Tooltip("Fuel seconds the tender considers a full light (LightSource has no capacity of its own).")]
        [Min(0.01f)] public float tenderTargetFuelSeconds = 120f;
        [Tooltip("Fuel seconds added to the light per refuel trip.")]
        [Min(0f)] public float tenderRefuelFuelSeconds = 60f;
        [Tooltip("Wood consumed from the ledger per refuel trip.")]
        [Min(0)] public int tenderWoodCostPerRefuel = 1;
        [Tooltip("Seconds the tender stands at the light feeding it.")]
        [Min(0.01f)] public float tenderRefuelWorkSeconds = 1.5f;

        [Header("Guard")]
        [Tooltip("Standing within this radius of the assigned post counts as on-post.")]
        [Min(0.1f)] public float guardPostRadius = 1.5f;

        [Header("Job walk-speed multipliers (applied to villagerWalkSpeed)")]
        [Min(0.1f)] public float salvagerSpeedMultiplier = 1f;
        [Min(0.1f)] public float builderSpeedMultiplier = 1f;
        [Min(0.1f)] public float woodcutterSpeedMultiplier = 1f;
        [Min(0.1f)] public float tenderSpeedMultiplier = 1.15f;
        [Min(0.1f)] public float guardSpeedMultiplier = 1.1f;
        [Min(0.1f)] public float productionSpeedMultiplier = 1f;

        /// <summary>Walk-speed multiplier for a job (1 for None/unknown).</summary>
        public float SpeedMultiplier(VillagerJob job)
        {
            switch (job)
            {
                case VillagerJob.Salvager: return salvagerSpeedMultiplier;
                case VillagerJob.Builder: return builderSpeedMultiplier;
                case VillagerJob.Woodcutter: return woodcutterSpeedMultiplier;
                case VillagerJob.Tender: return tenderSpeedMultiplier;
                case VillagerJob.Guard: return guardSpeedMultiplier;
                case VillagerJob.Farmer:
                case VillagerJob.Herder:
                case VillagerJob.Charcoaler:
                case VillagerJob.Smith:
                    return productionSpeedMultiplier;
                default: return 1f;
            }
        }

        /// <summary>Default roster count for a job (0 for None/unknown).</summary>
        public int DefaultCount(VillagerJob job)
        {
            switch (job)
            {
                case VillagerJob.Salvager: return defaultSalvagers;
                case VillagerJob.Builder: return defaultBuilders;
                case VillagerJob.Woodcutter: return defaultWoodcutters;
                case VillagerJob.Tender: return defaultTenders;
                case VillagerJob.Guard: return defaultGuards;
                case VillagerJob.Farmer: return defaultFarmers;
                case VillagerJob.Herder: return defaultHerders;
                case VillagerJob.Charcoaler: return defaultCharcoalers;
                case VillagerJob.Smith: return defaultSmiths;
                default: return 0;
            }
        }

        static JobsConfig _cached;

        /// <summary>
        /// Returns the config asset from Resources/JobsConfig if one exists,
        /// otherwise an in-memory instance with the coded defaults. Never returns null.
        /// </summary>
        public static JobsConfig LoadOrDefault()
        {
            if (_cached != null)
            {
                return _cached;
            }

            _cached = Resources.Load<JobsConfig>(ResourcePath);
            if (_cached == null)
            {
                _cached = CreateInstance<JobsConfig>();
                _cached.name = "JobsConfig (defaults)";
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
