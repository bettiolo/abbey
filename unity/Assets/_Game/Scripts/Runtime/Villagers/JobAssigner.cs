using System.Collections.Generic;
using Abbey.Core;
using UnityEngine;

namespace Abbey.Villagers
{
    /// <summary>
    /// Runtime job assignment. The static API (<see cref="Assign"/> /
    /// <see cref="Unassign"/> / <see cref="ApplyDefaultRoster"/>) is what tests
    /// and the later job UI call; the MonoBehaviour is play-mode scene glue that
    /// applies the data-driven default roster from <see cref="JobsConfig"/> to
    /// every registered villager on Start (mirroring
    /// <see cref="VillagerWorkAssigner"/>). Roster quotas fill in a fixed job
    /// order over villager registration order, so the result is deterministic;
    /// leftover villagers stay jobless. Every assignment lands in the event log
    /// (via <see cref="VillagerJobAgent.SetJob"/>).
    /// </summary>
    [DisallowMultipleComponent]
    public class JobAssigner : MonoBehaviour
    {
        /// <summary>Quota fill order for the default roster.</summary>
        static readonly VillagerJob[] RosterOrder =
        {
            VillagerJob.Salvager,
            VillagerJob.Builder,
            VillagerJob.Woodcutter,
            VillagerJob.Tender,
            VillagerJob.Guard
        };

        void Start()
        {
            if (!Application.isPlaying)
            {
                return;
            }
            ApplyDefaultRoster(DuskRecallSystem.Villagers, JobsConfig.LoadOrDefault());
        }

        /// <summary>
        /// Gives the villager a job, adding the <see cref="VillagerJobAgent"/> if
        /// needed. Returns the job agent (null for a null/dead villager).
        /// </summary>
        public static VillagerJobAgent Assign(VillagerAgent villager, VillagerJob job)
        {
            if (villager == null || villager.State == VillagerState.Dead)
            {
                return null;
            }
            var agent = villager.GetComponent<VillagerJobAgent>();
            if (agent == null)
            {
                agent = villager.gameObject.AddComponent<VillagerJobAgent>();
                agent.autoTick = villager.autoTick;
            }
            agent.SetJob(job);
            return agent;
        }

        /// <summary>Removes the villager's job (back to the plain day loop).</summary>
        public static VillagerJobAgent Unassign(VillagerAgent villager)
        {
            return Assign(villager, VillagerJob.None);
        }

        /// <summary>
        /// Applies the config roster over the villagers in order: the first
        /// defaultSalvagers become Salvagers, then Builders, Woodcutters, Tenders,
        /// Guards; the rest are left as they are. Missing/Dead villagers are
        /// skipped. Returns how many villagers received a job.
        /// </summary>
        public static int ApplyDefaultRoster(
            IReadOnlyList<VillagerAgent> villagers, JobsConfig config = null)
        {
            if (villagers == null)
            {
                return 0;
            }
            var cfg = config != null ? config : JobsConfig.LoadOrDefault();

            int jobIndex = 0;
            int filledInJob = 0;
            int assigned = 0;
            for (int i = 0; i < villagers.Count; i++)
            {
                var villager = villagers[i];
                if (villager == null
                    || villager.State == VillagerState.Missing
                    || villager.State == VillagerState.Dead)
                {
                    continue;
                }

                while (jobIndex < RosterOrder.Length
                       && filledInJob >= cfg.DefaultCount(RosterOrder[jobIndex]))
                {
                    jobIndex++;
                    filledInJob = 0;
                }
                if (jobIndex >= RosterOrder.Length)
                {
                    break; // quotas exhausted; the rest stay jobless
                }

                if (Assign(villager, RosterOrder[jobIndex]) != null)
                {
                    filledInJob++;
                    assigned++;
                }
            }

            GameEventLog.Append("job", $"roster applied ({assigned} villagers)");
            return assigned;
        }
    }
}
