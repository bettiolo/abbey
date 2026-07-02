using Abbey.Core;
using UnityEngine;

namespace Abbey.Light
{
    /// <summary>
    /// A gameplay light: campfire, lantern post, carried flame, sacred abbey fire.
    /// Registers with <see cref="DarknessEvaluator"/> while enabled. Fuel burns down
    /// over time; when it runs out the light dies (darkness is territory).
    /// [ExecuteAlways] so EditMode tests get OnEnable/OnDisable registration.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class LightSource : MonoBehaviour
    {
        [Tooltip("Full territory radius in world units at strength 1.")]
        [Min(0f)] public float radius = 5f;

        [Tooltip("0..1. Effective radius = radius * strength.")]
        [Range(0f, 1f)] public float strength = 1f;

        public bool isLit = true;

        [Tooltip("Sacred lights (abbey flame, bell tower) matter for win/loss and the hound.")]
        public bool sacred;

        [Tooltip("Seconds of fuel remaining. Negative = infinite (never burns out).")]
        public float fuelSeconds = -1f;

        [Tooltip("Fuel drained per simulated second while lit.")]
        [Min(0f)] public float fuelConsumptionPerSecond = 1f;

        [Tooltip("Advance fuel from Update using Time.deltaTime. Tests set false and call Tick().")]
        public bool autoTick = true;

        /// <summary>Territory radius after strength scaling; 0 when unlit.</summary>
        public float EffectiveRadius => isLit ? radius * Mathf.Clamp01(strength) : 0f;

        public bool HasInfiniteFuel => fuelSeconds < 0f;

        void OnEnable()
        {
            DarknessEvaluator.Register(this);
        }

        void OnDisable()
        {
            DarknessEvaluator.Unregister(this);
        }

        void Update()
        {
            if (!Application.isPlaying || !autoTick)
            {
                return;
            }
            Tick(Time.deltaTime);
        }

        /// <summary>Burns fuel for dt seconds; extinguishes the light when fuel hits zero.</summary>
        public void Tick(float dt)
        {
            if (!isLit || HasInfiniteFuel || dt <= 0f)
            {
                return;
            }

            fuelSeconds -= fuelConsumptionPerSecond * dt;
            if (fuelSeconds <= 0f)
            {
                fuelSeconds = 0f;
                Extinguish();
            }
        }

        public void Extinguish()
        {
            if (!isLit)
            {
                return;
            }
            isLit = false;
            GameEventLog.Append("LightExtinguished", $"{name} sacred={sacred}");
        }

        /// <summary>Relights the source. Optionally adds fuel (ignored if infinite).</summary>
        public void Ignite(float addedFuelSeconds = 0f)
        {
            if (!HasInfiniteFuel && addedFuelSeconds > 0f)
            {
                fuelSeconds += addedFuelSeconds;
            }
            if (isLit)
            {
                return;
            }
            isLit = true;
            GameEventLog.Append("LightIgnited", $"{name} sacred={sacred}");
        }

        void OnDrawGizmosSelected()
        {
            if (!isLit)
            {
                Gizmos.color = Color.gray;
                Gizmos.DrawWireSphere(transform.position, radius);
                return;
            }
            float edgeFraction = DarknessEvaluator.EdgeBandFraction;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, EffectiveRadius);
            Gizmos.color = new Color(1f, 0.6f, 0f);
            Gizmos.DrawWireSphere(transform.position, EffectiveRadius * (1f - edgeFraction));
        }
    }
}
