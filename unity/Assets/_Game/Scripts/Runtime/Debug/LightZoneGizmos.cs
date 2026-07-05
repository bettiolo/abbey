using Abbey.Light;
using UnityEngine;

namespace Abbey.Debugging
{
    /// <summary>
    /// Draws the light-territory rings for EVERY registered <see cref="LightSource"/>
    /// (LightSource itself only draws when selected): yellow = effective radius
    /// (Edge boundary), orange = inner Safe radius, gray = unlit. Circles are flat
    /// on the ground plane because territory is planar (XZ). Editor-view aid only;
    /// gizmos never render in builds.
    /// </summary>
    [DisallowMultipleComponent]
    public class LightZoneGizmos : MonoBehaviour
    {
        const int Segments = 48;

        [Tooltip("Height above the ground plane at which the rings are drawn.")]
        public float ringHeight = 0.05f;

        void OnDrawGizmos()
        {
            var sources = DarknessEvaluator.Sources;
            float innerFactor = 1f - DarknessEvaluator.EdgeBandFraction;

            for (int i = 0; i < sources.Count; i++)
            {
                var source = sources[i];
                if (source == null)
                {
                    continue;
                }

                Vector3 center = source.transform.position;
                center.y = ringHeight;

                if (!source.isLit)
                {
                    Gizmos.color = Color.gray;
                    DrawCircle(center, source.radius * Mathf.Clamp01(source.strength));
                    continue;
                }

                // The global weather multiplier scales the classified territory, so
                // the rings shrink under fog/rain/tempest and swell on a full moon.
                float effective = DarknessEvaluator.EffectiveRadiusOf(source);
                if (effective <= 0f)
                {
                    continue;
                }

                Gizmos.color = Color.yellow;
                DrawCircle(center, effective);
                Gizmos.color = new Color(1f, 0.6f, 0f);
                DrawCircle(center, effective * innerFactor);
            }
        }

        static void DrawCircle(Vector3 center, float radius)
        {
            if (radius <= 0f)
            {
                return;
            }
            Vector3 prev = center + new Vector3(radius, 0f, 0f);
            for (int i = 1; i <= Segments; i++)
            {
                float angle = i / (float)Segments * Mathf.PI * 2f;
                Vector3 next = center + new Vector3(
                    Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }
    }
}
