namespace Abbey.Villagers
{
    /// <summary>
    /// Phase 2 villager roles, per GAME_DESIGN.md §6 / VERTICAL_SLICE_SPEC §6.
    /// Injured is a <see cref="VillagerState"/>, not a job: injured villagers drop
    /// out of whatever job they hold and resume it on recovery. Builder is declared
    /// now but its work-site wiring arrives with construction (P2-03); until then a
    /// Builder idles gracefully.
    /// </summary>
    public enum VillagerJob
    {
        None,
        Salvager,
        Builder,
        Woodcutter,
        Tender,
        Guard,
        // Phase 3 renewable-economy production roles (P3-04). Each staffs a
        // matching ProductionBuilding (see VillagerJobs.ProductionBuildingId).
        Farmer,
        Herder,
        Charcoaler,
        Smith
    }

    /// <summary>Enum helpers shared by the job agent, assigner and the event log.</summary>
    public static class VillagerJobs
    {
        /// <summary>Snake_case id matching the design vocabulary (log-friendly).</summary>
        public static string Id(VillagerJob job)
        {
            switch (job)
            {
                case VillagerJob.None: return "none";
                case VillagerJob.Salvager: return "salvager";
                case VillagerJob.Builder: return "builder";
                case VillagerJob.Woodcutter: return "woodcutter";
                case VillagerJob.Tender: return "tender";
                case VillagerJob.Guard: return "guard";
                case VillagerJob.Farmer: return "farmer";
                case VillagerJob.Herder: return "herder";
                case VillagerJob.Charcoaler: return "charcoaler";
                case VillagerJob.Smith: return "smith";
                default: return job.ToString().ToLowerInvariant();
            }
        }

        /// <summary>True for the P3-04 production roles that staff a ProductionBuilding.</summary>
        public static bool IsProduction(VillagerJob job)
        {
            switch (job)
            {
                case VillagerJob.Farmer:
                case VillagerJob.Herder:
                case VillagerJob.Charcoaler:
                case VillagerJob.Smith:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Catalog id of the production building a role staffs (null for non-production
        /// jobs). Kept here — not on the economy layer — so the building/economy code
        /// never depends on the villager roles.
        /// </summary>
        public static string ProductionBuildingId(VillagerJob job)
        {
            switch (job)
            {
                case VillagerJob.Farmer: return "field_plot_t1";
                case VillagerJob.Herder: return "pasture_t1";
                case VillagerJob.Charcoaler: return "charcoal_kiln_t1";
                case VillagerJob.Smith: return "smithy_t1";
                default: return null;
            }
        }
    }
}
