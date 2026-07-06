using Abbey.Buildings;
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
        const float Epsilon = 0.0001f;
        const float CornerEpsilon = 0.05f;

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

        /// <summary>
        /// Path-wearing variant of <see cref="StepAroundBuildings"/> that composes
        /// main's building-footprint routing with P3-12's desire-path wear: it scales
        /// the speed by the <see cref="PathSpeedProvider"/> at the current position (a
        /// worn road is faster), routes around active building/construction footprints,
        /// then reports the segment actually travelled to the
        /// <see cref="TrafficReporter"/> so worn paths reflect the real routed motion
        /// (not the straight line through a building). With both hooks null it behaves
        /// exactly like <see cref="StepAroundBuildings"/>.
        /// </summary>
        public static Vector3 StepWornAroundBuildings(
            Vector3 position, Vector3 target, float speed, float dt,
            float arrivalRadius, float obstaclePadding, out bool arrived)
        {
            float mult = PathSpeedProvider != null
                ? Mathf.Max(0.01f, PathSpeedProvider(position))
                : 1f;
            Vector3 next = StepAroundBuildings(
                position, target, speed * mult, dt, arrivalRadius, obstaclePadding, out arrived);
            if (TrafficReporter != null && next != position)
            {
                TrafficReporter(position, next);
            }
            return next;
        }

        /// <summary>
        /// Steps toward a target while treating active building and construction
        /// footprints as occupied ground. When a straight step would cross an
        /// occupied rect, it steers toward the cheaper outside corner instead of
        /// walking through the structure. Deterministic and still single-step; no
        /// NavMesh dependency.
        /// </summary>
        public static Vector3 StepAroundBuildings(
            Vector3 position, Vector3 target, float speed, float dt,
            float arrivalRadius, float obstaclePadding, out bool arrived)
        {
            Vector3 moveTarget = target;
            if (TryGetContainingFootprint(target, obstaclePadding, out Rect targetFootprint))
            {
                moveTarget = NearestOutsidePoint(targetFootprint, position, target.y);
            }

            Vector3 straight = Step(position, moveTarget, speed, dt, arrivalRadius, out arrived);
            if (arrived)
            {
                return straight;
            }

            Vector3 next = AvoidBuildingFootprints(position, straight, moveTarget, obstaclePadding);
            arrived = Distance(next, moveTarget) <= arrivalRadius;
            return next;
        }

        /// <summary>
        /// Applies a direct movement delta while sliding/detouring around active
        /// building and construction footprints. Used for direct input and flee
        /// vectors that do not have a longer-lived target.
        /// </summary>
        public static Vector3 MoveAroundBuildings(
            Vector3 position, Vector3 delta, float obstaclePadding)
        {
            delta.y = 0f;
            if (delta.sqrMagnitude <= Epsilon)
            {
                return position;
            }

            Vector3 target = position + delta;
            target.y = position.y;
            return AvoidBuildingFootprints(position, target, target, obstaclePadding);
        }

        /// <summary>True when a world point is inside any expanded active footprint.</summary>
        public static bool IsInsideBuildingFootprint(Vector3 position, float obstaclePadding)
        {
            return TryGetContainingFootprint(position, obstaclePadding, out _);
        }

        static Vector3 AvoidBuildingFootprints(
            Vector3 position, Vector3 candidate, Vector3 intendedTarget, float obstaclePadding)
        {
            if (!TryGetBlockingFootprint(position, candidate, obstaclePadding, out Rect blocker))
            {
                return candidate;
            }

            float travel = Distance(position, candidate);
            if (travel <= Epsilon)
            {
                return position;
            }

            if (TryCornerDetour(position, intendedTarget, travel, obstaclePadding,
                    blocker, out Vector3 detour))
            {
                return detour;
            }

            Vector3 slide = TryAxisSlide(position, candidate, obstaclePadding, blocker);
            return TryGetBlockingFootprint(position, slide, obstaclePadding, out _)
                ? position
                : slide;
        }

        static bool TryCornerDetour(
            Vector3 position, Vector3 intendedTarget, float travel, float obstaclePadding,
            Rect blocker, out Vector3 detour)
        {
            detour = position;
            float bestScore = float.MaxValue;
            bool found = false;

            float gap = Mathf.Max(CornerEpsilon, obstaclePadding * 0.25f);
            Vector3[] corners =
            {
                new Vector3(blocker.xMin - gap, position.y, blocker.yMin - gap),
                new Vector3(blocker.xMin - gap, position.y, blocker.yMax + gap),
                new Vector3(blocker.xMax + gap, position.y, blocker.yMin - gap),
                new Vector3(blocker.xMax + gap, position.y, blocker.yMax + gap),
            };

            for (int i = 0; i < corners.Length; i++)
            {
                Vector3 waypoint = corners[i];
                Vector3 candidate = position + Direction(position, waypoint) * travel;
                candidate.y = position.y;
                if (TryGetBlockingFootprint(position, candidate, obstaclePadding, out _))
                {
                    continue;
                }

                float score = Distance(position, waypoint) + Distance(waypoint, intendedTarget);
                if (score < bestScore)
                {
                    bestScore = score;
                    detour = candidate;
                    found = true;
                }
            }

            return found;
        }

        static Vector3 TryAxisSlide(
            Vector3 position, Vector3 candidate, float obstaclePadding, Rect blocker)
        {
            Vector3 delta = candidate - position;
            Vector3 slide = candidate;
            if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.z))
            {
                slide.x = position.x < blocker.center.x
                    ? blocker.xMin - CornerEpsilon
                    : blocker.xMax + CornerEpsilon;
            }
            else
            {
                slide.z = position.z < blocker.center.y
                    ? blocker.yMin - CornerEpsilon
                    : blocker.yMax + CornerEpsilon;
            }
            slide.y = position.y;
            return slide;
        }

        static Vector3 NearestOutsidePoint(Rect rect, Vector3 from, float y)
        {
            float gap = CornerEpsilon;
            bool left = from.x < rect.xMin;
            bool right = from.x > rect.xMax;
            bool below = from.z < rect.yMin;
            bool above = from.z > rect.yMax;

            float x = Mathf.Clamp(from.x, rect.xMin, rect.xMax);
            float z = Mathf.Clamp(from.z, rect.yMin, rect.yMax);

            if (left)
            {
                x = rect.xMin - gap;
            }
            else if (right)
            {
                x = rect.xMax + gap;
            }

            if (below)
            {
                z = rect.yMin - gap;
            }
            else if (above)
            {
                z = rect.yMax + gap;
            }

            if (!left && !right && !below && !above)
            {
                float toLeft = Mathf.Abs(from.x - rect.xMin);
                float toRight = Mathf.Abs(rect.xMax - from.x);
                float toBottom = Mathf.Abs(from.z - rect.yMin);
                float toTop = Mathf.Abs(rect.yMax - from.z);
                float best = Mathf.Min(Mathf.Min(toLeft, toRight), Mathf.Min(toBottom, toTop));

                if (Mathf.Approximately(best, toLeft))
                {
                    x = rect.xMin - gap;
                }
                else if (Mathf.Approximately(best, toRight))
                {
                    x = rect.xMax + gap;
                }
                else if (Mathf.Approximately(best, toBottom))
                {
                    z = rect.yMin - gap;
                }
                else
                {
                    z = rect.yMax + gap;
                }
            }

            return new Vector3(x, y, z);
        }

        static bool TryGetBlockingFootprint(
            Vector3 from, Vector3 to, float obstaclePadding, out Rect footprint)
        {
            float bestDistance = float.MaxValue;
            footprint = default;
            bool found = false;
            float padding = Mathf.Max(0f, obstaclePadding);

            var buildings = Building.Active;
            for (int i = 0; i < buildings.Count; i++)
            {
                var building = buildings[i];
                if (building == null)
                {
                    continue;
                }
                Rect rect = Expanded(building.Footprint, padding);
                if (!BlocksMove(rect, from, to))
                {
                    continue;
                }
                float dist = DistanceToRectCenter(from, rect);
                if (dist < bestDistance)
                {
                    bestDistance = dist;
                    footprint = rect;
                    found = true;
                }
            }

            var sites = ConstructionSite.Active;
            for (int i = 0; i < sites.Count; i++)
            {
                var site = sites[i];
                if (site == null || site.IsComplete)
                {
                    continue;
                }
                Rect rect = Expanded(site.Footprint, padding);
                if (!BlocksMove(rect, from, to))
                {
                    continue;
                }
                float dist = DistanceToRectCenter(from, rect);
                if (dist < bestDistance)
                {
                    bestDistance = dist;
                    footprint = rect;
                    found = true;
                }
            }

            return found;
        }

        static bool TryGetContainingFootprint(
            Vector3 position, float obstaclePadding, out Rect footprint)
        {
            footprint = default;
            float padding = Mathf.Max(0f, obstaclePadding);

            var buildings = Building.Active;
            for (int i = 0; i < buildings.Count; i++)
            {
                var building = buildings[i];
                if (building == null)
                {
                    continue;
                }
                Rect rect = Expanded(building.Footprint, padding);
                if (ContainsXZ(rect, position))
                {
                    footprint = rect;
                    return true;
                }
            }

            var sites = ConstructionSite.Active;
            for (int i = 0; i < sites.Count; i++)
            {
                var site = sites[i];
                if (site == null || site.IsComplete)
                {
                    continue;
                }
                Rect rect = Expanded(site.Footprint, padding);
                if (ContainsXZ(rect, position))
                {
                    footprint = rect;
                    return true;
                }
            }

            return false;
        }

        static Rect Expanded(Rect rect, float padding)
        {
            return new Rect(
                rect.xMin - padding,
                rect.yMin - padding,
                rect.width + padding * 2f,
                rect.height + padding * 2f);
        }

        static bool BlocksMove(Rect rect, Vector3 from, Vector3 to)
        {
            if (rect.width <= 0f || rect.height <= 0f)
            {
                return false;
            }
            if (ContainsXZ(rect, to) || ContainsXZ(rect, from))
            {
                return true;
            }
            return SegmentIntersectsRect(
                new Vector2(from.x, from.z), new Vector2(to.x, to.z), rect);
        }

        static bool ContainsXZ(Rect rect, Vector3 point)
        {
            return point.x > rect.xMin && point.x < rect.xMax
                   && point.z > rect.yMin && point.z < rect.yMax;
        }

        static bool SegmentIntersectsRect(Vector2 a, Vector2 b, Rect rect)
        {
            float tMin = 0f;
            float tMax = 1f;
            Vector2 delta = b - a;
            return Clip(delta.x, rect.xMin - a.x, ref tMin, ref tMax)
                   && Clip(-delta.x, a.x - rect.xMax, ref tMin, ref tMax)
                   && Clip(delta.y, rect.yMin - a.y, ref tMin, ref tMax)
                   && Clip(-delta.y, a.y - rect.yMax, ref tMin, ref tMax);
        }

        static bool Clip(float denom, float numer, ref float tMin, ref float tMax)
        {
            if (Mathf.Abs(denom) <= Epsilon)
            {
                return numer <= 0f;
            }

            float t = numer / denom;
            if (denom > 0f)
            {
                if (t > tMax)
                {
                    return false;
                }
                if (t > tMin)
                {
                    tMin = t;
                }
            }
            else
            {
                if (t < tMin)
                {
                    return false;
                }
                if (t < tMax)
                {
                    tMax = t;
                }
            }
            return true;
        }

        static float DistanceToRectCenter(Vector3 position, Rect rect)
        {
            float dx = position.x - rect.center.x;
            float dz = position.z - rect.center.y;
            return dx * dx + dz * dz;
        }
    }
}
