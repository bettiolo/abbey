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
    }
}
