using UnityEngine;

namespace Abbey.Core
{
    /// <summary>
    /// Shared kinematic steering on the XZ ground plane. The greybox has no NavMesh:
    /// every agent moves in straight lines with an arrival radius, which keeps
    /// movement deterministic for manually ticked PlayMode tests.
    /// </summary>
    public static class PlanarMotion
    {
        /// <summary>
        /// Optional traffic sink (P3-12): set by <see cref="Abbey.Settlement.TrafficGrid"/>
        /// while it is enabled. <see cref="StepWorn"/> reports the segment it just
        /// travelled here so villager traffic wears desire paths, with no per-agent
        /// wiring. Null by default, so any walker that calls the plain <see cref="Step"/>
        /// (monsters, hound) never wears paths and existing tests are unchanged.
        /// </summary>
        public static System.Action<Vector3, Vector3> TrafficReporter;

        /// <summary>
        /// Optional path-speed provider (P3-12): set by
        /// <see cref="Abbey.Settlement.DesirePathSystem"/>. <see cref="StepWorn"/>
        /// multiplies its speed by the value returned for the current position, so a
        /// worn road grants a speed bonus. Null by default (multiplier 1).
        /// </summary>
        public static System.Func<Vector3, float> PathSpeedProvider;

        /// <summary>Clears the P3-12 movement hooks (test isolation).</summary>
        public static void ResetHooks()
        {
            TrafficReporter = null;
            PathSpeedProvider = null;
        }

        /// <summary>Distance between two points ignoring Y.</summary>
        public static float Distance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>Normalized XZ direction from one point to another (forward when coincident).</summary>
        public static Vector3 Direction(Vector3 from, Vector3 to)
        {
            var d = new Vector3(to.x - from.x, 0f, to.z - from.z);
            return d.sqrMagnitude > 0.000001f ? d.normalized : Vector3.forward;
        }

        /// <summary>
        /// Steps a position toward a target on the XZ plane, preserving Y.
        /// Sets <paramref name="arrived"/> when the result is inside the arrival radius.
        /// </summary>
        public static Vector3 Step(
            Vector3 position, Vector3 target, float speed, float dt,
            float arrivalRadius, out bool arrived)
        {
            float dist = Distance(position, target);
            if (dist <= arrivalRadius)
            {
                arrived = true;
                return position;
            }

            float travel = Mathf.Min(speed * Mathf.Max(dt, 0f), dist);
            Vector3 next = position + Direction(position, target) * travel;
            next.y = position.y;
            arrived = Distance(next, target) <= arrivalRadius;
            return next;
        }

        /// <summary>
        /// Path-wearing variant of <see cref="Step"/> (P3-12) for walkers that leave
        /// desire paths (villagers). It scales the speed by the
        /// <see cref="PathSpeedProvider"/> at the current position (a worn road is
        /// faster) and reports the travelled segment to the <see cref="TrafficReporter"/>
        /// so the ground remembers the traffic. With both hooks null it is identical to
        /// <see cref="Step"/>, so agents keep their behaviour until a grid exists.
        /// </summary>
        public static Vector3 StepWorn(
            Vector3 position, Vector3 target, float speed, float dt,
            float arrivalRadius, out bool arrived)
        {
            float mult = PathSpeedProvider != null
                ? Mathf.Max(0.01f, PathSpeedProvider(position))
                : 1f;
            Vector3 next = Step(position, target, speed * mult, dt, arrivalRadius, out arrived);
            if (TrafficReporter != null && next != position)
            {
                TrafficReporter(position, next);
            }
            return next;
        }
    }
}
