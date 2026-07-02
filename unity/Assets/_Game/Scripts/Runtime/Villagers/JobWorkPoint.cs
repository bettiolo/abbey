using System.Collections.Generic;
using Abbey.Core;
using UnityEngine;

namespace Abbey.Villagers
{
    /// <summary>
    /// A designated job work point in the world: a woodcutter tree/stand or a
    /// guard post. Static registry mirroring <see cref="Abbey.Economy.SalvageSite"/>
    /// — points register while enabled, job agents query the nearest point for
    /// their job. Positions are map layout, not balance, so a point is just a
    /// transform plus a job tag. [ExecuteAlways] so EditMode tests get
    /// OnEnable/OnDisable registration.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class JobWorkPoint : MonoBehaviour
    {
        [Tooltip("Which job works this point (Woodcutter tree, Guard post…).")]
        public VillagerJob job = VillagerJob.Woodcutter;

        static readonly List<JobWorkPoint> _active = new List<JobWorkPoint>();

        /// <summary>Every enabled work point (job assignment, debug tools).</summary>
        public static IReadOnlyList<JobWorkPoint> Active => _active;

        /// <summary>Test isolation.</summary>
        public static void ClearRegistry()
        {
            _active.Clear();
        }

        /// <summary>Nearest enabled point tagged for the job, or null if none exist.</summary>
        public static JobWorkPoint Nearest(VillagerJob job, Vector3 from)
        {
            JobWorkPoint best = null;
            float bestDist = float.MaxValue;
            for (int i = 0; i < _active.Count; i++)
            {
                var point = _active[i];
                if (point == null || point.job != job)
                {
                    continue;
                }
                float dist = PlanarMotion.Distance(from, point.transform.position);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = point;
                }
            }
            return best;
        }

        void OnEnable()
        {
            if (!_active.Contains(this))
            {
                _active.Add(this);
            }
        }

        void OnDisable()
        {
            _active.Remove(this);
        }
    }
}
