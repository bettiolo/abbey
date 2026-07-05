using System.Collections.Generic;
using Abbey.Core;
using Abbey.Villagers;
using UnityEngine;

namespace Abbey.Buildings
{
    /// <summary>
    /// The Asylum Corner's care zone (P3-02 rename of the legacy sick-corner
    /// building; Phase 2's instant heal-on-exposure behaviour is removed here).
    /// This is the documented shell P3-03 (sanity, dread and the asylum) fills in:
    /// it keeps the trigger radius and tracks which villagers currently occupy the
    /// zone (through the <see cref="DuskRecallSystem"/> villager registry) so the
    /// coming sanity-recovery / miss-a-night cooldown logic has an occupancy list to
    /// act on. It applies NO state changes to villagers — recovery, cooldown and the
    /// "insane settler released only by day" rules land in P3-03's SanitySystem.
    ///
    /// The radius comes from <see cref="Abbey.Core.PrototypeConfig.asylumRadius"/>
    /// via <see cref="Building.Construct"/> — no balance lives here. [ExecuteAlways]
    /// with a manual <see cref="Tick"/> for EditMode tests, mirroring the other
    /// simulation components.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class AsylumZone : MonoBehaviour
    {
        [Tooltip("Villagers within this radius occupy the asylum (set from PrototypeConfig.asylumRadius).")]
        [Min(0f)] public float radius = 4f;

        [Tooltip("Advance from Update using Time.deltaTime. Tests set false and call Tick().")]
        public bool autoTick = true;

        readonly List<VillagerAgent> _occupants = new List<VillagerAgent>();

        /// <summary>Villagers currently inside the zone (refreshed each Tick). P3-03 recovers these.</summary>
        public IReadOnlyList<VillagerAgent> Occupants => _occupants;

        void OnDisable()
        {
            _occupants.Clear();
        }

        void Update()
        {
            if (!Application.isPlaying || !autoTick)
            {
                return;
            }
            Tick(Time.deltaTime);
        }

        /// <summary>
        /// Deterministic simulation step (autoTick = false in tests). Recomputes the
        /// occupancy list from the villager registry; it deliberately changes no
        /// villager state — that is P3-03's job.
        /// </summary>
        public void Tick(float dt)
        {
            RefreshOccupancy();
        }

        /// <summary>Rebuilds <see cref="Occupants"/> from villagers inside the radius.</summary>
        public void RefreshOccupancy()
        {
            _occupants.Clear();
            var villagers = DuskRecallSystem.Villagers;
            for (int i = 0; i < villagers.Count; i++)
            {
                var villager = villagers[i];
                if (villager == null)
                {
                    continue;
                }
                if (PlanarMotion.Distance(
                        villager.transform.position, transform.position) <= radius)
                {
                    _occupants.Add(villager);
                }
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.6f, 0.5f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
