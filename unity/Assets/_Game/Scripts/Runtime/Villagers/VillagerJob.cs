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
        Guard
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
                default: return job.ToString().ToLowerInvariant();
            }
        }
    }
}
