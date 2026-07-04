using System.Collections.Generic;
using Abbey.Core;
using UnityEngine;

namespace Abbey.Light
{
    /// <summary>Territory classification of a world position.</summary>
    public enum LightZone
    {
        Safe,
        Edge,
        Dark
    }

    /// <summary>
    /// Static registry of all enabled <see cref="LightSource"/> components and the
    /// single authority for "light is territory" queries. Distances are planar (XZ)
    /// because the game plays on the ground plane under a locked isometric camera.
    ///
    /// Safe  = inside some lit source's inner radius: effectiveRadius * (1 - edgeBandFraction)
    /// Edge  = inside some lit source's effective radius but not Safe anywhere
    /// Dark  = outside every lit source.
    /// </summary>
    public static class DarknessEvaluator
    {
        static readonly List<LightSource> _sources = new List<LightSource>();
        static PrototypeConfig _config;

        /// <summary>Config override for tests; falls back to PrototypeConfig.LoadOrDefault().</summary>
        public static PrototypeConfig Config
        {
            get
            {
                if (_config == null)
                {
                    _config = PrototypeConfig.LoadOrDefault();
                }
                return _config;
            }
            set { _config = value; }
        }

        public static float EdgeBandFraction => Mathf.Clamp(Config.edgeBandFraction, 0.01f, 0.99f);

        public static IReadOnlyList<LightSource> Sources => _sources;

        public static void Register(LightSource source)
        {
            if (source != null && !_sources.Contains(source))
            {
                _sources.Add(source);
            }
        }

        public static void Unregister(LightSource source)
        {
            _sources.Remove(source);
        }

        /// <summary>Drops all registrations and the config override (test isolation).</summary>
        public static void Clear()
        {
            _sources.Clear();
            _config = null;
        }

        public static LightZone Classify(Vector3 worldPos)
        {
            bool inEdge = false;
            float innerFactor = 1f - EdgeBandFraction;

            for (int i = 0; i < _sources.Count; i++)
            {
                var source = _sources[i];
                if (source == null || !source.isLit)
                {
                    continue;
                }

                float effective = source.EffectiveRadius;
                if (effective <= 0f)
                {
                    continue;
                }

                float dist = PlanarDistance(worldPos, source.transform.position);
                if (dist <= effective * innerFactor)
                {
                    return LightZone.Safe;
                }
                if (dist <= effective)
                {
                    inEdge = true;
                }
            }

            return inEdge ? LightZone.Edge : LightZone.Dark;
        }

        /// <summary>
        /// Light intensity 0..1 at a position: the strongest single source contribution,
        /// falling off linearly from source strength at the center to 0 at the effective
        /// radius. Monsters compare this with their light tolerance.
        /// </summary>
        public static float LightIntensityAt(Vector3 worldPos)
        {
            float best = 0f;
            for (int i = 0; i < _sources.Count; i++)
            {
                var source = _sources[i];
                if (source == null || !source.isLit)
                {
                    continue;
                }

                float effective = source.EffectiveRadius;
                if (effective <= 0f)
                {
                    continue;
                }

                float dist = PlanarDistance(worldPos, source.transform.position);
                if (dist >= effective)
                {
                    continue;
                }

                float contribution = Mathf.Clamp01(source.strength) * (1f - dist / effective);
                if (contribution > best)
                {
                    best = contribution;
                }
            }
            return best;
        }

        /// <summary>The lit source contributing the most light at the position, or null in the dark.</summary>
        public static LightSource StrongestLightAt(Vector3 worldPos)
        {
            LightSource bestSource = null;
            float best = 0f;
            for (int i = 0; i < _sources.Count; i++)
            {
                var source = _sources[i];
                if (source == null || !source.isLit)
                {
                    continue;
                }

                float effective = source.EffectiveRadius;
                if (effective <= 0f)
                {
                    continue;
                }

                float dist = PlanarDistance(worldPos, source.transform.position);
                if (dist >= effective)
                {
                    continue;
                }

                float contribution = Mathf.Clamp01(source.strength) * (1f - dist / effective);
                if (bestSource == null || contribution > best)
                {
                    best = contribution;
                    bestSource = source;
                }
            }
            return bestSource;
        }

        /// <summary>
        /// The closest point classified Safe. Returns the input unchanged when it is
        /// already Safe, or when no lit sources exist (nowhere is safe — caller decides).
        /// </summary>
        public static Vector3 NearestSafePoint(Vector3 worldPos)
        {
            if (Classify(worldPos) == LightZone.Safe)
            {
                return worldPos;
            }

            float innerFactor = 1f - EdgeBandFraction;
            bool found = false;
            Vector3 bestPoint = worldPos;
            float bestDist = float.MaxValue;

            for (int i = 0; i < _sources.Count; i++)
            {
                var source = _sources[i];
                if (source == null || !source.isLit)
                {
                    continue;
                }

                float safeRadius = source.EffectiveRadius * innerFactor;
                if (safeRadius <= 0f)
                {
                    continue;
                }

                Vector3 center = source.transform.position;
                Vector3 toPos = worldPos - center;
                toPos.y = 0f;

                // Pad by movement arrival tolerance so agents that stop near the
                // target still classify as Safe instead of stopping in Edge.
                float inwardPadding = Mathf.Max(0.05f, Config.arrivalRadius + 0.05f);
                float targetRadius = Mathf.Max(0f, safeRadius - inwardPadding);
                Vector3 dir = toPos.sqrMagnitude > 0.0001f ? toPos.normalized : Vector3.forward;
                Vector3 candidate = new Vector3(center.x, worldPos.y, center.z) + dir * Mathf.Min(toPos.magnitude, targetRadius);

                float dist = PlanarDistance(worldPos, candidate);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestPoint = candidate;
                    found = true;
                }
            }

            return found ? bestPoint : worldPos;
        }

        static float PlanarDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }
    }
}
