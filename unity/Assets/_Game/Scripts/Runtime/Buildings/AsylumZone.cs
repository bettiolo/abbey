using System.Collections.Generic;
using Abbey.Core;
using Abbey.Villagers;
using UnityEngine;

namespace Abbey.Buildings
{
    /// <summary>
    /// The Asylum Corner's care zone. It keeps the trigger radius and the passive
    /// occupancy list (villagers currently standing inside, through the
    /// <see cref="DuskRecallSystem"/> registry), and — the P3-03 behaviour that
    /// replaced Phase 2's instant heal — it owns the <em>admission roster and
    /// cooldown</em>: an insane villager admitted by day is parked here, held for
    /// <see cref="AdmitDayOf"/>..cooldown days (so it misses the coming night), and
    /// released only once <see cref="CooldownElapsed"/>. The sanity-recovery rate
    /// itself is applied by <see cref="Abbey.Sanity.SanitySystem"/> (it owns the
    /// sanity value and the <c>SanityConfig</c> balance) — this component holds no
    /// balance value beyond the radius, which comes from
    /// <see cref="Abbey.Core.PrototypeConfig.asylumRadius"/> via
    /// <see cref="Building.Construct"/>.
    ///
    /// [ExecuteAlways] with a manual <see cref="Tick"/> for EditMode tests, mirroring
    /// the other simulation components.
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
        readonly Dictionary<VillagerAgent, int> _admitDay = new Dictionary<VillagerAgent, int>();
        readonly List<VillagerAgent> _roster = new List<VillagerAgent>();

        /// <summary>Villagers currently inside the zone (refreshed each Tick).</summary>
        public IReadOnlyList<VillagerAgent> Occupants => _occupants;

        /// <summary>Insane villagers currently held for care (admission order).</summary>
        public IReadOnlyList<VillagerAgent> Roster => _roster;

        /// <summary>Number of villagers currently admitted.</summary>
        public int AdmittedCount => _roster.Count;

        void OnDisable()
        {
            _occupants.Clear();
            _admitDay.Clear();
            _roster.Clear();
        }

        /// <summary>Whether this villager is currently admitted.</summary>
        public bool IsAdmitted(VillagerAgent villager)
        {
            return villager != null && _admitDay.ContainsKey(villager);
        }

        /// <summary>Day number the villager was admitted, or -1 if not admitted.</summary>
        public int AdmitDayOf(VillagerAgent villager)
        {
            return villager != null && _admitDay.TryGetValue(villager, out int day) ? day : -1;
        }

        /// <summary>
        /// Admits an insane villager for care on <paramref name="currentDay"/> and
        /// parks it at the zone centre (Safe light), so it misses the coming night.
        /// Idempotent — re-admitting keeps the original admit day. SanitySystem is
        /// the sole caller and raises the AsylumAdmitted event.
        /// </summary>
        public void Admit(VillagerAgent villager, int currentDay)
        {
            if (villager == null || _admitDay.ContainsKey(villager))
            {
                return;
            }
            _admitDay[villager] = currentDay;
            _roster.Add(villager);
            ParkOccupant(villager);
        }

        /// <summary>Keeps an admitted villager parked at the zone centre (called each Tick).</summary>
        public void ParkOccupant(VillagerAgent villager)
        {
            if (villager != null)
            {
                villager.transform.position = transform.position;
            }
        }

        /// <summary>
        /// True once the villager has been held for at least
        /// <paramref name="cooldownDays"/> days (a value of 1 means the villager is
        /// released the day after admission, having missed exactly one night).
        /// </summary>
        public bool CooldownElapsed(VillagerAgent villager, int currentDay, int cooldownDays)
        {
            return _admitDay.TryGetValue(villager, out int day)
                   && (currentDay - day) >= Mathf.Max(1, cooldownDays);
        }

        /// <summary>Discharges the villager from care.</summary>
        public void Release(VillagerAgent villager)
        {
            if (villager != null && _admitDay.Remove(villager))
            {
                _roster.Remove(villager);
            }
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
