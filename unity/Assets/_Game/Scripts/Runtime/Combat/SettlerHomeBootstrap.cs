using System.Collections.Generic;
using Abbey.Buildings;
using Abbey.Core;
using Abbey.Sanity;
using Abbey.Villagers;
using UnityEngine;

namespace Abbey.Combat
{
    /// <summary>
    /// Scene glue (not balance) that populates the P3-03 shelter map so homes have
    /// occupants for the P3-05 two-tier night defense. On Start it assigns each
    /// destructible-home <see cref="Building"/> the villagers nearest to it (up to
    /// <see cref="occupantsPerHome"/>) through <see cref="SanitySystem.AssignHome"/> —
    /// the same map the home-recovery dread spill already reads. Future tasks (trust /
    /// arrivals housing) will drive assignment properly; this keeps the prototype
    /// scene's homes defensible until then. Runs once; a villager is assigned to at
    /// most one home. Mirrors <see cref="Abbey.Economy.SettlementEconomyBootstrap"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public class SettlerHomeBootstrap : MonoBehaviour
    {
        [Tooltip("Settlers to house in each destructible home (nearest first).")]
        [Min(1)] public int occupantsPerHome = 2;

        bool _done;

        void Start()
        {
            AssignHomes();
        }

        /// <summary>Assigns nearest villagers to each destructible home (idempotent). Public for tests.</summary>
        public void AssignHomes()
        {
            if (_done)
            {
                return;
            }
            var sanity = SanitySystem.Instance;
            if (sanity == null)
            {
                return; // no sanity system in this scene: nothing to populate
            }

            var assigned = new HashSet<VillagerAgent>();
            var buildings = Building.Active;
            for (int i = 0; i < buildings.Count; i++)
            {
                var home = buildings[i];
                if (home == null || !home.IsDestructibleHome)
                {
                    continue;
                }
                AssignNearest(sanity, home, assigned);
            }
            _done = true;
        }

        void AssignNearest(SanitySystem sanity, Building home, HashSet<VillagerAgent> assigned)
        {
            var villagers = DuskRecallSystem.Villagers;
            for (int slot = 0; slot < occupantsPerHome; slot++)
            {
                VillagerAgent best = null;
                float bestDist = float.MaxValue;
                for (int i = 0; i < villagers.Count; i++)
                {
                    var v = villagers[i];
                    if (v == null || assigned.Contains(v))
                    {
                        continue;
                    }
                    float dist = PlanarMotion.Distance(home.transform.position, v.transform.position);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        best = v;
                    }
                }
                if (best == null)
                {
                    return;
                }
                sanity.AssignHome(best, home);
                assigned.Add(best);
            }
        }
    }
}
