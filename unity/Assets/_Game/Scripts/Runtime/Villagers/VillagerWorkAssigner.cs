using UnityEngine;

namespace Abbey.Villagers
{
    /// <summary>
    /// Play-mode glue for the bootstrapped scene: on Start it hands every
    /// registered <see cref="VillagerAgent"/> a day-work assignment, round-robin
    /// over <see cref="taskSites"/>, all depositing at <see cref="storagePoint"/>.
    /// The scene builder fills the site list from the greybox layout (beach salvage,
    /// camp build zones, forest edge) — positions are map layout, not balance, and
    /// live in the saved scene, never in code. Tests keep driving
    /// <see cref="VillagerAgent.AssignWork"/> directly and never need this.
    /// </summary>
    [DisallowMultipleComponent]
    public class VillagerWorkAssigner : MonoBehaviour
    {
        [Tooltip("Work sites villagers loop to, assigned round-robin.")]
        public Vector3[] taskSites = new Vector3[0];

        [Tooltip("Shared storage/deposit point for every assignment.")]
        public Vector3 storagePoint;

        void Start()
        {
            if (!Application.isPlaying || taskSites == null || taskSites.Length == 0)
            {
                return;
            }

            var villagers = DuskRecallSystem.Villagers;
            int assigned = 0;
            for (int i = 0; i < villagers.Count; i++)
            {
                var v = villagers[i];
                if (v == null || v.State != VillagerState.Idle)
                {
                    continue;
                }
                v.AssignWork(taskSites[assigned % taskSites.Length], storagePoint);
                assigned++;
            }
        }
    }
}
